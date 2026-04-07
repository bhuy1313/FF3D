using System;
using System.Collections.Generic;
using TMPro;

public static class CallPhaseUiChromeText
{
    private const string AddressFieldId = "Address";
    private const string FireLocationFieldId = "fire_location";
    private const string OccupantRiskFieldId = "OccupantRisk";
    private const string HazardFieldId = "hazard";
    private const string SpreadStatusFieldId = "SpreadStatus";
    private const string CallerSafetyFieldId = "CallerSafety";
    private const string SeverityFieldId = "Severity";

    public static string Tr(string key, string fallback)
    {
        return LanguageManager.Tr(key, fallback);
    }

    public static string Format(string key, string fallbackFormat, params object[] args)
    {
        string format = Tr(key, fallbackFormat);
        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            return string.Format(fallbackFormat, args);
        }
    }

    public static void ApplyCurrentFont(TMP_Text text, LanguageFontRole fontRole = LanguageFontRole.Default)
    {
        if (text == null || LanguageManager.Instance == null)
        {
            return;
        }

        TMP_FontAsset font = LanguageManager.Instance.GetCurrentTMPFont(fontRole);
        if (font != null)
        {
            text.font = font;
        }
    }

    public static bool IsCurrentLanguageVietnamese()
    {
        return LanguageManager.Instance == null || LanguageManager.Instance.CurrentLanguage == AppLanguage.Vietnamese;
    }

    public static string GetFieldDisplayName(string fieldId)
    {
        switch (fieldId)
        {
            case AddressFieldId:
                return Tr("callphase.field.address", "Address");
            case FireLocationFieldId:
                return Tr("callphase.field.fire_location", "Fire Location");
            case OccupantRiskFieldId:
                return Tr("callphase.field.occupant_risk", "Occupant Risk");
            case HazardFieldId:
                return Tr("callphase.field.hazard", "Hazard");
            case SpreadStatusFieldId:
                return Tr("callphase.field.spread_status", "Spread Status");
            case CallerSafetyFieldId:
                return Tr("callphase.field.caller_safety", "Caller Safety");
            case SeverityFieldId:
                return Tr("callphase.field.severity", "Severity");
            default:
                return fieldId ?? string.Empty;
        }
    }

    public static IEnumerable<string> GetFieldDisplayNameCandidates(string fieldId)
    {
        switch (fieldId)
        {
            case AddressFieldId:
                yield return "Address";
                yield return Tr("callphase.field.address", "Address");
                yield break;
            case FireLocationFieldId:
                yield return "Fire Location";
                yield return Tr("callphase.field.fire_location", "Fire Location");
                yield break;
            case OccupantRiskFieldId:
                yield return "Occupant Risk";
                yield return Tr("callphase.field.occupant_risk", "Occupant Risk");
                yield break;
            case HazardFieldId:
                yield return "Hazard";
                yield return Tr("callphase.field.hazard", "Hazard");
                yield break;
            case SpreadStatusFieldId:
                yield return "Spread Status";
                yield return Tr("callphase.field.spread_status", "Spread Status");
                yield break;
            case CallerSafetyFieldId:
                yield return "Caller Safety";
                yield return Tr("callphase.field.caller_safety", "Caller Safety");
                yield break;
            case SeverityFieldId:
                yield return "Severity";
                yield return Tr("callphase.field.severity", "Severity");
                yield break;
        }
    }

    public static string GetSeverityDisplayName(string severityValue)
    {
        if (string.IsNullOrWhiteSpace(severityValue))
        {
            return string.Empty;
        }

        switch (severityValue.Trim().ToLowerInvariant())
        {
            case "low":
                return Tr("callphase.severity.low", "Low");
            case "medium":
                return Tr("callphase.severity.medium", "Medium");
            case "high":
                return Tr("callphase.severity.high", "High");
            default:
                return severityValue.Trim();
        }
    }

    public static string GetMatchTypeDisplayName(string matchType)
    {
        if (string.IsNullOrWhiteSpace(matchType))
        {
            return string.Empty;
        }

        switch (matchType.Trim().ToLowerInvariant())
        {
            case "exact":
                return Tr("callphase.match.exact", "Exact");
            case "extra":
                return Tr("callphase.match.extra", "Extra");
            case "invalid":
                return Tr("callphase.match.invalid", "Invalid");
            default:
                return matchType.Trim();
        }
    }

    public static string GetPenaltyDisplayName(string penalty)
    {
        if (string.IsNullOrWhiteSpace(penalty))
        {
            return string.Empty;
        }

        switch (penalty.Trim().ToLowerInvariant())
        {
            case "none":
                return Tr("callphase.penalty.none", "None");
            case "minor":
                return Tr("callphase.penalty.minor", "Minor");
            case "major":
                return Tr("callphase.penalty.major", "Major");
            default:
                return penalty.Trim();
        }
    }

    public static bool TryGetFieldIdForLocalizationKey(string localizationKey, out string fieldId)
    {
        switch (localizationKey)
        {
            case "callphase.field.address":
                fieldId = AddressFieldId;
                return true;
            case "callphase.field.fire_location":
                fieldId = FireLocationFieldId;
                return true;
            case "callphase.field.occupant_risk":
                fieldId = OccupantRiskFieldId;
                return true;
            case "callphase.field.hazard":
                fieldId = HazardFieldId;
                return true;
            case "callphase.field.spread_status":
                fieldId = SpreadStatusFieldId;
                return true;
            case "callphase.field.caller_safety":
                fieldId = CallerSafetyFieldId;
                return true;
            case "callphase.field.severity":
                fieldId = SeverityFieldId;
                return true;
            default:
                fieldId = null;
                return false;
        }
    }
}
