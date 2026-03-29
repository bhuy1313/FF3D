using UnityEngine;

[CreateAssetMenu(
    fileName = "InteractTargetObjective",
    menuName = "TrueJourney/Missions/Objectives/Interact Target")]
public class InteractTargetObjectiveDefinition : MissionObjectiveDefinition
{
    [SerializeField] private string targetSignalKey = "interact-target";
    [SerializeField] private string pendingSummary = "Interact with target";
    [SerializeField] private string completedSummary = "Target interacted";

    public override MissionObjectiveEvaluation Evaluate(MissionProgressSnapshot snapshot)
    {
        string title = ResolveTitle("Interact Target");
        return new MissionObjectiveEvaluation(title, pendingSummary, false, false, !string.IsNullOrWhiteSpace(targetSignalKey));
    }

    public override MissionObjectiveEvaluation Evaluate(MissionObjectiveContext context)
    {
        string title = ResolveTitle("Interact Target");
        bool isRelevant = !string.IsNullOrWhiteSpace(targetSignalKey);
        bool isComplete = isRelevant && context.HasSignal(targetSignalKey);
        string summary = isComplete
            ? string.IsNullOrWhiteSpace(completedSummary) ? $"{title}: complete" : completedSummary
            : string.IsNullOrWhiteSpace(pendingSummary) ? $"{title}: pending" : pendingSummary;

        return new MissionObjectiveEvaluation(title, summary, isComplete, false, isRelevant);
    }
}
