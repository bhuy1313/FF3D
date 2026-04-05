using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TrueJourney.BotBehavior;
using UnityEngine;

public class VictimConditionTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type VictimConditionType = FindType("VictimCondition");
    private static readonly Type RescuableType = FindType("Rescuable");
    private static readonly Type MissionSystemType = FindType("IncidentMissionSystem");
    private static readonly Type TriageStateType = FindNestedType(VictimConditionType, "TriageState");
    private static readonly Type MissionStateType = FindNestedType(MissionSystemType, "MissionState");

    [Test]
    public void VictimCondition_TransitionsThroughTriageStates()
    {
        GameObject victimObject = new GameObject("Victim");

        try
        {
            Component victim = victimObject.AddComponent(VictimConditionType);
            SetPrivateField(victim, "maxCondition", 100f);
            SetPrivateField(victim, "urgentThreshold", 65f);
            SetPrivateField(victim, "criticalThreshold", 30f);
            SetPrivateField(victim, "currentCondition", 100f);
            InvokeInstanceMethod(victim, "OnValidate");

            InvokeInstanceMethod(victim, "ApplyConditionDamage", 40f);
            AssertState(victim, "Urgent");

            InvokeInstanceMethod(victim, "ApplyConditionDamage", 35f);
            AssertState(victim, "Critical");

            InvokeInstanceMethod(victim, "ApplyConditionDamage", 25f);
            AssertState(victim, "Deceased");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(victimObject);
        }
    }

    [Test]
    public void VictimCondition_UsesExpectedAnimatorParameterNames()
    {
        GameObject victimObject = new GameObject("Victim");

        try
        {
            Component victim = victimObject.AddComponent(VictimConditionType);

            Assert.That(GetFieldValue<string>(victim, "urgentAnimatorParameter"), Is.EqualTo("IsUrgent"));
            Assert.That(GetFieldValue<string>(victim, "criticalAnimatorParameter"), Is.EqualTo("IsCritical"));
            Assert.That(GetFieldValue<string>(victim, "deceasedAnimatorParameter"), Is.EqualTo("IsDeceased"));
            Assert.That(GetFieldValue<string>(victim, "carriedAnimatorParameter"), Is.EqualTo("IsCarried"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(victimObject);
        }
    }

    [Test]
    public void VictimPrefab_UsesIsCarriedAnimatorParameter()
    {
        string prefabPath = Path.Combine("Assets", "TrueJourney", "Prefab", "Bot", "Victim.prefab");
        string prefabContents = File.ReadAllText(prefabPath);

        Assert.That(
            prefabContents,
            Does.Contain("carriedAnimatorParameter: IsCarried"));
    }

    [Test]
    public void VictimController_DefinesCarryParameterAndDynamicPoseTransition()
    {
        string controllerPath = Path.Combine("Assets", "TrueJourney", "Imports", "character", "npc", "animation", "Victim.controller");
        string controllerContents = File.ReadAllText(controllerPath);

        Assert.That(
            controllerContents,
            Does.Contain("- m_Name: IsCarried"));
        Assert.That(
            controllerContents,
            Does.Contain("m_DstState: {fileID: 7346835221965220051}"));
    }

    [Test]
    public void Mission_Fails_WhenTrackedVictimDies_AndFailOnDeathIsEnabled()
    {
        GameObject missionObject = new GameObject("Mission");
        GameObject victimObject = new GameObject("Victim");

        try
        {
            missionObject.SetActive(false);
            Component mission = missionObject.AddComponent(MissionSystemType);
            Component victim = victimObject.AddComponent(VictimConditionType);

            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "autoDiscoverFires", false);
            SetPrivateField(mission, "autoDiscoverRescuables", false);
            SetPrivateField(mission, "autoDiscoverVictimConditions", false);
            SetPrivateField(mission, "failOnAnyVictimDeath", true);
            SetPrivateField(mission, "trackedVictimConditions", CreateTypedList(VictimConditionType, victim));

            InvokeInstanceMethod(mission, "StartMission");
            InvokeInstanceMethod(victim, "ApplyConditionDamage", 999f);
            InvokeInstanceMethod(mission, "Update");

            object failedState = Enum.Parse(MissionStateType, "Failed");
            Assert.That(GetFieldValue<object>(mission, "missionState"), Is.EqualTo(failedState));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(missionObject);
            UnityEngine.Object.DestroyImmediate(victimObject);
        }
    }

    [Test]
    public void VictimCondition_MarksVictimExtracted_AndStopsFurtherDamage_AfterRescue()
    {
        GameObject victimObject = new GameObject("Victim");

        try
        {
            Component victim = victimObject.AddComponent(VictimConditionType);
            Component rescuable = victimObject.GetComponent(RescuableType);

            SetPrivateField(victim, "currentCondition", 50f);
            InvokeInstanceMethod(victim, "Awake");
            InvokeInstanceMethod(victim, "OnEnable");

            InvokeInstanceMethod(rescuable, "CompleteRescueAt", Vector3.zero);

            Assert.That(GetFieldValue<bool>(victim, "isExtracted"), Is.True);
            Assert.That(GetFieldValue<bool>(victim, "isStabilized"), Is.True);

            InvokeInstanceMethod(victim, "ApplyConditionDamage", 10f);
            Assert.That(GetFieldValue<float>(victim, "currentCondition"), Is.EqualTo(50f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(victimObject);
        }
    }

    [Test]
    public void Rescuable_PreservesRescuedState_WhenReenabled()
    {
        GameObject victimObject = new GameObject("Victim");

        try
        {
            Component rescuable = victimObject.AddComponent(RescuableType);
            InvokeInstanceMethod(rescuable, "CompleteRescueAt", Vector3.one);

            victimObject.SetActive(false);
            victimObject.SetActive(true);

            Assert.That(GetPropertyValue<bool>(rescuable, "NeedsRescue"), Is.False);
            Assert.That(GetPropertyValue<bool>(rescuable, "IsRescued"), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(victimObject);
        }
    }

    [Test]
    public void Mission_StaysRunning_WhileCriticalVictimRemains_WhenCriticalClearanceIsRequired()
    {
        GameObject missionObject = new GameObject("Mission");
        GameObject victimObject = new GameObject("Victim");

        try
        {
            missionObject.SetActive(false);
            Component mission = missionObject.AddComponent(MissionSystemType);
            Component victim = victimObject.AddComponent(VictimConditionType);

            SetPrivateField(victim, "maxCondition", 100f);
            SetPrivateField(victim, "urgentThreshold", 65f);
            SetPrivateField(victim, "criticalThreshold", 30f);
            SetPrivateField(victim, "currentCondition", 20f);
            InvokeInstanceMethod(victim, "OnValidate");

            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "autoDiscoverFires", false);
            SetPrivateField(mission, "autoDiscoverRescuables", false);
            SetPrivateField(mission, "autoDiscoverVictimConditions", false);
            SetPrivateField(mission, "requireAllFiresExtinguished", false);
            SetPrivateField(mission, "requireAllRescuablesRescued", false);
            SetPrivateField(mission, "requireNoCriticalVictimsAtCompletion", true);
            SetPrivateField(mission, "trackedVictimConditions", CreateTypedList(VictimConditionType, victim));

            InvokeInstanceMethod(mission, "StartMission");
            InvokeInstanceMethod(mission, "Update");
            Assert.That(GetFieldValue<object>(mission, "missionState"), Is.EqualTo(Enum.Parse(MissionStateType, "Running")));

            InvokeInstanceMethod(victim, "RestoreCondition", 20f);
            InvokeInstanceMethod(mission, "Update");
            Assert.That(GetFieldValue<object>(mission, "missionState"), Is.EqualTo(Enum.Parse(MissionStateType, "Completed")));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(missionObject);
            UnityEngine.Object.DestroyImmediate(victimObject);
        }
    }

    [Test]
    public void BotDecisionService_PrefersHigherPriorityVictim()
    {
        GameObject stableVictimObject = new GameObject("StableVictim");
        GameObject criticalVictimObject = new GameObject("CriticalVictim");
        GameObject requesterObject = new GameObject("Requester");

        try
        {
            Component stableRescuable = stableVictimObject.AddComponent(RescuableType);
            Component criticalRescuable = criticalVictimObject.AddComponent(RescuableType);
            Component stableVictim = stableVictimObject.AddComponent(VictimConditionType);
            Component criticalVictim = criticalVictimObject.AddComponent(VictimConditionType);

            SetPrivateField(stableVictim, "currentCondition", 90f);
            SetPrivateField(criticalVictim, "currentCondition", 10f);
            InvokeInstanceMethod(stableVictim, "OnValidate");
            InvokeInstanceMethod(criticalVictim, "OnValidate");
            InvokeInstanceMethod(stableRescuable, "OnEnable");
            InvokeInstanceMethod(criticalRescuable, "OnEnable");

            BotRuntimeDecisionService service = new BotRuntimeDecisionService();
            IRescuableTarget selected = service.ResolveRescueTarget(Vector3.zero, null, requesterObject, 1000f);

            Assert.That(selected, Is.SameAs(criticalRescuable));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(stableVictimObject);
            UnityEngine.Object.DestroyImmediate(criticalVictimObject);
            UnityEngine.Object.DestroyImmediate(requesterObject);
        }
    }

    [Test]
    public void Rescuable_RequiresStabilization_ForCriticalVictim_WhenConfigured()
    {
        GameObject victimObject = new GameObject("Victim");

        try
        {
            Component victim = victimObject.AddComponent(VictimConditionType);
            IRescuableTarget rescuable = victimObject.GetComponent(RescuableType) as IRescuableTarget;

            SetPrivateField(victim, "criticalThreshold", 30f);
            SetPrivateField(victim, "currentCondition", 10f);
            SetPrivateField(victim, "requireStabilizationBeforeCarryWhenCritical", true);
            InvokeInstanceMethod(victim, "OnValidate");

            Assert.That(rescuable, Is.Not.Null);
            Assert.That(rescuable.RequiresStabilization, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(victimObject);
        }
    }

    [Test]
    public void StabilizationCoroutine_StabilizesVictim_AndRestoresCondition()
    {
        GameObject victimObject = new GameObject("Victim");
        GameObject rescuerObject = new GameObject("Rescuer");

        try
        {
            Component victim = victimObject.AddComponent(VictimConditionType);
            Component rescuable = victimObject.GetComponent(RescuableType);

            SetPrivateField(victim, "criticalThreshold", 30f);
            SetPrivateField(victim, "currentCondition", 10f);
            SetPrivateField(victim, "requireStabilizationBeforeCarryWhenCritical", true);
            SetPrivateField(rescuable, "stabilizeRestoreAmount", 20f);
            InvokeInstanceMethod(victim, "OnValidate");
            InvokeInstanceMethod(rescuable, "OnEnable");
            SetPrivateField(rescuable, "activeRescuer", rescuerObject);
            SetPrivateField(rescuable, "isRescueInProgress", true);

            IEnumerator routine = InvokeInstanceMethodWithReturn<IEnumerator>(rescuable, "StabilizeAfterDelay", 0.01f);
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(routine.MoveNext(), Is.False);

            Assert.That(GetFieldValue<bool>(victim, "isStabilized"), Is.True);
            Assert.That(GetFieldValue<float>(victim, "currentCondition"), Is.EqualTo(30f).Within(0.001f));
            Assert.That(GetFieldValue<bool>(rescuable, "isRescueInProgress"), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(victimObject);
            UnityEngine.Object.DestroyImmediate(rescuerObject);
        }
    }

    private static void AssertState(object victim, string stateName)
    {
        object expected = Enum.Parse(TriageStateType, stateName);
        Assert.That(GetFieldValue<object>(victim, "triageState"), Is.EqualTo(expected));
    }

    private static IList CreateTypedList(Type itemType, params object[] items)
    {
        Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(itemType);
        IList list = (IList)Activator.CreateInstance(listType);
        foreach (object item in items)
            list.Add(item);

        return list;
    }

    private static void InvokeInstanceMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Could not find method '{methodName}'.");
        method.Invoke(target, args);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static T GetFieldValue<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        return (T)field.GetValue(target);
    }

    private static T GetPropertyValue<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"Could not find property '{propertyName}'.");
        return (T)property.GetValue(target);
    }

    private static T InvokeInstanceMethodWithReturn<T>(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Could not find method '{methodName}'.");
        return (T)method.Invoke(target, args);
    }

    private static Type FindType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type found = assembly.GetType(typeName);
            if (found != null)
                return found;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types;
            }

            if (types == null)
                continue;

            for (int i = 0; i < types.Length; i++)
            {
                Type candidate = types[i];
                if (candidate != null && candidate.Name == typeName)
                    return candidate;
            }
        }

        Assert.Fail($"Could not find type '{typeName}'.");
        return null;
    }

    private static Type FindNestedType(Type parentType, string nestedTypeName)
    {
        Assert.That(parentType, Is.Not.Null);
        Type nestedType = parentType.GetNestedType(nestedTypeName, BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(nestedType, Is.Not.Null, $"Could not find nested type '{nestedTypeName}'.");
        return nestedType;
    }
}
