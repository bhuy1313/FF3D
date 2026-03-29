using UnityEngine;

[CreateAssetMenu(
    fileName = "ReachAreaObjective",
    menuName = "TrueJourney/Missions/Objectives/Reach Area")]
public class ReachAreaObjectiveDefinition : MissionObjectiveDefinition
{
    [SerializeField] private string targetSignalKey = "reach-area";
    [SerializeField] private string pendingSummary = "Reach target area";
    [SerializeField] private string completedSummary = "Target area reached";

    public override MissionObjectiveEvaluation Evaluate(MissionProgressSnapshot snapshot)
    {
        string title = ResolveTitle("Reach Area");
        return new MissionObjectiveEvaluation(title, pendingSummary, false, false, !string.IsNullOrWhiteSpace(targetSignalKey));
    }

    public override MissionObjectiveEvaluation Evaluate(MissionObjectiveContext context)
    {
        string title = ResolveTitle("Reach Area");
        bool isRelevant = !string.IsNullOrWhiteSpace(targetSignalKey);
        bool isComplete = isRelevant && context.HasSignal(targetSignalKey);
        string summary = isComplete
            ? string.IsNullOrWhiteSpace(completedSummary) ? $"{title}: complete" : completedSummary
            : string.IsNullOrWhiteSpace(pendingSummary) ? $"{title}: pending" : pendingSummary;

        return new MissionObjectiveEvaluation(title, summary, isComplete, false, isRelevant);
    }
}
