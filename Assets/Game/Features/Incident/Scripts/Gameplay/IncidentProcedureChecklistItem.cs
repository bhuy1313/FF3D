using System;
using UnityEngine;

[Serializable]
public class IncidentProcedureChecklistItem
{
    [SerializeField] private string itemId = "procedure_item";
    [SerializeField] private string title = "Checklist Item";
    [SerializeField] private string titleLocalizationKey;
    [SerializeField, TextArea] private string description;
    [SerializeField] private string descriptionLocalizationKey;
    [SerializeField] private IncidentProcedureChecklistItemType itemType = IncidentProcedureChecklistItemType.Required;
    [SerializeField] private IncidentProcedurePriority priority = IncidentProcedurePriority.Medium;
    [SerializeField] private IncidentProcedureChecklistValidationMode validationMode = IncidentProcedureChecklistValidationMode.ManualOnly;
    [SerializeField] private string completionSignalKey;
    [SerializeField] private string invalidationSignalKey;
    [SerializeField] private string completionStateKey;
    [SerializeField] private string invalidationStateKey;
    [SerializeField] private bool defaultChecked;
    [SerializeField] private bool hiddenUntilRelevant;
    [SerializeField] private string relevanceConditionSummary;
    [SerializeField] private IncidentProcedureScoringAxis scoringAxis = IncidentProcedureScoringAxis.ProcedureCompliance;
    [SerializeField, Min(-100)] private int scoreDelta = 5;
    [SerializeField, TextArea] private string failureFeedback;
    [SerializeField] private string failureFeedbackLocalizationKey;

    public string ItemId => itemId;
    public string Title => ResolveText(titleLocalizationKey, title, "Checklist Item");
    public string Description => ResolveText(descriptionLocalizationKey, description);
    public IncidentProcedureChecklistItemType ItemType => itemType;
    public IncidentProcedurePriority Priority => priority;
    public IncidentProcedureChecklistValidationMode ValidationMode => validationMode;
    public string CompletionSignalKey => completionSignalKey;
    public string InvalidationSignalKey => invalidationSignalKey;
    public string CompletionStateKey => completionStateKey;
    public string InvalidationStateKey => invalidationStateKey;
    public bool DefaultChecked => defaultChecked;
    public bool HiddenUntilRelevant => hiddenUntilRelevant;
    public string RelevanceConditionSummary => relevanceConditionSummary;
    public IncidentProcedureScoringAxis ScoringAxis => scoringAxis;
    public int ScoreDelta => scoreDelta;
    public string FailureFeedback => ResolveText(failureFeedbackLocalizationKey, failureFeedback);

    private static string ResolveText(string localizationKey, string fallback, string secondaryFallback = "")
    {
        string resolvedFallback = !string.IsNullOrWhiteSpace(fallback) ? fallback : secondaryFallback;
        return MissionLocalization.Get(localizationKey, resolvedFallback);
    }
}
