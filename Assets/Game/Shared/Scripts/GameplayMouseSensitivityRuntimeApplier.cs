using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameplayMouseSensitivityRuntimeApplier
{
    private static bool subscribed;
    private static bool hasCurrentSensitivity;
    private static float currentSensitivity = GameplayMouseSensitivitySettings.DefaultSensitivity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        EnsureSubscribed();
        ApplySavedOrDefaultSensitivity();
    }

    public static void ApplySavedOrDefaultSensitivity()
    {
        ApplySensitivity(GameplayMouseSensitivitySettings.GetSavedOrDefaultSensitivity());
    }

    public static float GetCurrentOrSavedSensitivity()
    {
        return hasCurrentSensitivity
            ? currentSensitivity
            : GameplayMouseSensitivitySettings.GetSavedOrDefaultSensitivity();
    }

    public static void ApplySensitivity(float sensitivity)
    {
        float clampedSensitivity = GameplayMouseSensitivitySettings.ClampSensitivity(sensitivity);
        currentSensitivity = clampedSensitivity;
        hasCurrentSensitivity = true;
        FirstPersonController[] controllers = Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include);

        for (int i = 0; i < controllers.Length; i++)
        {
            FirstPersonController controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            controller.SetMouseSensitivityMultiplier(clampedSensitivity);
        }
    }

    private static void EnsureSubscribed()
    {
        if (subscribed)
        {
            return;
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
        subscribed = true;
    }

    private static void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        ApplySavedOrDefaultSensitivity();
    }
}
