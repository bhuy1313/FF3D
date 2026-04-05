using UnityEngine;

[CreateAssetMenu(
    fileName = "DeliverTargetToZoneObjective",
    menuName = "TrueJourney/Missions/Objectives/Deliver Target To Zone")]
public class DeliverTargetToZoneObjectiveDefinition : MissionObjectiveDefinition
{
    [SerializeField] private string targetSignalKey = "deliver-target";
    [SerializeField] private string pendingSummary = "Deliver target to safe zone";
    [SerializeField] private string completedSummary = "Target delivered to safe zone";

    public override MissionObjectiveEvaluation Evaluate(MissionProgressSnapshot snapshot)
    {
        string title = ResolveTitle("Deliver Target");
        return new MissionObjectiveEvaluation(title, pendingSummary, false, false, !string.IsNullOrWhiteSpace(targetSignalKey));
    }

    public override MissionObjectiveEvaluation Evaluate(MissionObjectiveContext context)
    {
        string title = ResolveTitle("Deliver Target");
        bool isRelevant = !string.IsNullOrWhiteSpace(targetSignalKey);
        bool isComplete = isRelevant && context.HasSignal(targetSignalKey);
        string summary = isComplete
            ? string.IsNullOrWhiteSpace(completedSummary) ? $"{title}: complete" : completedSummary
            : string.IsNullOrWhiteSpace(pendingSummary) ? $"{title}: pending" : pendingSummary;

        return new MissionObjectiveEvaluation(title, summary, isComplete, false, isRelevant);
    }
}
