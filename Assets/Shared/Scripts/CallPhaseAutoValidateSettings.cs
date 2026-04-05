using UnityEngine;

public static class CallPhaseAutoValidateSettings
{
    public const string AutoValidateEnabledKey = "settings.callphase.autoValidateEnabled";
    public const bool DefaultEnabled = false;

    public static bool TryGetSavedEnabled(out bool enabled)
    {
        enabled = DefaultEnabled;

        if (!PlayerPrefs.HasKey(AutoValidateEnabledKey))
        {
            return false;
        }

        enabled = PlayerPrefs.GetInt(AutoValidateEnabledKey, DefaultEnabled ? 1 : 0) != 0;
        return true;
    }

    public static bool GetSavedOrDefaultEnabled()
    {
        return TryGetSavedEnabled(out bool enabled) ? enabled : DefaultEnabled;
    }

    public static void SaveEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(AutoValidateEnabledKey, enabled ? 1 : 0);
    }
}
