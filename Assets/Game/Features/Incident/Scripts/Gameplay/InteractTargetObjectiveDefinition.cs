using UnityEngine;

[CreateAssetMenu(
    fileName = "InteractTargetObjective",
    menuName = "TrueJourney/Missions/Objectives/Interact Target")]
public class InteractTargetObjectiveDefinition : MissionObjectiveDefinition
{
    [SerializeField] private string targetSignalKey = "interact-target";
    [SerializeField] private string pendingSummaryLocalizationKey;
    [SerializeField] private string pendingSummary = "Interact with target";
    [SerializeField] private string completedSummaryLocalizationKey;
    [SerializeField] private string completedSummary = "Target interacted";

    public override MissionObjectiveEvaluation Evaluate(MissionProgressSnapshot snapshot)
    {
        string title = ResolveTitle("Interact Target");
        return new MissionObjectiveEvaluation(title, ResolvePendingSummary(title), false, false, !string.IsNullOrWhiteSpace(targetSignalKey));
    }

    public override MissionObjectiveEvaluation Evaluate(MissionObjectiveContext context)
    {
        string title = ResolveTitle("Interact Target");
        bool isRelevant = !string.IsNullOrWhiteSpace(targetSignalKey);
        bool isComplete = isRelevant && context.HasSignal(targetSignalKey);
        string summary = isComplete
            ? ResolveCompletedSummary(title)
            : ResolvePendingSummary(title);

        return new MissionObjectiveEvaluation(title, summary, isComplete, false, isRelevant);
    }

    private string ResolvePendingSummary(string title)
    {
        return ResolveText(
            pendingSummaryLocalizationKey,
            pendingSummary,
            MissionLocalization.Format("mission.objective.summary.pending", "{0}: pending", title));
    }

    private string ResolveCompletedSummary(string title)
    {
        return ResolveText(
            completedSummaryLocalizationKey,
            completedSummary,
            MissionLocalization.Format("mission.objective.summary.completed", "{0}: complete", title));
    }
}
