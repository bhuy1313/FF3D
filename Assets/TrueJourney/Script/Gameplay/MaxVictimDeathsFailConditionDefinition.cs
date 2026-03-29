using UnityEngine;

[CreateAssetMenu(
    fileName = "MaxVictimDeathsFailCondition",
    menuName = "TrueJourney/Missions/Fail Conditions/Max Victim Deaths")]
public class MaxVictimDeathsFailConditionDefinition : MissionFailConditionDefinition
{
    [SerializeField] private bool autoDiscoverVictimConditions = true;
    [SerializeField] private int maxAllowedVictimDeaths = 0;

    public override void CollectTargets(MissionRuntimeSceneData sceneData)
    {
        if (autoDiscoverVictimConditions && sceneData != null)
        {
            sceneData.CollectSceneVictimConditions();
        }
    }

    public override MissionFailConditionEvaluation Evaluate(MissionFailConditionContext context)
    {
        string title = ResolveTitle("Max Victim Deaths");
        int maxDeaths = Mathf.Max(0, maxAllowedVictimDeaths);
        bool isRelevant = context.Snapshot.TotalTrackedVictims > 0;
        bool hasFailed = isRelevant && context.Snapshot.DeceasedVictimCount > maxDeaths;
        string summary = $"{title}: {context.Snapshot.DeceasedVictimCount}/{maxDeaths}";
        return new MissionFailConditionEvaluation(title, summary, hasFailed, isRelevant);
    }
}
