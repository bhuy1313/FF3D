using UnityEngine;

[CreateAssetMenu(
    fileName = "ReachAreaObjective",
    menuName = "TrueJourney/Missions/Objectives/Reach Area")]
public class ReachAreaObjectiveDefinition : MissionObjectiveDefinition
{
    [SerializeField] private string targetSignalKey = "reach-area";
    [SerializeField] private string pendingSummaryLocalizationKey;
    [SerializeField] private string pendingSummary = "Reach target area";
    [SerializeField] private string completedSummaryLocalizationKey;
    [SerializeField] private string completedSummary = "Target area reached";

    public override MissionObjectiveEvaluation Evaluate(MissionProgressSnapshot snapshot)
    {
        string title = ResolveTitle("Reach Area");
        return new MissionObjectiveEvaluation(title, ResolvePendingSummary(title), false, false, !string.IsNullOrWhiteSpace(targetSignalKey));
    }

    public override MissionObjectiveEvaluation Evaluate(MissionObjectiveContext context)
    {
        string title = ResolveTitle("Reach Area");
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
