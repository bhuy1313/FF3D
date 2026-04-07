using UnityEngine;

[CreateAssetMenu(
    fileName = "TimeLimitFailCondition",
    menuName = "TrueJourney/Missions/Fail Conditions/Time Limit")]
public class TimeLimitFailConditionDefinition : MissionFailConditionDefinition
{
    [SerializeField] private float timeLimitSeconds = 60f;
    [SerializeField] private string remainingSummaryLocalizationKey;
    [SerializeField] private string disabledSummaryLocalizationKey;

    public override MissionFailConditionEvaluation Evaluate(MissionFailConditionContext context)
    {
        string title = ResolveTitle("Time Limit");
        float limit = Mathf.Max(0f, timeLimitSeconds);
        bool isRelevant = limit > 0f;
        bool hasFailed = isRelevant && context.ElapsedTimeSeconds >= limit;
        float remaining = Mathf.Max(0f, limit - context.ElapsedTimeSeconds);
        string summary = isRelevant
            ? ResolveText(
                remainingSummaryLocalizationKey,
                string.Empty,
                MissionLocalization.Format("mission.fail_condition.time_limit.remaining", "{0}: {1:F1}s remaining", title, remaining))
            : ResolveText(
                disabledSummaryLocalizationKey,
                string.Empty,
                MissionLocalization.Format("mission.fail_condition.time_limit.disabled", "{0}: disabled", title));

        return new MissionFailConditionEvaluation(title, summary, hasFailed, isRelevant);
    }

    public override bool TryGetTimeLimitSeconds(out float resolvedTimeLimitSeconds)
    {
        resolvedTimeLimitSeconds = Mathf.Max(0f, timeLimitSeconds);
        return resolvedTimeLimitSeconds > 0f;
    }
}
