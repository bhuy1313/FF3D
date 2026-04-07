using UnityEngine;

[CreateAssetMenu(
    fileName = "ExtinguishFiresObjective",
    menuName = "TrueJourney/Missions/Objectives/Extinguish Fires")]
public class ExtinguishFiresObjectiveDefinition : MissionObjectiveDefinition
{
    [SerializeField] private bool autoDiscoverFires = true;

    public override void CollectTargets(MissionRuntimeSceneData sceneData)
    {
        if (autoDiscoverFires && sceneData != null)
        {
            sceneData.CollectSceneFires();
        }
    }

    public override MissionObjectiveEvaluation Evaluate(MissionProgressSnapshot snapshot)
    {
        string title = ResolveTitle("Extinguish Fires");
        bool isRelevant = snapshot.TotalTrackedFires > 0;
        bool isComplete = isRelevant && snapshot.ExtinguishedFireCount >= snapshot.TotalTrackedFires;
        string summary = MissionLocalization.Format(
            "mission.objective.extinguish_fires.summary",
            "{0}: {1}/{2} extinguished",
            title,
            snapshot.ExtinguishedFireCount,
            snapshot.TotalTrackedFires);
        return new MissionObjectiveEvaluation(title, summary, isComplete, false, isRelevant);
    }

    public override MissionObjectiveScoreEvaluation EvaluateScore(MissionObjectiveContext context, MissionObjectiveEvaluation evaluation)
    {
        if (!evaluation.IsRelevant)
        {
            return new MissionObjectiveScoreEvaluation(0, 0, string.Empty);
        }

        MissionProgressSnapshot snapshot = context.Snapshot;
        float progress = snapshot.TotalTrackedFires > 0
            ? (float)snapshot.ExtinguishedFireCount / snapshot.TotalTrackedFires
            : 0f;
        return CreateProgressiveScoreEvaluation(progress);
    }
}
