using System;
using UnityEngine;

[Serializable]
public class IncidentProcedureDebriefLine
{
    [SerializeField] private string lineId = "procedure_debrief";
    [SerializeField, TextArea] private string text;
    [SerializeField] private string textLocalizationKey;
    [SerializeField] private IncidentProcedureScoringAxis scoringAxis = IncidentProcedureScoringAxis.ProcedureCompliance;
    [SerializeField] private IncidentProcedurePriority severity = IncidentProcedurePriority.Medium;

    public string LineId => lineId;
    public string Text => MissionLocalization.Get(textLocalizationKey, text);
    public IncidentProcedureScoringAxis ScoringAxis => scoringAxis;
    public IncidentProcedurePriority Severity => severity;
}
