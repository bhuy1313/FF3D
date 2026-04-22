using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

public class BotCarryWeightTests
{
    private sealed class DummyFireTarget : MonoBehaviour, TrueJourney.BotBehavior.IFireTarget
    {
        public bool IsBurning => true;
        public FireHazardType FireType => FireHazardType.OrdinaryCombustibles;

        public void ApplyWater(float amount)
        {
        }

        public void ApplySuppression(float amount, FireSuppressionAgent agent)
        {
        }

        public void ApplySuppression(float amount, FireSuppressionAgent agent, GameObject sourceUser)
        {
        }

        public FireSuppressionOutcome EvaluateSuppressionOutcome(FireSuppressionAgent agent)
        {
            return FireSuppressionOutcome.SafeEffective;
        }

        public Vector3 GetWorldPosition()
        {
            return transform.position;
        }

        public float GetWorldRadius()
        {
            return 0.5f;
        }
    }

    private sealed class DummyBreakableTarget : MonoBehaviour, TrueJourney.BotBehavior.IBotBreakableTarget
    {
        public bool IsBroken => false;
        public bool CanBeClearedByBot => true;
        public bool IsBreakInProgress => false;
        public GameObject ActiveBreaker => null;

        public Vector3 GetWorldPosition()
        {
            return transform.position;
        }

        public bool TryGetBreakStandPose(Vector3 breakerPosition, out Vector3 standPosition, out Quaternion standRotation)
        {
            standPosition = transform.position;
            standRotation = Quaternion.identity;
            return true;
        }

        public bool IsOnSameSide(Vector3 pointA, Vector3 pointB)
        {
            return true;
        }

        public bool SupportsBreakTool(TrueJourney.BotBehavior.BreakToolKind toolKind)
        {
            return true;
        }

        public bool TryStartBreak(GameObject breaker, TrueJourney.BotBehavior.BreakToolKind toolKind)
        {
            return true;
        }
    }

    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type BotCommandAgentType = FindType("BotCommandAgent");
    private static readonly Type BotBehaviorContextType = FindType("BotBehaviorContext");
    private static readonly Type BotInventorySystemType = FindType("BotInventorySystem");
    private static readonly Type BotEquippedItemPoseDriverType = FindType("TrueJourney.BotBehavior.BotEquippedItemPoseDriver");
    private static readonly Type RescuableType = FindType("Rescuable");

    [Test]
    public void BotCommandAgent_EvaluatesCarrySpeedMultiplier_FromVictimWeight()
    {
        GameObject botObject = new GameObject("Bot");
        GameObject victimObject = new GameObject("Victim");

        try
        {
            Component botAgent = CreateBotAgent(botObject);
            Component rescuable = CreateCarriedRescuable(victimObject, botObject);

            SetFieldValue(botAgent, "carryWeightForMinimumSpeed", 80f);
            SetFieldValue(botAgent, "minimumCarrySpeedMultiplier", 0.45f);
            SetFieldValue(botAgent, "currentRescueTarget", rescuable);

            float speedMultiplier = (float)InvokeInstanceMethod(botAgent, "EvaluateCarryMovementSpeedMultiplier");
            Assert.That(speedMultiplier, Is.EqualTo(0.484375f).Within(0.001f));
        }
        finally
        {
            if (botObject != null)
            {
                UnityEngine.Object.DestroyImmediate(botObject);
            }

            if (victimObject != null)
            {
                UnityEngine.Object.DestroyImmediate(victimObject);
            }
        }
    }

    [Test]
    public void BotCommandAgent_UpdatesNavMeshSpeed_WhileCarryingVictim()
    {
        GameObject botObject = new GameObject("Bot");
        GameObject victimObject = new GameObject("Victim");

        try
        {
            Component botAgent = CreateBotAgent(botObject);
            NavMeshAgent navMeshAgent = botObject.GetComponent<NavMeshAgent>();
            navMeshAgent.speed = 3.5f;

            Component rescuable = CreateCarriedRescuable(victimObject, botObject);

            SetFieldValue(botAgent, "carryWeightForMinimumSpeed", 80f);
            SetFieldValue(botAgent, "minimumCarrySpeedMultiplier", 0.45f);
            SetFieldValue(botAgent, "currentRescueTarget", rescuable);
            InvokeInstanceMethod(botAgent, "CacheMovementSpeedDefaults");
            InvokeInstanceMethod(botAgent, "UpdateCarryMovementSpeed");

            Assert.That(navMeshAgent.speed, Is.EqualTo(1.6953125f).Within(0.001f));
        }
        finally
        {
            if (botObject != null)
            {
                UnityEngine.Object.DestroyImmediate(botObject);
            }

            if (victimObject != null)
            {
                UnityEngine.Object.DestroyImmediate(victimObject);
            }
        }
    }

    [Test]
    public void BotCommandAgent_PrepareForIssuedMoveCommand_PreservesCarriedVictimState()
    {
        GameObject botObject = new GameObject("Bot");
        GameObject victimObject = new GameObject("Victim");

        try
        {
            Component botAgent = CreateBotAgent(botObject);
            Component rescuable = CreateCarriedRescuable(victimObject, botObject);

            SetFieldValue(botAgent, "currentRescueTarget", rescuable);

            InvokeInstanceMethod(botAgent, "PrepareForIssuedCommand", BotCommandType.Move);

            Assert.That(GetFieldValue<object>(botAgent, "currentRescueTarget"), Is.EqualTo(rescuable));
            Assert.That(GetPropertyValue<bool>(botAgent, "IsCarryingRescueTarget"), Is.True);
        }
        finally
        {
            if (botObject != null)
            {
                UnityEngine.Object.DestroyImmediate(botObject);
            }

            if (victimObject != null)
            {
                UnityEngine.Object.DestroyImmediate(victimObject);
            }
        }
    }

    [Test]
    public void BotCommandAgent_Update_PreservesCarriedVictimStateWithoutRescueOrder()
    {
        GameObject botObject = new GameObject("Bot");
        GameObject victimObject = new GameObject("Victim");

        try
        {
            Component botAgent = CreateBotAgent(botObject);
            Component rescuable = CreateCarriedRescuable(victimObject, botObject);

            SetFieldValue(botAgent, "currentRescueTarget", rescuable);

            InvokeInstanceMethod(botAgent, "Update");

            Assert.That(GetFieldValue<object>(botAgent, "currentRescueTarget"), Is.EqualTo(rescuable));
            Assert.That(GetPropertyValue<bool>(botAgent, "IsCarryingRescueTarget"), Is.True);
        }
        finally
        {
            if (botObject != null)
            {
                UnityEngine.Object.DestroyImmediate(botObject);
            }

            if (victimObject != null)
            {
                UnityEngine.Object.DestroyImmediate(victimObject);
            }
        }
    }

    [Test]
    public void BotCommandAgent_MoveToRescueCarrySafeZoneCommand_ClearsRouteFireAndPathClearingRuntime()
    {
        GameObject botObject = new GameObject("Bot");
        GameObject fireObject = new GameObject("RouteFire");
        GameObject breakableObject = new GameObject("Breakable");

        try
        {
            Component botAgent = CreateBotAgent(botObject);
            DummyFireTarget fireTarget = fireObject.AddComponent<DummyFireTarget>();
            DummyBreakableTarget breakableTarget = breakableObject.AddComponent<DummyBreakableTarget>();

            SetFieldValue(botAgent, "currentRouteBlockingFire", fireTarget);
            SetFieldValue(botAgent, "currentBlockedBreakable", breakableTarget);

            InvokeInstanceMethod(botAgent, "MoveToRescueCarrySafeZoneCommand", new Vector3(4f, 0f, 6f));

            Assert.That(GetFieldValue<object>(botAgent, "currentRouteBlockingFire"), Is.Null);
            Assert.That(GetFieldValue<object>(botAgent, "currentBlockedBreakable"), Is.Null);
        }
        finally
        {
            if (botObject != null)
            {
                UnityEngine.Object.DestroyImmediate(botObject);
            }

            if (fireObject != null)
            {
                UnityEngine.Object.DestroyImmediate(fireObject);
            }

            if (breakableObject != null)
            {
                UnityEngine.Object.DestroyImmediate(breakableObject);
            }
        }
    }

    [Test]
    public void BotCommandAgent_CanAcceptExtinguishCommand_RequiresInventorySystem()
    {
        GameObject botObject = new GameObject("Bot");

        try
        {
            Component botAgent = CreateBotAgent(botObject);
            SetFieldValue(botAgent, "inventorySystem", null);

            bool canAccept = (bool)InvokeInstanceMethod(botAgent, "CanAcceptCommand", BotCommandType.Extinguish);

            Assert.That(canAccept, Is.False);
        }
        finally
        {
            if (botObject != null)
            {
                UnityEngine.Object.DestroyImmediate(botObject);
            }
        }
    }

    [Test]
    public void BotCommandAgent_PrepareForIssuedRescueCommand_ClearsFollowRuntime()
    {
        GameObject botObject = new GameObject("Bot");
        GameObject followTargetObject = new GameObject("FollowTarget");

        try
        {
            Component botAgent = CreateBotAgent(botObject);

            SetFieldValue(botAgent, "followTarget", followTargetObject.transform);
            SetFieldValue(botAgent, "lastFollowDestination", new Vector3(3f, 0f, 2f));
            SetFieldValue(botAgent, "currentEscortSlotIndex", 2);

            InvokeInstanceMethod(botAgent, "PrepareForIssuedCommand", BotCommandType.Rescue);

            Assert.That(GetFieldValue<object>(botAgent, "followTarget"), Is.Null);
            Assert.That(GetFieldValue<Vector3>(botAgent, "lastFollowDestination"), Is.EqualTo(Vector3.zero));
            Assert.That(GetFieldValue<int>(botAgent, "currentEscortSlotIndex"), Is.EqualTo(-1));
        }
        finally
        {
            if (followTargetObject != null)
            {
                UnityEngine.Object.DestroyImmediate(followTargetObject);
            }

            if (botObject != null)
            {
                UnityEngine.Object.DestroyImmediate(botObject);
            }
        }
    }

    private static Component CreateBotAgent(GameObject botObject)
    {
        botObject.AddComponent<NavMeshAgent>();
        botObject.AddComponent(BotBehaviorContextType);
        botObject.AddComponent(BotInventorySystemType);
        botObject.AddComponent(BotEquippedItemPoseDriverType);
        Component botAgent = botObject.AddComponent(BotCommandAgentType);
        InvokeOptionalMethod(botAgent, "Awake");
        return botAgent;
    }

    private static Component CreateCarriedRescuable(GameObject victimObject, GameObject rescuer)
    {
        victimObject.AddComponent<Rigidbody>();
        Component rescuable = victimObject.AddComponent(RescuableType);
        SetFieldValue(rescuable, "movementWeightKg", 75f);
        SetFieldValue(rescuable, "isCarried", true);
        SetFieldValue(rescuable, "activeRescuer", rescuer);
        return rescuable;
    }

    private static void InvokeOptionalMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        method?.Invoke(target, args);
    }

    private static object InvokeInstanceMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Could not find method '{methodName}' on {target.GetType().Name}.");
        return method.Invoke(target, args);
    }

    private static T GetFieldValue<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}' on {target.GetType().Name}.");
        return (T)field.GetValue(target);
    }

    private static T GetPropertyValue<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"Could not find property '{propertyName}' on {target.GetType().Name}.");
        return (T)property.GetValue(target);
    }

    private static void SetFieldValue(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}' on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private static Type FindType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type found = assembly.GetType(typeName);
            if (found != null)
            {
                return found;
            }

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
            {
                continue;
            }

            for (int i = 0; i < types.Length; i++)
            {
                Type candidate = types[i];
                if (candidate != null && (candidate.FullName == typeName || candidate.Name == typeName))
                {
                    return candidate;
                }
            }
        }

        Assert.Fail($"Could not find type '{typeName}'.");
        return null;
    }
}
