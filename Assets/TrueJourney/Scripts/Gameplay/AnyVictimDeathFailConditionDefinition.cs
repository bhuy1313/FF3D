using UnityEngine;

[CreateAssetMenu(
    fileName = "AnyVictimDeathFailCondition",
    menuName = "TrueJourney/Missions/Fail Conditions/Any Victim Death")]
public class AnyVictimDeathFailConditionDefinition : MissionFailConditionDefinition
{
    [SerializeField] private bool autoDiscoverVictimConditions = true;

    public override void CollectTargets(MissionRuntimeSceneData sceneData)
    {
        if (autoDiscoverVictimConditions && sceneData != null)
        {
            sceneData.CollectSceneVictimConditions();
        }
    }

    public override MissionFailConditionEvaluation Evaluate(MissionFailConditionContext context)
    {
        string title = ResolveTitle("Any Victim Death");
        bool isRelevant = context.Snapshot.TotalTrackedVictims > 0;
        bool hasFailed = isRelevant && context.Snapshot.DeceasedVictimCount > 0;
        string summary = MissionLocalization.Format(
            "mission.fail_condition.any_victim_death.summary",
            "{0}: {1} deceased",
            title,
            context.Snapshot.DeceasedVictimCount);
        return new MissionFailConditionEvaluation(title, summary, hasFailed, isRelevant);
    }
}
