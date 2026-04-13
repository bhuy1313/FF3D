using System;

public static class MinimapDisplayRuntime
{
    public static event Action<MinimapDisplayType> DisplayTypeChanged;

    private static MinimapDisplayType currentType = MinimapDisplaySettings.GetSavedOrDefaultType();

    public static MinimapDisplayType CurrentType => currentType;

    public static void ApplyType(MinimapDisplayType displayType)
    {
        MinimapDisplayType clampedType = MinimapDisplaySettings.ClampType((int)displayType);
        currentType = clampedType;
        DisplayTypeChanged?.Invoke(clampedType);
    }
}
