using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

public class TimeOfDayPresetControllerTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type TimeOfDayPresetControllerType = FindType("TimeOfDayPresetController");
    private static readonly Type TimeOfDayPresetEnumType = FindType("TimeOfDayPresetController+TimeOfDayPreset");

    private struct RenderSettingsSnapshot
    {
        public Material skybox;
        public bool fog;
        public FogMode fogMode;
        public Color fogColor;
        public float fogStartDistance;
        public float fogEndDistance;
        public AmbientMode ambientMode;
        public Color ambientSkyColor;
        public Color ambientEquatorColor;
        public Color ambientGroundColor;
        public float ambientIntensity;
        public float reflectionIntensity;
        public Light sun;
    }

    [Test]
    public void SetPreset_AppliesNoonPresetToRenderSettingsAndSun()
    {
        RenderSettingsSnapshot snapshot = CaptureRenderSettings();
        GameObject sunObject = new GameObject("TimeOfDay Sun");

        try
        {
            Light light = sunObject.AddComponent<Light>();
            Component controller = sunObject.AddComponent(TimeOfDayPresetControllerType);
            object noonPreset = Enum.Parse(TimeOfDayPresetEnumType, "Noon");

            InvokeInstanceMethod(controller, "SetPreset", noonPreset);

            Assert.That(GetPropertyValue(controller, "SelectedPreset"), Is.EqualTo(noonPreset));
            Assert.That(light.intensity, Is.EqualTo(1.35f).Within(0.001f));
            Assert.That(light.color.r, Is.EqualTo(1f).Within(0.001f));
            Assert.That(light.color.g, Is.EqualTo(0.97f).Within(0.001f));
            Assert.That(light.color.b, Is.EqualTo(0.91f).Within(0.001f));
            Assert.That(sunObject.transform.eulerAngles.x, Is.EqualTo(70f).Within(0.001f));
            Assert.That(sunObject.transform.eulerAngles.y, Is.EqualTo(350f).Within(0.001f));
            Assert.That(RenderSettings.fog, Is.True);
            Assert.That(RenderSettings.fogMode, Is.EqualTo(FogMode.Linear));
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Trilight));
            Assert.That(RenderSettings.reflectionIntensity, Is.EqualTo(0.24f).Within(0.001f));
            Assert.That(RenderSettings.sun, Is.SameAs(light));
        }
        finally
        {
            RestoreRenderSettings(snapshot);
            UnityEngine.Object.DestroyImmediate(sunObject);
        }
    }

    private static RenderSettingsSnapshot CaptureRenderSettings()
    {
        return new RenderSettingsSnapshot
        {
            skybox = RenderSettings.skybox,
            fog = RenderSettings.fog,
            fogMode = RenderSettings.fogMode,
            fogColor = RenderSettings.fogColor,
            fogStartDistance = RenderSettings.fogStartDistance,
            fogEndDistance = RenderSettings.fogEndDistance,
            ambientMode = RenderSettings.ambientMode,
            ambientSkyColor = RenderSettings.ambientSkyColor,
            ambientEquatorColor = RenderSettings.ambientEquatorColor,
            ambientGroundColor = RenderSettings.ambientGroundColor,
            ambientIntensity = RenderSettings.ambientIntensity,
            reflectionIntensity = RenderSettings.reflectionIntensity,
            sun = RenderSettings.sun
        };
    }

    private static void RestoreRenderSettings(RenderSettingsSnapshot snapshot)
    {
        RenderSettings.skybox = snapshot.skybox;
        RenderSettings.fog = snapshot.fog;
        RenderSettings.fogMode = snapshot.fogMode;
        RenderSettings.fogColor = snapshot.fogColor;
        RenderSettings.fogStartDistance = snapshot.fogStartDistance;
        RenderSettings.fogEndDistance = snapshot.fogEndDistance;
        RenderSettings.ambientMode = snapshot.ambientMode;
        RenderSettings.ambientSkyColor = snapshot.ambientSkyColor;
        RenderSettings.ambientEquatorColor = snapshot.ambientEquatorColor;
        RenderSettings.ambientGroundColor = snapshot.ambientGroundColor;
        RenderSettings.ambientIntensity = snapshot.ambientIntensity;
        RenderSettings.reflectionIntensity = snapshot.reflectionIntensity;
        RenderSettings.sun = snapshot.sun;
    }

    private static void InvokeInstanceMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Could not find method '{methodName}'.");
        method.Invoke(target, args);
    }

    private static object GetPropertyValue(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"Could not find property '{propertyName}'.");
        return property.GetValue(target);
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
                if (candidate != null && candidate.FullName == typeName)
                {
                    return candidate;
                }
            }
        }

        Assert.Fail($"Could not find type '{typeName}'.");
        return null;
    }
}
