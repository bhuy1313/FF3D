using UnityEngine;

public static class GameplayRenderDistanceSettings
{
    public enum RenderDistanceLevel
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    public const string RenderDistanceLevelKey = "settings.graphics.renderDistanceLevel";
    public const RenderDistanceLevel DefaultLevel = RenderDistanceLevel.Medium;

    public static int GetClampedStep(int step)
    {
        return Mathf.Clamp(step, (int)RenderDistanceLevel.Low, (int)RenderDistanceLevel.High);
    }

    public static RenderDistanceLevel GetClampedLevel(RenderDistanceLevel level)
    {
        return (RenderDistanceLevel)GetClampedStep((int)level);
    }

    public static bool TryGetSavedLevel(out RenderDistanceLevel level)
    {
        level = DefaultLevel;

        if (!PlayerPrefs.HasKey(RenderDistanceLevelKey))
        {
            return false;
        }

        level = GetClampedLevel((RenderDistanceLevel)PlayerPrefs.GetInt(RenderDistanceLevelKey, (int)DefaultLevel));
        return true;
    }

    public static RenderDistanceLevel GetSavedOrDefaultLevel()
    {
        return TryGetSavedLevel(out RenderDistanceLevel level) ? level : DefaultLevel;
    }

    public static void SaveLevel(RenderDistanceLevel level)
    {
        PlayerPrefs.SetInt(RenderDistanceLevelKey, (int)GetClampedLevel(level));
    }

    public static void SaveStep(int step)
    {
        SaveLevel((RenderDistanceLevel)GetClampedStep(step));
    }
}
