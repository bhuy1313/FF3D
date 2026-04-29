using System;
using System.Collections.Generic;

[Serializable]
public class IncidentWorldSetupPayload
{
    public string caseId;
    public string scenarioId;
    public string fireOrigin;
    public string logicalFireLocation;
    public string hazardType;
    public string isolationType;
    public bool requiresIsolation;
    public float initialFireIntensity;
    public int initialFireCount;
    public string fireSpreadPreset;
    public float startSmokeDensity;
    public float smokeAccumulationMultiplier = 1f;
    public string ventilationPreset;
    public string occupantRiskPreset;
    public string severityBand;
    public float confidenceScore;
    /// <summary>
    /// If non-zero, fire spawn placement RNG uses this explicit seed (combined with a discriminator per pass).
    /// 0 means seed is derived from scene path + caseId + scenarioId + fireOrigin + logicalFireLocation.
    /// </summary>
    public int placementRandomSeed;
    public IncidentWorldSetupReportSnapshot reportSnapshot = new IncidentWorldSetupReportSnapshot();
    public List<string> appliedSignals = new List<string>();
}

[Serializable]
public class IncidentWorldSetupReportSnapshot
{
    public string address;
    public string fireLocation;
    public string occupantRisk;
    public string hazard;
    public string spreadStatus;
    public string callerSafety;
    public string severity;
}
