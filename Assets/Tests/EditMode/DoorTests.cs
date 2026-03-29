using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class DoorTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type DoorType = FindType("Door");

    [Test]
    public void Interact_ComputesOpenRotationBySettingLocalYForStraightDoor()
    {
        GameObject root = new GameObject("Root");
        GameObject doorChild = new GameObject("Door");

        try
        {
            doorChild.transform.SetParent(root.transform, false);
            doorChild.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);

            Component door = root.AddComponent(DoorType);
            InvokeInstanceMethod(door, "InitializeDoorState");
            InvokeInstanceMethod(door, "Interact", null);

            Quaternion targetRotation = GetPrivateField<Quaternion>(door, "targetLocalRotation");
            Quaternion expectedRotation = Quaternion.Euler(0f, -65f, 0f);

            Assert.That(Quaternion.Angle(targetRotation, expectedRotation), Is.LessThan(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void Interact_ComputesOpenRotationBySettingLocalYForTiltedDoorMesh()
    {
        GameObject root = new GameObject("Root");
        GameObject doorChild = new GameObject("Door");

        try
        {
            doorChild.transform.SetParent(root.transform, false);
            doorChild.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            Component door = root.AddComponent(DoorType);
            InvokeInstanceMethod(door, "InitializeDoorState");
            InvokeInstanceMethod(door, "Interact", null);

            Quaternion targetRotation = GetPrivateField<Quaternion>(door, "targetLocalRotation");
            Quaternion expectedRotation = Quaternion.Euler(-90f, -90f, 0f);

            Assert.That(Quaternion.Angle(targetRotation, expectedRotation), Is.LessThan(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void InvokeInstanceMethod(object target, string methodName, params object[] args)
    {
        Type[] parameterTypes = args?.Select(argument => argument?.GetType() ?? typeof(object)).ToArray() ?? Type.EmptyTypes;
        MethodInfo method = FindMethod(target.GetType(), methodName, parameterTypes.Length);
        Assert.That(method, Is.Not.Null, $"Could not find method '{methodName}'.");
        method.Invoke(target, args);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = FindField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        return (T)field.GetValue(target);
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
