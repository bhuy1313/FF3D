using UnityEngine;

[CreateAssetMenu(
    fileName = "RescueTargetsObjective",
    menuName = "TrueJourney/Missions/Objectives/Rescue Targets")]
public class RescueTargetsObjectiveDefinition : MissionObjectiveDefinition
{
    [SerializeField] private bool autoDiscoverRescuables = true;

    public override void CollectTargets(MissionRuntimeSceneData sceneData)
    {
        if (autoDiscoverRescuables && sceneData != null)
        {
            sceneData.CollectSceneRescuables();
        }
    }

    public override MissionObjectiveEvaluation Evaluate(MissionProgressSnapshot snapshot)
    {
        string title = ResolveTitle("Rescue Targets");
        bool isRelevant = snapshot.TotalTrackedRescuables > 0;
        bool isComplete = isRelevant && snapshot.RescuedCount >= snapshot.TotalTrackedRescuables;
        string summary = $"{title}: {snapshot.RescuedCount}/{snapshot.TotalTrackedRescuables}";
        return new MissionObjectiveEvaluation(title, summary, isComplete, false, isRelevant);
    }
}
