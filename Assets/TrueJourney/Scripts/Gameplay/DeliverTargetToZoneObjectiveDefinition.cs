using UnityEngine;

[CreateAssetMenu(
    fileName = "DeliverTargetToZoneObjective",
    menuName = "TrueJourney/Missions/Objectives/Deliver Target To Zone")]
public class DeliverTargetToZoneObjectiveDefinition : MissionObjectiveDefinition
{
    [SerializeField] private string targetSignalKey = "deliver-target";
    [SerializeField] private string pendingSummaryLocalizationKey;
    [SerializeField] private string pendingSummary = "Deliver target to safe zone";
    [SerializeField] private string completedSummaryLocalizationKey;
    [SerializeField] private string completedSummary = "Target delivered to safe zone";

    public override MissionObjectiveEvaluation Evaluate(MissionProgressSnapshot snapshot)
    {
        string title = ResolveTitle("Deliver Target");
        return new MissionObjectiveEvaluation(title, ResolvePendingSummary(title), false, false, !string.IsNullOrWhiteSpace(targetSignalKey));
    }

    public override MissionObjectiveEvaluation Evaluate(MissionObjectiveContext context)
    {
        string title = ResolveTitle("Deliver Target");
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
