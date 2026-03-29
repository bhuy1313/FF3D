using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class FireTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type FireType = FindType("Fire");

    [Test]
    public void ApplyWater_FiresBurningStateChanged_WhenFireIsFullyExtinguished()
    {
        GameObject fireObject = new GameObject("Fire");

        try
        {
            fireObject.AddComponent<SphereCollider>();
            Component fire = fireObject.AddComponent(FireType);

            SetPrivateField(fire, "currentHp", 1f);
            SetPrivateField(fire, "maxHp", 1f);

            bool burningStateChangedCalled = false;
            bool receivedBurningState = true;
            int extinguishEventCount = 0;

            EventInfo burningStateChangedEvent = FireType.GetEvent("BurningStateChanged", InstanceFlags);
            Assert.That(burningStateChangedEvent, Is.Not.Null);
            Action<bool> burningStateHandler = isBurning =>
            {
                burningStateChangedCalled = true;
                receivedBurningState = isBurning;
            };
            burningStateChangedEvent.AddEventHandler(fire, burningStateHandler);

            EventInfo extinguishEvent = FireType.GetEvent("Extinguished", InstanceFlags);
            Assert.That(extinguishEvent, Is.Not.Null);
            Action extinguishHandler = () => extinguishEventCount++;
            extinguishEvent.AddEventHandler(fire, extinguishHandler);

            InvokeInstanceMethod(fire, "ApplyWater", 1f);

            Assert.That(burningStateChangedCalled, Is.True);
            Assert.That(receivedBurningState, Is.False);
            Assert.That(extinguishEventCount, Is.EqualTo(1));
            Assert.That(GetFieldValue<float>(fire, "currentHp"), Is.EqualTo(0f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fireObject);
        }
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
