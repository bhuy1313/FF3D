using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameplayFovRuntimeApplier
{
    private const string PlayerFollowCameraName = "PlayerFollowCamera";
    private static bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        EnsureSubscribed();
        ApplySavedOrDefaultFov();
    }

    public static void ApplySavedOrDefaultFov()
    {
        ApplyFov(GameplayFovSettings.GetSavedOrDefaultFov());
    }

    public static void ApplyFov(float fov)
    {
        float clampedFov = GameplayFovSettings.ClampFov(fov);
        CinemachineCamera[] cameras = UnityEngine.Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include);

        for (int i = 0; i < cameras.Length; i++)
        {
            CinemachineCamera camera = cameras[i];
            if (camera == null || !string.Equals(camera.name, PlayerFollowCameraName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            LensSettings lens = camera.Lens;
            if (Mathf.Approximately(lens.FieldOfView, clampedFov))
            {
                continue;
            }

            lens.FieldOfView = clampedFov;
            camera.Lens = lens;
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
        ApplySavedOrDefaultFov();
    }
}
