using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class DoorTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type DoorType = FindType("Door");
    private static readonly Type FireType = FindType("Fire");
    private static readonly Type SmokeHazardType = FindType("SmokeHazard");
    private static readonly Type VentType = FindType("Vent");
    private static readonly Type PlayerVitalsType = FindType("PlayerVitals");

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

    [Test]
    public void SmokeHazard_BuildsSmokeDensityFromLinkedBurningFire()
    {
        GameObject zone = new GameObject("SmokeZone");
        GameObject fireObject = new GameObject("Fire");

        try
        {
            Component smokeHazard = zone.AddComponent(SmokeHazardType);
            Component fire = fireObject.AddComponent(FireType);

            SetPrivateField(fire, "maxHp", 1f);
            SetPrivateField(fire, "currentHp", 1f);
            SetPrivateField(smokeHazard, "linkedFires", CreateSingleEntryArray(FireType, fire));
            SetPrivateField(smokeHazard, "passiveVentilationRelief", 0f);
            SetPrivateField(smokeHazard, "smokePerBurningFire", 0.45f);
            SetPrivateField(smokeHazard, "smokePerFireIntensity", 0.55f);
            SetPrivateField(smokeHazard, "smokeAccumulationRate", 10f);

            InvokeInstanceMethod(smokeHazard, "UpdateSmokeDensity", 0.25f);

            float density = GetPrivateField<float>(smokeHazard, "currentSmokeDensity");
            Assert.That(density, Is.EqualTo(1f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(zone);
            UnityEngine.Object.DestroyImmediate(fireObject);
        }
    }

    [Test]
    public void SmokeHazard_OpenDoorAndVentReduceTargetSmokeDensity()
    {
        GameObject zone = new GameObject("SmokeZone");
        GameObject fireObject = new GameObject("Fire");
        GameObject doorObject = new GameObject("DoorRoot");
        GameObject ventObject = new GameObject("Vent");

        try
        {
            Component smokeHazard = zone.AddComponent(SmokeHazardType);
            Component fire = fireObject.AddComponent(FireType);
            Component door = doorObject.AddComponent(DoorType);
            Rigidbody ventBody = ventObject.AddComponent<Rigidbody>();
            Component vent = ventObject.AddComponent(VentType);

            SetPrivateField(fire, "maxHp", 1f);
            SetPrivateField(fire, "currentHp", 1f);
            SetPrivateField(door, "isOpen", true);
            SetPrivateField(door, "smokeVentilationReliefWhenOpen", 0.35f);
            SetPrivateField(vent, "rb", ventBody);
            SetPrivateField(vent, "startsOpen", true);
            SetPrivateField(vent, "isOpen", true);
            SetPrivateField(vent, "smokeVentilationReliefWhenOpen", 0.4f);
            SetPrivateField(smokeHazard, "linkedFires", CreateSingleEntryArray(FireType, fire));
            SetPrivateField(smokeHazard, "linkedDoors", CreateSingleEntryArray(DoorType, door));
            SetPrivateField(smokeHazard, "linkedVents", CreateSingleEntryArray(VentType, vent));
            SetPrivateField(smokeHazard, "passiveVentilationRelief", 0.05f);
            SetPrivateField(smokeHazard, "smokePerBurningFire", 0.4f);
            SetPrivateField(smokeHazard, "smokePerFireIntensity", 0.6f);

            float targetDensity = (float)InvokeInstanceMethod(smokeHazard, "CalculateTargetSmokeDensity");

            Assert.That(targetDensity, Is.EqualTo(0.2f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(zone);
            UnityEngine.Object.DestroyImmediate(fireObject);
            UnityEngine.Object.DestroyImmediate(doorObject);
            UnityEngine.Object.DestroyImmediate(ventObject);
        }
    }

    [Test]
    public void SmokeHazard_ScalesPlayerOxygenDrainWithCurrentDensity()
    {
        GameObject zone = new GameObject("SmokeZone");
        GameObject player = new GameObject("Player");

        try
        {
            Component smokeHazard = zone.AddComponent(SmokeHazardType);
            Component vitals = player.AddComponent(PlayerVitalsType);
            BoxCollider collider = player.AddComponent<BoxCollider>();

            InvokeInstanceMethod(vitals, "Awake");
            SetPrivateField(smokeHazard, "currentSmokeDensity", 0.5f);
            SetPrivateField(smokeHazard, "minimumDangerousDensity", 0.05f);
            SetPrivateField(smokeHazard, "oxygenDrainPerSecond", 10f);
            SetPrivateField(smokeHazard, "affectPlayers", true);
            SetPrivateField(smokeHazard, "affectVictims", false);

            InvokeInstanceMethod(smokeHazard, "ApplySmokeEffects", collider, 1f);

            float currentOxygen = (float)PlayerVitalsType.GetProperty("CurrentOxygen", InstanceFlags).GetValue(vitals);
            Assert.That(currentOxygen, Is.EqualTo(95f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(zone);
            UnityEngine.Object.DestroyImmediate(player);
        }
    }

    private static object InvokeInstanceMethod(object target, string methodName, params object[] args)
    {
        Type[] parameterTypes = args?.Select(argument => argument?.GetType() ?? typeof(object)).ToArray() ?? Type.EmptyTypes;
        MethodInfo method = FindMethod(target.GetType(), methodName, parameterTypes.Length);
        Assert.That(method, Is.Not.Null, $"Could not find method '{methodName}'.");
        return method.Invoke(target, args);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = FindField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = FindField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static Array CreateSingleEntryArray(Type elementType, object value)
    {
        Array array = Array.CreateInstance(elementType, 1);
        array.SetValue(value, 0);
        return array;
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
