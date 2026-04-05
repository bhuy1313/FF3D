using System.Text;
using UnityEngine;

[CreateAssetMenu(
    fileName = "VictimOutcomeObjective",
    menuName = "TrueJourney/Missions/Objectives/Victim Outcome")]
public class VictimOutcomeObjectiveDefinition : MissionObjectiveDefinition
{
    [SerializeField] private bool autoDiscoverVictimConditions = true;
    [SerializeField] private bool failOnAnyVictimDeath = false;
    [SerializeField] private int maxAllowedVictimDeaths = -1;
    [SerializeField] private bool requireNoCriticalVictimsAtCompletion = false;
    [SerializeField] private bool requireAllLivingVictimsStabilized = false;

    public override void CollectTargets(MissionRuntimeSceneData sceneData)
    {
        if (autoDiscoverVictimConditions && sceneData != null)
        {
            sceneData.CollectSceneVictimConditions();
        }
    }

    public override MissionObjectiveEvaluation Evaluate(MissionProgressSnapshot snapshot)
    {
        string title = ResolveTitle("Victim Outcome");
        bool isRelevant = snapshot.TotalTrackedVictims > 0;

        bool failedByAnyDeath = failOnAnyVictimDeath && snapshot.DeceasedVictimCount > 0;
        bool failedByDeathLimit = maxAllowedVictimDeaths >= 0 && snapshot.DeceasedVictimCount > maxAllowedVictimDeaths;
        bool criticalResolved = !requireNoCriticalVictimsAtCompletion || snapshot.CriticalVictimCount == 0;
        bool livingVictimsStabilized = !requireAllLivingVictimsStabilized || snapshot.AliveVictimCount == snapshot.StabilizedVictimCount;
        bool isComplete = isRelevant && !failedByAnyDeath && !failedByDeathLimit && criticalResolved && livingVictimsStabilized;
        bool hasFailed = isRelevant && (failedByAnyDeath || failedByDeathLimit);

        StringBuilder summaryBuilder = new StringBuilder();
        summaryBuilder.Append(title);
        summaryBuilder.Append(": U ");
        summaryBuilder.Append(snapshot.UrgentVictimCount);
        summaryBuilder.Append(" | C ");
        summaryBuilder.Append(snapshot.CriticalVictimCount);
        summaryBuilder.Append(" | S ");
        summaryBuilder.Append(snapshot.StabilizedVictimCount);
        summaryBuilder.Append(" | X ");
        summaryBuilder.Append(snapshot.ExtractedVictimCount);
        summaryBuilder.Append(" | D ");
        summaryBuilder.Append(snapshot.DeceasedVictimCount);

        return new MissionObjectiveEvaluation(title, summaryBuilder.ToString(), isComplete, hasFailed, isRelevant);
    }
}
