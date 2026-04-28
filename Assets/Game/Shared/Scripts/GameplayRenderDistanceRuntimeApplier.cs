using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameplayRenderDistanceRuntimeApplier
{
    private const string PlayerFollowCameraName = "PlayerFollowCamera";

    private struct RenderDistanceValues
    {
        public float ShadowDistance;
        public float TerrainDetailDistance;
        public float TerrainTreeDistance;
        public float LodBias;
        public float FarClipPlane;
        public float FogStartDistance;
        public float FogEndDistance;
    }

    private static bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        EnsureSubscribed();
        ApplySavedOrDefaultLevel();
    }

    public static void ApplySavedOrDefaultLevel()
    {
        ApplyLevel(GameplayRenderDistanceSettings.GetSavedOrDefaultLevel());
    }

    public static void ApplyStep(int step)
    {
        ApplyLevel((GameplayRenderDistanceSettings.RenderDistanceLevel)GameplayRenderDistanceSettings.GetClampedStep(step));
    }

    public static void ApplyLevel(GameplayRenderDistanceSettings.RenderDistanceLevel level)
    {
        RenderDistanceValues values = ResolveValues(level);

        QualitySettings.shadowDistance = values.ShadowDistance;
        QualitySettings.lodBias = values.LodBias;
        if (!IsGameplaySceneContext())
        {
            return;
        }

        ApplyFarClipPlane(values.FarClipPlane);
        ApplyFog(values.FogStartDistance, values.FogEndDistance);

        Terrain[] terrains = Terrain.activeTerrains;
        for (int index = 0; index < terrains.Length; index++)
        {
            Terrain terrain = terrains[index];
            if (terrain == null)
            {
                continue;
            }

            terrain.detailObjectDistance = values.TerrainDetailDistance;
            terrain.treeDistance = values.TerrainTreeDistance;
        }
    }

    private static bool IsGameplaySceneContext()
    {
        if (UnityEngine.Object.FindAnyObjectByType<PlayerVitals>(FindObjectsInactive.Exclude) != null)
        {
            return true;
        }

        if (UnityEngine.Object.FindAnyObjectByType<IncidentMissionSystem>(FindObjectsInactive.Exclude) != null)
        {
            return true;
        }

        CinemachineCamera[] cinemachineCameras = UnityEngine.Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include);
        for (int index = 0; index < cinemachineCameras.Length; index++)
        {
            CinemachineCamera camera = cinemachineCameras[index];
            if (camera != null && string.Equals(camera.name, PlayerFollowCameraName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static RenderDistanceValues ResolveValues(GameplayRenderDistanceSettings.RenderDistanceLevel level)
    {
        switch (GameplayRenderDistanceSettings.GetClampedLevel(level))
        {
            case GameplayRenderDistanceSettings.RenderDistanceLevel.Low:
                return new RenderDistanceValues
                {
                    ShadowDistance = 30f,
                    TerrainDetailDistance = 30f,
                    TerrainTreeDistance = 180f,
                    LodBias = 1f,
                    FarClipPlane = 50f,
                    FogStartDistance = 28f,
                    FogEndDistance = 50f
                };
            case GameplayRenderDistanceSettings.RenderDistanceLevel.High:
                return new RenderDistanceValues
                {
                    ShadowDistance = 100f,
                    TerrainDetailDistance = 100f,
                    TerrainTreeDistance = 500f,
                    LodBias = 3f,
                    FarClipPlane = 150f,
                    FogStartDistance = 95f,
                    FogEndDistance = 150f
                };
            default:
                return new RenderDistanceValues
                {
                    ShadowDistance = 60f,
                    TerrainDetailDistance = 60f,
                    TerrainTreeDistance = 300f,
                    LodBias = 2f,
                    FarClipPlane = 100f,
                    FogStartDistance = 60f,
                    FogEndDistance = 100f
                };
        }
    }

    private static void ApplyFarClipPlane(float farClipPlane)
    {
        float clampedFarClipPlane = Mathf.Max(50f, farClipPlane);

        CinemachineCamera[] cinemachineCameras = UnityEngine.Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include);
        for (int index = 0; index < cinemachineCameras.Length; index++)
        {
            CinemachineCamera cinemachineCamera = cinemachineCameras[index];
            if (cinemachineCamera == null || !string.Equals(cinemachineCamera.name, PlayerFollowCameraName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            LensSettings lens = cinemachineCamera.Lens;
            if (!Mathf.Approximately(lens.FarClipPlane, clampedFarClipPlane))
            {
                lens.FarClipPlane = clampedFarClipPlane;
                cinemachineCamera.Lens = lens;
            }
        }

        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        for (int index = 0; index < cameras.Length; index++)
        {
            Camera camera = cameras[index];
            if (camera == null)
            {
                continue;
            }

            if (!camera.CompareTag("MainCamera") && !string.Equals(camera.name, "Main Camera", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Mathf.Approximately(camera.farClipPlane, clampedFarClipPlane))
            {
                camera.farClipPlane = clampedFarClipPlane;
            }
        }
    }

    private static void ApplyFog(float fogStartDistance, float fogEndDistance)
    {
        float clampedFogEnd = Mathf.Max(50f, fogEndDistance);
        float clampedFogStart = Mathf.Clamp(fogStartDistance, 0f, clampedFogEnd - 1f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = clampedFogStart;
        RenderSettings.fogEndDistance = clampedFogEnd;
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
        ApplySavedOrDefaultLevel();
    }
}
