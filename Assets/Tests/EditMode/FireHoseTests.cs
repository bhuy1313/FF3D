using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class FireHoseTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type FireHoseType = FindType("FireHose");
    private static readonly Type FireType = FindType("Fire");
    private static readonly Type FireGroupType = FindType("FireGroup");

    [Test]
    public void ApplyWaterToColliderSafe_FallsBackToDirectFire_WhenNoFireGroupExists()
    {
        GameObject fireObject = new GameObject("Fire");

        try
        {
            fireObject.AddComponent<SphereCollider>();
            Component fire = fireObject.AddComponent(FireType);
            Collider collider = fireObject.GetComponent<Collider>();

            SetPrivateField(fire, "currentHp", 1f);
            SetPrivateField(fire, "maxHp", 1f);

            MethodInfo method = FireHoseType.GetMethod("ApplyWaterToColliderSafe", StaticFlags);
            Assert.That(method, Is.Not.Null);

            object processedGroups = Activator.CreateInstance(typeof(System.Collections.Generic.HashSet<>).MakeGenericType(FireGroupType));
            object processedFires = Activator.CreateInstance(typeof(System.Collections.Generic.HashSet<>).MakeGenericType(FireType));
            method.Invoke(null, new object[] { collider, 0.25f, processedGroups, processedFires });

            float currentHp = GetFieldValue<float>(fire, "currentHp");
            Assert.That(currentHp, Is.LessThan(1f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fireObject);
        }
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
}
