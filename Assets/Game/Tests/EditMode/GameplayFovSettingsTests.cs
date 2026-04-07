using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

public class GameplayFovSettingsTests
{
    private Type settingsType;
    private string fovKey;
    private float minFov;
    private float maxFov;
    private float defaultFov;

    [SetUp]
    public void SetUp()
    {
        settingsType = Type.GetType("GameplayFovSettings, Assembly-CSharp");
        Assert.That(settingsType, Is.Not.Null);

        fovKey = GetConstString("FovKey");
        minFov = GetConstFloat("MinFov");
        maxFov = GetConstFloat("MaxFov");
        defaultFov = GetConstFloat("DefaultFov");

        PlayerPrefs.DeleteKey(fovKey);
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrWhiteSpace(fovKey))
        {
            PlayerPrefs.DeleteKey(fovKey);
        }
    }

    [Test]
    public void GetSavedOrDefaultFov_ReturnsDefault_WhenNothingSaved()
    {
        Assert.That(InvokeStatic<float>("GetSavedOrDefaultFov"), Is.EqualTo(defaultFov));
    }

    [Test]
    public void SaveFov_ClampsToRange()
    {
        InvokeStatic("SaveFov", maxFov + 50f);

        object[] args = { 0f };
        bool foundSavedValue = (bool)settingsType.GetMethod("TryGetSavedFov", BindingFlags.Public | BindingFlags.Static).Invoke(null, args);

        Assert.That(foundSavedValue, Is.True);
        Assert.That((float)args[0], Is.EqualTo(maxFov).Within(0.0001f));
    }

    [Test]
    public void ClampFov_ClampsBelowMinimum()
    {
        float result = InvokeStatic<float>("ClampFov", minFov - 10f);
        Assert.That(result, Is.EqualTo(minFov).Within(0.0001f));
    }

    private float GetConstFloat(string fieldName)
    {
        return (float)settingsType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static).GetValue(null);
    }

    private string GetConstString(string fieldName)
    {
        return (string)settingsType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static).GetValue(null);
    }

    private void InvokeStatic(string methodName, params object[] args)
    {
        settingsType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
    }

    private T InvokeStatic<T>(string methodName, params object[] args)
    {
        return (T)settingsType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
    }
}
