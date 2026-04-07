using UnityEngine;

public static class DisplaySettingsBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeDisplaySettings()
    {
        DisplaySettingsService.ApplySavedDisplaySettings();
        DisplayModeHotkeyController.EnsureCreated();
        FPSOverlayRuntimeController.EnsureCreated();
    }
}
