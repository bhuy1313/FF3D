using UnityEngine;

public enum MinimapDisplayType
{
    Square = 0,
    Circle = 1
}

public static class MinimapDisplaySettings
{
    public const string PlayerPrefsKey = "settings.minimap.display_type";
    public const MinimapDisplayType DefaultType = MinimapDisplayType.Square;

    public static bool TryGetSavedType(out MinimapDisplayType savedType)
    {
        if (!PlayerPrefs.HasKey(PlayerPrefsKey))
        {
            savedType = DefaultType;
            return false;
        }

        savedType = ClampType(PlayerPrefs.GetInt(PlayerPrefsKey, (int)DefaultType));
        return true;
    }

    public static MinimapDisplayType GetSavedOrDefaultType()
    {
        return TryGetSavedType(out MinimapDisplayType savedType) ? savedType : DefaultType;
    }

    public static void SaveType(MinimapDisplayType type)
    {
        PlayerPrefs.SetInt(PlayerPrefsKey, (int)ClampType((int)type));
    }

    public static MinimapDisplayType ClampType(int rawValue)
    {
        return rawValue == (int)MinimapDisplayType.Circle
            ? MinimapDisplayType.Circle
            : MinimapDisplayType.Square;
    }
}
