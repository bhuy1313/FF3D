using System;

public static class MinimapDisplayRuntime
{
    public static event Action<MinimapDisplayType> DisplayTypeChanged;
    public static event Action<bool> EnabledChanged;

    private static MinimapDisplayType currentType = MinimapDisplaySettings.GetSavedOrDefaultType();
    private static bool currentEnabled = MinimapDisplaySettings.GetSavedOrDefaultEnabled();

    public static MinimapDisplayType CurrentType => currentType;
    public static bool CurrentEnabled => currentEnabled;

    public static void ApplyType(MinimapDisplayType displayType)
    {
        MinimapDisplayType clampedType = MinimapDisplaySettings.ClampType((int)displayType);
        currentType = clampedType;
        DisplayTypeChanged?.Invoke(clampedType);
    }

    public static void ApplyEnabled(bool enabled)
    {
        currentEnabled = enabled;
        EnabledChanged?.Invoke(enabled);
    }
}
