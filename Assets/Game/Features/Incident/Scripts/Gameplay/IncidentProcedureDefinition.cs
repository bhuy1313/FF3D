using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "FF3D/Incident/Procedure Definition",
    fileName = "NewIncidentProcedureDefinition")]
public sealed class IncidentProcedureDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string procedureId = "incident_procedure";
    [SerializeField] private string scenarioId;
    [SerializeField] private string title = "Procedure Recommendation";
    [SerializeField] private string titleLocalizationKey;
    [SerializeField, TextArea] private string summary;
    [SerializeField] private string summaryLocalizationKey;

    [Header("Applicability")]
    [SerializeField] private string hazardType;
    [SerializeField] private string fireLocation;
    [SerializeField] private string severityBand;
    [SerializeField] private bool requiresKnownVictimRisk;

    [Header("Command Priorities")]
    [SerializeField] private IncidentProcedureScoringAxis primaryPriority = IncidentProcedureScoringAxis.LifeSafety;
    [SerializeField] private IncidentProcedureScoringAxis secondaryPriority = IncidentProcedureScoringAxis.FireControl;
    [SerializeField] private IncidentProcedureScoringAxis tertiaryPriority = IncidentProcedureScoringAxis.CrewSafety;

    [Header("Procedure")]
    [SerializeField] private List<IncidentProcedureChecklistItem> checklistItems = new List<IncidentProcedureChecklistItem>();
    [SerializeField] private List<IncidentProcedureExceptionRule> exceptionRules = new List<IncidentProcedureExceptionRule>();
    [SerializeField] private List<IncidentProcedureDebriefLine> successDebriefLines = new List<IncidentProcedureDebriefLine>();
    [SerializeField] private List<IncidentProcedureDebriefLine> failureDebriefLines = new List<IncidentProcedureDebriefLine>();

    [Header("Scoring Weights")]
    [SerializeField, Min(0)] private int lifeSafetyWeight = 40;
    [SerializeField, Min(0)] private int fireControlWeight = 25;
    [SerializeField, Min(0)] private int crewSafetyWeight = 20;
    [SerializeField, Min(0)] private int procedureComplianceWeight = 10;
    [SerializeField, Min(0)] private int timeEfficiencyWeight = 5;

    public string ProcedureId => procedureId;
    public string ScenarioId => scenarioId;
    public string Title => MissionLocalization.Get(titleLocalizationKey, title);
    public string Summary => MissionLocalization.Get(summaryLocalizationKey, summary);
    public string HazardType => hazardType;
    public string FireLocation => fireLocation;
    public string SeverityBand => severityBand;
    public bool RequiresKnownVictimRisk => requiresKnownVictimRisk;
    public IncidentProcedureScoringAxis PrimaryPriority => primaryPriority;
    public IncidentProcedureScoringAxis SecondaryPriority => secondaryPriority;
    public IncidentProcedureScoringAxis TertiaryPriority => tertiaryPriority;
    public IReadOnlyList<IncidentProcedureChecklistItem> ChecklistItems => checklistItems;
    public IReadOnlyList<IncidentProcedureExceptionRule> ExceptionRules => exceptionRules;
    public IReadOnlyList<IncidentProcedureDebriefLine> SuccessDebriefLines => successDebriefLines;
    public IReadOnlyList<IncidentProcedureDebriefLine> FailureDebriefLines => failureDebriefLines;
    public int LifeSafetyWeight => Mathf.Max(0, lifeSafetyWeight);
    public int FireControlWeight => Mathf.Max(0, fireControlWeight);
    public int CrewSafetyWeight => Mathf.Max(0, crewSafetyWeight);
    public int ProcedureComplianceWeight => Mathf.Max(0, procedureComplianceWeight);
    public int TimeEfficiencyWeight => Mathf.Max(0, timeEfficiencyWeight);

    public bool Matches(IncidentWorldSetupPayload payload)
    {
        if (payload == null)
        {
            return false;
        }

        if (!MatchesValue(scenarioId, payload.scenarioId))
        {
            return false;
        }

        if (!MatchesValue(hazardType, payload.hazardType))
        {
            return false;
        }

        if (!MatchesValue(fireLocation, payload.logicalFireLocation))
        {
            return false;
        }

        if (!MatchesValue(severityBand, payload.severityBand))
        {
            return false;
        }

        if (requiresKnownVictimRisk && !payload.estimatedTrappedCountKnown)
        {
            return false;
        }

        return true;
    }

    private static bool MatchesValue(string expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        return string.Equals(
            expected.Trim(),
            actual != null ? actual.Trim() : string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }
}
