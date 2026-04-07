using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MissionHudPresenter : MonoBehaviour
{
    private const float MissingMissionHideGraceSeconds = 0.5f;

    private enum ObjectiveVisualState
    {
        Pending = 0,
        Active = 1,
        Completed = 2,
        Failed = 3
    }

    [System.Serializable]
    private sealed class ObjectiveItemView
    {
        public GameObject Root;
        public RawImage Icon;
        public TMP_Text Label;

        public bool IsValid => Root != null && Label != null;

        public void SetActive(bool active)
        {
            if (Root != null && Root.activeSelf != active)
            {
                Root.SetActive(active);
            }
        }
    }

    private enum ToastType
    {
        Done = 0,
        Update = 1
    }

    private readonly struct ToastRequest
    {
        public ToastRequest(ToastType type, string title, string body, float durationSeconds)
        {
            Type = type;
            Title = title;
            Body = body;
            DurationSeconds = Mathf.Max(0.01f, durationSeconds);
        }

        public ToastType Type { get; }
        public string Title { get; }
        public string Body { get; }
        public float DurationSeconds { get; }
    }

    [Header("References")]
    [SerializeField] private IncidentMissionSystem missionSystem;
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private TMP_Text missionTitleText;
    [SerializeField] private TMP_Text missionDescriptionText;
    [SerializeField] private TMP_Text stageText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text objectivesText;

    [Header("New HUD")]
    [SerializeField] private TMP_Text missionHeaderText;
    [SerializeField] private TMP_Text missionTimerText;
    [SerializeField] private TMP_Text missionSummaryText;
    [SerializeField] private TMP_Text stageCounterText;
    [SerializeField] private TMP_Text stageHeadlineText;
    [SerializeField] private Image stageProgressFillImage;
    [SerializeField] private TMP_Text stageProgressText;
    [SerializeField] private TMP_Text objectivesHeaderText;
    [SerializeField] private Transform objectivesListRoot;
    [SerializeField] private GameObject objectiveItemPrefab;
    [SerializeField] private Texture pendingObjectiveIcon;
    [SerializeField] private Texture activeObjectiveIcon;
    [SerializeField] private Texture completedObjectiveIcon;
    [SerializeField] private Texture failedObjectiveIcon;

    [Header("Toasts")]
    [SerializeField] private GameObject objectiveDoneToastRoot;
    [SerializeField] private TMP_Text objectiveDoneToastText;
    [SerializeField] private GameObject objectiveUpdateToastRoot;
    [SerializeField] private TMP_Text objectiveUpdateToastTitleText;
    [SerializeField] private TMP_Text objectiveUpdateToastBodyText;

    [Header("Display")]
    [SerializeField] private bool hideWhenMissionMissing = true;
    [SerializeField] private bool hideWhenMissionIdle = false;
    [SerializeField] private bool showMissionDescription = false;
    [SerializeField] private bool showCompletedObjectives = true;
    [SerializeField] private bool showObjectiveScores = true;
    [SerializeField] private string pendingPrefix = "[ ]";
    [SerializeField] private string completedPrefix = "[DONE]";
    [SerializeField] private string failedPrefix = "[FAILED]";
    [SerializeField] private string timerRemainingFormat = "{0:F1}s left";
    [SerializeField] private string timerElapsedFormat = "{0:F1}s elapsed";
    [SerializeField] private string scoreFormat = "Score: {0}/{1}{2}";

    [Header("New HUD Display")]
    [SerializeField] private string stageCounterFormat = "Stage {0} / {1}";
    [SerializeField] private string objectivesHeaderFormat = "OBJECTIVES - {0}";
    [SerializeField] private string progressPercentFormat = "{0:0}%";
    [SerializeField] private string progressWithScoreFormat = "{0:0}% | {1}";
    [SerializeField] private Color pendingObjectiveColor = new Color(1f, 1f, 1f, 0.65f);
    [SerializeField] private Color activeObjectiveColor = Color.white;
    [SerializeField] private Color completedObjectiveColor = new Color(0.59f, 1f, 0.77f, 0.9f);
    [SerializeField] private Color failedObjectiveColor = new Color(1f, 0.62f, 0.62f, 0.95f);

    [Header("Toast Display")]
    [SerializeField] private bool showInitialObjectiveUpdateToast = true;
    [SerializeField] private float objectiveDoneToastDuration = 1.1f;
    [SerializeField] private float objectiveUpdateToastDuration = 1.45f;
    [SerializeField] private float toastGapSeconds = 0.15f;
    [SerializeField] private string objectiveDoneToastFormat = "{0} done";
    [SerializeField] private string objectiveUpdateToastTitle = "OBJECTIVE UPDATED";

    private readonly StringBuilder objectiveBuilder = new StringBuilder();
    private readonly List<ObjectiveItemView> objectiveItemViews = new List<ObjectiveItemView>();
    private readonly Queue<ToastRequest> pendingToastRequests = new Queue<ToastRequest>();

    private bool objectiveViewsInitialized;
    private bool toastStateInitialized;
    private int observedStageIndex = -1;
    private int lastDoneToastStageIndex = -1;
    private IncidentMissionSystem.MissionState observedMissionState = IncidentMissionSystem.MissionState.Idle;
    private string observedStageTitle = string.Empty;
    private bool observedStageTransitionPending;
    private GameObject activeToastRoot;
    private float activeToastHideTime;
    private float nextToastAvailableTime;
    private float lastMissionSystemSeenTime = float.NegativeInfinity;
    private bool hasAppliedVisibility;
    private bool lastAppliedVisibility;

    private void Awake()
    {
        ResolveReferences();
        HideAllToasts();
        RefreshView();
    }

    private void OnEnable()
    {
        hasAppliedVisibility = false;
        ResetToastTracking();
        HideAllToasts();
        RefreshView();
    }

    private void Update()
    {
        RefreshView();
        RefreshToastFlow();
    }

    private void ResolveReferences()
    {
        if (missionSystem == null)
        {
            missionSystem = FindAnyObjectByType<IncidentMissionSystem>(FindObjectsInactive.Exclude);
        }

        if (rootCanvasGroup == null)
        {
            rootCanvasGroup = GetComponent<CanvasGroup>();
        }

        if (missionSystem != null)
        {
            lastMissionSystemSeenTime = Time.unscaledTime;
        }
    }

    private void RefreshView()
    {
        ResolveReferences();
        bool visible = ShouldBeVisible();
        ApplyVisibility(visible);
        if (!visible || missionSystem == null)
        {
            return;
        }

        string missionTitle = missionSystem.MissionTitle;
        string missionSummary = BuildMissionSummaryText();
        string legacyStageText = BuildStageText();
        string compactTimerText = BuildCompactTimerText();
        string objectivesListText = BuildObjectivesText();

        SetText(missionTitleText, missionTitle);
        SetText(missionDescriptionText, missionSummary);
        SetText(stateText, MissionLocalization.Format("mission.hud.state", "State: {0}", LocalizeMissionState(missionSystem.State)));
        SetText(stageText, legacyStageText);
        SetText(timerText, BuildTimerText());
        SetText(scoreText, BuildScoreText());
        SetText(objectivesText, objectivesListText);

        SetText(missionHeaderText, missionTitle);
        SetText(missionTimerText, compactTimerText);
        SetText(missionSummaryText, missionSummary);
        SetText(stageCounterText, BuildStageCounterText());
        SetText(stageHeadlineText, BuildStageHeadlineText());
        SetText(stageProgressText, BuildProgressText());
        SetText(objectivesHeaderText, BuildObjectivesHeaderText());
        SetFillAmount(stageProgressFillImage, BuildProgressNormalized());
        RefreshObjectiveItemList();
    }

    private bool ShouldBeVisible()
    {
        if (missionSystem == null)
        {
            if (!hideWhenMissionMissing)
            {
                return true;
            }

            return Time.unscaledTime - lastMissionSystemSeenTime <= MissingMissionHideGraceSeconds;
        }

        if (hideWhenMissionIdle && missionSystem.State == IncidentMissionSystem.MissionState.Idle)
        {
            return false;
        }

        return true;
    }

    private void ApplyVisibility(bool visible)
    {
        if (rootCanvasGroup == null)
        {
            return;
        }

        if (hasAppliedVisibility && lastAppliedVisibility == visible)
        {
            return;
        }

        rootCanvasGroup.alpha = visible ? 1f : 0f;
        rootCanvasGroup.interactable = false;
        rootCanvasGroup.blocksRaycasts = false;
        hasAppliedVisibility = true;
        lastAppliedVisibility = visible;
    }

    private string BuildStageText()
    {
        if (missionSystem == null || !missionSystem.HasActiveStage)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(missionSystem.CurrentStageTitle))
        {
            return MissionLocalization.Format(
                "mission.hud.stage.short",
                "Stage {0}/{1}",
                missionSystem.CurrentStageIndex + 1,
                missionSystem.TotalStageCount);
        }

        return MissionLocalization.Format(
            "mission.hud.stage.full",
            "Stage {0}/{1}: {2}",
            missionSystem.CurrentStageIndex + 1,
            missionSystem.TotalStageCount,
            missionSystem.CurrentStageTitle);
    }

    private string BuildTimerText()
    {
        if (missionSystem == null)
        {
            return string.Empty;
        }

        if (missionSystem.TimeLimitSeconds > 0f)
        {
            return MissionLocalization.Format("mission.hud.timer.remaining", timerRemainingFormat, missionSystem.RemainingTimeSeconds);
        }

        return MissionLocalization.Format("mission.hud.timer.elapsed", timerElapsedFormat, missionSystem.ElapsedTime);
    }

    private string BuildCompactTimerText()
    {
        if (missionSystem == null)
        {
            return string.Empty;
        }

        float seconds = missionSystem.TimeLimitSeconds > 0f
            ? missionSystem.RemainingTimeSeconds
            : missionSystem.ElapsedTime;
        return FormatClock(seconds);
    }

    private string BuildObjectivesText()
    {
        if (missionSystem == null)
        {
            return string.Empty;
        }

        objectiveBuilder.Clear();
        bool wroteAnyLine = false;
        for (int i = 0; i < missionSystem.ObjectiveStatusCount; i++)
        {
            if (!missionSystem.TryGetObjectiveStatus(i, out MissionObjectiveStatusSnapshot status))
            {
                continue;
            }

            if (!showCompletedObjectives && status.IsComplete && !status.HasFailed)
            {
                continue;
            }

            if (wroteAnyLine)
            {
                objectiveBuilder.Append('\n');
            }

            string prefix = status.HasFailed ? GetFailedPrefix() : status.IsComplete ? GetCompletedPrefix() : GetPendingPrefix();
            objectiveBuilder.Append(prefix);
            objectiveBuilder.Append(' ');
            objectiveBuilder.Append(status.Summary);
            if (showObjectiveScores && status.MaxScore > 0)
            {
                objectiveBuilder.Append(" (");
                objectiveBuilder.Append(status.Score);
                objectiveBuilder.Append('/');
                objectiveBuilder.Append(status.MaxScore);
                objectiveBuilder.Append(')');
            }
            wroteAnyLine = true;
        }

        return objectiveBuilder.ToString();
    }

    private string BuildScoreText()
    {
        if (missionSystem == null || missionSystem.DisplayedMaximumScore <= 0)
        {
            return string.Empty;
        }

        string rankSuffix = string.IsNullOrWhiteSpace(missionSystem.DisplayedScoreRank)
            ? string.Empty
            : $" [{missionSystem.DisplayedScoreRank}]";
        return MissionLocalization.Format(
            "mission.hud.score",
            scoreFormat,
            missionSystem.DisplayedScore,
            missionSystem.DisplayedMaximumScore,
            rankSuffix);
    }

    private string BuildMissionSummaryText()
    {
        if (missionSystem == null)
        {
            return string.Empty;
        }

        if (showMissionDescription && !string.IsNullOrWhiteSpace(missionSystem.MissionDescription))
        {
            return missionSystem.MissionDescription;
        }

        if (!string.IsNullOrWhiteSpace(missionSystem.CurrentStageDescription))
        {
            return missionSystem.CurrentStageDescription;
        }

        return string.Empty;
    }

    private string BuildStageCounterText()
    {
        if (missionSystem == null || !missionSystem.HasActiveStage || missionSystem.TotalStageCount <= 0)
        {
            return string.Empty;
        }

        int stageNumber = missionSystem.State == IncidentMissionSystem.MissionState.Completed
            ? missionSystem.TotalStageCount
            : Mathf.Clamp(missionSystem.CurrentStageIndex + 1, 1, missionSystem.TotalStageCount);
        return MissionLocalization.Format(
            "mission.hud.stage.counter",
            stageCounterFormat,
            stageNumber,
            missionSystem.TotalStageCount);
    }

    private string BuildStageHeadlineText()
    {
        if (missionSystem == null)
        {
            return string.Empty;
        }

        if (missionSystem.HasActiveStage)
        {
            if (!string.IsNullOrWhiteSpace(missionSystem.CurrentStageTitle))
            {
                return missionSystem.CurrentStageTitle;
            }

            if (!string.IsNullOrWhiteSpace(missionSystem.CurrentStageDescription))
            {
                return missionSystem.CurrentStageDescription;
            }
        }

        return missionSystem.State switch
        {
            IncidentMissionSystem.MissionState.Completed => MissionLocalization.Get("mission.hud.state.completed_headline", "Mission Complete"),
            IncidentMissionSystem.MissionState.Failed => MissionLocalization.Get("mission.hud.state.failed_headline", "Mission Failed"),
            _ => string.Empty
        };
    }

    private string BuildObjectivesHeaderText()
    {
        if (missionSystem == null)
        {
            return MissionLocalization.Get("mission.hud.objectives.header.default", "OBJECTIVES");
        }

        if (string.IsNullOrWhiteSpace(objectivesHeaderFormat))
        {
            return MissionLocalization.Get("mission.hud.objectives.header.default", "OBJECTIVES");
        }

        return MissionLocalization.Format(
            "mission.hud.objectives.header.with_state",
            objectivesHeaderFormat,
            LocalizeMissionState(missionSystem.State).ToUpperInvariant());
    }

    private float BuildProgressNormalized()
    {
        if (missionSystem == null)
        {
            return 0f;
        }

        if (missionSystem.State == IncidentMissionSystem.MissionState.Completed)
        {
            return 1f;
        }

        if (missionSystem.HasActiveStage && missionSystem.TotalStageCount > 0)
        {
            int stageNumber = Mathf.Clamp(missionSystem.CurrentStageIndex + 1, 0, missionSystem.TotalStageCount);
            return Mathf.Clamp01(stageNumber / (float)missionSystem.TotalStageCount);
        }

        if (missionSystem.ObjectiveStatusCount <= 0)
        {
            return 0f;
        }

        int completedCount = 0;
        for (int i = 0; i < missionSystem.ObjectiveStatusCount; i++)
        {
            if (missionSystem.TryGetObjectiveStatus(i, out MissionObjectiveStatusSnapshot status) &&
                status.IsComplete &&
                !status.HasFailed)
            {
                completedCount++;
            }
        }

        return Mathf.Clamp01(completedCount / (float)missionSystem.ObjectiveStatusCount);
    }

    private string BuildProgressText()
    {
        float percent = BuildProgressNormalized() * 100f;
        string progressText = MissionLocalization.Format("mission.hud.progress.percent", progressPercentFormat, percent);
        string scoreValue = BuildScoreValueText();
        if (string.IsNullOrWhiteSpace(scoreValue))
        {
            return progressText;
        }

        return MissionLocalization.Format("mission.hud.progress.with_score", progressWithScoreFormat, percent, scoreValue);
    }

    private string BuildScoreValueText()
    {
        if (missionSystem == null || missionSystem.DisplayedMaximumScore <= 0)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(missionSystem.DisplayedScoreRank))
        {
            return $"{missionSystem.DisplayedScore}/{missionSystem.DisplayedMaximumScore}";
        }

        return $"{missionSystem.DisplayedScore}/{missionSystem.DisplayedMaximumScore} [{missionSystem.DisplayedScoreRank}]";
    }

    private void RefreshToastFlow()
    {
        ResolveReferences();
        if (missionSystem == null)
        {
            ResetToastTracking();
            HideAllToasts();
            return;
        }

        int currentStageIndex = missionSystem.HasActiveStage ? missionSystem.CurrentStageIndex : -1;
        string currentStageTitle = missionSystem.HasActiveStage ? missionSystem.CurrentStageTitle : string.Empty;
        bool currentStageTransitionPending = missionSystem.IsStageTransitionPending;

        if (!toastStateInitialized)
        {
            observedMissionState = missionSystem.State;
            observedStageIndex = currentStageIndex;
            observedStageTitle = currentStageTitle;
            observedStageTransitionPending = currentStageTransitionPending;
            toastStateInitialized = true;

            if (showInitialObjectiveUpdateToast &&
                missionSystem.State == IncidentMissionSystem.MissionState.Running &&
                missionSystem.HasActiveStage)
            {
                ClearPendingToasts();
                QueueObjectiveUpdateToast();
            }

            UpdateToastPlayback();
            return;
        }

        if (missionSystem.State != observedMissionState)
        {
            HandleMissionStateChanged(observedMissionState, missionSystem.State);
        }
        else if (missionSystem.State == IncidentMissionSystem.MissionState.Running &&
                 currentStageTransitionPending &&
                 !observedStageTransitionPending)
        {
            TryQueueObjectiveDoneToast(observedStageIndex, observedStageTitle);
        }
        else if (missionSystem.State == IncidentMissionSystem.MissionState.Running && currentStageIndex != observedStageIndex)
        {
            HandleStageChanged(currentStageIndex);
        }

        observedMissionState = missionSystem.State;
        observedStageIndex = currentStageIndex;
        observedStageTitle = currentStageTitle;
        observedStageTransitionPending = currentStageTransitionPending;

        UpdateToastPlayback();
    }

    private void HandleMissionStateChanged(IncidentMissionSystem.MissionState previousState, IncidentMissionSystem.MissionState currentState)
    {
        if (currentState == IncidentMissionSystem.MissionState.Running)
        {
            ClearPendingToasts();
            HideAllToasts();
            lastDoneToastStageIndex = -1;
            if (missionSystem.HasActiveStage)
            {
                QueueObjectiveUpdateToast();
            }
            return;
        }

        if (previousState == IncidentMissionSystem.MissionState.Running &&
            currentState == IncidentMissionSystem.MissionState.Completed)
        {
            TryQueueObjectiveDoneToast(observedStageIndex, observedStageTitle);
            return;
        }

        if (currentState == IncidentMissionSystem.MissionState.Failed ||
            currentState == IncidentMissionSystem.MissionState.Idle)
        {
            ClearPendingToasts();
            HideAllToasts();
        }
    }

    private void HandleStageChanged(int currentStageIndex)
    {
        TryQueueObjectiveDoneToast(observedStageIndex, observedStageTitle);

        if (currentStageIndex >= 0)
        {
            QueueObjectiveUpdateToast();
        }
    }

    private void TryQueueObjectiveDoneToast(int stageIndex, string stageTitle)
    {
        if (stageIndex < 0 || stageIndex == lastDoneToastStageIndex)
        {
            return;
        }

        lastDoneToastStageIndex = stageIndex;
        string resolvedStageTitle = string.IsNullOrWhiteSpace(stageTitle)
            ? MissionLocalization.Format("mission.hud.stage.single", "Stage {0}", Mathf.Max(1, stageIndex + 1))
            : stageTitle.Trim();
        pendingToastRequests.Enqueue(new ToastRequest(
            ToastType.Done,
            string.Empty,
            MissionLocalization.Format("mission.hud.toast.objective_done", objectiveDoneToastFormat, resolvedStageTitle),
            objectiveDoneToastDuration));
    }

    private void QueueObjectiveUpdateToast()
    {
        pendingToastRequests.Enqueue(new ToastRequest(
            ToastType.Update,
            MissionLocalization.Get("mission.hud.toast.objective_updated", objectiveUpdateToastTitle),
            BuildObjectiveUpdateToastBodyText(),
            objectiveUpdateToastDuration));
    }

    private string GetPendingPrefix()
    {
        return MissionLocalization.Get("mission.hud.prefix.pending", pendingPrefix);
    }

    private string GetCompletedPrefix()
    {
        return MissionLocalization.Get("mission.hud.prefix.completed", completedPrefix);
    }

    private string GetFailedPrefix()
    {
        return MissionLocalization.Get("mission.hud.prefix.failed", failedPrefix);
    }

    private static string LocalizeMissionState(IncidentMissionSystem.MissionState state)
    {
        return state switch
        {
            IncidentMissionSystem.MissionState.Idle => MissionLocalization.Get("mission.state.idle", "Idle"),
            IncidentMissionSystem.MissionState.Running => MissionLocalization.Get("mission.state.running", "Running"),
            IncidentMissionSystem.MissionState.Completed => MissionLocalization.Get("mission.state.completed", "Completed"),
            IncidentMissionSystem.MissionState.Failed => MissionLocalization.Get("mission.state.failed", "Failed"),
            _ => state.ToString()
        };
    }

    private string BuildObjectiveUpdateToastBodyText()
    {
        if (missionSystem == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < missionSystem.ObjectiveStatusCount; i++)
        {
            if (!missionSystem.TryGetObjectiveStatus(i, out MissionObjectiveStatusSnapshot status))
            {
                continue;
            }

            if (!status.IsComplete && !status.HasFailed)
            {
                return !string.IsNullOrWhiteSpace(status.Summary) ? status.Summary : status.Title;
            }
        }

        if (!string.IsNullOrWhiteSpace(missionSystem.CurrentStageTitle))
        {
            return missionSystem.CurrentStageTitle;
        }

        if (!string.IsNullOrWhiteSpace(missionSystem.CurrentStageDescription))
        {
            return missionSystem.CurrentStageDescription;
        }

        return missionSystem.MissionTitle;
    }

    private void UpdateToastPlayback()
    {
        float now = Time.unscaledTime;
        if (activeToastRoot != null && now >= activeToastHideTime)
        {
            activeToastRoot.SetActive(false);
            activeToastRoot = null;
            nextToastAvailableTime = now + Mathf.Max(0f, toastGapSeconds);
        }

        if (activeToastRoot != null || pendingToastRequests.Count <= 0 || now < nextToastAvailableTime)
        {
            return;
        }

        ToastRequest request = pendingToastRequests.Dequeue();
        ShowToast(request);
        activeToastHideTime = now + request.DurationSeconds;
    }

    private void ShowToast(ToastRequest request)
    {
        HideAllToasts();

        switch (request.Type)
        {
            case ToastType.Done:
                if (objectiveDoneToastRoot == null)
                {
                    return;
                }

                SetText(objectiveDoneToastText, request.Body);
                objectiveDoneToastRoot.SetActive(true);
                RefreshToastLayout(objectiveDoneToastRoot);
                activeToastRoot = objectiveDoneToastRoot;
                return;

            case ToastType.Update:
                if (objectiveUpdateToastRoot == null)
                {
                    return;
                }

                SetText(objectiveUpdateToastTitleText, request.Title);
                SetText(objectiveUpdateToastBodyText, request.Body);
                objectiveUpdateToastRoot.SetActive(true);
                RefreshToastLayout(objectiveUpdateToastRoot);
                activeToastRoot = objectiveUpdateToastRoot;
                return;
        }
    }

    private void HideAllToasts()
    {
        if (objectiveDoneToastRoot != null && objectiveDoneToastRoot.activeSelf)
        {
            objectiveDoneToastRoot.SetActive(false);
        }

        if (objectiveUpdateToastRoot != null && objectiveUpdateToastRoot.activeSelf)
        {
            objectiveUpdateToastRoot.SetActive(false);
        }

        activeToastRoot = null;
        activeToastHideTime = 0f;
        nextToastAvailableTime = 0f;
    }

    private void ClearPendingToasts()
    {
        pendingToastRequests.Clear();
    }

    private void ResetToastTracking()
    {
        toastStateInitialized = false;
        observedStageIndex = -1;
        lastDoneToastStageIndex = -1;
        observedMissionState = IncidentMissionSystem.MissionState.Idle;
        observedStageTitle = string.Empty;
        observedStageTransitionPending = false;
        ClearPendingToasts();
    }

    private static void RefreshToastLayout(GameObject toastRoot)
    {
        if (toastRoot == null)
        {
            return;
        }

        RectTransform rootRect = toastRoot.transform as RectTransform;
        if (rootRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        RebuildLayoutRecursive(rootRect);
        LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
        Canvas.ForceUpdateCanvases();
    }

    private static void RebuildLayoutRecursive(RectTransform rectTransform)
    {
        for (int i = 0; i < rectTransform.childCount; i++)
        {
            if (rectTransform.GetChild(i) is RectTransform childRect)
            {
                RebuildLayoutRecursive(childRect);
                LayoutRebuilder.ForceRebuildLayoutImmediate(childRect);
            }
        }
    }

    private void RefreshObjectiveItemList()
    {
        if (objectivesListRoot == null)
        {
            return;
        }

        EnsureObjectiveItemViewsInitialized();

        int visibleCount = 0;
        bool assignedActiveObjective = false;
        for (int i = 0; i < missionSystem.ObjectiveStatusCount; i++)
        {
            if (!missionSystem.TryGetObjectiveStatus(i, out MissionObjectiveStatusSnapshot status))
            {
                continue;
            }

            if (!showCompletedObjectives && status.IsComplete && !status.HasFailed)
            {
                continue;
            }

            EnsureObjectiveItemCapacity(visibleCount + 1);
            if (visibleCount >= objectiveItemViews.Count)
            {
                break;
            }

            ObjectiveVisualState visualState = ResolveObjectiveVisualState(status, ref assignedActiveObjective);
            BindObjectiveItemView(objectiveItemViews[visibleCount], status, visualState);
            visibleCount++;
        }

        for (int i = visibleCount; i < objectiveItemViews.Count; i++)
        {
            objectiveItemViews[i].SetActive(false);
        }
    }

    private ObjectiveVisualState ResolveObjectiveVisualState(MissionObjectiveStatusSnapshot status, ref bool assignedActiveObjective)
    {
        if (status.HasFailed)
        {
            return ObjectiveVisualState.Failed;
        }

        if (status.IsComplete)
        {
            return ObjectiveVisualState.Completed;
        }

        if (!assignedActiveObjective)
        {
            assignedActiveObjective = true;
            return ObjectiveVisualState.Active;
        }

        return ObjectiveVisualState.Pending;
    }

    private void BindObjectiveItemView(ObjectiveItemView view, MissionObjectiveStatusSnapshot status, ObjectiveVisualState visualState)
    {
        if (view == null || !view.IsValid)
        {
            return;
        }

        view.SetActive(true);
        SetText(view.Label, BuildObjectiveItemText(status));

        Color targetColor = GetObjectiveColor(visualState);
        view.Label.color = targetColor;
        if (view.Icon != null)
        {
            Texture targetIcon = GetObjectiveIcon(visualState);
            if (targetIcon != null)
            {
                view.Icon.texture = targetIcon;
            }

            view.Icon.color = targetColor;
        }
    }

    private string BuildObjectiveItemText(MissionObjectiveStatusSnapshot status)
    {
        string summary = !string.IsNullOrWhiteSpace(status.Summary)
            ? status.Summary
            : status.Title;

        if (!showObjectiveScores || status.MaxScore <= 0)
        {
            return summary;
        }

        return $"{summary} ({status.Score}/{status.MaxScore})";
    }

    private void EnsureObjectiveItemViewsInitialized()
    {
        if (objectiveViewsInitialized)
        {
            return;
        }

        objectiveItemViews.Clear();
        for (int i = 0; i < objectivesListRoot.childCount; i++)
        {
            RegisterObjectiveItemView(objectivesListRoot.GetChild(i).gameObject);
        }

        objectiveViewsInitialized = true;
    }

    private void EnsureObjectiveItemCapacity(int count)
    {
        EnsureObjectiveItemViewsInitialized();
        while (objectiveItemViews.Count < count && objectiveItemPrefab != null && objectivesListRoot != null)
        {
            GameObject instance = Instantiate(objectiveItemPrefab, objectivesListRoot, false);
            RegisterObjectiveItemView(instance);
        }
    }

    private void RegisterObjectiveItemView(GameObject itemRoot)
    {
        if (itemRoot == null)
        {
            return;
        }

        ObjectiveItemView view = new ObjectiveItemView
        {
            Root = itemRoot,
            Icon = itemRoot.GetComponentInChildren<RawImage>(true),
            Label = itemRoot.GetComponentInChildren<TMP_Text>(true)
        };

        if (view.IsValid)
        {
            objectiveItemViews.Add(view);
        }
    }

    private Texture GetObjectiveIcon(ObjectiveVisualState visualState)
    {
        return visualState switch
        {
            ObjectiveVisualState.Active => activeObjectiveIcon != null ? activeObjectiveIcon : pendingObjectiveIcon,
            ObjectiveVisualState.Completed => completedObjectiveIcon != null ? completedObjectiveIcon : pendingObjectiveIcon,
            ObjectiveVisualState.Failed => failedObjectiveIcon != null ? failedObjectiveIcon : pendingObjectiveIcon,
            _ => pendingObjectiveIcon
        };
    }

    private Color GetObjectiveColor(ObjectiveVisualState visualState)
    {
        return visualState switch
        {
            ObjectiveVisualState.Active => activeObjectiveColor,
            ObjectiveVisualState.Completed => completedObjectiveColor,
            ObjectiveVisualState.Failed => failedObjectiveColor,
            _ => pendingObjectiveColor
        };
    }

    private static string FormatClock(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int secs = totalSeconds % 60;
        return hours > 0
            ? $"{hours:00}:{minutes:00}:{secs:00}"
            : $"{minutes:00}:{secs:00}";
    }

    private static void SetFillAmount(Image fillImage, float value)
    {
        if (fillImage == null)
        {
            return;
        }

        fillImage.fillAmount = Mathf.Clamp01(value);
    }

    private static void SetText(TMP_Text textComponent, string value)
    {
        if (textComponent == null)
        {
            return;
        }

        textComponent.text = value ?? string.Empty;
    }
}
