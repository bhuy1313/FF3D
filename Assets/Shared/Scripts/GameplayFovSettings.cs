using UnityEngine;

public static class GameplayFovSettings
{
    public const string FovKey = "settings.graphics.gameplayFov";
    public const float MinFov = 40f;
    public const float MaxFov = 80f;
    public const float DefaultFov = 40f;

    public static float ClampFov(float fov)
    {
        return Mathf.Clamp(fov, MinFov, MaxFov);
    }

    public static bool TryGetSavedFov(out float fov)
    {
        fov = DefaultFov;

        if (!PlayerPrefs.HasKey(FovKey))
        {
            return false;
        }

        fov = ClampFov(PlayerPrefs.GetFloat(FovKey, DefaultFov));
        return true;
    }

    public static float GetSavedOrDefaultFov()
    {
        return TryGetSavedFov(out float fov) ? fov : DefaultFov;
    }

    public static void SaveFov(float fov)
    {
        PlayerPrefs.SetFloat(FovKey, ClampFov(fov));
    }
}
