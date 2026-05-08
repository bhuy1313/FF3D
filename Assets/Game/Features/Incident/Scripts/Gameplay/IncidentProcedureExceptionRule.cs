using System;
using UnityEngine;

[Serializable]
public class IncidentProcedureExceptionRule
{
    [SerializeField] private string ruleId = "procedure_exception";
    [SerializeField] private string title = "Critical Exception";
    [SerializeField] private string titleLocalizationKey;
    [SerializeField, TextArea] private string triggerSummary;
    [SerializeField] private string triggerSummaryLocalizationKey;
    [SerializeField, TextArea] private string rationale;
    [SerializeField] private string rationaleLocalizationKey;
    [SerializeField] private IncidentProcedureScoringAxis primaryBenefitAxis = IncidentProcedureScoringAxis.LifeSafety;
    [SerializeField] private IncidentProcedureScoringAxis primaryRiskAxis = IncidentProcedureScoringAxis.CrewSafety;
    [SerializeField] private string recommendedSignalKey;
    [SerializeField] private string penaltySignalKey;
    [SerializeField, Min(-100)] private int recommendedScoreDelta = 10;
    [SerializeField, Min(-100)] private int penaltyScoreDelta = -10;

    public string RuleId => ruleId;
    public string Title => ResolveText(titleLocalizationKey, title, "Critical Exception");
    public string TriggerSummary => ResolveText(triggerSummaryLocalizationKey, triggerSummary);
    public string Rationale => ResolveText(rationaleLocalizationKey, rationale);
    public IncidentProcedureScoringAxis PrimaryBenefitAxis => primaryBenefitAxis;
    public IncidentProcedureScoringAxis PrimaryRiskAxis => primaryRiskAxis;
    public string RecommendedSignalKey => recommendedSignalKey;
    public string PenaltySignalKey => penaltySignalKey;
    public int RecommendedScoreDelta => recommendedScoreDelta;
    public int PenaltyScoreDelta => penaltyScoreDelta;

    private static string ResolveText(string localizationKey, string fallback, string secondaryFallback = "")
    {
        string resolvedFallback = !string.IsNullOrWhiteSpace(fallback) ? fallback : secondaryFallback;
        return MissionLocalization.Get(localizationKey, resolvedFallback);
    }
}
