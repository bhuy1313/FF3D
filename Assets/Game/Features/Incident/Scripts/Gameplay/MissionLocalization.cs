using System;

public static class MissionLocalization
{
    public static string Get(string localizationKey, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(localizationKey))
        {
            return fallback;
        }

        return LanguageManager.Tr(localizationKey, fallback);
    }

    public static string Format(string localizationKey, string fallbackFormat, params object[] args)
    {
        string format = Get(localizationKey, fallbackFormat);
        if (string.IsNullOrEmpty(format))
        {
            return string.Empty;
        }

        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            return string.IsNullOrEmpty(fallbackFormat) ? format : string.Format(fallbackFormat, args);
        }
    }
}
