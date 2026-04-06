using System;
using System.Collections.Generic;
using UnityEngine;

public static class DisplaySettingsService
{
    public readonly struct ResolutionOption
    {
        public ResolutionOption(int width, int height, string label)
        {
            Width = width;
            Height = height;
            Label = label;
        }

        public int Width { get; }
        public int Height { get; }
        public string Label { get; }
    }

    public const string ResolutionWidthKey = "settings.graphics.resolution.width";
    public const string ResolutionHeightKey = "settings.graphics.resolution.height";
    public const string FullScreenModeKey = "settings.graphics.fullscreenMode";
    public const string VSyncEnabledKey = "settings.graphics.vsync";
    public const string ShowFpsOverlayKey = "settings.graphics.showFps";

    private static readonly ResolutionOption[] FallbackResolutions =
    {
        new ResolutionOption(3840, 2160, "3840 x 2160 (4K)"),
        new ResolutionOption(1920, 1080, "1920 x 1080 (FHD)"),
        new ResolutionOption(1280, 720, "1280 x 720 (HD)")
    };

    public static List<ResolutionOption> GetSupportedResolutions()
    {
        List<ResolutionOption> options = new List<ResolutionOption>();
        HashSet<string> seenResolutions = new HashSet<string>(StringComparer.Ordinal);

        Resolution[] systemResolutions = Screen.resolutions;
        foreach (Resolution resolution in systemResolutions)
        {
            AddResolutionOption(options, seenResolutions, resolution.width, resolution.height);
        }

        Resolution currentResolution = Screen.currentResolution;
        AddResolutionOption(options, seenResolutions, currentResolution.width, currentResolution.height);

        if (options.Count == 0)
        {
            foreach (ResolutionOption fallback in FallbackResolutions)
            {
                AddResolutionOption(options, seenResolutions, fallback.Width, fallback.Height, fallback.Label);
            }
        }

        options.Sort(CompareResolutionOptions);
        return options;
    }

    public static int GetRecommendedIndex(IReadOnlyList<ResolutionOption> options)
    {
        if (options == null || options.Count == 0)
        {
            return -1;
        }

        Resolution currentResolution = Screen.currentResolution;
        return FindBestIndex(options, currentResolution.width, currentResolution.height);
    }

    public static int FindBestIndex(IReadOnlyList<ResolutionOption> options, int width, int height)
    {
        if (options == null || options.Count == 0 || width <= 0 || height <= 0)
        {
            return -1;
        }

        for (int i = 0; i < options.Count; i++)
        {
            if (options[i].Width == width && options[i].Height == height)
            {
                return i;
            }
        }

        long targetPixels = (long)width * height;
        float targetAspect = (float)width / height;
        int bestIndex = 0;
        long bestPixelDelta = long.MaxValue;
        float bestAspectDelta = float.MaxValue;

        for (int i = 0; i < options.Count; i++)
        {
            ResolutionOption option = options[i];
            long optionPixels = (long)option.Width * option.Height;
            long pixelDelta = Math.Abs(optionPixels - targetPixels);
            float aspectDelta = Mathf.Abs(((float)option.Width / option.Height) - targetAspect);

            if (pixelDelta < bestPixelDelta ||
                (pixelDelta == bestPixelDelta && aspectDelta < bestAspectDelta))
            {
                bestIndex = i;
                bestPixelDelta = pixelDelta;
                bestAspectDelta = aspectDelta;
            }
        }

        return bestIndex;
    }

    public static bool TryGetSavedResolution(out int width, out int height)
    {
        width = 0;
        height = 0;

        if (!PlayerPrefs.HasKey(ResolutionWidthKey) || !PlayerPrefs.HasKey(ResolutionHeightKey))
        {
            return false;
        }

        width = PlayerPrefs.GetInt(ResolutionWidthKey, 0);
        height = PlayerPrefs.GetInt(ResolutionHeightKey, 0);
        return width > 0 && height > 0;
    }

    public static bool TryGetSavedFullScreenMode(out FullScreenMode fullScreenMode)
    {
        fullScreenMode = GetNormalizedFullScreenMode(Screen.fullScreenMode);

        if (!PlayerPrefs.HasKey(FullScreenModeKey))
        {
            return false;
        }

        int rawValue = PlayerPrefs.GetInt(FullScreenModeKey, (int)fullScreenMode);
        if (!Enum.IsDefined(typeof(FullScreenMode), rawValue))
        {
            return false;
        }

        fullScreenMode = GetNormalizedFullScreenMode((FullScreenMode)rawValue);
        return true;
    }

    public static void SaveResolution(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        PlayerPrefs.SetInt(ResolutionWidthKey, width);
        PlayerPrefs.SetInt(ResolutionHeightKey, height);
    }

    public static void SaveFullScreenMode(FullScreenMode fullScreenMode)
    {
        PlayerPrefs.SetInt(FullScreenModeKey, (int)GetNormalizedFullScreenMode(fullScreenMode));
    }

    public static void SaveCurrentDisplayMode()
    {
        SaveFullScreenMode(Screen.fullScreenMode);
    }

    public static bool GetCurrentVSyncEnabled()
    {
        return QualitySettings.vSyncCount > 0;
    }

    public static bool TryGetSavedVSyncEnabled(out bool enabled)
    {
        enabled = GetCurrentVSyncEnabled();

        if (!PlayerPrefs.HasKey(VSyncEnabledKey))
        {
            return false;
        }

        enabled = PlayerPrefs.GetInt(VSyncEnabledKey, enabled ? 1 : 0) != 0;
        return true;
    }

    public static void SaveVSyncEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(VSyncEnabledKey, enabled ? 1 : 0);
    }

    public static void ApplyVSync(bool enabled)
    {
        QualitySettings.vSyncCount = enabled ? 1 : 0;
    }

    public static bool ApplySavedVSync()
    {
        if (!TryGetSavedVSyncEnabled(out bool enabled))
        {
            return false;
        }

        ApplyVSync(enabled);
        return true;
    }

    public static bool TryGetSavedShowFpsOverlay(out bool enabled)
    {
        enabled = false;

        if (!PlayerPrefs.HasKey(ShowFpsOverlayKey))
        {
            return false;
        }

        enabled = PlayerPrefs.GetInt(ShowFpsOverlayKey, 0) != 0;
        return true;
    }

    public static void SaveShowFpsOverlay(bool enabled)
    {
        PlayerPrefs.SetInt(ShowFpsOverlayKey, enabled ? 1 : 0);
    }

    public static void ApplyResolution(ResolutionOption resolution)
    {
        ApplyResolution(resolution.Width, resolution.Height, GetSavedOrCurrentFullScreenMode());
    }

    public static void ApplyResolution(int width, int height)
    {
        ApplyResolution(width, height, GetSavedOrCurrentFullScreenMode());
    }

    public static void ApplyResolution(int width, int height, FullScreenMode fullScreenMode)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        Screen.SetResolution(width, height, GetNormalizedFullScreenMode(fullScreenMode));
    }

    public static bool ApplySavedDisplaySettings()
    {
        ApplySavedVSync();

        FullScreenMode fullScreenMode = GetSavedOrCurrentFullScreenMode();
        if (TryGetSavedResolution(out int savedWidth, out int savedHeight))
        {
            ApplyResolution(savedWidth, savedHeight, fullScreenMode);
            return true;
        }

        Resolution currentResolution = Screen.currentResolution;
        ApplyResolution(currentResolution.width, currentResolution.height, fullScreenMode);
        return false;
    }

    public static void ToggleFullScreenWindowed()
    {
        FullScreenMode targetMode = GetNormalizedFullScreenMode(Screen.fullScreenMode) == FullScreenMode.Windowed
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;

        int width;
        int height;
        if (!TryGetSavedResolution(out width, out height))
        {
            Resolution currentResolution = Screen.currentResolution;
            width = currentResolution.width;
            height = currentResolution.height;
        }

        ApplyResolution(width, height, targetMode);
        SaveResolution(width, height);
        SaveFullScreenMode(targetMode);
        PlayerPrefs.Save();
    }

    private static FullScreenMode GetSavedOrCurrentFullScreenMode()
    {
        if (TryGetSavedFullScreenMode(out FullScreenMode fullScreenMode))
        {
            return fullScreenMode;
        }

        return GetNormalizedFullScreenMode(Screen.fullScreenMode);
    }

    private static FullScreenMode GetNormalizedFullScreenMode(FullScreenMode fullScreenMode)
    {
        return fullScreenMode == FullScreenMode.Windowed
            ? FullScreenMode.Windowed
            : FullScreenMode.FullScreenWindow;
    }

    private static void AddResolutionOption(List<ResolutionOption> options, HashSet<string> seenResolutions, int width, int height, string label = null)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        string key = width + "x" + height;
        if (!seenResolutions.Add(key))
        {
            return;
        }

        options.Add(new ResolutionOption(width, height, string.IsNullOrEmpty(label) ? FormatResolutionLabel(width, height) : label));
    }

    private static int CompareResolutionOptions(ResolutionOption left, ResolutionOption right)
    {
        long leftPixels = (long)left.Width * left.Height;
        long rightPixels = (long)right.Width * right.Height;
        int pixelComparison = rightPixels.CompareTo(leftPixels);
        if (pixelComparison != 0)
        {
            return pixelComparison;
        }

        int widthComparison = right.Width.CompareTo(left.Width);
        if (widthComparison != 0)
        {
            return widthComparison;
        }

        return right.Height.CompareTo(left.Height);
    }

    private static string FormatResolutionLabel(int width, int height)
    {
        if (width >= 3840 && height >= 2160)
        {
            return width + " x " + height + " (4K)";
        }

        if (width >= 2560 && height >= 1440)
        {
            return width + " x " + height + " (QHD)";
        }

        if (width >= 1920 && height >= 1080)
        {
            return width + " x " + height + " (FHD)";
        }

        if (width >= 1280 && height >= 720)
        {
            return width + " x " + height + " (HD)";
        }

        return width + " x " + height;
    }
}
