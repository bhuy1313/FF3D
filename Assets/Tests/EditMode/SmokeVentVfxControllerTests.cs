using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class SmokeVentVfxControllerTests
{
    private sealed class DummyVentPoint : MonoBehaviour, ISmokeVentPoint
    {
        public bool IsOpen => smokeVentilationRelief > 0f;
        public float SmokeVentilationRelief => smokeVentilationRelief;
        public float FireDraftRisk => 0f;

        public float smokeVentilationRelief;
    }

    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type SmokeVentVfxControllerType = FindType("SmokeVentVfxController");
    private static readonly Type SmokeHazardType = FindType("SmokeHazard");

    [Test]
    public void ResolveSmokeDensity_UsesPreviewDensity_WhenNoHazardIsAssigned()
    {
        GameObject target = new GameObject("VentVfx");

        try
        {
            Component controller = target.AddComponent(SmokeVentVfxControllerType);
            SetPrivateField(controller, "usePreviewDensityWhenNoHazard", true);
            SetPrivateField(controller, "previewSmokeDensity", 0.72f);

            float density = (float)InvokeInstanceMethod(controller, "ResolveSmokeDensity");

            Assert.That(density, Is.EqualTo(0.72f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void ResolveSmokeDensity_PrefersAssignedSmokeHazardDensity()
    {
        GameObject target = new GameObject("VentVfx");
        GameObject hazardObject = new GameObject("SmokeHazard");

        try
        {
            Component controller = target.AddComponent(SmokeVentVfxControllerType);
            Component hazard = hazardObject.AddComponent(SmokeHazardType);

            SetPrivateField(controller, "usePreviewDensityWhenNoHazard", true);
            SetPrivateField(controller, "previewSmokeDensity", 0.2f);
            SetPrivateField(controller, "smokeHazard", hazard);
            SetPrivateField(hazard, "currentSmokeDensity", 0.48f);

            float density = (float)InvokeInstanceMethod(controller, "ResolveSmokeDensity");

            Assert.That(density, Is.EqualTo(0.48f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(hazardObject);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void CalculateEmissionStrength_ScalesWithVentReliefAndSmokeDensity()
    {
        GameObject target = new GameObject("VentVfx");

        try
        {
            Component controller = target.AddComponent(SmokeVentVfxControllerType);
            DummyVentPoint ventPoint = target.AddComponent<DummyVentPoint>();
            ventPoint.smokeVentilationRelief = 0.4f;

            SetPrivateField(controller, "ventStrengthMultiplier", 2.5f);
            SetPrivateField(controller, "ventPointSource", ventPoint);
            InvokeInstanceMethod(controller, "ResolveReferences");

            float strength = (float)InvokeInstanceMethod(controller, "CalculateEmissionStrength", 0.5f);

            Assert.That(strength, Is.EqualTo(0.5f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void RebuildEmitters_InstantiatesAssignedSmokePrefabAndScalesEmission()
    {
        GameObject target = new GameObject("VentVfx");
        GameObject emitPoint = new GameObject("EmitPoint");
        GameObject smokePrefab = new GameObject("SmokePrefab");

        try
        {
            emitPoint.transform.SetParent(target.transform, false);

            ParticleSystem prefabParticle = smokePrefab.AddComponent<ParticleSystem>();
            ParticleSystem.EmissionModule prefabEmission = prefabParticle.emission;
            prefabEmission.rateOverTimeMultiplier = 10f;

            Component controller = target.AddComponent(SmokeVentVfxControllerType);
            SetPrivateField(controller, "smokePrefab", smokePrefab);
            SetPrivateField(controller, "emitPoints", new[] { emitPoint.transform });

            InvokeInstanceMethod(controller, "RebuildEmitters");
            InvokeInstanceMethod(controller, "SetEmissionStrength", 0.5f);

            ParticleSystem spawnedParticle = target.GetComponentInChildren<ParticleSystem>(true);
            Assert.That(spawnedParticle, Is.Not.Null);
            Assert.That(spawnedParticle.gameObject.name, Does.StartWith("SmokePrefab_SmokeEmitter_"));
            Assert.That(spawnedParticle.emission.rateOverTimeMultiplier, Is.EqualTo(5f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(smokePrefab);
            UnityEngine.Object.DestroyImmediate(target);
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
