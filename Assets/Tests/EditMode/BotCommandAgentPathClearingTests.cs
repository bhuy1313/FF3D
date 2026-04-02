using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using TrueJourney.BotBehavior;

public class BotCommandAgentPathClearingTests
{
    private sealed class DummyBreakableTarget : MonoBehaviour, IBotBreakableTarget
    {
        [SerializeField] private bool isBroken;
        [SerializeField] private bool canBeClearedByBot = true;

        public bool IsBroken => isBroken;
        public bool CanBeClearedByBot => canBeClearedByBot;
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

        public bool SupportsBreakTool(BreakToolKind toolKind)
        {
            return true;
        }

        public bool TryStartBreak(GameObject breaker, BreakToolKind toolKind)
        {
            return true;
        }
    }

    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type BotCommandAgentType = FindType("BotCommandAgent");

    [Test]
    public void TryFindBreakableInFront_FindsBreakableDirectlyAheadWithinLookAhead()
    {
        GameObject agentObject = new GameObject("BotAgent");
        GameObject breakableObject = new GameObject("BreakableAhead");

        try
        {
            Component agent = agentObject.AddComponent(BotCommandAgentType);
            DummyBreakableTarget breakable = breakableObject.AddComponent<DummyBreakableTarget>();
            breakableObject.transform.position = new Vector3(0f, 0f, 4f);

            SetPrivateField(agent, "breakableLookAheadDistance", 8f);
            SetPrivateField(agent, "breakableCorridorWidth", 1f);
            BotRuntimeRegistry.RegisterBreakableTarget(breakable);

            object[] args = { new Vector3(0f, 0f, 10f), null };
            bool found = (bool)InvokeInstanceMethod(agent, "TryFindBreakableInFront", args);

            Assert.That(found, Is.True);
            Assert.That(args[1], Is.EqualTo(breakable));
        }
        finally
        {
            if (breakableObject.TryGetComponent(out DummyBreakableTarget breakable))
            {
                BotRuntimeRegistry.UnregisterBreakableTarget(breakable);
            }

            UnityEngine.Object.DestroyImmediate(agentObject);
            UnityEngine.Object.DestroyImmediate(breakableObject);
        }
    }

    [Test]
    public void IsBreakableStillRelevant_ReturnsFalse_WhenBreakableIsOnlyNearButNotAhead()
    {
        GameObject agentObject = new GameObject("BotAgent");
        GameObject breakableObject = new GameObject("BreakableSide");

        try
        {
            Component agent = agentObject.AddComponent(BotCommandAgentType);
            DummyBreakableTarget breakable = breakableObject.AddComponent<DummyBreakableTarget>();
            breakableObject.transform.position = new Vector3(5f, 0f, 0f);

            SetPrivateField(agent, "breakableLookAheadDistance", 8f);
            SetPrivateField(agent, "breakableCorridorWidth", 1f);
            SetPrivateField(agent, "lastIssuedDestination", new Vector3(0f, 0f, 10f));
            BotRuntimeRegistry.RegisterBreakableTarget(breakable);

            bool isRelevant = (bool)InvokeInstanceMethod(agent, "IsBreakableStillRelevant", breakable);

            Assert.That(isRelevant, Is.False);
        }
        finally
        {
            if (breakableObject.TryGetComponent(out DummyBreakableTarget breakable))
            {
                BotRuntimeRegistry.UnregisterBreakableTarget(breakable);
            }

            UnityEngine.Object.DestroyImmediate(agentObject);
            UnityEngine.Object.DestroyImmediate(breakableObject);
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
