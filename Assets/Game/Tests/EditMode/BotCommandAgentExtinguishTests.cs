using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using TrueJourney.BotBehavior;

public class BotCommandAgentExtinguishTests
{
    private sealed class DummyExtinguisherTool : MonoBehaviour, IBotExtinguisherItem
    {
        public Rigidbody Rigidbody => null;
        public float ApplyWaterPerSecond => 1f;
        public FireSuppressionAgent SuppressionAgent => FireSuppressionAgent.DryChemical;
        public float PreferredSprayDistance => 2f;
        public float MaxSprayDistance => 4f;
        public float MaxVerticalReach => 3f;
        public float BallisticLaunchSpeed => 0f;
        public float BallisticGravityMultiplier => 1f;
        public bool RequiresPreciseAim => false;
        public bool HasUsableCharge => true;
        public bool IsHeld => true;
        public GameObject ClaimOwner => gameObject;
        public bool IsAvailableTo(GameObject requester) => true;
        public bool TryClaim(GameObject requester) => true;
        public void ReleaseClaim(GameObject requester) { }
        public void SetExternalAimDirection(Vector3 worldDirection, GameObject user) { }
        public void ClearExternalAimDirection(GameObject user) { }
        public void SetExternalSprayState(bool enable, GameObject user) { }
    }

    private sealed class DummyFireTarget : MonoBehaviour, IFireTarget
    {
        [SerializeField] private bool isBurning = true;
        [SerializeField] private float worldRadius = 0.25f;

        public bool IsBurning => isBurning;
        public FireHazardType FireType => FireHazardType.OrdinaryCombustibles;
        public float SuppressedAmount { get; private set; }

        public void ApplyWater(float amount) { }

        public void ApplySuppression(float amount, FireSuppressionAgent agent)
        {
            SuppressedAmount += amount;
        }

        public Vector3 GetWorldPosition()
        {
            return transform.position;
        }

        public float GetWorldRadius()
        {
            return worldRadius;
        }
    }

    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type BotCommandAgentType = FindType("BotCommandAgent");

    [Test]
    public void CanExtinguishFromPosition_ReturnsTrue_WhenFireIsVisible()
    {
        GameObject agentObject = new GameObject("BotAgent");
        GameObject fireObject = new GameObject("FireTarget");
        GameObject toolObject = new GameObject("Tool");

        try
        {
            Component agent = agentObject.AddComponent(BotCommandAgentType);
            DummyFireTarget fireTarget = fireObject.AddComponent<DummyFireTarget>();
            DummyExtinguisherTool tool = toolObject.AddComponent<DummyExtinguisherTool>();

            agentObject.transform.position = Vector3.zero;
            fireObject.transform.position = new Vector3(0f, 0f, 3f);

            bool canExtinguish = (bool)InvokeInstanceMethod(
                agent,
                "CanExtinguishFromPosition",
                tool,
                agentObject.transform.position,
                fireTarget.GetWorldPosition(),
                fireTarget);

            Assert.That(canExtinguish, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(agentObject);
            UnityEngine.Object.DestroyImmediate(fireObject);
            UnityEngine.Object.DestroyImmediate(toolObject);
        }
    }

    [Test]
    public void CanExtinguishFromPosition_ReturnsFalse_WhenWallBlocksLineOfSight()
    {
        GameObject agentObject = new GameObject("BotAgent");
        GameObject fireObject = new GameObject("FireTarget");
        GameObject toolObject = new GameObject("Tool");
        GameObject wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

        try
        {
            Component agent = agentObject.AddComponent(BotCommandAgentType);
            DummyFireTarget fireTarget = fireObject.AddComponent<DummyFireTarget>();
            DummyExtinguisherTool tool = toolObject.AddComponent<DummyExtinguisherTool>();

            agentObject.transform.position = Vector3.zero;
            fireObject.transform.position = new Vector3(0f, 0f, 3f);
            wallObject.transform.position = new Vector3(0f, 0.5f, 1.5f);
            wallObject.transform.localScale = new Vector3(2f, 3f, 0.2f);

            bool canExtinguish = (bool)InvokeInstanceMethod(
                agent,
                "CanExtinguishFromPosition",
                tool,
                agentObject.transform.position,
                fireTarget.GetWorldPosition(),
                fireTarget);

            Assert.That(canExtinguish, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(agentObject);
            UnityEngine.Object.DestroyImmediate(fireObject);
            UnityEngine.Object.DestroyImmediate(toolObject);
            UnityEngine.Object.DestroyImmediate(wallObject);
        }
    }

    [Test]
    public void ResolveExtinguisherRouteTarget_PrefersLockedBurningTarget()
    {
        GameObject agentObject = new GameObject("BotAgent");
        GameObject lockedFireObject = new GameObject("LockedFire");
        GameObject otherFireObject = new GameObject("OtherFire");

        try
        {
            Component agent = agentObject.AddComponent(BotCommandAgentType);
            DummyFireTarget lockedFire = lockedFireObject.AddComponent<DummyFireTarget>();
            DummyFireTarget otherFire = otherFireObject.AddComponent<DummyFireTarget>();

            lockedFireObject.transform.position = new Vector3(0f, 0f, 6f);
            otherFireObject.transform.position = new Vector3(0f, 0f, 2f);

            SetPrivateField(agent, "lockedExtinguisherFireTarget", lockedFire);
            SetPrivateField(agent, "currentFireTarget", otherFire);

            IFireTarget resolvedTarget = (IFireTarget)InvokeInstanceMethod(
                agent,
                "ResolveExtinguisherRouteTarget",
                Vector3.zero);

            Assert.That(resolvedTarget, Is.EqualTo(lockedFire));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(agentObject);
            UnityEngine.Object.DestroyImmediate(lockedFireObject);
            UnityEngine.Object.DestroyImmediate(otherFireObject);
        }
    }

    private static object InvokeInstanceMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = FindMethod(target.GetType(), methodName, args?.Length ?? 0);
        Assert.That(method, Is.Not.Null, $"Could not find method '{methodName}'.");
        return method.Invoke(target, args);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = FindField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static MethodInfo FindMethod(Type type, string methodName, int parameterCount)
    {
        while (type != null)
        {
            MethodInfo[] methods = type.GetMethods(InstanceFlags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo candidate = methods[i];
                if (candidate.Name == methodName && candidate.GetParameters().Length == parameterCount)
                {
                    return candidate;
                }
            }

            type = type.BaseType;
        }

        return null;
    }

    private static FieldInfo FindField(Type type, string fieldName)
    {
        while (type != null)
        {
            FieldInfo field = type.GetField(fieldName, InstanceFlags);
            if (field != null)
            {
                return field;
            }

            type = type.BaseType;
        }

        return null;
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
                if (candidate != null && candidate.Name == typeName)
                {
                    return candidate;
                }
            }
        }

        Assert.Fail($"Could not find type '{typeName}'.");
        return null;
    }
}
