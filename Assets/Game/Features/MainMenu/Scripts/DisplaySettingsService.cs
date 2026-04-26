using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class DisplaySettingsService
{
    public enum AntiAliasingMode
    {
        None = 0,
        FXAA = 1,
        SMAA = 2,
        TAA = 3
    }

    public enum ShadowQualityLevel
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

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

    public readonly struct AntiAliasingOption
    {
        public AntiAliasingOption(AntiAliasingMode mode, string label)
        {
            Mode = mode;
            Label = label;
        }

        public AntiAliasingMode Mode { get; }
        public string Label { get; }
    }

    public const string ResolutionWidthKey = "settings.graphics.resolution.width";
    public const string ResolutionHeightKey = "settings.graphics.resolution.height";
    public const string FullScreenModeKey = "settings.graphics.fullscreenMode";
    public const string VSyncEnabledKey = "settings.graphics.vsync";
    public const string ShadowQualityKey = "settings.graphics.shadowQuality";
    public const string AntiAliasingModeKey = "settings.graphics.antiAliasingMode";
    public const string ShowFpsOverlayKey = "settings.graphics.showFps";

    private static readonly ResolutionOption[] FallbackResolutions =
    {
        new ResolutionOption(3840, 2160, "3840 x 2160"),
        new ResolutionOption(1920, 1080, "1920 x 1080"),
        new ResolutionOption(1280, 720, "1280 x 720")
    };

    private static readonly AntiAliasingOption[] AntiAliasingOptions =
    {
        new AntiAliasingOption(AntiAliasingMode.None, "OFF"),
        new AntiAliasingOption(AntiAliasingMode.FXAA, "FXAA"),
        new AntiAliasingOption(AntiAliasingMode.SMAA, "SMAA"),
        new AntiAliasingOption(AntiAliasingMode.TAA, "TAA")
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

    public static IReadOnlyList<AntiAliasingOption> GetAvailableAntiAliasingOptions()
    {
        return AntiAliasingOptions;
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

    public static AntiAliasingMode GetCurrentAntiAliasingMode()
    {
        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int index = 0; index < cameras.Length; index++)
        {
            Camera camera = cameras[index];
            if (camera == null || !camera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
            {
                continue;
            }

            return FromUrpAntiAliasing(cameraData.antialiasing);
        }

        return AntiAliasingMode.None;
    }

    public static bool TryGetSavedAntiAliasingMode(out AntiAliasingMode mode)
    {
        mode = GetCurrentAntiAliasingMode();
        if (!PlayerPrefs.HasKey(AntiAliasingModeKey))
        {
            return false;
        }

        int rawValue = PlayerPrefs.GetInt(AntiAliasingModeKey, (int)mode);
        if (rawValue < (int)AntiAliasingMode.None || rawValue > (int)AntiAliasingMode.TAA)
        {
            mode = AntiAliasingMode.None;
            return false;
        }

        mode = (AntiAliasingMode)rawValue;
        return true;
    }

    public static void SaveAntiAliasingMode(AntiAliasingMode mode)
    {
        PlayerPrefs.SetInt(AntiAliasingModeKey, (int)mode);
    }

    public static void ApplyAntiAliasingMode(AntiAliasingMode mode)
    {
        AntialiasingMode urpMode = ToUrpAntiAliasing(mode);
        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        bool applied = false;

        for (int index = 0; index < cameras.Length; index++)
        {
            Camera camera = cameras[index];
            if (camera == null || !camera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
            {
                continue;
            }

            cameraData.antialiasing = urpMode;
            if (mode != AntiAliasingMode.None)
            {
                cameraData.renderPostProcessing = true;
            }

            applied = true;
        }

        if (!applied)
        {
            Debug.LogWarning("DisplaySettingsService: No URP camera data found to apply anti-aliasing.");
        }
    }

    public static bool ApplySavedAntiAliasingMode()
    {
        if (!TryGetSavedAntiAliasingMode(out AntiAliasingMode mode))
        {
            return false;
        }

        ApplyAntiAliasingMode(mode);
        return true;
    }

    public static void ApplySavedAntiAliasingModeForCurrentScene()
    {
        if (TryGetSavedAntiAliasingMode(out AntiAliasingMode mode))
        {
            ApplyAntiAliasingMode(mode);
        }
    }

    public static ShadowQualityLevel GetCurrentShadowQuality()
    {
        LightShadows currentLightShadows = GetCurrentDirectionalLightShadows();
        return currentLightShadows switch
        {
            LightShadows.None => ShadowQualityLevel.Low,
            LightShadows.Hard => ShadowQualityLevel.Medium,
            _ => ShadowQualityLevel.High
        };
    }

    public static bool TryGetSavedShadowQuality(out ShadowQualityLevel level)
    {
        level = GetCurrentShadowQuality();
        if (!PlayerPrefs.HasKey(ShadowQualityKey))
        {
            return false;
        }

        int rawValue = PlayerPrefs.GetInt(ShadowQualityKey, (int)level);
        if (rawValue < (int)ShadowQualityLevel.Low || rawValue > (int)ShadowQualityLevel.High)
        {
            // Backward compatibility: old "Ultra" or invalid values collapse to High.
            level = ShadowQualityLevel.High;
            return false;
        }

        level = (ShadowQualityLevel)rawValue;
        return true;
    }

    public static void SaveShadowQuality(ShadowQualityLevel level)
    {
        PlayerPrefs.SetInt(ShadowQualityKey, (int)level);
    }

    public static void ApplyShadowQuality(ShadowQualityLevel level)
    {
        LightShadows targetLightShadows = level switch
        {
            ShadowQualityLevel.Low => LightShadows.None,
            ShadowQualityLevel.Medium => LightShadows.Hard,
            _ => LightShadows.Soft
        };

        if (!ApplyDirectionalLightShadows(targetLightShadows))
        {
            Debug.LogWarning("DisplaySettingsService: No active Directional Light found to apply shadow quality.");
        }
    }

    public static bool ApplySavedShadowQuality()
    {
        if (!TryGetSavedShadowQuality(out ShadowQualityLevel level))
        {
            return false;
        }

        ApplyShadowQuality(level);
        return true;
    }

    public static void ApplySavedShadowQualityForCurrentScene()
    {
        if (TryGetSavedShadowQuality(out ShadowQualityLevel level))
        {
            ApplyShadowQuality(level);
        }
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
        ApplySavedShadowQuality();
        ApplySavedAntiAliasingMode();

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
        return width + " x " + height;
    }

    private static LightShadows GetCurrentDirectionalLightShadows()
    {
        Light directionalLight = FindDirectionalLight();
        if (directionalLight == null)
        {
            return LightShadows.None;
        }

        return directionalLight.shadows;
    }

    private static bool ApplyDirectionalLightShadows(LightShadows shadows)
    {
        bool applied = false;
        Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int index = 0; index < lights.Length; index++)
        {
            Light light = lights[index];
            if (light == null || light.type != LightType.Directional)
            {
                continue;
            }

            light.shadows = shadows;
            applied = true;
        }

        if (!applied)
        {
            Light fallbackLight = FindDirectionalLight();
            if (fallbackLight != null)
            {
                fallbackLight.shadows = shadows;
                applied = true;
            }
        }

        return applied;
    }

    private static Light FindDirectionalLight()
    {
        if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional)
        {
            return RenderSettings.sun;
        }

        Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int index = 0; index < lights.Length; index++)
        {
            Light light = lights[index];
            if (light != null && light.type == LightType.Directional)
            {
                return light;
            }
        }

        return null;
    }

    private static AntialiasingMode ToUrpAntiAliasing(AntiAliasingMode mode)
    {
        return mode switch
        {
            AntiAliasingMode.FXAA => AntialiasingMode.FastApproximateAntialiasing,
            AntiAliasingMode.SMAA => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
            AntiAliasingMode.TAA => AntialiasingMode.TemporalAntiAliasing,
            _ => AntialiasingMode.None
        };
    }

    private static AntiAliasingMode FromUrpAntiAliasing(AntialiasingMode mode)
    {
        return mode switch
        {
            AntialiasingMode.FastApproximateAntialiasing => AntiAliasingMode.FXAA,
            AntialiasingMode.SubpixelMorphologicalAntiAliasing => AntiAliasingMode.SMAA,
            AntialiasingMode.TemporalAntiAliasing => AntiAliasingMode.TAA,
            _ => AntiAliasingMode.None
        };
    }
}
