using UnityEngine;

[CreateAssetMenu(
    fileName = "BreakTargetObjective",
    menuName = "TrueJourney/Missions/Objectives/Break Target")]
public class BreakTargetObjectiveDefinition : MissionObjectiveDefinition
{
    [SerializeField] private string targetSignalKey = "break-target";
    [SerializeField] private string pendingSummary = "Break target";
    [SerializeField] private string completedSummary = "Target broken";

    public override MissionObjectiveEvaluation Evaluate(MissionProgressSnapshot snapshot)
    {
        string title = ResolveTitle("Break Target");
        return new MissionObjectiveEvaluation(title, pendingSummary, false, false, !string.IsNullOrWhiteSpace(targetSignalKey));
    }

    public override MissionObjectiveEvaluation Evaluate(MissionObjectiveContext context)
    {
        string title = ResolveTitle("Break Target");
        bool isRelevant = !string.IsNullOrWhiteSpace(targetSignalKey);
        bool isComplete = isRelevant && context.HasSignal(targetSignalKey);
        string summary = isComplete
            ? string.IsNullOrWhiteSpace(completedSummary) ? $"{title}: complete" : completedSummary
            : string.IsNullOrWhiteSpace(pendingSummary) ? $"{title}: pending" : pendingSummary;

        return new MissionObjectiveEvaluation(title, summary, isComplete, false, isRelevant);
    }
}
