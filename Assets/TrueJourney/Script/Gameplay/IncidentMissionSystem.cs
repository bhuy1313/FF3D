using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class IncidentMissionSystem : MonoBehaviour
{
    public enum MissionState
    {
        Idle = 0,
        Running = 1,
        Completed = 2,
        Failed = 3
    }

    [Header("Mission")]
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

    public string MissionId => missionId;
    public string MissionTitle => missionTitle;
    public string MissionDescription => missionDescription;
    public MissionState State => missionState;
    public float ElapsedTime => elapsedTime;
    public float TimeLimitSeconds => timeLimitSeconds;
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

    private GUIStyle overlayGuiStyle;

    private void OnEnable()
    {
        RefreshObjectives();

        if (autoStartOnEnable)
            StartMission();
    }

    private void Update()
    {
        if (missionState != MissionState.Running)
            return;

        elapsedTime += Time.deltaTime;
        RefreshProgress();

        if (HasFailedVictimOutcome())
        {
            FailMission();
            return;
        }

        if (timeLimitSeconds > 0f && elapsedTime >= timeLimitSeconds)
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

        RefreshProgress();
    }

    [ContextMenu("Start Mission")]
    public void StartMission()
    {
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

        missionState = MissionState.Failed;
        onMissionFailed?.Invoke();
    }

    [ContextMenu("Complete Mission")]
    public void CompleteMission()
    {
        if (missionState == MissionState.Completed)
            return;

        RefreshProgress();
        missionState = MissionState.Completed;
        onMissionCompleted?.Invoke();
    }

    public bool IsObjectiveComplete()
    {
        RefreshProgress();
        return AreCompletionConditionsMet();
    }

    private void OnGUI()
    {
        if (!showMissionOverlay)
            return;

        EnsureOverlayGuiStyle();

        string timerText = timeLimitSeconds > 0f
            ? $"{Mathf.Max(0f, timeLimitSeconds - elapsedTime):F1}s left"
            : $"{elapsedTime:F1}s elapsed";
        string overlayText =
            $"{missionTitle}\n" +
            $"State: {missionState}\n" +
            $"Fires: {extinguishedFireCount}/{totalTrackedFires}\n" +
            $"Rescues: {rescuedCount}/{totalTrackedRescuables}\n" +
            BuildVictimOverlayLine() +
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

    private bool AreCompletionConditionsMet()
    {
        bool hasAnyObjective = totalTrackedFires > 0 || totalTrackedRescuables > 0 || totalTrackedVictims > 0;
        if (!hasAnyObjective)
            return false;

        bool firesComplete = !requireAllFiresExtinguished || totalTrackedFires == extinguishedFireCount;
        bool rescuesComplete = !requireAllRescuablesRescued || totalTrackedRescuables == rescuedCount;
        bool deathsWithinLimit = maxAllowedVictimDeaths < 0 || deceasedVictimCount <= maxAllowedVictimDeaths;
        bool criticalVictimsResolved = !requireNoCriticalVictimsAtCompletion || criticalVictimCount == 0;
        bool livingVictimsStabilized = !requireAllLivingVictimsStabilized || aliveVictimCount == stabilizedVictimCount;
        return firesComplete && rescuesComplete && deathsWithinLimit && criticalVictimsResolved && livingVictimsStabilized;
    }

    private static List<T> CollectSceneObjects<T>() where T : Component
    {
        T[] found = FindObjectsByType<T>(FindObjectsSortMode.None);
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
        if (failOnAnyVictimDeath && deceasedVictimCount > 0)
            return true;

        return maxAllowedVictimDeaths >= 0 && deceasedVictimCount > maxAllowedVictimDeaths;
    }
}
