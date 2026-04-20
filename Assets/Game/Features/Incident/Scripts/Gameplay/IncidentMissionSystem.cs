using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public partial class IncidentMissionSystem : MonoBehaviour
{
    [System.Serializable]
    private class MissionObjectiveStatus
    {
        [SerializeField] private string title;
        [SerializeField] private string summary;
        [SerializeField] private bool isComplete;
        [SerializeField] private bool hasFailed;
        [SerializeField] private int score;
        [SerializeField] private int maxScore;

        public string Title => title;
        public string Summary => summary;
        public bool IsComplete => isComplete;
        public bool HasFailed => hasFailed;
        public int Score => score;
        public int MaxScore => maxScore;

        public void Set(MissionObjectiveEvaluation evaluation, MissionObjectiveScoreEvaluation scoreEvaluation)
        {
            title = evaluation.Title;
            summary = evaluation.Summary;
            isComplete = evaluation.IsComplete;
            hasFailed = evaluation.HasFailed;
            score = scoreEvaluation.Score;
            maxScore = scoreEvaluation.MaxScore;
        }
    }

    public enum MissionState
    {
        Idle = 0,
        Running = 1,
        Completed = 2,
        Failed = 3
    }

    private const int LegacyObjectiveScoreWeight = 10;

    [Header("Mission")]
    [SerializeField] private MissionDefinition missionDefinition;
    [SerializeField] private MissionSceneObjectRegistry sceneObjectRegistry;
    [SerializeField] private string missionId = "incident";
    [SerializeField] private string missionTitle = "Resolve Incident";
    [SerializeField] private string missionDescription = "Extinguish fires and rescue civilians.";
    [SerializeField] private bool autoStartOnEnable = true;
    [SerializeField] private float timeLimitSeconds = 0f;

    [Header("Overlay")]
    [SerializeField] private bool showMissionOverlay = true;
    [SerializeField] private Vector2 overlayOffset = new Vector2(16f, 110f);

    [Header("Objectives")]
    [SerializeField] private bool autoDiscoverFires = true;
    [SerializeField] private bool autoDiscoverRescuables = true;
    [SerializeField] private bool autoDiscoverVictimConditions = true;
    [SerializeField] private bool requireAllFiresExtinguished = true;
    [SerializeField] private bool requireAllRescuablesRescued = true;
    [SerializeField] private bool failOnAnyVictimDeath = false;
    [SerializeField] private int maxAllowedVictimDeaths = -1;
    [SerializeField] private bool requireNoCriticalVictimsAtCompletion = false;
    [SerializeField] private bool requireAllLivingVictimsStabilized = false;
    [SerializeField] private List<Fire> trackedFires = new List<Fire>();
    [SerializeField] private List<Rescuable> trackedRescuables = new List<Rescuable>();
    [SerializeField] private List<VictimCondition> trackedVictimConditions = new List<VictimCondition>();

    [Header("Events")]
    [SerializeField] private UnityEvent onMissionStarted;
    [SerializeField] private UnityEvent onMissionCompleted;
    [SerializeField] private UnityEvent onMissionFailed;

    [Header("Runtime")]
    [SerializeField] private MissionState missionState = MissionState.Idle;
    [SerializeField] private float elapsedTime;
    [SerializeField] private int totalTrackedFires;
    [SerializeField] private int extinguishedFireCount;
    [SerializeField] private int totalTrackedRescuables;
    [SerializeField] private int rescuedCount;
    [SerializeField] private int totalTrackedVictims;
    [SerializeField] private int aliveVictimCount;
    [SerializeField] private int urgentVictimCount;
    [SerializeField] private int criticalVictimCount;
    [SerializeField] private int stabilizedVictimCount;
    [SerializeField] private int extractedVictimCount;
    [SerializeField] private int deceasedVictimCount;
    [SerializeField] private List<MissionObjectiveStatus> objectiveStatuses = new List<MissionObjectiveStatus>();
    [SerializeField] private int currentScore;
    [SerializeField] private int maximumScore;
    [SerializeField] private string currentScoreRank;
    [SerializeField] private int finalScore;
    [SerializeField] private int finalMaximumScore;
    [SerializeField] private string finalScoreRank;
    [SerializeField] private int finalTotalTrackedFires;
    [SerializeField] private int finalExtinguishedFireCount;
    [SerializeField] private int finalTotalTrackedRescuables;
    [SerializeField] private int finalRescuedCount;
    [SerializeField] private int finalTotalTrackedVictims;
    [SerializeField] private int finalUrgentVictimCount;
    [SerializeField] private int finalCriticalVictimCount;
    [SerializeField] private int finalStabilizedVictimCount;
    [SerializeField] private int finalExtractedVictimCount;
    [SerializeField] private int finalDeceasedVictimCount;
    [SerializeField] private bool progressDirty = true;
    [SerializeField] private List<string> activatedSignalKeys = new List<string>();

    private readonly List<MissionObjectiveDefinition> activePersistentObjectiveDefinitions = new List<MissionObjectiveDefinition>();
    private readonly List<MissionFailConditionDefinition> activeFailConditionDefinitions = new List<MissionFailConditionDefinition>();
    private readonly HashSet<MissionObjectiveDefinition> objectiveScratchSet = new HashSet<MissionObjectiveDefinition>();
    private readonly List<MissionObjectiveDefinition> resultObjectiveScratch = new List<MissionObjectiveDefinition>();
    private readonly List<MissionFailConditionDefinition> resultFailConditionScratch = new List<MissionFailConditionDefinition>();

    public string MissionId => ResolveMissionId();
    public string MissionOperationTitle => ResolveMissionOperationTitle();
    public string MissionTitle => ResolveMissionTitle();
    public string MissionDescription => ResolveMissionDescription();
    public MissionState State => missionState;
    public float ElapsedTime => elapsedTime;
    public float TimeLimitSeconds => ResolveTimeLimitSeconds();
    public int TotalTrackedFires => totalTrackedFires;
    public int ExtinguishedFireCount => extinguishedFireCount;
    public int ActiveFireCount => Mathf.Max(0, totalTrackedFires - extinguishedFireCount);
    public int TotalTrackedRescuables => totalTrackedRescuables;
    public int RescuedCount => rescuedCount;
    public int RemainingRescuableCount => Mathf.Max(0, totalTrackedRescuables - rescuedCount);
    public int TotalTrackedVictims => totalTrackedVictims;
    public int AliveVictimCount => aliveVictimCount;
    public int UrgentVictimCount => urgentVictimCount;
    public int CriticalVictimCount => criticalVictimCount;
    public int StabilizedVictimCount => stabilizedVictimCount;
    public int ExtractedVictimCount => extractedVictimCount;
    public int DeceasedVictimCount => deceasedVictimCount;
    public bool HasActiveStage => false;
    public int CurrentStageIndex => -1;
    public int TotalStageCount => 0;
    public string CurrentStageTitle => string.Empty;
    public string CurrentStageDescription => string.Empty;
    public bool IsStageTransitionPending => false;
    public float RemainingStageTransitionDelaySeconds => 0f;
    public int ObjectiveStatusCount => objectiveStatuses != null ? objectiveStatuses.Count : 0;
    public int ResultObjectiveStatusCount => GetResultObjectiveStatusCount();
    public int CurrentScore => currentScore;
    public int MaximumScore => maximumScore;
    public string CurrentScoreRank => currentScoreRank;
    public int FinalScore => finalScore;
    public int FinalMaximumScore => finalMaximumScore;
    public string FinalScoreRank => finalScoreRank;
    public int DisplayedScore => missionState == MissionState.Completed || missionState == MissionState.Failed ? finalScore : currentScore;
    public int DisplayedMaximumScore => missionState == MissionState.Completed || missionState == MissionState.Failed ? finalMaximumScore : maximumScore;
    public string DisplayedScoreRank => missionState == MissionState.Completed || missionState == MissionState.Failed ? finalScoreRank : currentScoreRank;
    public int DisplayedTotalTrackedFires => missionState == MissionState.Completed || missionState == MissionState.Failed ? finalTotalTrackedFires : totalTrackedFires;
    public int DisplayedExtinguishedFireCount => missionState == MissionState.Completed || missionState == MissionState.Failed ? finalExtinguishedFireCount : extinguishedFireCount;
    public int DisplayedTotalTrackedRescuables => missionState == MissionState.Completed || missionState == MissionState.Failed ? finalTotalTrackedRescuables : totalTrackedRescuables;
    public int DisplayedRescuedCount => missionState == MissionState.Completed || missionState == MissionState.Failed ? finalRescuedCount : rescuedCount;
    public int DisplayedTotalTrackedVictims => missionState == MissionState.Completed || missionState == MissionState.Failed ? finalTotalTrackedVictims : totalTrackedVictims;
    public int DisplayedUrgentVictimCount => missionState == MissionState.Completed || missionState == MissionState.Failed ? finalUrgentVictimCount : urgentVictimCount;
    public int DisplayedCriticalVictimCount => missionState == MissionState.Completed || missionState == MissionState.Failed ? finalCriticalVictimCount : criticalVictimCount;
    public int DisplayedStabilizedVictimCount => missionState == MissionState.Completed || missionState == MissionState.Failed ? finalStabilizedVictimCount : stabilizedVictimCount;
    public int DisplayedExtractedVictimCount => missionState == MissionState.Completed || missionState == MissionState.Failed ? finalExtractedVictimCount : extractedVictimCount;
    public int DisplayedDeceasedVictimCount => missionState == MissionState.Completed || missionState == MissionState.Failed ? finalDeceasedVictimCount : deceasedVictimCount;
    public float RemainingTimeSeconds => Mathf.Max(0f, ResolveTimeLimitSeconds() - elapsedTime);
    public string CurrentStageId => string.Empty;
    public bool FailsOnAnyVictimDeath => ResolveFailsOnAnyVictimDeath();

    private GUIStyle overlayGuiStyle;

    private void OnEnable()
    {
        LanguageManager.LanguageChanged -= HandleLanguageChanged;
        LanguageManager.LanguageChanged += HandleLanguageChanged;
        RefreshObjectives();

        if (autoStartOnEnable)
            StartMission();
    }

    private void OnDisable()
    {
        LanguageManager.LanguageChanged -= HandleLanguageChanged;
        UnsubscribeFromTrackedTargetEvents();
    }

    private void Update()
    {
        if (missionState != MissionState.Running)
            return;

        elapsedTime += Time.deltaTime;
        RefreshRuntimeStateIfDirty();

        if (HasFailedObjectiveOutcome())
        {
            FailMission();
            return;
        }

        if (HasFailedConditionOutcome())
        {
            FailMission();
            return;
        }

        if (AreCompletionConditionsMet())
            CompleteMission();
    }

    [ContextMenu("Refresh Objectives")]
    public void RefreshObjectives()
    {
        UnsubscribeFromTrackedTargetEvents();
        if (!RefreshObjectivesFromDefinition())
        {
            RefreshLegacyObjectives();
        }

        SubscribeToTrackedTargetEvents();
        MarkProgressDirty();
        RefreshRuntimeStateIfDirty();
    }

    [ContextMenu("Start Mission")]
    public void StartMission()
    {
        ResetScoreRuntime();
        ResetFinalPerformanceSnapshot();
        ResetSignalEmitters();
        RefreshObjectives();
        elapsedTime = 0f;
        missionState = MissionState.Running;
        onMissionStarted?.Invoke();
    }

    [ContextMenu("Fail Mission")]
    public void FailMission()
    {
        if (missionState == MissionState.Failed)
            return;

        RefreshProgress();
        RefreshObjectiveStatuses();
        missionState = MissionState.Failed;
        RefreshScoreState();
        CacheFinalScore();
        CacheFinalPerformanceSnapshot();
        onMissionFailed?.Invoke();
    }

    [ContextMenu("Complete Mission")]
    public void CompleteMission()
    {
        if (missionState == MissionState.Completed)
            return;

        RefreshProgress();
        RefreshObjectiveStatuses();
        missionState = MissionState.Completed;
        RefreshScoreState();
        CacheFinalScore();
        CacheFinalPerformanceSnapshot();
        onMissionCompleted?.Invoke();
    }

    public bool IsObjectiveComplete()
    {
        RefreshRuntimeStateIfDirty();
        return AreCompletionConditionsMet();
    }

    public bool TryGetObjectiveStatus(int index, out MissionObjectiveStatusSnapshot status)
    {
        status = default;
        if (objectiveStatuses == null || index < 0 || index >= objectiveStatuses.Count)
        {
            return false;
        }

        MissionObjectiveStatus objectiveStatus = objectiveStatuses[index];
        if (objectiveStatus == null)
        {
            return false;
        }

        status = new MissionObjectiveStatusSnapshot(
            objectiveStatus.Title,
            objectiveStatus.Summary,
            objectiveStatus.IsComplete,
            objectiveStatus.HasFailed,
            objectiveStatus.Score,
            objectiveStatus.MaxScore);
        return true;
    }

    public bool TryGetResultObjectiveStatus(int index, out MissionObjectiveStatusSnapshot status)
    {
        return TryGetObjectiveStatus(index, out status);
    }

    public bool TryResolveSceneObject(string key, out GameObject targetObject)
    {
        targetObject = null;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        MissionSceneObjectRegistry registry = ResolveSceneObjectRegistry();
        return registry != null && registry.TryResolveGameObject(key, out targetObject);
    }

    public bool NotifySignal(string signalKey)
    {
        if (missionState != MissionState.Running || string.IsNullOrWhiteSpace(signalKey))
        {
            return false;
        }

        string normalizedKey = signalKey.Trim();
        if (HasSignal(normalizedKey))
        {
            return false;
        }

        activatedSignalKeys.Add(normalizedKey);
        MarkProgressDirty();
        return true;
    }

    public bool HasSignal(string signalKey)
    {
        if (string.IsNullOrWhiteSpace(signalKey) || activatedSignalKeys == null)
        {
            return false;
        }

        string normalizedKey = signalKey.Trim();
        for (int i = 0; i < activatedSignalKeys.Count; i++)
        {
            string candidate = activatedSignalKeys[i];
            if (!string.IsNullOrWhiteSpace(candidate) &&
                string.Equals(candidate.Trim(), normalizedKey, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void OnGUI()
    {
        if (!showMissionOverlay)
            return;

        EnsureOverlayGuiStyle();

        float activeTimeLimit = ResolveTimeLimitSeconds();
        string timerText = activeTimeLimit > 0f
            ? MissionLocalization.Format("mission.hud.timer.remaining", "{0:F1}s left", Mathf.Max(0f, activeTimeLimit - elapsedTime))
            : MissionLocalization.Format("mission.hud.timer.elapsed", "{0:F1}s elapsed", elapsedTime);
        string overlayText =
            $"{MissionTitle}\n" +
            $"{MissionLocalization.Format("mission.hud.state", "State: {0}", LocalizeMissionState(missionState))}\n" +
            BuildStageOverlayLine() +
            BuildObjectiveOverlayLines() +
            BuildScoreOverlayLine() +
            $"{MissionLocalization.Format("mission.overlay.time", "Time: {0}", timerText)}";

        Vector2 size = overlayGuiStyle.CalcSize(new GUIContent(overlayText));
        Rect rect = new Rect(
            overlayOffset.x,
            overlayOffset.y,
            Mathf.Max(280f, size.x + 16f),
            size.y + 12f);

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.Box(rect, GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f), overlayText, overlayGuiStyle);
        GUI.color = previousColor;
    }

    private void ResetScoreRuntime()
    {
        Scoring.ResetScoreRuntime();
    }

    private void CacheFinalScore()
    {
        Scoring.CacheFinalScore();
    }

    private void ResetFinalPerformanceSnapshot()
    {
        finalTotalTrackedFires = 0;
        finalExtinguishedFireCount = 0;
        finalTotalTrackedRescuables = 0;
        finalRescuedCount = 0;
        finalTotalTrackedVictims = 0;
        finalUrgentVictimCount = 0;
        finalCriticalVictimCount = 0;
        finalStabilizedVictimCount = 0;
        finalExtractedVictimCount = 0;
        finalDeceasedVictimCount = 0;
    }

    private void CacheFinalPerformanceSnapshot()
    {
        MissionProgressSnapshot snapshot = BuildResultPerformanceSnapshot();
        finalTotalTrackedFires = snapshot.TotalTrackedFires;
        finalExtinguishedFireCount = snapshot.ExtinguishedFireCount;
        finalTotalTrackedRescuables = snapshot.TotalTrackedRescuables;
        finalRescuedCount = snapshot.RescuedCount;
        finalTotalTrackedVictims = snapshot.TotalTrackedVictims;
        finalUrgentVictimCount = snapshot.UrgentVictimCount;
        finalCriticalVictimCount = snapshot.CriticalVictimCount;
        finalStabilizedVictimCount = snapshot.StabilizedVictimCount;
        finalExtractedVictimCount = snapshot.ExtractedVictimCount;
        finalDeceasedVictimCount = snapshot.DeceasedVictimCount;
    }

    private void RefreshScoreState()
    {
        Scoring.RefreshScoreState();
    }

    private bool ResolveFailsOnAnyVictimDeath()
    {
        return Objectives.FailsOnAnyVictimDeath();
    }

    private bool AreCompletionConditionsMet()
    {
        return Objectives.AreCompletionConditionsMet();
    }

    private bool AreLegacyCompletionConditionsMet(MissionProgressSnapshot snapshot)
    {
        return Objectives.AreLegacyCompletionConditionsMet(snapshot);
    }

    private void EnsureOverlayGuiStyle()
    {
        if (overlayGuiStyle != null)
            return;

        overlayGuiStyle = new GUIStyle(GUI.skin.label)
        {
            richText = false,
            alignment = TextAnchor.UpperLeft,
            fontSize = 13,
            wordWrap = true
        };
        overlayGuiStyle.normal.textColor = Color.white;
    }

    private string BuildVictimOverlayLine()
    {
        if (totalTrackedVictims <= 0)
            return string.Empty;

        return MissionLocalization.Format(
            "mission.overlay.victims",
            "Victims: U {0} | C {1} | S {2} | X {3} | D {4}\n",
            urgentVictimCount,
            criticalVictimCount,
            stabilizedVictimCount,
            extractedVictimCount,
            deceasedVictimCount);
    }

    private bool HasFailedVictimOutcome()
    {
        if (HasAnyActiveDefinitionObjectives())
        {
            return false;
        }

        if (failOnAnyVictimDeath && deceasedVictimCount > 0)
            return true;

        return maxAllowedVictimDeaths >= 0 && deceasedVictimCount > maxAllowedVictimDeaths;
    }

    private bool RefreshObjectivesFromDefinition()
    {
        return Objectives.RefreshObjectivesFromDefinition();
    }

    private void RefreshLegacyObjectives()
    {
        Objectives.RefreshLegacyObjectives();
    }

    private void SubscribeToTrackedTargetEvents()
    {
        SubscribeToFireEvents();
        SubscribeToRescuableEvents();
        SubscribeToVictimConditionEvents();
    }

    private void UnsubscribeFromTrackedTargetEvents()
    {
        UnsubscribeFromFireEvents();
        UnsubscribeFromRescuableEvents();
        UnsubscribeFromVictimConditionEvents();
    }

    private void SubscribeToFireEvents()
    {
        if (trackedFires == null)
        {
            return;
        }

        for (int i = 0; i < trackedFires.Count; i++)
        {
            Fire fire = trackedFires[i];
            if (fire != null)
            {
                fire.BurningStateChanged += HandleTrackedFireBurningStateChanged;
            }
        }
    }

    private void UnsubscribeFromFireEvents()
    {
        if (trackedFires == null)
        {
            return;
        }

        for (int i = 0; i < trackedFires.Count; i++)
        {
            Fire fire = trackedFires[i];
            if (fire != null)
            {
                fire.BurningStateChanged -= HandleTrackedFireBurningStateChanged;
            }
        }
    }

    private void SubscribeToRescuableEvents()
    {
        if (trackedRescuables == null)
        {
            return;
        }

        for (int i = 0; i < trackedRescuables.Count; i++)
        {
            Rescuable rescuable = trackedRescuables[i];
            if (rescuable != null)
            {
                rescuable.RescueCompleted += HandleTrackedRescueCompleted;
            }
        }
    }

    private void UnsubscribeFromRescuableEvents()
    {
        if (trackedRescuables == null)
        {
            return;
        }

        for (int i = 0; i < trackedRescuables.Count; i++)
        {
            Rescuable rescuable = trackedRescuables[i];
            if (rescuable != null)
            {
                rescuable.RescueCompleted -= HandleTrackedRescueCompleted;
            }
        }
    }

    private void SubscribeToVictimConditionEvents()
    {
        if (trackedVictimConditions == null)
        {
            return;
        }

        for (int i = 0; i < trackedVictimConditions.Count; i++)
        {
            VictimCondition victimCondition = trackedVictimConditions[i];
            if (victimCondition != null)
            {
                victimCondition.OnTriageStateChanged += HandleTrackedVictimTriageStateChanged;
                victimCondition.OnConditionContextChanged += HandleTrackedVictimConditionContextChanged;
                victimCondition.OnVictimStabilized += HandleTrackedVictimContextChanged;
                victimCondition.OnVictimExtracted += HandleTrackedVictimContextChanged;
            }
        }
    }

    private void UnsubscribeFromVictimConditionEvents()
    {
        if (trackedVictimConditions == null)
        {
            return;
        }

        for (int i = 0; i < trackedVictimConditions.Count; i++)
        {
            VictimCondition victimCondition = trackedVictimConditions[i];
            if (victimCondition != null)
            {
                victimCondition.OnTriageStateChanged -= HandleTrackedVictimTriageStateChanged;
                victimCondition.OnConditionContextChanged -= HandleTrackedVictimConditionContextChanged;
                victimCondition.OnVictimStabilized -= HandleTrackedVictimContextChanged;
                victimCondition.OnVictimExtracted -= HandleTrackedVictimContextChanged;
            }
        }
    }

    private void HandleTrackedFireBurningStateChanged(bool isBurning)
    {
        MarkProgressDirty();
    }

    private void HandleTrackedRescueCompleted()
    {
        MarkProgressDirty();
        if (missionState == MissionState.Running)
        {
            RefreshRuntimeStateIfDirty();
        }
    }

    private void HandleTrackedVictimTriageStateChanged(VictimCondition.TriageState triageState)
    {
        MarkProgressDirty();
    }

    private void HandleTrackedVictimConditionContextChanged()
    {
        MarkProgressDirty();
    }

    private void HandleTrackedVictimContextChanged()
    {
        MarkProgressDirty();
    }

    private MissionProgressSnapshot BuildProgressSnapshot()
    {
        return new MissionProgressSnapshot(
            totalTrackedFires,
            extinguishedFireCount,
            totalTrackedRescuables,
            rescuedCount,
            totalTrackedVictims,
            aliveVictimCount,
            urgentVictimCount,
            criticalVictimCount,
            stabilizedVictimCount,
            extractedVictimCount,
            deceasedVictimCount);
    }

    private MissionProgressSnapshot BuildResultPerformanceSnapshot()
    {
        List<Fire> resultFires = null;
        List<Rescuable> resultRescuables = null;
        List<VictimCondition> resultVictimConditions = null;

        if (missionDefinition != null)
        {
            MissionRuntimeSceneData sceneData = new MissionRuntimeSceneData();
            CollectResultSceneTargets(sceneData);

            resultFires = sceneData.CreateFireList();
            resultRescuables = sceneData.CreateRescuableList();
            resultVictimConditions = sceneData.CreateVictimConditionList();
            AppendVictimConditionsFromRescuables(resultVictimConditions, resultRescuables);
        }

        if (resultFires == null || resultFires.Count == 0)
        {
            resultFires = CollectSceneObjectsIncludingInactive<Fire>();
        }

        if (resultRescuables == null || resultRescuables.Count == 0)
        {
            resultRescuables = CollectSceneObjectsIncludingInactive<Rescuable>();
        }

        if (resultVictimConditions == null || resultVictimConditions.Count == 0)
        {
            resultVictimConditions = CollectSceneObjectsIncludingInactive<VictimCondition>();
            AppendVictimConditionsFromRescuables(resultVictimConditions, resultRescuables);
        }

        RemoveNullEntries(resultFires);
        RemoveNullEntries(resultRescuables);
        RemoveNullEntries(resultVictimConditions);

        return new MissionProgressSnapshot(
            resultFires.Count,
            CountExtinguishedFires(resultFires),
            resultRescuables.Count,
            CountRescuedTargets(resultRescuables),
            resultVictimConditions.Count,
            CountLivingVictims(resultVictimConditions),
            CountVictimsInState(resultVictimConditions, VictimCondition.TriageState.Urgent),
            CountVictimsInState(resultVictimConditions, VictimCondition.TriageState.Critical),
            CountStabilizedVictims(resultVictimConditions),
            CountExtractedVictims(resultVictimConditions),
            CountVictimsInState(resultVictimConditions, VictimCondition.TriageState.Deceased));
    }

    private void CollectResultSceneTargets(MissionRuntimeSceneData sceneData)
    {
        if (sceneData == null || missionDefinition == null)
        {
            return;
        }

        missionDefinition.CollectPersistentObjectives(resultObjectiveScratch);
        for (int i = 0; i < resultObjectiveScratch.Count; i++)
        {
            MissionObjectiveDefinition objective = resultObjectiveScratch[i];
            if (objective != null)
            {
                objective.CollectTargets(sceneData);
            }
        }

        missionDefinition.CollectFailConditions(resultFailConditionScratch);
        for (int i = 0; i < resultFailConditionScratch.Count; i++)
        {
            MissionFailConditionDefinition failCondition = resultFailConditionScratch[i];
            if (failCondition != null)
            {
                failCondition.CollectTargets(sceneData);
            }
        }
    }

    private static void AppendVictimConditionsFromRescuables(List<VictimCondition> victims, List<Rescuable> rescuables)
    {
        if (victims == null || rescuables == null)
        {
            return;
        }

        HashSet<VictimCondition> existingVictims = new HashSet<VictimCondition>(victims);
        for (int i = 0; i < rescuables.Count; i++)
        {
            Rescuable rescuable = rescuables[i];
            if (rescuable == null)
            {
                continue;
            }

            VictimCondition victimCondition = rescuable.GetComponent<VictimCondition>();
            if (victimCondition != null && existingVictims.Add(victimCondition))
            {
                victims.Add(victimCondition);
            }
        }
    }

    private bool ArePersistentDefinitionObjectivesSatisfied(MissionProgressSnapshot snapshot, bool allowNoRelevantObjectives)
    {
        return AreObjectiveDefinitionsSatisfied(activePersistentObjectiveDefinitions, snapshot, allowNoRelevantObjectives);
    }

    private bool AreObjectiveDefinitionsSatisfied(List<MissionObjectiveDefinition> objectives, MissionProgressSnapshot snapshot, bool allowNoRelevantObjectives)
    {
        MissionObjectiveContext context = BuildObjectiveContext(snapshot);
        bool hasRelevantObjective = false;
        for (int i = 0; i < objectives.Count; i++)
        {
            MissionObjectiveDefinition objective = objectives[i];
            if (objective == null)
            {
                continue;
            }

            MissionObjectiveEvaluation evaluation = objective.Evaluate(context);
            if (!evaluation.IsRelevant)
            {
                continue;
            }

            hasRelevantObjective = true;
            if (!evaluation.IsComplete)
            {
                return false;
            }
        }

        return hasRelevantObjective || allowNoRelevantObjectives;
    }

    private bool HasAnyActiveDefinitionObjectives()
    {
        return activePersistentObjectiveDefinitions.Count > 0;
    }

    private bool HasFailedObjectiveOutcome()
    {
        return Objectives.HasFailedObjectiveOutcome();
    }

    private bool HasFailedConditionOutcome()
    {
        return Objectives.HasFailedConditionOutcome();
    }

    private void RefreshObjectiveStatuses()
    {
        Objectives.RefreshObjectiveStatuses();
    }

    private int GetResultObjectiveStatusCount()
    {
        return ObjectiveStatusCount;
    }

    private void BuildLegacyObjectiveStatuses(MissionProgressSnapshot snapshot)
    {
        Objectives.BuildLegacyObjectiveStatuses(snapshot);
    }

    private void AddObjectiveStatus(MissionObjectiveEvaluation evaluation, MissionObjectiveScoreEvaluation scoreEvaluation)
    {
        Objectives.AddObjectiveStatus(evaluation, scoreEvaluation);
    }

    private static MissionObjectiveScoreEvaluation CreateLegacyBinaryScore(bool isComplete)
    {
        int score = isComplete ? LegacyObjectiveScoreWeight : 0;
        return new MissionObjectiveScoreEvaluation(score, LegacyObjectiveScoreWeight, string.Empty);
    }

    private static MissionObjectiveScoreEvaluation CreateLegacyProgressiveScore(float normalizedProgress)
    {
        int score = Mathf.Clamp(Mathf.RoundToInt(LegacyObjectiveScoreWeight * Mathf.Clamp01(normalizedProgress)), 0, LegacyObjectiveScoreWeight);
        return new MissionObjectiveScoreEvaluation(score, LegacyObjectiveScoreWeight, string.Empty);
    }

    private int SumObjectiveStatusScore()
    {
        if (objectiveStatuses == null)
        {
            return 0;
        }

        int score = 0;
        for (int i = 0; i < objectiveStatuses.Count; i++)
        {
            MissionObjectiveStatus status = objectiveStatuses[i];
            if (status == null)
            {
                continue;
            }

            score += Mathf.Max(0, status.Score);
        }

        return score;
    }

    private int CalculateMissionObjectiveMaximumScore()
    {
        objectiveScratchSet.Clear();
        int score = 0;

        for (int i = 0; i < activePersistentObjectiveDefinitions.Count; i++)
        {
            MissionObjectiveDefinition objective = activePersistentObjectiveDefinitions[i];
            if (objective != null && objectiveScratchSet.Add(objective))
            {
                score += objective.ScoreWeight;
            }
        }

        if (activePersistentObjectiveDefinitions.Count > 0)
        {
            objectiveScratchSet.Clear();
            return score;
        }

        objectiveScratchSet.Clear();
        int legacyScore = 0;
        if (requireAllFiresExtinguished && totalTrackedFires > 0)
        {
            legacyScore += LegacyObjectiveScoreWeight;
        }

        if (requireAllRescuablesRescued && totalTrackedRescuables > 0)
        {
            legacyScore += LegacyObjectiveScoreWeight;
        }

        bool usesVictimObjective =
            totalTrackedVictims > 0 &&
            (failOnAnyVictimDeath || maxAllowedVictimDeaths >= 0 || requireNoCriticalVictimsAtCompletion || requireAllLivingVictimsStabilized);
        if (usesVictimObjective)
        {
            legacyScore += LegacyObjectiveScoreWeight;
        }

        return legacyScore;
    }

    private string BuildObjectiveOverlayLines()
    {
        if (objectiveStatuses == null || objectiveStatuses.Count == 0)
        {
            return
                MissionLocalization.Format("mission.overlay.fires", "Fires: {0}/{1}\n", extinguishedFireCount, totalTrackedFires) +
                MissionLocalization.Format("mission.overlay.rescues", "Rescues: {0}/{1}\n", rescuedCount, totalTrackedRescuables) +
                BuildVictimOverlayLine();
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < objectiveStatuses.Count; i++)
        {
            MissionObjectiveStatus status = objectiveStatuses[i];
            if (status == null)
            {
                continue;
            }

            string prefix = status.HasFailed
                ? MissionLocalization.Get("mission.hud.prefix.failed", "[FAILED]")
                : status.IsComplete
                    ? MissionLocalization.Get("mission.hud.prefix.completed", "[DONE]")
                    : MissionLocalization.Get("mission.hud.prefix.pending", "[ ]");
            builder.Append(prefix);
            builder.Append(' ');
            builder.Append(status.Summary);
            builder.Append('\n');
        }

        return builder.ToString();
    }

    private string BuildScoreOverlayLine()
    {
        if (DisplayedMaximumScore <= 0)
        {
            return string.Empty;
        }

        string rankSuffix = string.IsNullOrWhiteSpace(DisplayedScoreRank)
            ? string.Empty
            : $" ({DisplayedScoreRank})";
        return MissionLocalization.Format(
            "mission.hud.score",
            "Score: {0}/{1}{2}",
            DisplayedScore,
            DisplayedMaximumScore,
            rankSuffix) + "\n";
    }

    private string BuildStageOverlayLine()
        => string.Empty;

    private static string LocalizeMissionState(MissionState state)
    {
        return state switch
        {
            MissionState.Idle => MissionLocalization.Get("mission.state.idle", "Idle"),
            MissionState.Running => MissionLocalization.Get("mission.state.running", "Running"),
            MissionState.Completed => MissionLocalization.Get("mission.state.completed", "Completed"),
            MissionState.Failed => MissionLocalization.Get("mission.state.failed", "Failed"),
            _ => state.ToString()
        };
    }

    private MissionSceneObjectRegistry ResolveSceneObjectRegistry()
    {
        if (sceneObjectRegistry != null)
        {
            return sceneObjectRegistry;
        }

        sceneObjectRegistry = GetComponent<MissionSceneObjectRegistry>();
        if (sceneObjectRegistry != null)
        {
            return sceneObjectRegistry;
        }

        sceneObjectRegistry = FindAnyObjectByType<MissionSceneObjectRegistry>(FindObjectsInactive.Exclude);
        return sceneObjectRegistry;
    }

    private string ResolveMissionId()
    {
        if (missionDefinition != null && !string.IsNullOrWhiteSpace(missionDefinition.MissionId))
        {
            return missionDefinition.MissionId;
        }

        return missionId;
    }

    private string ResolveMissionTitle()
    {
        if (missionDefinition != null && !string.IsNullOrWhiteSpace(missionDefinition.MissionTitle))
        {
            return missionDefinition.MissionTitle;
        }

        return missionTitle;
    }

    private string ResolveMissionOperationTitle()
    {
        if (missionDefinition != null && !string.IsNullOrWhiteSpace(missionDefinition.OperationTitle))
        {
            return missionDefinition.OperationTitle;
        }

        string resolvedMissionTitle = ResolveMissionTitle();
        if (string.IsNullOrWhiteSpace(resolvedMissionTitle))
        {
            return "OPERATION";
        }

        return $"OPERATION: {resolvedMissionTitle.Trim().ToUpperInvariant()}";
    }

    private string ResolveMissionDescription()
    {
        if (missionDefinition != null && !string.IsNullOrWhiteSpace(missionDefinition.MissionDescription))
        {
            return missionDefinition.MissionDescription;
        }

        return missionDescription;
    }

    private float ResolveTimeLimitSeconds()
    {
        if (activeFailConditionDefinitions != null)
        {
            for (int i = 0; i < activeFailConditionDefinitions.Count; i++)
            {
                MissionFailConditionDefinition failCondition = activeFailConditionDefinitions[i];
                if (failCondition != null && failCondition.TryGetTimeLimitSeconds(out float resolvedTimeLimitSeconds))
                {
                    return resolvedTimeLimitSeconds;
                }
            }
        }

        if (missionDefinition != null && missionDefinition.TimeLimitSeconds > 0f)
        {
            return missionDefinition.TimeLimitSeconds;
        }

        return timeLimitSeconds;
    }

    private MissionObjectiveContext BuildObjectiveContext(MissionProgressSnapshot snapshot)
    {
        return new MissionObjectiveContext(this, snapshot, elapsedTime);
    }

    private void ResetSignalEmitters()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IMissionSignalResettable resettable)
            {
                resettable.ResetMissionSignalState();
            }
        }
    }

    private void HandleLanguageChanged(AppLanguage _)
    {
        RefreshObjectives();
    }
}
