using UnityEngine;
using UnityEngine.SceneManagement;

public static class DisplaySettingsBootstrap
{
    private static bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeDisplaySettings()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        DisplaySettingsService.ApplySavedDisplaySettings();
        SceneManager.sceneLoaded += OnSceneLoaded;
        DisplayModeHotkeyController.EnsureCreated();
        FPSOverlayRuntimeController.EnsureCreated();
    }

    private static void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        DisplaySettingsService.ApplySavedShadowQualityForCurrentScene();
        DisplaySettingsService.ApplySavedAntiAliasingModeForCurrentScene();
    }
}
