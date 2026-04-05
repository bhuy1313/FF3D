using UnityEngine;

public static class CallPhaseAutoQuestionSettings
{
    public const string AutoQuestionEnabledKey = "settings.callphase.autoQuestionEnabled";
    public const bool DefaultEnabled = false;

    public static bool TryGetSavedEnabled(out bool enabled)
    {
        enabled = DefaultEnabled;

        if (!PlayerPrefs.HasKey(AutoQuestionEnabledKey))
        {
            return false;
        }

        enabled = PlayerPrefs.GetInt(AutoQuestionEnabledKey, DefaultEnabled ? 1 : 0) != 0;
        return true;
    }

    public static bool GetSavedOrDefaultEnabled()
    {
        return TryGetSavedEnabled(out bool enabled) ? enabled : DefaultEnabled;
    }

    public static void SaveEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(AutoQuestionEnabledKey, enabled ? 1 : 0);
    }
}
