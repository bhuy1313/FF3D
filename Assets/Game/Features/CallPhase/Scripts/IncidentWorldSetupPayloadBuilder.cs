using System;
using System.Collections.Generic;
using UnityEngine;

public static class IncidentWorldSetupPayloadBuilder
{
    private const string AddressFieldId = "Address";
    private const string FireLocationFieldId = "fire_location";
    private const string OccupantRiskFieldId = "OccupantRisk";
    private const string HazardFieldId = "hazard";
    private const string SpreadStatusFieldId = "SpreadStatus";
    private const string CallerSafetyFieldId = "CallerSafety";
    private const string SeverityFieldId = "Severity";

    private static readonly string[] ConfidenceFieldIds =
    {
        FireLocationFieldId,
        OccupantRiskFieldId,
        HazardFieldId,
        SpreadStatusFieldId,
        CallerSafetyFieldId,
        SeverityFieldId
    };

    public static IncidentWorldSetupPayload Build(CallPhaseScenarioData scenarioData, IncidentReportController incidentReportController, string caseId = "")
    {
        CallPhaseScenarioIncidentSeedData baseSeed = scenarioData != null && scenarioData.incidentSeed != null
            ? scenarioData.incidentSeed
            : new CallPhaseScenarioIncidentSeedData();

        IncidentWorldSetupReportSnapshot snapshot = BuildSnapshot(incidentReportController);
        List<string> appliedSignals = new List<string>();

        string logicalFireLocation = ResolveLogicalFireLocation(snapshot.fireLocation, baseSeed.fireOrigin, appliedSignals);
        string severityBand = ResolveSeverityBand(snapshot.severity, appliedSignals);
        string hazardType = ResolveHazardType(baseSeed.hazardType, snapshot.hazard, logicalFireLocation, appliedSignals);
        string fireOrigin = ResolveFireOrigin(baseSeed.fireOrigin, logicalFireLocation, hazardType, appliedSignals);
        string isolationType = ResolveIsolationType(hazardType);

        float intensity = Mathf.Clamp(baseSeed.initialFireIntensity, 0.1f, 1f);
        int fireCount = Mathf.Clamp(Mathf.Max(1, baseSeed.initialFireCount), 1, 5);
        float smokeDensity = Mathf.Clamp01(baseSeed.startSmokeDensity);
        float smokeMultiplier = Mathf.Clamp(baseSeed.smokeAccumulationMultiplier, 0.5f, 2.5f);
        string spreadPreset = Sanitize(baseSeed.fireSpreadPreset, "Moderate");
        string ventilationPreset = ResolveVentilationPreset(baseSeed.ventilationPreset, snapshot, logicalFireLocation, appliedSignals);
        string occupantRiskPreset = ResolveOccupantRiskPreset(snapshot.occupantRisk, appliedSignals);
        bool estimatedTrappedCountKnown = scenarioData != null && scenarioData.dispatchEstimatedVictimCountKnown;
        int estimatedTrappedCountMin = 0;
        int estimatedTrappedCountMax = 0;
        ResolveEstimatedTrappedCountRange(
            scenarioData,
            estimatedTrappedCountKnown,
            out estimatedTrappedCountMin,
            out estimatedTrappedCountMax);

        ApplySeverityInfluence(severityBand, ref intensity, ref fireCount, ref smokeDensity, ref smokeMultiplier, appliedSignals);
        ApplySpreadInfluence(snapshot.spreadStatus, ref intensity, ref fireCount, ref smokeDensity, ref smokeMultiplier, ref spreadPreset, appliedSignals);
        ApplyHazardInfluence(hazardType, ref intensity, ref smokeDensity, ref smokeMultiplier, appliedSignals);
        ApplyOccupantRiskInfluence(occupantRiskPreset, ref smokeDensity, ref smokeMultiplier, appliedSignals);
        ApplyVentilationInfluence(ventilationPreset, ref intensity, ref smokeDensity, ref smokeMultiplier, appliedSignals);
        ApplyCallerSafetyInfluence(snapshot.callerSafety, ref smokeDensity, ref smokeMultiplier, appliedSignals);
        ApplyEstimatedVictimSignal(
            estimatedTrappedCountKnown,
            estimatedTrappedCountMin,
            estimatedTrappedCountMax,
            appliedSignals);

        return new IncidentWorldSetupPayload
        {
            caseId = caseId ?? string.Empty,
            scenarioId = scenarioData != null ? Sanitize(scenarioData.scenarioId, "scenario_unknown") : "scenario_unknown",
            fireOrigin = fireOrigin,
            logicalFireLocation = logicalFireLocation,
            hazardType = hazardType,
            isolationType = isolationType,
            requiresIsolation = baseSeed.requiresIsolation || !string.Equals(isolationType, "None", StringComparison.OrdinalIgnoreCase),
            initialFireIntensity = Mathf.Clamp01(intensity),
            initialFireCount = Mathf.Clamp(fireCount, 1, 5),
            fireSpreadPreset = spreadPreset,
            startSmokeDensity = Mathf.Clamp01(smokeDensity),
            smokeAccumulationMultiplier = Mathf.Clamp(smokeMultiplier, 0.5f, 2.5f),
            ventilationPreset = ventilationPreset,
            occupantRiskPreset = occupantRiskPreset,
            severityBand = severityBand,
            estimatedTrappedCountKnown = estimatedTrappedCountKnown,
            estimatedTrappedCountMin = estimatedTrappedCountMin,
            estimatedTrappedCountMax = estimatedTrappedCountMax,
            confidenceScore = ComputeConfidenceScore(incidentReportController, snapshot),
            reportSnapshot = snapshot,
            appliedSignals = appliedSignals
        };
    }

    private static void ResolveEstimatedTrappedCountRange(
        CallPhaseScenarioData scenarioData,
        bool estimatedTrappedCountKnown,
        out int estimatedTrappedCountMin,
        out int estimatedTrappedCountMax)
    {
        estimatedTrappedCountMin = 0;
        estimatedTrappedCountMax = 0;

        if (!estimatedTrappedCountKnown || scenarioData == null)
        {
            return;
        }

        estimatedTrappedCountMin = Mathf.Max(0, scenarioData.dispatchEstimatedVictimCountMin);
        estimatedTrappedCountMax = Mathf.Max(0, scenarioData.dispatchEstimatedVictimCountMax);
        if (estimatedTrappedCountMax < estimatedTrappedCountMin)
        {
            estimatedTrappedCountMax = estimatedTrappedCountMin;
        }
    }

    private static IncidentWorldSetupReportSnapshot BuildSnapshot(IncidentReportController incidentReportController)
    {
        return new IncidentWorldSetupReportSnapshot
        {
            address = GetReportValue(incidentReportController, AddressFieldId),
            fireLocation = GetReportValue(incidentReportController, FireLocationFieldId),
            occupantRisk = GetReportValue(incidentReportController, OccupantRiskFieldId),
            hazard = GetReportValue(incidentReportController, HazardFieldId),
            spreadStatus = GetReportValue(incidentReportController, SpreadStatusFieldId),
            callerSafety = GetReportValue(incidentReportController, CallerSafetyFieldId),
            severity = GetReportValue(incidentReportController, SeverityFieldId)
        };
    }

    private static string ResolveLogicalFireLocation(string reportedFireLocation, string fallbackFireOrigin, List<string> appliedSignals)
    {
        string normalizedLocation = Normalize(reportedFireLocation);
        if (normalizedLocation.Contains("kitchen"))
        {
            appliedSignals.Add("Fire location points to kitchen compartment.");
            return "Kitchen";
        }

        if (normalizedLocation.Contains("laundry") || normalizedLocation.Contains("washer") || normalizedLocation.Contains("utility"))
        {
            appliedSignals.Add("Fire location points to laundry/utility compartment.");
            return "Laundry";
        }

        if (normalizedLocation.Contains("garage"))
        {
            appliedSignals.Add("Fire location points to garage compartment.");
            return "Garage";
        }

        if (normalizedLocation.Contains("living"))
        {
            appliedSignals.Add("Fire location points to living area.");
            return "LivingRoom";
        }

        if (normalizedLocation.Contains("bedroom"))
        {
            appliedSignals.Add("Fire location points to bedroom compartment.");
            return "Bedroom";
        }

        if (normalizedLocation.Contains("hall") || normalizedLocation.Contains("corridor"))
        {
            appliedSignals.Add("Fire location points to circulation space.");
            return "Hallway";
        }

        string fallback = Sanitize(fallbackFireOrigin, "Unknown");
        int separatorIndex = fallback.IndexOf('_');
        if (separatorIndex > 0)
        {
            fallback = fallback.Substring(0, separatorIndex);
        }

        return fallback;
    }

    private static string ResolveSeverityBand(string reportedSeverity, List<string> appliedSignals)
    {
        string normalizedSeverity = Normalize(reportedSeverity);
        if (normalizedSeverity.Contains("high"))
        {
            appliedSignals.Add("Caller assessment indicates high severity.");
            return "High";
        }

        if (normalizedSeverity.Contains("medium"))
        {
            appliedSignals.Add("Caller assessment indicates medium severity.");
            return "Medium";
        }

        if (normalizedSeverity.Contains("low"))
        {
            appliedSignals.Add("Caller assessment indicates low severity.");
            return "Low";
        }

        return "Medium";
    }

    private static string ResolveHazardType(string fallbackHazardType, string reportedHazard, string logicalFireLocation, List<string> appliedSignals)
    {
        string normalizedHazard = Normalize(reportedHazard);
        string normalizedLocation = Normalize(logicalFireLocation);

        if (ContainsAny(normalizedHazard, "electrical", "wiring", "socket", "outlet", "breaker", "panel", "sparks"))
        {
            appliedSignals.Add("Hazard wording suggests an electrical source.");
            return "Electrical";
        }

        if (ContainsAny(normalizedHazard, "gas", "propane", "cylinder", "stove", "oven"))
        {
            appliedSignals.Add("Hazard wording suggests a gas-fed fire.");
            return "Gas";
        }

        if (ContainsAny(normalizedHazard, "oil", "grease", "petrol", "fuel", "solvent", "flammableliquid"))
        {
            appliedSignals.Add("Hazard wording suggests a flammable-liquid fuel package.");
            return "FlammableLiquid";
        }

        if (normalizedLocation == "laundry" && ContainsAny(normalizedHazard, "machine", "appliance", "dryer", "washer"))
        {
            appliedSignals.Add("Laundry appliance wording biases hazard toward electrical.");
            return "Electrical";
        }

        return Sanitize(fallbackHazardType, "OrdinaryCombustibles");
    }

    private static string ResolveFireOrigin(string fallbackFireOrigin, string logicalFireLocation, string hazardType, List<string> appliedSignals)
    {
        string sanitizedFallback = Sanitize(fallbackFireOrigin, "Unknown");
        string normalizedLocation = Normalize(logicalFireLocation);
        string normalizedHazard = Normalize(hazardType);

        if (normalizedLocation == "kitchen")
        {
            if (normalizedHazard == "gas")
            {
                appliedSignals.Add("Kitchen + gas maps origin to stove top.");
                return "Kitchen_StoveTop";
            }

            if (normalizedHazard == "flammableliquid")
            {
                appliedSignals.Add("Kitchen liquid fuel bias maps origin to cooking vessel area.");
                return "Kitchen_PanArea";
            }

            return sanitizedFallback.StartsWith("Kitchen_", StringComparison.Ordinal) ? sanitizedFallback : "Kitchen_GeneralArea";
        }

        if (normalizedLocation == "laundry")
        {
            if (normalizedHazard == "electrical")
            {
                appliedSignals.Add("Laundry + electrical maps origin to washer outlet.");
                return "Laundry_WasherOutlet";
            }

            return sanitizedFallback.StartsWith("Laundry_", StringComparison.Ordinal) ? sanitizedFallback : "Laundry_GeneralArea";
        }

        if (normalizedLocation == "garage")
        {
            if (normalizedHazard == "flammableliquid")
            {
                appliedSignals.Add("Garage + flammable liquid maps origin to workbench corner.");
                return "Garage_WorkbenchCorner";
            }

            return sanitizedFallback.StartsWith("Garage_", StringComparison.Ordinal) ? sanitizedFallback : "Garage_GeneralArea";
        }

        return sanitizedFallback;
    }

    private static string ResolveVentilationPreset(string fallbackVentilationPreset, IncidentWorldSetupReportSnapshot snapshot, string logicalFireLocation, List<string> appliedSignals)
    {
        string preset = Sanitize(fallbackVentilationPreset, "Neutral");
        string normalizedCallerSafety = Normalize(snapshot.callerSafety);
        string normalizedSpread = Normalize(snapshot.spreadStatus);
        string normalizedLocation = Normalize(logicalFireLocation);

        if (ContainsAny(normalizedCallerSafety, "outside", "evacuated", "alreadyout"))
        {
            if (normalizedLocation == "kitchen")
            {
                appliedSignals.Add("Caller already outside keeps kitchen ventilation more open.");
                return "OpenKitchen";
            }

            appliedSignals.Add("Caller already outside suggests open egress path.");
            return "OpenEgress";
        }

        if (ContainsAny(normalizedSpread, "contained", "localized"))
        {
            appliedSignals.Add("Contained wording preserves a more compartmentalized ventilation state.");
            return preset == "OpenEgress" ? "Neutral" : preset;
        }

        return preset;
    }

    private static string ResolveOccupantRiskPreset(string occupantRisk, List<string> appliedSignals)
    {
        string normalizedRisk = Normalize(occupantRisk);
        if (ContainsAny(normalizedRisk, "trapped", "upstairs", "inside", "cannotgetout", "child", "elderly"))
        {
            appliedSignals.Add("Occupant risk implies compromised tenability.");
            return "Compromised";
        }

        if (ContainsAny(normalizedRisk, "unknown", "unconfirmed"))
        {
            return "Unknown";
        }

        return string.IsNullOrWhiteSpace(normalizedRisk) ? "Unknown" : "Manageable";
    }

    private static string ResolveIsolationType(string hazardType)
    {
        if (string.Equals(hazardType, "Electrical", StringComparison.OrdinalIgnoreCase))
        {
            return "Electrical";
        }

        if (string.Equals(hazardType, "Gas", StringComparison.OrdinalIgnoreCase))
        {
            return "Gas";
        }

        return "None";
    }

    private static void ApplySeverityInfluence(string severityBand, ref float intensity, ref int fireCount, ref float smokeDensity, ref float smokeMultiplier, List<string> appliedSignals)
    {
        switch (Normalize(severityBand))
        {
            case "high":
                intensity += 0.16f;
                fireCount += 1;
                smokeDensity += 0.12f;
                smokeMultiplier += 0.22f;
                appliedSignals.Add("High severity increases heat release, smoke load, and multi-seat risk.");
                break;
            case "medium":
                intensity += 0.08f;
                smokeDensity += 0.06f;
                smokeMultiplier += 0.1f;
                appliedSignals.Add("Medium severity nudges fire growth above baseline.");
                break;
            case "low":
                intensity += 0.02f;
                smokeDensity += 0.02f;
                appliedSignals.Add("Low severity keeps the incident near baseline.");
                break;
        }
    }

    private static void ApplySpreadInfluence(string spreadStatus, ref float intensity, ref int fireCount, ref float smokeDensity, ref float smokeMultiplier, ref string spreadPreset, List<string> appliedSignals)
    {
        string normalizedSpread = Normalize(spreadStatus);
        if (ContainsAny(normalizedSpread, "spreading", "spread", "movingto", "extending"))
        {
            intensity += 0.1f;
            fireCount += 1;
            smokeDensity += 0.08f;
            smokeMultiplier += 0.2f;
            spreadPreset = "Aggressive";
            appliedSignals.Add("Spread wording indicates active extension beyond the ignition area.");
            return;
        }

        if (ContainsAny(normalizedSpread, "contained", "localized", "stayingin"))
        {
            intensity -= 0.04f;
            smokeMultiplier -= 0.06f;
            spreadPreset = "Contained";
            appliedSignals.Add("Contained wording keeps spread behavior tighter.");
        }
    }

    private static void ApplyHazardInfluence(string hazardType, ref float intensity, ref float smokeDensity, ref float smokeMultiplier, List<string> appliedSignals)
    {
        switch (Normalize(hazardType))
        {
            case "electrical":
                intensity += 0.04f;
                smokeDensity += 0.03f;
                appliedSignals.Add("Electrical fires bias toward faster early heat and toxic smoke.");
                break;
            case "gas":
                intensity += 0.08f;
                smokeDensity += 0.04f;
                smokeMultiplier += 0.14f;
                appliedSignals.Add("Gas-fed hazard increases sustained flame energy.");
                break;
            case "flammableliquid":
                intensity += 0.09f;
                smokeDensity += 0.05f;
                smokeMultiplier += 0.16f;
                appliedSignals.Add("Flammable-liquid hazard increases spread potential and smoke loading.");
                break;
        }
    }

    private static void ApplyOccupantRiskInfluence(string occupantRiskPreset, ref float smokeDensity, ref float smokeMultiplier, List<string> appliedSignals)
    {
        if (!string.Equals(occupantRiskPreset, "Compromised", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        smokeDensity += 0.06f;
        smokeMultiplier += 0.08f;
        appliedSignals.Add("Compromised occupants imply worse tenability at discovery time.");
    }

    private static void ApplyVentilationInfluence(string ventilationPreset, ref float intensity, ref float smokeDensity, ref float smokeMultiplier, List<string> appliedSignals)
    {
        switch (Normalize(ventilationPreset))
        {
            case "openegress":
            case "openkitchen":
                intensity += 0.03f;
                smokeDensity -= 0.03f;
                smokeMultiplier += 0.04f;
                appliedSignals.Add("Open ventilation feeds combustion but vents some smoke layer.");
                break;
            case "closedcompartment":
                smokeDensity += 0.08f;
                smokeMultiplier += 0.1f;
                appliedSignals.Add("Closed compartment traps smoke and accelerates accumulation.");
                break;
        }
    }

    private static void ApplyCallerSafetyInfluence(string callerSafety, ref float smokeDensity, ref float smokeMultiplier, List<string> appliedSignals)
    {
        string normalizedCallerSafety = Normalize(callerSafety);
        if (ContainsAny(normalizedCallerSafety, "inside", "trapped", "stillin"))
        {
            smokeDensity += 0.04f;
            smokeMultiplier += 0.05f;
            appliedSignals.Add("Caller still inside suggests earlier untenable smoke conditions.");
        }
    }

    private static void ApplyEstimatedVictimSignal(
        bool estimatedTrappedCountKnown,
        int estimatedTrappedCountMin,
        int estimatedTrappedCountMax,
        List<string> appliedSignals)
    {
        if (!estimatedTrappedCountKnown)
        {
            return;
        }

        string estimateLabel = FormatEstimatedVictimRange(estimatedTrappedCountMin, estimatedTrappedCountMax);
        if (string.IsNullOrWhiteSpace(estimateLabel))
        {
            return;
        }

        appliedSignals.Add($"Dispatch estimate indicates {estimateLabel} trapped victim(s).");
    }

    private static string FormatEstimatedVictimRange(int estimatedTrappedCountMin, int estimatedTrappedCountMax)
    {
        estimatedTrappedCountMin = Mathf.Max(0, estimatedTrappedCountMin);
        estimatedTrappedCountMax = Mathf.Max(estimatedTrappedCountMin, estimatedTrappedCountMax);

        if (estimatedTrappedCountMin == estimatedTrappedCountMax)
        {
            return estimatedTrappedCountMin.ToString();
        }

        return $"{estimatedTrappedCountMin}-{estimatedTrappedCountMax}";
    }

    private static float ComputeConfidenceScore(IncidentReportController incidentReportController, IncidentWorldSetupReportSnapshot snapshot)
    {
        float score = 0.3f;

        for (int i = 0; i < ConfidenceFieldIds.Length; i++)
        {
            string fieldId = ConfidenceFieldIds[i];
            IncidentReportFieldView fieldView = incidentReportController != null ? incidentReportController.GetFieldView(fieldId) : null;
            if (fieldView == null || string.IsNullOrWhiteSpace(fieldView.CurrentValue) || fieldView.CurrentState == ReportFieldState.Empty)
            {
                continue;
            }

            score += 0.08f;
            if (fieldView.CurrentState == ReportFieldState.Confirmed || fieldView.CurrentState == ReportFieldState.Assessed)
            {
                score += 0.04f;
            }
        }

        if (!string.IsNullOrWhiteSpace(snapshot.fireLocation))
        {
            score += 0.08f;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.hazard))
        {
            score += 0.06f;
        }

        return Mathf.Clamp01(score);
    }

    private static string GetReportValue(IncidentReportController incidentReportController, string fieldId)
    {
        if (incidentReportController == null)
        {
            return string.Empty;
        }

        IncidentReportFieldView fieldView = incidentReportController.GetFieldView(fieldId);
        return fieldView != null && !string.IsNullOrWhiteSpace(fieldView.CurrentValue)
            ? fieldView.CurrentValue.Trim()
            : string.Empty;
    }

    private static bool ContainsAny(string source, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(source) || tokens == null)
        {
            return false;
        }

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (!string.IsNullOrWhiteSpace(token) && source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string Sanitize(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace(" ", string.Empty).ToLowerInvariant();
    }
}
