using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class IncidentMissionSystem : MonoBehaviour
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

        public string Title => title;
        public string Summary => summary;
        public bool IsComplete => isComplete;
        public bool HasFailed => hasFailed;

        public void Set(MissionObjectiveEvaluation evaluation)
        {
            title = evaluation.Title;
            summary = evaluation.Summary;
            isComplete = evaluation.IsComplete;
            hasFailed = evaluation.HasFailed;
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
    [SerializeField] private bool progressDirty = true;
    [SerializeField] private List<string> activatedSignalKeys = new List<string>();

    private readonly List<MissionObjectiveDefinition> activeObjectiveDefinitions = new List<MissionObjectiveDefinition>();
    private readonly List<MissionFailConditionDefinition> activeFailConditionDefinitions = new List<MissionFailConditionDefinition>();
    private readonly List<MissionStageDefinition> activeStageDefinitions = new List<MissionStageDefinition>();

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
    public float RemainingTimeSeconds => Mathf.Max(0f, ResolveTimeLimitSeconds() - elapsedTime);
    public string CurrentStageId => ResolveCurrentStageId();

    private GUIStyle overlayGuiStyle;

    private void OnEnable()
    {
        RefreshObjectives();

        if (autoStartOnEnable)
            StartMission();
    }

    private void OnDisable()
    {
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

        missionState = MissionState.Failed;
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
            objectiveStatus.HasFailed);
        return true;
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
            ? $"{Mathf.Max(0f, activeTimeLimit - elapsedTime):F1}s left"
            : $"{elapsedTime:F1}s elapsed";
        string overlayText =
            $"{MissionTitle}\n" +
            $"State: {missionState}\n" +
            BuildStageOverlayLine() +
            BuildObjectiveOverlayLines() +
            $"Time: {timerText}";

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

    private void RefreshProgress()
    {
        RemoveNullEntries(trackedFires);
        RemoveNullEntries(trackedRescuables);
        RemoveNullEntries(trackedVictimConditions);

        totalTrackedFires = trackedFires.Count;
        extinguishedFireCount = CountExtinguishedFires(trackedFires);
        totalTrackedRescuables = trackedRescuables.Count;
        rescuedCount = CountRescuedTargets(trackedRescuables);
        totalTrackedVictims = trackedVictimConditions.Count;
        aliveVictimCount = CountLivingVictims(trackedVictimConditions);
        urgentVictimCount = CountVictimsInState(trackedVictimConditions, VictimCondition.TriageState.Urgent);
        criticalVictimCount = CountVictimsInState(trackedVictimConditions, VictimCondition.TriageState.Critical);
        stabilizedVictimCount = CountStabilizedVictims(trackedVictimConditions);
        extractedVictimCount = CountExtractedVictims(trackedVictimConditions);
        deceasedVictimCount = CountVictimsInState(trackedVictimConditions, VictimCondition.TriageState.Deceased);
    }

    private void RefreshRuntimeStateIfDirty()
    {
        if (!progressDirty)
        {
            return;
        }

        RefreshProgress();
        RefreshObjectiveStatuses();
        progressDirty = false;
    }

    private void MarkProgressDirty()
    {
        progressDirty = true;
    }

    private bool AreCompletionConditionsMet()
    {
        MissionProgressSnapshot snapshot = BuildProgressSnapshot();
        if (HasActiveStageSequence())
        {
            return IsFinalMissionStage() && AreActiveDefinitionObjectivesSatisfied(snapshot, true);
        }

        if (activeObjectiveDefinitions.Count > 0)
        {
            return AreActiveDefinitionObjectivesSatisfied(snapshot, false);
        }

        return AreLegacyCompletionConditionsMet(snapshot);
    }

    private bool AreLegacyCompletionConditionsMet(MissionProgressSnapshot snapshot)
    {
        bool hasAnyObjective = snapshot.TotalTrackedFires > 0 || snapshot.TotalTrackedRescuables > 0 || snapshot.TotalTrackedVictims > 0;
        if (!hasAnyObjective)
            return false;

        bool firesComplete = !requireAllFiresExtinguished || snapshot.TotalTrackedFires == snapshot.ExtinguishedFireCount;
        bool rescuesComplete = !requireAllRescuablesRescued || snapshot.TotalTrackedRescuables == snapshot.RescuedCount;
        bool deathsWithinLimit = maxAllowedVictimDeaths < 0 || snapshot.DeceasedVictimCount <= maxAllowedVictimDeaths;
        bool criticalVictimsResolved = !requireNoCriticalVictimsAtCompletion || snapshot.CriticalVictimCount == 0;
        bool livingVictimsStabilized = !requireAllLivingVictimsStabilized || snapshot.AliveVictimCount == snapshot.StabilizedVictimCount;
        return firesComplete && rescuesComplete && deathsWithinLimit && criticalVictimsResolved && livingVictimsStabilized;
    }

    private static List<T> CollectSceneObjects<T>() where T : Component
    {
        T[] found = FindObjectsByType<T>();
        List<T> results = new List<T>(found.Length);
        for (int i = 0; i < found.Length; i++)
        {
            T candidate = found[i];
            if (candidate != null && candidate.gameObject.scene.IsValid())
                results.Add(candidate);
        }

        return results;
    }

    private static int CountExtinguishedFires(List<Fire> fires)
    {
        int count = 0;
        for (int i = 0; i < fires.Count; i++)
        {
            Fire fire = fires[i];
            if (fire == null || !fire.IsBurning)
                count++;
        }

        return count;
    }

    private static int CountRescuedTargets(List<Rescuable> rescuables)
    {
        int count = 0;
        for (int i = 0; i < rescuables.Count; i++)
        {
            Rescuable rescuable = rescuables[i];
            if (rescuable == null || !rescuable.NeedsRescue)
                count++;
        }

        return count;
    }

    private static int CountVictimsInState(List<VictimCondition> victims, VictimCondition.TriageState state)
    {
        int count = 0;
        for (int i = 0; i < victims.Count; i++)
        {
            VictimCondition victim = victims[i];
            if (victim != null && victim.CurrentTriageState == state)
                count++;
        }

        return count;
    }

    private static int CountLivingVictims(List<VictimCondition> victims)
    {
        int count = 0;
        for (int i = 0; i < victims.Count; i++)
        {
            VictimCondition victim = victims[i];
            if (victim != null && victim.IsAlive)
                count++;
        }

        return count;
    }

    private static int CountStabilizedVictims(List<VictimCondition> victims)
    {
        int count = 0;
        for (int i = 0; i < victims.Count; i++)
        {
            VictimCondition victim = victims[i];
            if (victim != null && victim.IsAlive && victim.IsStabilized)
                count++;
        }

        return count;
    }

    private static int CountExtractedVictims(List<VictimCondition> victims)
    {
        int count = 0;
        for (int i = 0; i < victims.Count; i++)
        {
            VictimCondition victim = victims[i];
            if (victim != null && victim.IsExtracted)
                count++;
        }

        return count;
    }

    private static void RemoveNullEntries<T>(List<T> items) where T : Object
    {
        if (items == null)
            return;

        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] == null)
                items.RemoveAt(i);
        }
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

        return $"Victims: U {urgentVictimCount} | C {criticalVictimCount} | S {stabilizedVictimCount} | X {extractedVictimCount} | D {deceasedVictimCount}\n";
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
        activeObjectiveDefinitions.Clear();
        activeFailConditionDefinitions.Clear();
        activeStageDefinitions.Clear();
        if (missionDefinition == null)
        {
            ClearStageRuntimePresentation();
            return false;
        }

        missionDefinition.CollectStages(activeStageDefinitions);
        missionDefinition.CollectFailConditions(activeFailConditionDefinitions);
        if (activeStageDefinitions.Count > 0)
        {
            currentStageIndex = Mathf.Clamp(currentStageIndex < 0 ? 0 : currentStageIndex, 0, activeStageDefinitions.Count - 1);
            totalStageCount = activeStageDefinitions.Count;

            MissionStageDefinition currentStage = activeStageDefinitions[currentStageIndex];
            currentStageTitle = currentStage != null ? currentStage.StageTitle : string.Empty;
            currentStageDescription = currentStage != null ? currentStage.StageDescription : string.Empty;

            missionDefinition.CollectObjectives(activeObjectiveDefinitions, currentStageIndex);
        }
        else
        {
            ClearStageRuntimePresentation();
            missionDefinition.CollectObjectives(activeObjectiveDefinitions);
            if (activeObjectiveDefinitions.Count == 0)
            {
                return false;
            }
        }

        MissionRuntimeSceneData sceneData = new MissionRuntimeSceneData();
        for (int i = 0; i < activeObjectiveDefinitions.Count; i++)
        {
            activeObjectiveDefinitions[i].CollectTargets(sceneData);
        }

        for (int i = 0; i < activeFailConditionDefinitions.Count; i++)
        {
            MissionFailConditionDefinition failCondition = activeFailConditionDefinitions[i];
            if (failCondition != null)
            {
                failCondition.CollectTargets(sceneData);
            }
        }

        trackedFires = sceneData.CreateFireList();
        trackedRescuables = sceneData.CreateRescuableList();
        trackedVictimConditions = sceneData.CreateVictimConditionList();
        return activeStageDefinitions.Count > 0 || activeObjectiveDefinitions.Count > 0 || activeFailConditionDefinitions.Count > 0;
    }

    private void RefreshLegacyObjectives()
    {
        activeObjectiveDefinitions.Clear();
        activeFailConditionDefinitions.Clear();
        activeStageDefinitions.Clear();
        ClearStageRuntimePresentation();

        if (autoDiscoverFires)
            trackedFires = CollectSceneObjects<Fire>();
        else
            RemoveNullEntries(trackedFires);

        if (autoDiscoverRescuables)
            trackedRescuables = CollectSceneObjects<Rescuable>();
        else
            RemoveNullEntries(trackedRescuables);

        if (autoDiscoverVictimConditions)
            trackedVictimConditions = CollectSceneObjects<VictimCondition>();
        else
            RemoveNullEntries(trackedVictimConditions);
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
        if (activeObjectiveDefinitions.Count == 0)
        {
            return false;
        }

        MissionProgressSnapshot snapshot = BuildProgressSnapshot();
        MissionObjectiveContext context = BuildObjectiveContext(snapshot);
        for (int i = 0; i < activeObjectiveDefinitions.Count; i++)
        {
            MissionObjectiveDefinition objective = activeObjectiveDefinitions[i];
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

    private bool HasFailedConditionOutcome()
    {
        MissionFailConditionContext context = new MissionFailConditionContext(BuildProgressSnapshot(), elapsedTime);

        if (activeFailConditionDefinitions.Count > 0)
        {
            for (int i = 0; i < activeFailConditionDefinitions.Count; i++)
            {
                MissionFailConditionDefinition failCondition = activeFailConditionDefinitions[i];
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

        float activeTimeLimit = ResolveTimeLimitSeconds();
        if (activeTimeLimit > 0f && elapsedTime >= activeTimeLimit)
        {
            return true;
        }

        if (activeObjectiveDefinitions.Count == 0)
        {
            return HasFailedVictimOutcome();
        }

        return false;
    }

    private void RefreshObjectiveStatuses()
    {
        objectiveStatuses.Clear();

        MissionProgressSnapshot snapshot = BuildProgressSnapshot();
        MissionObjectiveContext context = BuildObjectiveContext(snapshot);
        if (activeObjectiveDefinitions.Count > 0)
        {
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

                MissionObjectiveStatus status = new MissionObjectiveStatus();
                status.Set(evaluation);
                objectiveStatuses.Add(status);
            }

            return;
        }

        BuildLegacyObjectiveStatuses(snapshot);
    }

    private void BuildLegacyObjectiveStatuses(MissionProgressSnapshot snapshot)
    {
        if (requireAllFiresExtinguished && snapshot.TotalTrackedFires > 0)
        {
            AddObjectiveStatus(new MissionObjectiveEvaluation(
                "Extinguish Fires",
                $"Extinguish Fires: {snapshot.ExtinguishedFireCount}/{snapshot.TotalTrackedFires}",
                snapshot.ExtinguishedFireCount >= snapshot.TotalTrackedFires,
                false,
                true));
        }

        if (requireAllRescuablesRescued && snapshot.TotalTrackedRescuables > 0)
        {
            AddObjectiveStatus(new MissionObjectiveEvaluation(
                "Rescue Targets",
                $"Rescue Targets: {snapshot.RescuedCount}/{snapshot.TotalTrackedRescuables}",
                snapshot.RescuedCount >= snapshot.TotalTrackedRescuables,
                false,
                true));
        }

        bool usesVictimObjective =
            snapshot.TotalTrackedVictims > 0 &&
            (failOnAnyVictimDeath || maxAllowedVictimDeaths >= 0 || requireNoCriticalVictimsAtCompletion || requireAllLivingVictimsStabilized);

        if (usesVictimObjective)
        {
            bool failedByAnyDeath = failOnAnyVictimDeath && snapshot.DeceasedVictimCount > 0;
            bool failedByDeathLimit = maxAllowedVictimDeaths >= 0 && snapshot.DeceasedVictimCount > maxAllowedVictimDeaths;
            bool criticalResolved = !requireNoCriticalVictimsAtCompletion || snapshot.CriticalVictimCount == 0;
            bool livingVictimsStabilized = !requireAllLivingVictimsStabilized || snapshot.AliveVictimCount == snapshot.StabilizedVictimCount;

            AddObjectiveStatus(new MissionObjectiveEvaluation(
                "Victim Outcome",
                $"Victim Outcome: U {snapshot.UrgentVictimCount} | C {snapshot.CriticalVictimCount} | S {snapshot.StabilizedVictimCount} | X {snapshot.ExtractedVictimCount} | D {snapshot.DeceasedVictimCount}",
                !failedByAnyDeath && !failedByDeathLimit && criticalResolved && livingVictimsStabilized,
                failedByAnyDeath || failedByDeathLimit,
                true));
        }
    }

    private void AddObjectiveStatus(MissionObjectiveEvaluation evaluation)
    {
        MissionObjectiveStatus status = new MissionObjectiveStatus();
        status.Set(evaluation);
        objectiveStatuses.Add(status);
    }

    private string BuildObjectiveOverlayLines()
    {
        if (objectiveStatuses == null || objectiveStatuses.Count == 0)
        {
            return
                $"Fires: {extinguishedFireCount}/{totalTrackedFires}\n" +
                $"Rescues: {rescuedCount}/{totalTrackedRescuables}\n" +
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

            string prefix = status.HasFailed ? "[FAILED]" : status.IsComplete ? "[DONE]" : "[ ]";
            builder.Append(prefix);
            builder.Append(' ');
            builder.Append(status.Summary);
            builder.Append('\n');
        }

        return builder.ToString();
    }

    private string BuildStageOverlayLine()
    {
        if (!HasActiveStageSequence())
        {
            return string.Empty;
        }

        string stageLabel = string.IsNullOrWhiteSpace(currentStageTitle)
            ? $"Stage {currentStageIndex + 1}"
            : $"Stage {currentStageIndex + 1}/{totalStageCount}: {currentStageTitle}";
        return $"{stageLabel}\n";
    }

    private bool TryAdvanceMissionStageIfReady()
    {
        if (!HasActiveStageSequence())
        {
            return false;
        }

        MissionProgressSnapshot snapshot = BuildProgressSnapshot();
        if (!AreActiveDefinitionObjectivesSatisfied(snapshot, true))
        {
            return false;
        }

        InvokeCurrentStageCompleted();

        if (IsFinalMissionStage())
        {
            return false;
        }

        int nextStageIndex = currentStageIndex + 1;
        float nextStageDelaySeconds = ResolveCurrentStageTransitionDelaySeconds();
        if (nextStageDelaySeconds > 0f)
        {
            ScheduleStageTransition(nextStageIndex, nextStageDelaySeconds);
            return true;
        }

        BeginStage(nextStageIndex);
        return true;
    }

    private bool HasActiveStageSequence()
    {
        return activeStageDefinitions != null && activeStageDefinitions.Count > 0;
    }

    private bool IsFinalMissionStage()
    {
        return HasActiveStageSequence() && currentStageIndex >= activeStageDefinitions.Count - 1;
    }

    private float ResolveCurrentStageTransitionDelaySeconds()
    {
        if (!TryGetCurrentStageDefinition(out MissionStageDefinition stage) || stage == null)
        {
            return 0f;
        }

        return stage.NextStageDelaySeconds;
    }

    private void ResetMissionStageRuntime()
    {
        ResetSignalState();
        ClearPendingStageTransition();
        if (missionDefinition != null && missionDefinition.HasStages)
        {
            currentStageIndex = 0;
            return;
        }

        ClearStageRuntimePresentation();
    }

    private void ClearStageRuntimePresentation()
    {
        currentStageIndex = -1;
        totalStageCount = 0;
        currentStageTitle = string.Empty;
        currentStageDescription = string.Empty;
        ClearPendingStageTransition();
        lastStartedStageEventIndex = -1;
        lastCompletedStageEventIndex = -1;
    }

    private void ResetSignalState()
    {
        if (activatedSignalKeys != null)
        {
            activatedSignalKeys.Clear();
        }
    }

    private bool UpdatePendingStageTransition()
    {
        if (!isStageTransitionPending)
        {
            return false;
        }

        if (elapsedTime < pendingStageStartTime)
        {
            return true;
        }

        int nextStageIndex = pendingStageIndex;
        ClearPendingStageTransition();
        BeginStage(nextStageIndex);
        return true;
    }

    private void ScheduleStageTransition(int nextStageIndex, float delaySeconds)
    {
        isStageTransitionPending = true;
        pendingStageIndex = nextStageIndex;
        pendingStageStartTime = elapsedTime + Mathf.Max(0f, delaySeconds);
    }

    private void BeginStage(int stageIndex)
    {
        currentStageIndex = stageIndex;
        RefreshObjectives();
        InvokeCurrentStageStarted();
    }

    private void ClearPendingStageTransition()
    {
        isStageTransitionPending = false;
        pendingStageIndex = -1;
        pendingStageStartTime = -1f;
    }

    private void InvokeCurrentStageStarted()
    {
        if (!HasActiveStageSequence() || currentStageIndex == lastStartedStageEventIndex)
        {
            return;
        }

        string stageId = ResolveCurrentStageId();
        lastStartedStageEventIndex = currentStageIndex;
        onStageStarted?.Invoke(currentStageIndex, stageId);
        ExecuteCurrentStageActions(MissionActionTrigger.StageStarted, stageId);
        InvokeStageBindings(stageId, true);
    }

    private void InvokeCurrentStageCompleted()
    {
        if (!HasActiveStageSequence() || currentStageIndex == lastCompletedStageEventIndex)
        {
            return;
        }

        string stageId = ResolveCurrentStageId();
        lastCompletedStageEventIndex = currentStageIndex;
        onStageCompleted?.Invoke(currentStageIndex, stageId);
        ExecuteCurrentStageActions(MissionActionTrigger.StageCompleted, stageId);
        InvokeStageBindings(stageId, false);
    }

    private void ExecuteCurrentStageActions(MissionActionTrigger trigger, string stageId)
    {
        if (!TryGetCurrentStageDefinition(out MissionStageDefinition stage))
        {
            return;
        }

        MissionActionExecutionContext context = new MissionActionExecutionContext(
            this,
            missionDefinition,
            stage,
            currentStageIndex,
            stageId,
            trigger);

        stage.ExecuteActions(context);
    }

    private void InvokeStageBindings(string stageId, bool started)
    {
        if (stageActionBindings == null || string.IsNullOrWhiteSpace(stageId))
        {
            return;
        }

        for (int i = 0; i < stageActionBindings.Count; i++)
        {
            MissionStageActionBinding binding = stageActionBindings[i];
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

    private string ResolveCurrentStageId()
    {
        if (!HasActiveStageSequence() || currentStageIndex < 0 || currentStageIndex >= activeStageDefinitions.Count)
        {
            return string.Empty;
        }

        MissionStageDefinition stage = activeStageDefinitions[currentStageIndex];
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

        return $"stage-{currentStageIndex + 1}";
    }

    private bool TryGetCurrentStageDefinition(out MissionStageDefinition stage)
    {
        stage = null;
        if (!HasActiveStageSequence() || currentStageIndex < 0 || currentStageIndex >= activeStageDefinitions.Count)
        {
            return false;
        }

        stage = activeStageDefinitions[currentStageIndex];
        return stage != null;
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
}
