using UnityEngine;

public static class AudioVolumeSettings
{
    public const float MinVolume = 0f;
    public const float MaxVolume = 1f;

    public static float ClampVolume(float volume)
    {
        return Mathf.Clamp01(volume);
    }

    public static bool TryGetSavedVolume(AudioBus bus, out float volume)
    {
        volume = GetDefaultVolume(bus);
        string key = GetPlayerPrefsKey(bus);
        if (string.IsNullOrEmpty(key) || !PlayerPrefs.HasKey(key))
        {
            return false;
        }

        volume = ClampVolume(PlayerPrefs.GetFloat(key, volume));
        return true;
    }

    public static float GetSavedOrDefaultVolume(AudioBus bus)
    {
        return TryGetSavedVolume(bus, out float volume) ? volume : GetDefaultVolume(bus);
    }

    public static void SaveVolume(AudioBus bus, float volume)
    {
        string key = GetPlayerPrefsKey(bus);
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        PlayerPrefs.SetFloat(key, ClampVolume(volume));
    }

    public static string GetPlayerPrefsKey(AudioBus bus)
    {
        switch (bus)
        {
            case AudioBus.Master:
                return "settings.audio.master";
            case AudioBus.Music:
                return "settings.audio.music";
            case AudioBus.Sfx:
                return "settings.audio.sfx";
            case AudioBus.Ui:
                return "settings.audio.ui";
            case AudioBus.Ambience:
                return "settings.audio.ambience";
            case AudioBus.Voice:
                return "settings.audio.voice";
            case AudioBus.Alert:
                return "settings.audio.alert";
            default:
                return string.Empty;
        }
    }

    public static float GetDefaultVolume(AudioBus bus)
    {
        switch (bus)
        {
            case AudioBus.Master:
                return 1f;
            case AudioBus.Music:
                return 0.75f;
            case AudioBus.Sfx:
                return 1f;
            case AudioBus.Ui:
                return 1f;
            case AudioBus.Ambience:
                return 0.85f;
            case AudioBus.Voice:
                return 1f;
            case AudioBus.Alert:
                return 1f;
            default:
                return 1f;
        }
    }
}
