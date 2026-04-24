using System.Collections.Generic;
using UnityEngine;

public partial class IncidentMissionSystem
{
    private IncidentMissionObjectiveService objectiveService;
    private IncidentMissionScoreService scoreService;

    private IncidentMissionObjectiveService Objectives => objectiveService ??= new IncidentMissionObjectiveService(this);
    private IncidentMissionScoreService Scoring => scoreService ??= new IncidentMissionScoreService(this);

    private sealed class IncidentMissionObjectiveService
    {
        private readonly IncidentMissionSystem owner;

        public IncidentMissionObjectiveService(IncidentMissionSystem owner)
        {
            this.owner = owner;
        }

        public bool AreCompletionConditionsMet()
        {
            MissionProgressSnapshot snapshot = owner.BuildProgressSnapshot();
            if (owner.activePersistentObjectiveDefinitions.Count > 0)
            {
                return owner.ArePersistentDefinitionObjectivesSatisfied(snapshot, false);
            }

            return AreLegacyCompletionConditionsMet(snapshot);
        }

        public bool AreLegacyCompletionConditionsMet(MissionProgressSnapshot snapshot)
        {
            bool hasAnyObjective = snapshot.TotalTrackedFires > 0 || snapshot.TotalTrackedRescuables > 0 || snapshot.TotalTrackedVictims > 0;
            if (!hasAnyObjective)
            {
                return false;
            }

            bool firesComplete = !owner.requireAllFiresExtinguished || snapshot.TotalTrackedFires == snapshot.ExtinguishedFireCount;
            bool rescuesComplete = !owner.requireAllRescuablesRescued || snapshot.TotalTrackedRescuables == snapshot.RescuedCount;
            bool deathsWithinLimit = owner.maxAllowedVictimDeaths < 0 || snapshot.DeceasedVictimCount <= owner.maxAllowedVictimDeaths;
            bool criticalVictimsResolved = !owner.requireNoCriticalVictimsAtCompletion || snapshot.CriticalVictimCount == 0;
            bool livingVictimsStabilized = !owner.requireAllLivingVictimsStabilized || snapshot.AliveVictimCount == snapshot.StabilizedVictimCount;
            return firesComplete && rescuesComplete && deathsWithinLimit && criticalVictimsResolved && livingVictimsStabilized;
        }

        public bool RefreshObjectivesFromDefinition()
        {
            owner.activePersistentObjectiveDefinitions.Clear();
            owner.activeFailConditionDefinitions.Clear();
            if (owner.missionDefinition == null)
            {
                return false;
            }

            owner.missionDefinition.CollectFailConditions(owner.activeFailConditionDefinitions);
            owner.missionDefinition.CollectPersistentObjectives(owner.activePersistentObjectiveDefinitions);

            MissionRuntimeSceneData sceneData = new MissionRuntimeSceneData();
            for (int i = 0; i < owner.activePersistentObjectiveDefinitions.Count; i++)
            {
                owner.activePersistentObjectiveDefinitions[i].CollectTargets(sceneData);
            }

            for (int i = 0; i < owner.activeFailConditionDefinitions.Count; i++)
            {
                MissionFailConditionDefinition failCondition = owner.activeFailConditionDefinitions[i];
                if (failCondition != null)
                {
                    failCondition.CollectTargets(sceneData);
                }
            }

            owner.trackedFires = sceneData.CreateFireList();
            owner.trackedFireSimulationManagers = sceneData.CreateFireSimulationManagerList();
            owner.trackedRescuables = sceneData.CreateRescuableList();
            owner.trackedVictimConditions = sceneData.CreateVictimConditionList();
            return owner.activePersistentObjectiveDefinitions.Count > 0 || owner.activeFailConditionDefinitions.Count > 0;
        }

        public void RefreshLegacyObjectives()
        {
            owner.activePersistentObjectiveDefinitions.Clear();
            owner.activeFailConditionDefinitions.Clear();

            if (owner.autoDiscoverFires)
            {
                owner.trackedFires = CollectSceneObjects<Fire>();
                owner.trackedFireSimulationManagers = CollectSceneObjects<FireSimulationManager>();
            }
            else
            {
                RemoveNullEntries(owner.trackedFires);
                RemoveNullEntries(owner.trackedFireSimulationManagers);
            }

            if (owner.autoDiscoverRescuables)
            {
                owner.trackedRescuables = CollectSceneObjects<Rescuable>();
            }
            else
            {
                RemoveNullEntries(owner.trackedRescuables);
            }

            if (owner.autoDiscoverVictimConditions)
            {
                owner.trackedVictimConditions = CollectSceneObjects<VictimCondition>();
            }
            else
            {
                RemoveNullEntries(owner.trackedVictimConditions);
            }
        }

        public bool HasFailedObjectiveOutcome()
        {
            if (!owner.HasAnyActiveDefinitionObjectives())
            {
                return false;
            }

            MissionProgressSnapshot snapshot = owner.BuildProgressSnapshot();
            MissionObjectiveContext context = owner.BuildObjectiveContext(snapshot);
            for (int i = 0; i < owner.activePersistentObjectiveDefinitions.Count; i++)
            {
                MissionObjectiveDefinition objective = owner.activePersistentObjectiveDefinitions[i];
                if (objective == null)
                {
                    continue;
                }

                MissionObjectiveEvaluation evaluation = objective.Evaluate(context);
                if (evaluation.IsRelevant && evaluation.HasFailed)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasFailedConditionOutcome()
        {
            MissionFailConditionContext context = new MissionFailConditionContext(owner, owner.BuildProgressSnapshot(), owner.elapsedTime);

            if (owner.activeFailConditionDefinitions.Count > 0)
            {
                for (int i = 0; i < owner.activeFailConditionDefinitions.Count; i++)
                {
                    MissionFailConditionDefinition failCondition = owner.activeFailConditionDefinitions[i];
                    if (failCondition == null)
                    {
                        continue;
                    }

                    MissionFailConditionEvaluation evaluation = failCondition.Evaluate(context);
                    if (evaluation.IsRelevant && evaluation.HasFailed)
                    {
                        return true;
                    }
                }

                return false;
            }

            float activeTimeLimit = owner.ResolveTimeLimitSeconds();
            if (activeTimeLimit > 0f && owner.elapsedTime >= activeTimeLimit)
            {
                return true;
            }

            if (!owner.HasAnyActiveDefinitionObjectives())
            {
                return owner.HasFailedVictimOutcome();
            }

            return false;
        }

        public bool FailsOnAnyVictimDeath()
        {
            if (owner.activeFailConditionDefinitions != null && owner.activeFailConditionDefinitions.Count > 0)
            {
                for (int i = 0; i < owner.activeFailConditionDefinitions.Count; i++)
                {
                    MissionFailConditionDefinition failCondition = owner.activeFailConditionDefinitions[i];
                    if (failCondition is AnyVictimDeathFailConditionDefinition)
                    {
                        return true;
                    }

                    if (failCondition is MaxVictimDeathsFailConditionDefinition maxDeathsFailCondition &&
                        maxDeathsFailCondition.MaxAllowedVictimDeaths <= 0)
                    {
                        return true;
                    }
                }
            }

            if (owner.activePersistentObjectiveDefinitions != null)
            {
                for (int i = 0; i < owner.activePersistentObjectiveDefinitions.Count; i++)
                {
                    if (owner.activePersistentObjectiveDefinitions[i] is VictimOutcomeObjectiveDefinition victimOutcomeObjective &&
                        victimOutcomeObjective.FailsOnAnyVictimDeath)
                    {
                        return true;
                    }
                }
            }

            if (!owner.HasAnyActiveDefinitionObjectives())
            {
                return owner.failOnAnyVictimDeath || owner.maxAllowedVictimDeaths == 0;
            }

            return false;
        }

        public void RefreshObjectiveStatuses()
        {
            owner.objectiveStatuses.Clear();

            MissionProgressSnapshot snapshot = owner.BuildProgressSnapshot();
            MissionObjectiveContext context = owner.BuildObjectiveContext(snapshot);
            if (owner.HasAnyActiveDefinitionObjectives())
            {
                owner.objectiveScratchSet.Clear();
                CollectObjectiveStatuses(owner.activePersistentObjectiveDefinitions, context);
                owner.objectiveScratchSet.Clear();
                return;
            }

            BuildLegacyObjectiveStatuses(snapshot);
        }

        public void BuildLegacyObjectiveStatuses(MissionProgressSnapshot snapshot)
        {
            if (owner.requireAllFiresExtinguished && snapshot.TotalTrackedFires > 0)
            {
                float fireProgress = snapshot.TotalTrackedFires > 0
                    ? (float)snapshot.ExtinguishedFireCount / snapshot.TotalTrackedFires
                    : 0f;
                AddObjectiveStatus(new MissionObjectiveEvaluation(
                    MissionLocalization.Get("mission.objective.extinguish_fires.title", "Extinguish Fires"),
                    MissionLocalization.Format(
                        "mission.objective.extinguish_fires.summary",
                        "{0}: {1}/{2} extinguished",
                        MissionLocalization.Get("mission.objective.extinguish_fires.title", "Extinguish Fires"),
                        snapshot.ExtinguishedFireCount,
                        snapshot.TotalTrackedFires),
                    snapshot.ExtinguishedFireCount >= snapshot.TotalTrackedFires,
                    false,
                    true),
                    CreateLegacyProgressiveScore(fireProgress));
            }

            if (owner.requireAllRescuablesRescued && snapshot.TotalTrackedRescuables > 0)
            {
                float rescueProgress = snapshot.TotalTrackedRescuables > 0
                    ? (float)snapshot.RescuedCount / snapshot.TotalTrackedRescuables
                    : 0f;
                AddObjectiveStatus(new MissionObjectiveEvaluation(
                    MissionLocalization.Get("mission.objective.rescue_targets.title", "Rescue Targets"),
                    MissionLocalization.Format(
                        "mission.objective.rescue_targets.summary",
                        "{0}: {1}/{2}",
                        MissionLocalization.Get("mission.objective.rescue_targets.title", "Rescue Targets"),
                        snapshot.RescuedCount,
                        snapshot.TotalTrackedRescuables),
                    snapshot.RescuedCount >= snapshot.TotalTrackedRescuables,
                    false,
                    true),
                    CreateLegacyProgressiveScore(rescueProgress));
            }

            bool usesVictimObjective =
                snapshot.TotalTrackedVictims > 0 &&
                (owner.failOnAnyVictimDeath || owner.maxAllowedVictimDeaths >= 0 || owner.requireNoCriticalVictimsAtCompletion || owner.requireAllLivingVictimsStabilized);

            if (usesVictimObjective)
            {
                bool failedByAnyDeath = owner.failOnAnyVictimDeath && snapshot.DeceasedVictimCount > 0;
                bool failedByDeathLimit = owner.maxAllowedVictimDeaths >= 0 && snapshot.DeceasedVictimCount > owner.maxAllowedVictimDeaths;
                bool criticalResolved = !owner.requireNoCriticalVictimsAtCompletion || snapshot.CriticalVictimCount == 0;
                bool livingVictimsStabilized = !owner.requireAllLivingVictimsStabilized || snapshot.AliveVictimCount == snapshot.StabilizedVictimCount;

                AddObjectiveStatus(new MissionObjectiveEvaluation(
                    MissionLocalization.Get("mission.objective.victim_outcome.title", "Victim Outcome"),
                    MissionLocalization.Format(
                        "mission.objective.victim_outcome.summary",
                        "{0}: U {1} | C {2} | S {3} | X {4} | D {5}",
                        MissionLocalization.Get("mission.objective.victim_outcome.title", "Victim Outcome"),
                        snapshot.UrgentVictimCount,
                        snapshot.CriticalVictimCount,
                        snapshot.StabilizedVictimCount,
                        snapshot.ExtractedVictimCount,
                        snapshot.DeceasedVictimCount),
                    !failedByAnyDeath && !failedByDeathLimit && criticalResolved && livingVictimsStabilized,
                    failedByAnyDeath || failedByDeathLimit,
                    true),
                    CreateLegacyBinaryScore(!failedByAnyDeath && !failedByDeathLimit && criticalResolved && livingVictimsStabilized));
            }
        }

        public void AddObjectiveStatus(MissionObjectiveEvaluation evaluation, MissionObjectiveScoreEvaluation scoreEvaluation)
        {
            MissionObjectiveStatus status = new MissionObjectiveStatus();
            status.Set(evaluation, scoreEvaluation);
            owner.objectiveStatuses.Add(status);
        }

        public MissionObjectiveScoreEvaluation CreateLegacyBinaryScore(bool isComplete)
        {
            int score = isComplete ? LegacyObjectiveScoreWeight : 0;
            return new MissionObjectiveScoreEvaluation(score, LegacyObjectiveScoreWeight, string.Empty);
        }

        public MissionObjectiveScoreEvaluation CreateLegacyProgressiveScore(float normalizedProgress)
        {
            int score = Mathf.Clamp(Mathf.RoundToInt(LegacyObjectiveScoreWeight * Mathf.Clamp01(normalizedProgress)), 0, LegacyObjectiveScoreWeight);
            return new MissionObjectiveScoreEvaluation(score, LegacyObjectiveScoreWeight, string.Empty);
        }

        private void CollectObjectiveStatuses(List<MissionObjectiveDefinition> objectives, MissionObjectiveContext context)
        {
            for (int i = 0; i < objectives.Count; i++)
            {
                MissionObjectiveDefinition objective = objectives[i];
                if (objective == null || !owner.objectiveScratchSet.Add(objective))
                {
                    continue;
                }

                MissionObjectiveEvaluation evaluation = objective.Evaluate(context);
                if (!evaluation.IsRelevant)
                {
                    continue;
                }

                MissionObjectiveScoreEvaluation scoreEvaluation = objective.EvaluateScore(context, evaluation);
                MissionObjectiveStatus status = new MissionObjectiveStatus();
                status.Set(evaluation, scoreEvaluation);
                owner.objectiveStatuses.Add(status);
            }
        }

        private static List<T> CollectSceneObjects<T>() where T : Component
        {
            T[] found = FindObjectsByType<T>();
            List<T> results = new List<T>(found.Length);
            for (int i = 0; i < found.Length; i++)
            {
                T candidate = found[i];
                if (candidate != null && candidate.gameObject.scene.IsValid())
                {
                    results.Add(candidate);
                }
            }

            return results;
        }

        private static void RemoveNullEntries<T>(List<T> items) where T : Object
        {
            if (items == null)
            {
                return;
            }

            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (items[i] == null)
                {
                    items.RemoveAt(i);
                }
            }
        }
    }

    private sealed class IncidentMissionScoreService
    {
        private readonly IncidentMissionSystem owner;

        public IncidentMissionScoreService(IncidentMissionSystem owner)
        {
            this.owner = owner;
        }

        public void ResetScoreRuntime()
        {
            owner.currentScore = 0;
            owner.maximumScore = 0;
            owner.currentScoreRank = string.Empty;
            owner.finalScore = 0;
            owner.finalMaximumScore = 0;
            owner.finalScoreRank = string.Empty;
        }

        public void CacheFinalScore()
        {
            owner.finalScore = owner.currentScore;
            owner.finalMaximumScore = owner.maximumScore;
            owner.finalScoreRank = owner.currentScoreRank;
        }

        public void RefreshScoreState()
        {
            int objectiveScore = owner.SumObjectiveStatusScore();
            int objectiveMaxScore = owner.CalculateMissionObjectiveMaximumScore();
            MissionScoreConfig scoreConfig = owner.missionDefinition != null ? owner.missionDefinition.ScoreConfig : null;

            int bonusScore = 0;
            int bonusMaxScore = 0;
            if (scoreConfig != null && scoreConfig.EnableScoring)
            {
                bonusScore += EvaluateSignalRuleScore(scoreConfig);
                bonusMaxScore += scoreConfig.GetMaximumSignalRuleScore();

                if (owner.missionState == MissionState.Completed)
                {
                    bonusScore += scoreConfig.CompletionBonus;
                    bonusScore += scoreConfig.EvaluateTimeBonus(owner.elapsedTime);
                }

                if (owner.totalTrackedVictims > 0)
                {
                    bonusMaxScore += scoreConfig.NoVictimDeathsBonus;
                    if (owner.deceasedVictimCount <= 0)
                    {
                        bonusScore += scoreConfig.NoVictimDeathsBonus;
                    }
                    else
                    {
                        bonusScore -= owner.deceasedVictimCount * scoreConfig.PerVictimDeathPenalty;
                    }
                }

                bonusMaxScore += scoreConfig.CompletionBonus + scoreConfig.TimeBonusMaxScore;
                if (owner.missionState == MissionState.Failed)
                {
                    bonusScore -= scoreConfig.FailurePenalty;
                }
            }

            owner.maximumScore = Mathf.Max(0, objectiveMaxScore + bonusMaxScore);
            owner.currentScore = Mathf.Clamp(objectiveScore + bonusScore, 0, owner.maximumScore);
            owner.currentScoreRank = scoreConfig != null && scoreConfig.EnableScoring
                ? scoreConfig.EvaluateRank(owner.currentScore, owner.maximumScore)
                : string.Empty;
        }

        private int EvaluateSignalRuleScore(MissionScoreConfig scoreConfig)
        {
            if (scoreConfig == null || scoreConfig.SignalScoreRules == null)
            {
                return 0;
            }

            int score = 0;
            for (int i = 0; i < scoreConfig.SignalScoreRules.Count; i++)
            {
                MissionSignalScoreRule rule = scoreConfig.SignalScoreRules[i];
                if (rule == null || string.IsNullOrWhiteSpace(rule.SignalKey))
                {
                    continue;
                }

                if (owner.HasSignal(rule.SignalKey))
                {
                    score += rule.ScoreDelta;
                }
            }

            return score;
        }
    }
}
