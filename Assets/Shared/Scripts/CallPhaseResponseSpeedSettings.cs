using UnityEngine;

public static class CallPhaseResponseSpeedSettings
{
    public const string ResponseSpeedStepKey = "settings.callphase.responseSpeedStep";
    public const int SlowStep = 0;
    public const int MediumStep = 1;
    public const int FastStep = 2;
    public const int DefaultStep = MediumStep;

    private const float SlowDelayMultiplier = 1.4f;
    private const float MediumDelayMultiplier = 1f;
    private const float FastDelayMultiplier = 0.65f;

    public static int GetClampedStep(int step)
    {
        return Mathf.Clamp(step, SlowStep, FastStep);
    }

    public static bool TryGetSavedStep(out int step)
    {
        step = DefaultStep;

        if (!PlayerPrefs.HasKey(ResponseSpeedStepKey))
        {
            return false;
        }

        step = GetClampedStep(PlayerPrefs.GetInt(ResponseSpeedStepKey, DefaultStep));
        return true;
    }

    public static int GetSavedOrDefaultStep()
    {
        return TryGetSavedStep(out int step) ? step : DefaultStep;
    }

    public static void SaveStep(int step)
    {
        PlayerPrefs.SetInt(ResponseSpeedStepKey, GetClampedStep(step));
    }

    public static float GetDelayMultiplierForStep(int step)
    {
        switch (GetClampedStep(step))
        {
            case SlowStep:
                return SlowDelayMultiplier;
            case FastStep:
                return FastDelayMultiplier;
            default:
                return MediumDelayMultiplier;
        }
    }

    public static float GetSavedOrDefaultDelayMultiplier()
    {
        return GetDelayMultiplierForStep(GetSavedOrDefaultStep());
    }

    public static float ApplyDelayPreference(float baseDelaySeconds)
    {
        return Mathf.Max(0f, baseDelaySeconds) * GetSavedOrDefaultDelayMultiplier();
    }
}
