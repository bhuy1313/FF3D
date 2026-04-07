using System.Collections.Generic;
using UnityEngine;

public partial class IncidentMissionSystem
{
    private IncidentMissionObjectiveService objectiveService;
    private IncidentMissionScoreService scoreService;
    private IncidentMissionStageService stageService;

    private IncidentMissionObjectiveService Objectives => objectiveService ??= new IncidentMissionObjectiveService(this);
    private IncidentMissionScoreService Scoring => scoreService ??= new IncidentMissionScoreService(this);
    private IncidentMissionStageService Stages => stageService ??= new IncidentMissionStageService(this);

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
            if (owner.HasActiveStageSequence())
            {
                return owner.IsFinalMissionStage() && owner.AreActiveDefinitionObjectivesSatisfied(snapshot, true);
            }

            if (owner.activeObjectiveDefinitions.Count > 0)
            {
                return owner.AreActiveDefinitionObjectivesSatisfied(snapshot, false);
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
            owner.activeObjectiveDefinitions.Clear();
            owner.activeFailConditionDefinitions.Clear();
            owner.activeStageDefinitions.Clear();
            if (owner.missionDefinition == null)
            {
                owner.ClearStageRuntimePresentation();
                return false;
            }

            owner.missionDefinition.CollectStages(owner.activeStageDefinitions);
            owner.missionDefinition.CollectFailConditions(owner.activeFailConditionDefinitions);
            if (owner.activeStageDefinitions.Count > 0)
            {
                owner.currentStageIndex = Mathf.Clamp(owner.currentStageIndex < 0 ? 0 : owner.currentStageIndex, 0, owner.activeStageDefinitions.Count - 1);
                owner.totalStageCount = owner.activeStageDefinitions.Count;

                MissionStageDefinition currentStage = owner.activeStageDefinitions[owner.currentStageIndex];
                owner.currentStageTitle = currentStage != null ? currentStage.StageTitle : string.Empty;
                owner.currentStageDescription = currentStage != null ? currentStage.StageDescription : string.Empty;

                owner.missionDefinition.CollectObjectives(owner.activeObjectiveDefinitions, owner.currentStageIndex);
            }
            else
            {
                owner.ClearStageRuntimePresentation();
                owner.missionDefinition.CollectObjectives(owner.activeObjectiveDefinitions);
                if (owner.activeObjectiveDefinitions.Count == 0)
                {
                    return false;
                }
            }

            MissionRuntimeSceneData sceneData = new MissionRuntimeSceneData();
            for (int i = 0; i < owner.activeObjectiveDefinitions.Count; i++)
            {
                owner.activeObjectiveDefinitions[i].CollectTargets(sceneData);
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
            owner.trackedRescuables = sceneData.CreateRescuableList();
            owner.trackedVictimConditions = sceneData.CreateVictimConditionList();
            return owner.activeStageDefinitions.Count > 0 || owner.activeObjectiveDefinitions.Count > 0 || owner.activeFailConditionDefinitions.Count > 0;
        }

        public void RefreshLegacyObjectives()
        {
            owner.activeObjectiveDefinitions.Clear();
            owner.activeFailConditionDefinitions.Clear();
            owner.activeStageDefinitions.Clear();
            owner.ClearStageRuntimePresentation();

            if (owner.autoDiscoverFires)
            {
                owner.trackedFires = CollectSceneObjects<Fire>();
            }
            else
            {
                RemoveNullEntries(owner.trackedFires);
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
            if (owner.activeObjectiveDefinitions.Count == 0)
            {
                return false;
            }

            MissionProgressSnapshot snapshot = owner.BuildProgressSnapshot();
            MissionObjectiveContext context = owner.BuildObjectiveContext(snapshot);
            for (int i = 0; i < owner.activeObjectiveDefinitions.Count; i++)
            {
                MissionObjectiveDefinition objective = owner.activeObjectiveDefinitions[i];
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
            MissionFailConditionContext context = new MissionFailConditionContext(owner.BuildProgressSnapshot(), owner.elapsedTime);

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

            if (owner.activeObjectiveDefinitions.Count == 0)
            {
                return owner.HasFailedVictimOutcome();
            }

            return false;
        }

        public void RefreshObjectiveStatuses()
        {
            owner.objectiveStatuses.Clear();

            MissionProgressSnapshot snapshot = owner.BuildProgressSnapshot();
            MissionObjectiveContext context = owner.BuildObjectiveContext(snapshot);
            if (owner.activeObjectiveDefinitions.Count > 0)
            {
                for (int i = 0; i < owner.activeObjectiveDefinitions.Count; i++)
                {
                    MissionObjectiveDefinition objective = owner.activeObjectiveDefinitions[i];
                    if (objective == null)
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
            if (owner.completedStageScoreRecords != null)
            {
                owner.completedStageScoreRecords.Clear();
            }

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
            int objectiveScore = owner.CalculateCompletedStageObjectiveScore();
            if (!owner.HasActiveStageSequence() || !owner.HasCapturedStageScore(owner.currentStageIndex))
            {
                objectiveScore += owner.SumObjectiveStatusScore();
            }

            int objectiveMaxScore = owner.CalculateMissionObjectiveMaximumScore();
            MissionScoreConfig scoreConfig = owner.missionDefinition != null ? owner.missionDefinition.ScoreConfig : null;

            int bonusScore = 0;
            int bonusMaxScore = 0;
            if (scoreConfig != null && scoreConfig.EnableScoring)
            {
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

        public void CaptureCurrentStageScoreIfNeeded(string stageId)
        {
            if (!owner.HasActiveStageSequence() || owner.currentStageIndex < 0 || owner.HasCapturedStageScore(owner.currentStageIndex))
            {
                return;
            }

            int stageScore = owner.SumObjectiveStatusScore();
            int stageMaxScore = 0;
            if (owner.objectiveStatuses != null)
            {
                for (int i = 0; i < owner.objectiveStatuses.Count; i++)
                {
                    MissionObjectiveStatus status = owner.objectiveStatuses[i];
                    if (status != null)
                    {
                        stageMaxScore += Mathf.Max(0, status.MaxScore);
                    }
                }
            }

            string stageTitle = owner.currentStageTitle;
            MissionStageScoreRecord record = new MissionStageScoreRecord();
            record.Set(owner.currentStageIndex, stageId, stageTitle, stageScore, stageMaxScore);
            owner.completedStageScoreRecords.Add(record);
        }
    }

    private sealed class IncidentMissionStageService
    {
        private readonly IncidentMissionSystem owner;

        public IncidentMissionStageService(IncidentMissionSystem owner)
        {
            this.owner = owner;
        }

        public bool TryAdvanceMissionStageIfReady()
        {
            if (!owner.HasActiveStageSequence())
            {
                return false;
            }

            MissionProgressSnapshot snapshot = owner.BuildProgressSnapshot();
            if (!owner.AreActiveDefinitionObjectivesSatisfied(snapshot, true))
            {
                return false;
            }

            owner.InvokeCurrentStageCompleted();

            if (owner.IsFinalMissionStage())
            {
                return false;
            }

            int nextStageIndex = owner.currentStageIndex + 1;
            float nextStageDelaySeconds = ResolveCurrentStageTransitionDelaySeconds();
            if (nextStageDelaySeconds > 0f)
            {
                ScheduleStageTransition(nextStageIndex, nextStageDelaySeconds);
                return true;
            }

            BeginStage(nextStageIndex);
            return true;
        }

        public bool HasActiveStageSequence()
        {
            return owner.activeStageDefinitions != null && owner.activeStageDefinitions.Count > 0;
        }

        public bool IsFinalMissionStage()
        {
            return HasActiveStageSequence() && owner.currentStageIndex >= owner.activeStageDefinitions.Count - 1;
        }

        public float ResolveCurrentStageTransitionDelaySeconds()
        {
            if (!owner.TryGetCurrentStageDefinition(out MissionStageDefinition stage) || stage == null)
            {
                return 0f;
            }

            return stage.NextStageDelaySeconds;
        }

        public void ResetMissionStageRuntime()
        {
            ResetSignalState();
            ClearPendingStageTransition();
            if (owner.missionDefinition != null && owner.missionDefinition.HasStages)
            {
                owner.currentStageIndex = 0;
                return;
            }

            ClearStageRuntimePresentation();
        }

        public void ClearStageRuntimePresentation()
        {
            owner.currentStageIndex = -1;
            owner.totalStageCount = 0;
            owner.currentStageTitle = string.Empty;
            owner.currentStageDescription = string.Empty;
            ClearPendingStageTransition();
            owner.lastStartedStageEventIndex = -1;
            owner.lastCompletedStageEventIndex = -1;
        }

        public void ResetSignalState()
        {
            if (owner.activatedSignalKeys != null)
            {
                owner.activatedSignalKeys.Clear();
            }
        }

        public bool UpdatePendingStageTransition()
        {
            if (!owner.isStageTransitionPending)
            {
                return false;
            }

            if (owner.elapsedTime < owner.pendingStageStartTime)
            {
                return true;
            }

            int nextStageIndex = owner.pendingStageIndex;
            ClearPendingStageTransition();
            BeginStage(nextStageIndex);
            return true;
        }

        public void ScheduleStageTransition(int nextStageIndex, float delaySeconds)
        {
            owner.isStageTransitionPending = true;
            owner.pendingStageIndex = nextStageIndex;
            owner.pendingStageStartTime = owner.elapsedTime + Mathf.Max(0f, delaySeconds);
        }

        public void BeginStage(int stageIndex)
        {
            owner.currentStageIndex = stageIndex;
            owner.RefreshObjectives();
            owner.InvokeCurrentStageStarted();
        }

        public void ClearPendingStageTransition()
        {
            owner.isStageTransitionPending = false;
            owner.pendingStageIndex = -1;
            owner.pendingStageStartTime = -1f;
        }

        public void InvokeCurrentStageStarted()
        {
            if (!HasActiveStageSequence() || owner.currentStageIndex == owner.lastStartedStageEventIndex)
            {
                return;
            }

            string stageId = owner.ResolveCurrentStageId();
            owner.lastStartedStageEventIndex = owner.currentStageIndex;
            owner.onStageStarted?.Invoke(owner.currentStageIndex, stageId);
            owner.ExecuteCurrentStageActions(MissionActionTrigger.StageStarted, stageId);
            owner.InvokeStageBindings(stageId, true);
        }

        public void InvokeCurrentStageCompleted()
        {
            if (!HasActiveStageSequence() || owner.currentStageIndex == owner.lastCompletedStageEventIndex)
            {
                return;
            }

            string stageId = owner.ResolveCurrentStageId();
            owner.CaptureCurrentStageScoreIfNeeded(stageId);
            owner.lastCompletedStageEventIndex = owner.currentStageIndex;
            owner.onStageCompleted?.Invoke(owner.currentStageIndex, stageId);
            owner.ExecuteCurrentStageActions(MissionActionTrigger.StageCompleted, stageId);
            owner.InvokeStageBindings(stageId, false);
        }

        public void ExecuteCurrentStageActions(MissionActionTrigger trigger, string stageId)
        {
            if (!owner.TryGetCurrentStageDefinition(out MissionStageDefinition stage))
            {
                return;
            }

            MissionActionExecutionContext context = new MissionActionExecutionContext(
                owner,
                owner.missionDefinition,
                stage,
                owner.currentStageIndex,
                stageId,
                trigger);

            stage.ExecuteActions(context);
        }

        public void InvokeStageBindings(string stageId, bool started)
        {
            if (owner.stageActionBindings == null || string.IsNullOrWhiteSpace(stageId))
            {
                return;
            }

            for (int i = 0; i < owner.stageActionBindings.Count; i++)
            {
                MissionStageActionBinding binding = owner.stageActionBindings[i];
                if (binding == null || !binding.Matches(stageId))
                {
                    continue;
                }

                if (started)
                {
                    binding.InvokeStarted();
                }
                else
                {
                    binding.InvokeCompleted();
                }
            }
        }

        public string ResolveCurrentStageId()
        {
            if (!HasActiveStageSequence() || owner.currentStageIndex < 0 || owner.currentStageIndex >= owner.activeStageDefinitions.Count)
            {
                return string.Empty;
            }

            MissionStageDefinition stage = owner.activeStageDefinitions[owner.currentStageIndex];
            if (stage == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(stage.StageId))
            {
                return stage.StageId;
            }

            if (!string.IsNullOrWhiteSpace(stage.StageTitle))
            {
                return stage.StageTitle;
            }

            return $"stage-{owner.currentStageIndex + 1}";
        }

        public bool TryGetCurrentStageDefinition(out MissionStageDefinition stage)
        {
            stage = null;
            if (!HasActiveStageSequence() || owner.currentStageIndex < 0 || owner.currentStageIndex >= owner.activeStageDefinitions.Count)
            {
                return false;
            }

            stage = owner.activeStageDefinitions[owner.currentStageIndex];
            return stage != null;
        }
    }
}
