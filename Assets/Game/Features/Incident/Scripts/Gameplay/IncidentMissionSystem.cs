using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public partial class IncidentMissionSystem : MonoBehaviour
{
    [System.Serializable]
    public class MissionStageChangedEvent : UnityEvent<int, string>
    {
    }

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

    [System.Serializable]
    private class MissionCompletedObjectiveRecord
    {
        [SerializeField] private int stageIndex = -1;
        [SerializeField] private string stageId;
        [SerializeField] private string title;
        [SerializeField] private string summary;
        [SerializeField] private bool isComplete;
        [SerializeField] private bool hasFailed;
        [SerializeField] private int score;
        [SerializeField] private int maxScore;

        public int StageIndex => stageIndex;

        public void Set(int resolvedStageIndex, string resolvedStageId, MissionObjectiveStatus status)
        {
            stageIndex = resolvedStageIndex;
            stageId = resolvedStageId;
            title = status != null ? status.Title : string.Empty;
            summary = status != null ? status.Summary : string.Empty;
            isComplete = status != null && status.IsComplete;
            hasFailed = status != null && status.HasFailed;
            score = status != null ? status.Score : 0;
            maxScore = status != null ? status.MaxScore : 0;
        }

        public MissionObjectiveStatusSnapshot ToSnapshot()
        {
            return new MissionObjectiveStatusSnapshot(title, summary, isComplete, hasFailed, score, maxScore);
        }
    }

    [System.Serializable]
    private class MissionStageScoreRecord
    {
        [SerializeField] private int stageIndex;
        [SerializeField] private string stageId;
        [SerializeField] private string stageTitle;
        [SerializeField] private int score;
        [SerializeField] private int maxScore;

        public int StageIndex => stageIndex;
        public string StageId => stageId;
        public string StageTitle => stageTitle;
        public int Score => score;
        public int MaxScore => maxScore;

        public void Set(int resolvedStageIndex, string resolvedStageId, string resolvedStageTitle, int resolvedScore, int resolvedMaxScore)
        {
            stageIndex = resolvedStageIndex;
            stageId = resolvedStageId;
            stageTitle = resolvedStageTitle;
            score = Mathf.Max(0, resolvedScore);
            maxScore = Mathf.Max(0, resolvedMaxScore);
        }
    }

    [System.Serializable]
    private class MissionStageActionBinding
    {
        [SerializeField] private string stageId;
        [SerializeField] private UnityEvent onStageStarted;
        [SerializeField] private UnityEvent onStageCompleted;

        public bool Matches(string candidateStageId)
        {
            if (string.IsNullOrWhiteSpace(stageId) || string.IsNullOrWhiteSpace(candidateStageId))
            {
                return false;
            }

            return string.Equals(stageId.Trim(), candidateStageId.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }

        public void InvokeStarted()
        {
            onStageStarted?.Invoke();
        }

        public void InvokeCompleted()
        {
            onStageCompleted?.Invoke();
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
    [SerializeField] private MissionStageChangedEvent onStageStarted;
    [SerializeField] private MissionStageChangedEvent onStageCompleted;
    [SerializeField] private List<MissionStageActionBinding> stageActionBindings = new List<MissionStageActionBinding>();

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
    [SerializeField] private int currentStageIndex = -1;
    [SerializeField] private int totalStageCount;
    [SerializeField] private string currentStageTitle;
    [SerializeField] private string currentStageDescription;
    [SerializeField] private bool isStageTransitionPending;
    [SerializeField] private int pendingStageIndex = -1;
    [SerializeField] private float pendingStageStartTime = -1f;
    [SerializeField] private int lastStartedStageEventIndex = -1;
    [SerializeField] private int lastCompletedStageEventIndex = -1;
    [SerializeField] private List<MissionObjectiveStatus> objectiveStatuses = new List<MissionObjectiveStatus>();
    [SerializeField] private List<MissionCompletedObjectiveRecord> completedObjectiveRecords = new List<MissionCompletedObjectiveRecord>();
    [SerializeField] private List<MissionStageScoreRecord> completedStageScoreRecords = new List<MissionStageScoreRecord>();
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

    private readonly List<MissionObjectiveDefinition> activeObjectiveDefinitions = new List<MissionObjectiveDefinition>();
    private readonly List<MissionFailConditionDefinition> activeFailConditionDefinitions = new List<MissionFailConditionDefinition>();
    private readonly List<MissionStageDefinition> activeStageDefinitions = new List<MissionStageDefinition>();
    private readonly List<MissionObjectiveDefinition> scoreScratchObjectives = new List<MissionObjectiveDefinition>();
    private readonly List<MissionStageDefinition> resultStageScratch = new List<MissionStageDefinition>();
    private readonly List<MissionObjectiveDefinition> resultObjectiveScratch = new List<MissionObjectiveDefinition>();
    private readonly List<MissionFailConditionDefinition> resultFailConditionScratch = new List<MissionFailConditionDefinition>();

    public string MissionId => ResolveMissionId();
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
    public bool HasActiveStage => HasActiveStageSequence();
    public int CurrentStageIndex => currentStageIndex;
    public int TotalStageCount => totalStageCount;
    public string CurrentStageTitle => currentStageTitle;
    public string CurrentStageDescription => currentStageDescription;
    public bool IsStageTransitionPending => isStageTransitionPending;
    public float RemainingStageTransitionDelaySeconds => isStageTransitionPending
        ? Mathf.Max(0f, pendingStageStartTime - elapsedTime)
        : 0f;
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
    public string CurrentStageId => ResolveCurrentStageId();
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

        if (UpdatePendingStageTransition())
        {
            return;
        }

        if (TryAdvanceMissionStageIfReady())
        {
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
        ResetMissionStageRuntime();
        ResetScoreRuntime();
        ResetFinalPerformanceSnapshot();
        ResetObjectiveHistoryRuntime();
        ResetSignalEmitters();
        RefreshObjectives();
        elapsedTime = 0f;
        missionState = MissionState.Running;
        onMissionStarted?.Invoke();
        InvokeCurrentStageStarted();
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
        InvokeCurrentStageCompleted();
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
        status = default;
        if (index < 0)
        {
            return false;
        }

        int completedCount = completedObjectiveRecords != null ? completedObjectiveRecords.Count : 0;
        if (index < completedCount)
        {
            MissionCompletedObjectiveRecord record = completedObjectiveRecords[index];
            if (record == null)
            {
                return false;
            }

            status = record.ToSnapshot();
            return true;
        }

        if (!ShouldIncludeCurrentObjectiveStatusesInResult())
        {
            return false;
        }

        int currentIndex = index - completedCount;
        return TryGetObjectiveStatus(currentIndex, out status);
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
        if (activeObjectiveDefinitions.Count > 0)
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

        missionDefinition.CollectObjectives(resultObjectiveScratch);
        for (int i = 0; i < resultObjectiveScratch.Count; i++)
        {
            MissionObjectiveDefinition objective = resultObjectiveScratch[i];
            if (objective != null)
            {
                objective.CollectTargets(sceneData);
            }
        }

        missionDefinition.CollectStages(resultStageScratch);
        for (int stageIndex = 0; stageIndex < resultStageScratch.Count; stageIndex++)
        {
            MissionStageDefinition stage = resultStageScratch[stageIndex];
            if (stage == null)
            {
                continue;
            }

            resultObjectiveScratch.Clear();
            stage.CollectObjectives(resultObjectiveScratch);
            for (int objectiveIndex = 0; objectiveIndex < resultObjectiveScratch.Count; objectiveIndex++)
            {
                MissionObjectiveDefinition objective = resultObjectiveScratch[objectiveIndex];
                if (objective != null)
                {
                    objective.CollectTargets(sceneData);
                }
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

    private bool AreActiveDefinitionObjectivesSatisfied(MissionProgressSnapshot snapshot, bool allowNoRelevantObjectives)
    {
        MissionObjectiveContext context = BuildObjectiveContext(snapshot);
        bool hasRelevantObjective = false;
        for (int i = 0; i < activeObjectiveDefinitions.Count; i++)
        {
            MissionObjectiveDefinition objective = activeObjectiveDefinitions[i];
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

    private void ResetObjectiveHistoryRuntime()
    {
        Objectives.ResetObjectiveHistoryRuntime();
    }

    private void CaptureCurrentStageObjectiveHistoryIfNeeded(string stageId)
    {
        Objectives.CaptureCurrentStageObjectiveHistoryIfNeeded(stageId);
    }

    private bool HasCapturedStageObjectiveHistory(int stageIndex)
    {
        return Objectives.HasCapturedStageObjectiveHistory(stageIndex);
    }

    private int GetResultObjectiveStatusCount()
    {
        int completedCount = completedObjectiveRecords != null ? completedObjectiveRecords.Count : 0;
        if (!ShouldIncludeCurrentObjectiveStatusesInResult())
        {
            return completedCount;
        }

        return completedCount + ObjectiveStatusCount;
    }

    private bool ShouldIncludeCurrentObjectiveStatusesInResult()
    {
        if (!HasActiveStageSequence())
        {
            return ObjectiveStatusCount > 0;
        }

        return !HasCapturedStageObjectiveHistory(currentStageIndex) && ObjectiveStatusCount > 0;
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
            if (status != null)
            {
                score += Mathf.Max(0, status.Score);
            }
        }

        return score;
    }

    private int CalculateCompletedStageObjectiveScore()
    {
        if (completedStageScoreRecords == null)
        {
            return 0;
        }

        int score = 0;
        for (int i = 0; i < completedStageScoreRecords.Count; i++)
        {
            MissionStageScoreRecord record = completedStageScoreRecords[i];
            if (record != null)
            {
                score += Mathf.Max(0, record.Score);
            }
        }

        return score;
    }

    private bool HasCapturedStageScore(int stageIndex)
    {
        if (stageIndex < 0 || completedStageScoreRecords == null)
        {
            return false;
        }

        for (int i = 0; i < completedStageScoreRecords.Count; i++)
        {
            MissionStageScoreRecord record = completedStageScoreRecords[i];
            if (record != null && record.StageIndex == stageIndex)
            {
                return true;
            }
        }

        return false;
    }

    private int CalculateMissionObjectiveMaximumScore()
    {
        if (HasActiveStageSequence())
        {
            int score = 0;
            for (int i = 0; i < activeStageDefinitions.Count; i++)
            {
                MissionStageDefinition stage = activeStageDefinitions[i];
                if (stage == null)
                {
                    continue;
                }

                scoreScratchObjectives.Clear();
                stage.CollectObjectives(scoreScratchObjectives);
                for (int objectiveIndex = 0; objectiveIndex < scoreScratchObjectives.Count; objectiveIndex++)
                {
                    MissionObjectiveDefinition objective = scoreScratchObjectives[objectiveIndex];
                    if (objective != null)
                    {
                        score += objective.ScoreWeight;
                    }
                }
            }

            scoreScratchObjectives.Clear();
            return score;
        }

        if (activeObjectiveDefinitions.Count > 0)
        {
            int score = 0;
            for (int i = 0; i < activeObjectiveDefinitions.Count; i++)
            {
                MissionObjectiveDefinition objective = activeObjectiveDefinitions[i];
                if (objective != null)
                {
                    score += objective.ScoreWeight;
                }
            }

            return score;
        }

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

    private void CaptureCurrentStageScoreIfNeeded(string stageId)
    {
        Scoring.CaptureCurrentStageScoreIfNeeded(stageId);
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
    {
        if (!HasActiveStageSequence())
        {
            return string.Empty;
        }

        string stageLabel = string.IsNullOrWhiteSpace(currentStageTitle)
            ? MissionLocalization.Format("mission.hud.stage.single", "Stage {0}", currentStageIndex + 1)
            : MissionLocalization.Format("mission.hud.stage.full", "Stage {0}/{1}: {2}", currentStageIndex + 1, totalStageCount, currentStageTitle);
        return $"{stageLabel}\n";
    }

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
