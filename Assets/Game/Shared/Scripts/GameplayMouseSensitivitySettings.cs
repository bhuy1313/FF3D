using UnityEngine;

public static class GameplayMouseSensitivitySettings
{
    public const string SensitivityKey = "settings.control.mouseSensitivity";
    private const string SensitivityScaleVersionKey = "settings.control.mouseSensitivityScaleVersion";
    private const int CurrentScaleVersion = 2;
    private const float TemporaryScaleMinSensitivity = 0.05f;
    private const float TemporaryScaleMaxSensitivity = 0.15f;

    public const float MinSensitivity = 0f;
    public const float MaxSensitivity = 2f;
    public const float DefaultSensitivity = 1f;
    public const float SliderMinValue = 0f;
    public const float SliderMaxValue = 1f;

    public static float ClampSensitivity(float sensitivity)
    {
        return Mathf.Clamp(sensitivity, MinSensitivity, MaxSensitivity);
    }

    public static bool TryGetSavedSensitivity(out float sensitivity)
    {
        sensitivity = DefaultSensitivity;

        if (!PlayerPrefs.HasKey(SensitivityKey))
        {
            return false;
        }

        MigrateLegacyTemporaryScaleIfNeeded();
        sensitivity = ClampSensitivity(PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity));
        return true;
    }

    public static float GetSavedOrDefaultSensitivity()
    {
        return TryGetSavedSensitivity(out float sensitivity) ? sensitivity : DefaultSensitivity;
    }

    public static void SaveSensitivity(float sensitivity)
    {
        PlayerPrefs.SetFloat(SensitivityKey, ClampSensitivity(sensitivity));
        PlayerPrefs.SetInt(SensitivityScaleVersionKey, CurrentScaleVersion);
    }

    public static float SliderValueToSensitivity(float sliderValue)
    {
        float normalizedValue = Mathf.InverseLerp(SliderMinValue, SliderMaxValue, sliderValue);
        return Mathf.Lerp(MinSensitivity, MaxSensitivity, normalizedValue);
    }

    public static float SensitivityToSliderValue(float sensitivity)
    {
        float normalizedValue = Mathf.InverseLerp(MinSensitivity, MaxSensitivity, ClampSensitivity(sensitivity));
        return Mathf.Lerp(SliderMinValue, SliderMaxValue, normalizedValue);
    }

    private static void MigrateLegacyTemporaryScaleIfNeeded()
    {
        if (PlayerPrefs.GetInt(SensitivityScaleVersionKey, 0) >= CurrentScaleVersion)
        {
            return;
        }

        if (!PlayerPrefs.HasKey(SensitivityKey))
        {
            PlayerPrefs.SetInt(SensitivityScaleVersionKey, CurrentScaleVersion);
            return;
        }

        float savedSensitivity = PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity);
        if (savedSensitivity >= TemporaryScaleMinSensitivity && savedSensitivity <= TemporaryScaleMaxSensitivity)
        {
            PlayerPrefs.SetFloat(SensitivityKey, ClampSensitivity(savedSensitivity * 10f));
        }

        PlayerPrefs.SetInt(SensitivityScaleVersionKey, CurrentScaleVersion);
    }
}
