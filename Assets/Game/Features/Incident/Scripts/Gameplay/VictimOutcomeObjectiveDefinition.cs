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

    public bool FailsOnAnyVictimDeath => failOnAnyVictimDeath || Mathf.Max(-1, maxAllowedVictimDeaths) == 0;

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
        summaryBuilder.Append(MissionLocalization.Format(
            "mission.objective.victim_outcome.summary",
            "{0}: U {1} | C {2} | S {3} | X {4} | D {5}",
            title,
            snapshot.UrgentVictimCount,
            snapshot.CriticalVictimCount,
            snapshot.StabilizedVictimCount,
            snapshot.ExtractedVictimCount,
            snapshot.DeceasedVictimCount));

        return new MissionObjectiveEvaluation(title, summaryBuilder.ToString(), isComplete, hasFailed, isRelevant);
    }

    public override MissionObjectiveScoreEvaluation EvaluateScore(MissionObjectiveContext context, MissionObjectiveEvaluation evaluation)
    {
        if (!evaluation.IsRelevant)
        {
            return new MissionObjectiveScoreEvaluation(0, 0, string.Empty);
        }

        MissionProgressSnapshot snapshot = context.Snapshot;
        float progress = 1f;

        if (snapshot.TotalTrackedVictims > 0)
        {
            progress -= Mathf.Clamp01((float)snapshot.DeceasedVictimCount / snapshot.TotalTrackedVictims);
        }

        if (requireNoCriticalVictimsAtCompletion && snapshot.TotalTrackedVictims > 0)
        {
            progress -= Mathf.Clamp01((float)snapshot.CriticalVictimCount / snapshot.TotalTrackedVictims) * 0.5f;
        }

        if (requireAllLivingVictimsStabilized && snapshot.AliveVictimCount > 0)
        {
            int unstabilizedCount = Mathf.Max(0, snapshot.AliveVictimCount - snapshot.StabilizedVictimCount);
            progress -= Mathf.Clamp01((float)unstabilizedCount / snapshot.AliveVictimCount) * 0.5f;
        }

        if (evaluation.HasFailed)
        {
            progress = Mathf.Min(progress, 0f);
        }

        return CreateProgressiveScoreEvaluation(progress);
    }
}
