using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class IncidentMissionSystemTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type MissionSystemType = FindType("IncidentMissionSystem");
    private static readonly Type FireType = FindType("Fire");
    private static readonly Type RescuableType = FindType("Rescuable");
    private static readonly Type MissionStateType = FindNestedType(MissionSystemType, "MissionState");

    [Test]
    public void Mission_Completes_WhenAllTrackedObjectivesAreResolved()
    {
        GameObject missionObject = new GameObject("Mission");
        GameObject fireObject = new GameObject("Fire");
        GameObject rescuableObject = new GameObject("Rescuable");

        try
        {
            missionObject.SetActive(false);
            Component mission = missionObject.AddComponent(MissionSystemType);
            Component fire = fireObject.AddComponent(FireType);
            Component rescuable = rescuableObject.AddComponent(RescuableType);

            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "autoDiscoverFires", false);
            SetPrivateField(mission, "autoDiscoverRescuables", false);
            SetPrivateField(mission, "trackedFires", CreateTypedList(FireType, fire));
            SetPrivateField(mission, "trackedRescuables", CreateTypedList(RescuableType, rescuable));

            SetPrivateField(fire, "currentHp", 0f);
            SetPrivateField(rescuable, "isRescued", true);

            InvokeInstanceMethod(mission, "StartMission");
            InvokeInstanceMethod(mission, "Update");

            object completedState = Enum.Parse(MissionStateType, "Completed");
            Assert.That(GetFieldValue<object>(mission, "missionState"), Is.EqualTo(completedState));
            Assert.That(GetFieldValue<int>(mission, "extinguishedFireCount"), Is.EqualTo(1));
            Assert.That(GetFieldValue<int>(mission, "rescuedCount"), Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(missionObject);
            UnityEngine.Object.DestroyImmediate(fireObject);
            UnityEngine.Object.DestroyImmediate(rescuableObject);
        }
    }

    [Test]
    public void Mission_Fails_WhenTimeLimitExpires()
    {
        GameObject missionObject = new GameObject("Mission");
        GameObject fireObject = new GameObject("Fire");

        try
        {
            missionObject.SetActive(false);
            Component mission = missionObject.AddComponent(MissionSystemType);
            Component fire = fireObject.AddComponent(FireType);

            SetPrivateField(mission, "autoStartOnEnable", false);
            SetPrivateField(mission, "autoDiscoverFires", false);
            SetPrivateField(mission, "autoDiscoverRescuables", false);
            SetPrivateField(mission, "timeLimitSeconds", 1f);
            SetPrivateField(mission, "trackedFires", CreateTypedList(FireType, fire));
            SetPrivateField(fire, "currentHp", 1f);

            InvokeInstanceMethod(mission, "StartMission");
            SetPrivateField(mission, "elapsedTime", 1.1f);
            InvokeInstanceMethod(mission, "Update");

            object failedState = Enum.Parse(MissionStateType, "Failed");
            Assert.That(GetFieldValue<object>(mission, "missionState"), Is.EqualTo(failedState));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(missionObject);
            UnityEngine.Object.DestroyImmediate(fireObject);
        }
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
