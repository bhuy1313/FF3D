using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ThermalAndIsolationFeatureTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type HazardIsolationDeviceType = FindType("HazardIsolationDevice");
    private static readonly Type ThermalCameraType = FindType("ThermalCamera");
    private static readonly Type ThermalVisionControllerType = FindType("ThermalVisionController");
    private static readonly Type FireType = FindType("Fire");
    private static readonly Type RescuableType = FindType("Rescuable");
    private static readonly Type VictimConditionType = FindType("VictimCondition");
    private static readonly Type SmokeHazardType = FindType("SmokeHazard");
    private static readonly Type SmokeAlarmDetectorType = FindType("SmokeAlarmDetector");
    private static readonly Type PortableWorkLightDeployedType = FindType("PortableWorkLightDeployed");
    private static readonly Type FireHazardType = FindType("FireHazardType");
    private static readonly Type ThermalSignatureCategoryType = FindType("ThermalSignatureCategory");
    private static readonly Type BotRuntimeRegistryType = FindType("TrueJourney.BotBehavior.BotRuntimeRegistry");

    [Test]
    public void ThermalCamera_Use_CreatesController_AndTogglesThermalVision()
    {
        GameObject playerObject = new GameObject("Player");
        GameObject thermalCameraObject = new GameObject("ThermalCamera");

        try
        {
            thermalCameraObject.AddComponent<Rigidbody>();
            Component thermalCamera = thermalCameraObject.AddComponent(ThermalCameraType);

            InvokeInstanceMethod(thermalCamera, "OnPickup", playerObject);
            InvokeInstanceMethod(thermalCamera, "Use", playerObject);

            Component controller = playerObject.GetComponent(ThermalVisionControllerType);
            Assert.That(controller, Is.Not.Null);
            Assert.That(GetPropertyValue<bool>(controller, "IsThermalVisionActive"), Is.True);

            InvokeInstanceMethod(thermalCamera, "Use", playerObject);
            Assert.That(GetPropertyValue<bool>(controller, "IsThermalVisionActive"), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(thermalCameraObject);
            UnityEngine.Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void ThermalCamera_DoesNotEnableThermalVision_WhenBatteryIsEmpty()
    {
        GameObject playerObject = new GameObject("Player");
        GameObject thermalCameraObject = new GameObject("ThermalCamera");

        try
        {
            thermalCameraObject.AddComponent<Rigidbody>();
            Component thermalCamera = thermalCameraObject.AddComponent(ThermalCameraType);

            SetPrivateField(thermalCamera, "currentBatterySeconds", 0f);
            InvokeInstanceMethod(thermalCamera, "OnPickup", playerObject);
            InvokeInstanceMethod(thermalCamera, "Use", playerObject);

            Component controller = playerObject.GetComponent(ThermalVisionControllerType);
            Assert.That(controller, Is.Not.Null);
            Assert.That(GetPropertyValue<bool>(controller, "IsThermalVisionActive"), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(thermalCameraObject);
            UnityEngine.Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void ThermalCamera_RechargesWhileStowed_WhenFlagEnabled()
    {
        GameObject playerObject = new GameObject("Player");
        GameObject thermalCameraObject = new GameObject("ThermalCamera");

        try
        {
            thermalCameraObject.AddComponent<Rigidbody>();
            Component thermalCamera = thermalCameraObject.AddComponent(ThermalCameraType);

            SetPrivateField(thermalCamera, "maxBatterySeconds", 100f);
            SetPrivateField(thermalCamera, "currentBatterySeconds", 10f);
            SetPrivateField(thermalCamera, "batteryRechargePerSecond", 5f);
            SetPrivateField(thermalCamera, "rechargeWhileStowed", true);

            InvokeInstanceMethod(thermalCamera, "OnInventoryTick", playerObject, false, 2f);

            Assert.That(GetFieldValue<float>(thermalCamera, "currentBatterySeconds"), Is.EqualTo(20f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(thermalCameraObject);
            UnityEngine.Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void ThermalCamera_DoesNotRechargeWhileStowed_WhenFlagDisabled()
    {
        GameObject playerObject = new GameObject("Player");
        GameObject thermalCameraObject = new GameObject("ThermalCamera");

        try
        {
            thermalCameraObject.AddComponent<Rigidbody>();
            Component thermalCamera = thermalCameraObject.AddComponent(ThermalCameraType);

            SetPrivateField(thermalCamera, "maxBatterySeconds", 100f);
            SetPrivateField(thermalCamera, "currentBatterySeconds", 10f);
            SetPrivateField(thermalCamera, "batteryRechargePerSecond", 5f);
            SetPrivateField(thermalCamera, "rechargeWhileStowed", false);

            InvokeInstanceMethod(thermalCamera, "OnInventoryTick", playerObject, false, 2f);

            Assert.That(GetFieldValue<float>(thermalCamera, "currentBatterySeconds"), Is.EqualTo(10f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(thermalCameraObject);
            UnityEngine.Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void Fire_RegistersAsThermalSignature_WhenBurning()
    {
        GameObject fireObject = new GameObject("ThermalFire");

        try
        {
            fireObject.AddComponent<SphereCollider>();
            Component fire = fireObject.AddComponent(FireType);

            SetPrivateField(fire, "currentHp", 1f);
            SetPrivateField(fire, "maxHp", 1f);

            Assert.That(GetPropertyValue<bool>(fire, "HasThermalSignature"), Is.True);
            Assert.That(Convert.ToSingle(InvokeInstanceMethodWithReturn(fire, "GetThermalSignatureStrength")), Is.GreaterThan(0f));
            Assert.That(ContainsThermalSignatureSource(fire), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fireObject);
        }
    }

    [Test]
    public void VictimCondition_ExposesCriticalThermalSignature()
    {
        GameObject victimObject = new GameObject("Victim");

        try
        {
            victimObject.AddComponent(RescuableType);
            Component victimCondition = victimObject.AddComponent(VictimConditionType);

            Type triageType = VictimConditionType.GetNestedType("TriageState", BindingFlags.Public);
            Assert.That(triageType, Is.Not.Null);

            SetPrivateField(victimCondition, "triageState", Enum.Parse(triageType, "Critical"));
            SetPrivateField(victimCondition, "isExtracted", false);

            Assert.That(GetPropertyValue<bool>(victimCondition, "HasThermalSignature"), Is.True);
            Assert.That(
                GetPropertyValue<object>(victimCondition, "ThermalSignatureCategory"),
                Is.EqualTo(Enum.Parse(ThermalSignatureCategoryType, "VictimCritical")));
            Assert.That(Convert.ToSingle(InvokeInstanceMethodWithReturn(victimCondition, "GetThermalSignatureStrength")), Is.EqualTo(1f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(victimObject);
        }
    }

    [Test]
    public void GasShutoffValve_Isolation_UpdatesLinkedFireAndSummary()
    {
        GameObject deviceObject = new GameObject("GasValve");
        GameObject fireObject = new GameObject("GasFire");

        try
        {
            fireObject.transform.SetParent(deviceObject.transform, false);
            fireObject.AddComponent<SphereCollider>();
            Component fire = fireObject.AddComponent(FireType);

            Component device = deviceObject.AddComponent(HazardIsolationDeviceType);
            ConfigureIsolationHazardType(device, "Gas");
            SetPrivateField(device, "interactionDuration", 0f);
            SetPrivateField(device, "linkedFires", new[] { fire });

            InvokeInstanceMethod(device, "IsolateHazard");

            Assert.That(GetPropertyValue<bool>(device, "IsIsolated"), Is.True);
            Assert.That(GetPropertyValue<string>(device, "CurrentStateSummary"), Is.EqualTo("Gas Shutoff Valve: isolated"));
            Assert.That(GetPropertyValue<object>(fire, "FireType"), Is.EqualTo(Enum.Parse(FireHazardType, "GasFed")));
            Assert.That(GetPropertyValue<bool>(fire, "IsHazardSourceIsolated"), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(deviceObject);
        }
    }

    [Test]
    public void ElectricalPanel_DefaultDisplayName_IsUsedWhenConfigured()
    {
        GameObject deviceObject = new GameObject("ElectricalPanel");

        try
        {
            Component device = deviceObject.AddComponent(HazardIsolationDeviceType);
            ConfigureIsolationHazardType(device, "Electrical");
            SetPrivateField(device, "interactionDuration", 0f);

            InvokeInstanceMethod(device, "IsolateHazard");

            Assert.That(GetPropertyValue<string>(device, "HazardDisplayName"), Is.EqualTo("Electrical Panel"));
            Assert.That(GetPropertyValue<string>(device, "CurrentStateSummary"), Is.EqualTo("Electrical Panel: isolated"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(deviceObject);
        }
    }

    [Test]
    public void SmokeAlarmDetector_Triggers_WhenLinkedSmokeHazardExceedsThreshold()
    {
        GameObject detectorObject = new GameObject("SmokeAlarm");
        GameObject hazardObject = new GameObject("SmokeHazard");

        try
        {
            hazardObject.AddComponent<BoxCollider>();
            hazardObject.AddComponent<Rigidbody>();
            Component smokeHazard = hazardObject.AddComponent(SmokeHazardType);
            SetPrivateField(smokeHazard, "currentSmokeDensity", 0.45f);

            Component detector = detectorObject.AddComponent(SmokeAlarmDetectorType);
            SetPrivateField(detector, "linkedSmokeHazards", CreateTypedArray(SmokeHazardType, smokeHazard));
            SetPrivateField(detector, "triggerSmokeThreshold", 0.2f);
            SetPrivateField(detector, "triggerDelaySeconds", 0f);

            InvokeInstanceMethod(detector, "Update");

            Assert.That(GetPropertyValue<bool>(detector, "IsAlarmTriggered"), Is.True);
            Assert.That(GetPropertyValue<float>(detector, "CurrentDetectedSmokeDensity"), Is.EqualTo(0.45f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(detectorObject);
            UnityEngine.Object.DestroyImmediate(hazardObject);
        }
    }

    [Test]
    public void PortableWorkLightDeployed_Interact_TogglesLightState()
    {
        GameObject lightObject = new GameObject("PortableWorkLight");
        GameObject lightChild = new GameObject("Lamp");

        try
        {
            lightChild.transform.SetParent(lightObject.transform, false);
            Light controlledLight = lightChild.AddComponent<Light>();

            Component deployedLight = lightObject.AddComponent(PortableWorkLightDeployedType);
            SetPrivateField(deployedLight, "controlledLights", CreateTypedArray(typeof(Light), controlledLight));
            SetPrivateField(deployedLight, "startsEnabled", true);

            InvokeInstanceMethod(deployedLight, "SetLightEnabled", true);
            Assert.That(GetPropertyValue<bool>(deployedLight, "IsLightEnabled"), Is.True);
            Assert.That(controlledLight.enabled, Is.True);

            InvokeInstanceMethod(deployedLight, "Interact", new GameObject("Player"));
            Assert.That(GetPropertyValue<bool>(deployedLight, "IsLightEnabled"), Is.False);
            Assert.That(controlledLight.enabled, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(lightObject);
        }
    }

    private static bool ContainsThermalSignatureSource(object source)
    {
        PropertyInfo activeSourcesProperty = BotRuntimeRegistryType.GetProperty("ActiveThermalSignatureSources", BindingFlags.Static | BindingFlags.Public);
        Assert.That(activeSourcesProperty, Is.Not.Null, "Could not find ActiveThermalSignatureSources property.");

        IEnumerable candidates = activeSourcesProperty.GetValue(null) as IEnumerable;
        Assert.That(candidates, Is.Not.Null, "ActiveThermalSignatureSources did not return an enumerable.");

        foreach (object candidate in candidates)
        {
            if (ReferenceEquals(candidate, source))
            {
                return true;
            }
        }

        return false;
    }

    private static void ConfigureIsolationHazardType(Component device, string enumName)
    {
        Type enumType = HazardIsolationDeviceType.GetNestedType("IsolationHazardType", BindingFlags.NonPublic);
        Assert.That(enumType, Is.Not.Null, "Could not resolve HazardIsolationDevice.IsolationHazardType.");
        SetPrivateField(device, "hazardType", Enum.Parse(enumType, enumName));
    }

    private static void InvokeInstanceMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Could not find method '{methodName}'.");
        method.Invoke(target, args);
    }

    private static object InvokeInstanceMethodWithReturn(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Could not find method '{methodName}'.");
        return method.Invoke(target, args);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static T GetPropertyValue<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"Could not find property '{propertyName}'.");
        return (T)property.GetValue(target);
    }

    private static T GetFieldValue<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        return (T)field.GetValue(target);
    }

    private static Array CreateTypedArray(Type elementType, params object[] values)
    {
        Array array = Array.CreateInstance(elementType, values.Length);
        for (int i = 0; i < values.Length; i++)
        {
            array.SetValue(values[i], i);
        }

        return array;
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
        }

        Assert.Fail($"Could not find type '{typeName}'.");
        return null;
    }
}
