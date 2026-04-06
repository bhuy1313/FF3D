using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

public class CallPhaseResponseSpeedSettingsTests
{
    private Type settingsType;
    private string responseSpeedStepKey;
    private int defaultStep;
    private int fastStep;

    [SetUp]
    public void SetUp()
    {
        settingsType = Type.GetType("CallPhaseResponseSpeedSettings, Assembly-CSharp");
        Assert.That(settingsType, Is.Not.Null);

        responseSpeedStepKey = GetConstString("ResponseSpeedStepKey");
        defaultStep = GetConstInt("DefaultStep");
        fastStep = GetConstInt("FastStep");

        PlayerPrefs.DeleteKey(responseSpeedStepKey);
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrWhiteSpace(responseSpeedStepKey))
        {
            PlayerPrefs.DeleteKey(responseSpeedStepKey);
        }
    }

    [Test]
    public void GetSavedOrDefaultStep_ReturnsMedium_WhenNothingSaved()
    {
        Assert.That(InvokeStatic<int>("GetSavedOrDefaultStep"), Is.EqualTo(defaultStep));
    }

    [Test]
    public void SaveStep_ClampsAndRoundTrips()
    {
        InvokeStatic("SaveStep", 99);

        object[] args = { 0 };
        bool foundSavedStep = (bool)settingsType.GetMethod("TryGetSavedStep", BindingFlags.Public | BindingFlags.Static).Invoke(null, args);

        Assert.That(foundSavedStep, Is.True);
        Assert.That((int)args[0], Is.EqualTo(fastStep));
    }

    [Test]
    public void ApplyDelayPreference_UsesSavedMultiplier()
    {
        InvokeStatic("SaveStep", fastStep);

        float adjustedDelay = InvokeStatic<float>("ApplyDelayPreference", 2f);

        Assert.That(adjustedDelay, Is.EqualTo(1.3f).Within(0.0001f));
    }

    private int GetConstInt(string fieldName)
    {
        return (int)settingsType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static).GetValue(null);
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
