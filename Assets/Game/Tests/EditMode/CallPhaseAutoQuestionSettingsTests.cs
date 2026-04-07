using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

public class CallPhaseAutoQuestionSettingsTests
{
    private Type settingsType;
    private string autoQuestionEnabledKey;
    private bool defaultEnabled;

    [SetUp]
    public void SetUp()
    {
        settingsType = Type.GetType("CallPhaseAutoQuestionSettings, Assembly-CSharp");
        Assert.That(settingsType, Is.Not.Null);

        autoQuestionEnabledKey = GetConstString("AutoQuestionEnabledKey");
        defaultEnabled = GetConstBool("DefaultEnabled");

        PlayerPrefs.DeleteKey(autoQuestionEnabledKey);
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrWhiteSpace(autoQuestionEnabledKey))
        {
            PlayerPrefs.DeleteKey(autoQuestionEnabledKey);
        }
    }

    [Test]
    public void GetSavedOrDefaultEnabled_ReturnsDefault_WhenNothingSaved()
    {
        Assert.That(InvokeStatic<bool>("GetSavedOrDefaultEnabled"), Is.EqualTo(defaultEnabled));
    }

    [Test]
    public void SaveEnabled_RoundTripsSavedValue()
    {
        InvokeStatic("SaveEnabled", true);

        object[] args = { false };
        bool foundSavedValue = (bool)settingsType.GetMethod("TryGetSavedEnabled", BindingFlags.Public | BindingFlags.Static).Invoke(null, args);

        Assert.That(foundSavedValue, Is.True);
        Assert.That((bool)args[0], Is.True);
    }

    private bool GetConstBool(string fieldName)
    {
        return (bool)settingsType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static).GetValue(null);
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
