using UnityEngine;

[CreateAssetMenu(
    fileName = "SignalTriggeredFailCondition",
    menuName = "TrueJourney/Missions/Fail Conditions/Signal Triggered")]
public class SignalTriggeredFailConditionDefinition : MissionFailConditionDefinition
{
    [SerializeField] private string triggerSignalKey = "fail-signal";
    [SerializeField] private string safeSummaryLocalizationKey;
    [SerializeField] private string safeSummary = "Condition clear";
    [SerializeField] private string failedSummaryLocalizationKey;
    [SerializeField] private string failedSummary = "Fail condition triggered";

    public override MissionFailConditionEvaluation Evaluate(MissionFailConditionContext context)
    {
        string title = ResolveTitle("Signal Fail Condition");
        bool isRelevant = !string.IsNullOrWhiteSpace(triggerSignalKey);
        bool hasFailed = isRelevant && context.HasSignal(triggerSignalKey);
        string summary = hasFailed
            ? ResolveText(
                failedSummaryLocalizationKey,
                failedSummary,
                MissionLocalization.Format("mission.fail_condition.signal.failed", "{0}: triggered", title))
            : ResolveText(
                safeSummaryLocalizationKey,
                safeSummary,
                MissionLocalization.Format("mission.fail_condition.signal.safe", "{0}: clear", title));

        return new MissionFailConditionEvaluation(title, summary, hasFailed, isRelevant);
    }
}
