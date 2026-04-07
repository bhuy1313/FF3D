using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class FireTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type FireType = FindType("Fire");
    private static readonly Type FireHazardType = FindType("FireHazardType");
    private static readonly Type FireSuppressionAgentType = FindType("FireSuppressionAgent");
    private static readonly Type FireExtinguisherType = FindType("FireExtinguisher");
    private static readonly Type FireExtinguisherKindType = FindType("FireExtinguisherType");

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

    [Test]
    public void ElectricalFire_BlocksWaterUntilHazardIsolated()
    {
        GameObject fireObject = new GameObject("ElectricalFire");

        try
        {
            fireObject.AddComponent<SphereCollider>();
            Component fire = fireObject.AddComponent(FireType);

            SetPrivateField(fire, "fireType", Enum.Parse(FireHazardType, "Electrical"));
            SetPrivateField(fire, "currentHp", 1f);
            SetPrivateField(fire, "maxHp", 1f);

            InvokeInstanceMethod(fire, "ApplySuppression", 0.5f, Enum.Parse(FireSuppressionAgentType, "Water"));
            Assert.That(GetFieldValue<float>(fire, "currentHp"), Is.EqualTo(1f).Within(0.001f));

            InvokeInstanceMethod(fire, "SetHazardSourceIsolated", true);
            InvokeInstanceMethod(fire, "ApplySuppression", 0.5f, Enum.Parse(FireSuppressionAgentType, "Water"));
            Assert.That(GetFieldValue<float>(fire, "currentHp"), Is.LessThan(1f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fireObject);
        }
    }

    [Test]
    public void GasFedFire_CannotBeFullyExtinguishedUntilHazardIsolated()
    {
        GameObject fireObject = new GameObject("GasFire");

        try
        {
            fireObject.AddComponent<SphereCollider>();
            Component fire = fireObject.AddComponent(FireType);

            SetPrivateField(fire, "fireType", Enum.Parse(FireHazardType, "GasFed"));
            SetPrivateField(fire, "currentHp", 1f);
            SetPrivateField(fire, "maxHp", 1f);
            SetPrivateField(fire, "requiresIsolationToFullyExtinguish", true);
            SetPrivateField(fire, "hazardActiveMinimumHpNormalized", 0.2f);

            InvokeInstanceMethod(fire, "ApplySuppression", 5f, Enum.Parse(FireSuppressionAgentType, "DryChemical"));
            Assert.That(GetFieldValue<float>(fire, "currentHp"), Is.GreaterThan(0.19f));

            InvokeInstanceMethod(fire, "SetHazardSourceIsolated", true);
            InvokeInstanceMethod(fire, "ApplySuppression", 5f, Enum.Parse(FireSuppressionAgentType, "DryChemical"));
            Assert.That(GetFieldValue<float>(fire, "currentHp"), Is.EqualTo(0f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fireObject);
        }
    }

    [Test]
    public void FlammableLiquidFire_PrefersDryChemicalOverWater()
    {
        GameObject waterFireObject = new GameObject("WaterSuppressedFire");
        GameObject dryFireObject = new GameObject("DrySuppressedFire");

        try
        {
            waterFireObject.AddComponent<SphereCollider>();
            dryFireObject.AddComponent<SphereCollider>();
            Component waterFire = waterFireObject.AddComponent(FireType);
            Component dryFire = dryFireObject.AddComponent(FireType);

            SetPrivateField(waterFire, "fireType", Enum.Parse(FireHazardType, "FlammableLiquid"));
            SetPrivateField(dryFire, "fireType", Enum.Parse(FireHazardType, "FlammableLiquid"));
            SetPrivateField(waterFire, "currentHp", 1f);
            SetPrivateField(dryFire, "currentHp", 1f);
            SetPrivateField(waterFire, "maxHp", 1f);
            SetPrivateField(dryFire, "maxHp", 1f);

            InvokeInstanceMethod(waterFire, "ApplySuppression", 0.5f, Enum.Parse(FireSuppressionAgentType, "Water"));
            InvokeInstanceMethod(dryFire, "ApplySuppression", 0.5f, Enum.Parse(FireSuppressionAgentType, "DryChemical"));

            Assert.That(GetFieldValue<float>(dryFire, "currentHp"), Is.LessThan(GetFieldValue<float>(waterFire, "currentHp")));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(waterFireObject);
            UnityEngine.Object.DestroyImmediate(dryFireObject);
        }
    }

    [Test]
    public void ElectricalFire_PrefersCo2OverDryChemicalWhileHazardActive()
    {
        GameObject co2FireObject = new GameObject("CO2Fire");
        GameObject dryFireObject = new GameObject("DryFire");

        try
        {
            co2FireObject.AddComponent<SphereCollider>();
            dryFireObject.AddComponent<SphereCollider>();
            Component co2Fire = co2FireObject.AddComponent(FireType);
            Component dryFire = dryFireObject.AddComponent(FireType);

            SetPrivateField(co2Fire, "fireType", Enum.Parse(FireHazardType, "Electrical"));
            SetPrivateField(dryFire, "fireType", Enum.Parse(FireHazardType, "Electrical"));
            SetPrivateField(co2Fire, "currentHp", 1f);
            SetPrivateField(dryFire, "currentHp", 1f);
            SetPrivateField(co2Fire, "maxHp", 1f);
            SetPrivateField(dryFire, "maxHp", 1f);

            InvokeInstanceMethod(co2Fire, "ApplySuppression", 0.5f, Enum.Parse(FireSuppressionAgentType, "CO2"));
            InvokeInstanceMethod(dryFire, "ApplySuppression", 0.5f, Enum.Parse(FireSuppressionAgentType, "DryChemical"));

            Assert.That(GetFieldValue<float>(co2Fire, "currentHp"), Is.LessThan(GetFieldValue<float>(dryFire, "currentHp")));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(co2FireObject);
            UnityEngine.Object.DestroyImmediate(dryFireObject);
        }
    }

    [Test]
    public void OrdinaryCombustibles_PreferWaterOverCo2()
    {
        GameObject waterFireObject = new GameObject("WaterFire");
        GameObject co2FireObject = new GameObject("CO2Fire");

        try
        {
            waterFireObject.AddComponent<SphereCollider>();
            co2FireObject.AddComponent<SphereCollider>();
            Component waterFire = waterFireObject.AddComponent(FireType);
            Component co2Fire = co2FireObject.AddComponent(FireType);

            SetPrivateField(waterFire, "fireType", Enum.Parse(FireHazardType, "OrdinaryCombustibles"));
            SetPrivateField(co2Fire, "fireType", Enum.Parse(FireHazardType, "OrdinaryCombustibles"));
            SetPrivateField(waterFire, "currentHp", 1f);
            SetPrivateField(co2Fire, "currentHp", 1f);
            SetPrivateField(waterFire, "maxHp", 1f);
            SetPrivateField(co2Fire, "maxHp", 1f);

            InvokeInstanceMethod(waterFire, "ApplySuppression", 0.5f, Enum.Parse(FireSuppressionAgentType, "Water"));
            InvokeInstanceMethod(co2Fire, "ApplySuppression", 0.5f, Enum.Parse(FireSuppressionAgentType, "CO2"));

            Assert.That(GetFieldValue<float>(waterFire, "currentHp"), Is.LessThan(GetFieldValue<float>(co2Fire, "currentHp")));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(waterFireObject);
            UnityEngine.Object.DestroyImmediate(co2FireObject);
        }
    }

    [Test]
    public void FireExtinguisher_UsesConfiguredCo2SuppressionAgent()
    {
        GameObject extinguisherObject = new GameObject("Extinguisher");

        try
        {
            extinguisherObject.AddComponent<Rigidbody>();
            Component extinguisher = extinguisherObject.AddComponent(FireExtinguisherType);

            SetPrivateField(extinguisher, "extinguisherType", Enum.Parse(FireExtinguisherKindType, "CO2"));

            PropertyInfo suppressionAgentProperty = FireExtinguisherType.GetProperty("SuppressionAgent", InstanceFlags);
            Assert.That(suppressionAgentProperty, Is.Not.Null);

            object configuredAgent = suppressionAgentProperty.GetValue(extinguisher);
            Assert.That(configuredAgent, Is.EqualTo(Enum.Parse(FireSuppressionAgentType, "CO2")));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(extinguisherObject);
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
