using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MissionEndOverlayController : MonoBehaviour
{
    private static readonly string[] ObjectiveRowNames =
    {
        "OpenGateObjectiveRow",
        "ContainFireObjectiveRow",
        "BreakBarricadeObjectiveRow",
        "RescueVictimObjectiveRow",
        "ReachExitObjectiveRow",
    };

    private static readonly string[] ObjectiveTextNames =
    {
        "OpenGateObjectiveText",
        "ContainFireObjectiveText",
        "BreakBarricadeObjectiveText",
        "RescueVictimObjectiveText",
        "ReachExitObjectiveText",
    };

    private static readonly Color ObjectivePendingColor = new Color(0.92f, 0.95f, 1f, 1f);
    private static readonly Color ObjectiveCompletedColor = new Color(0.33f, 0.86f, 0.56f, 1f);
    private static readonly Color ObjectiveFailedColor = new Color(0.94f, 0.34f, 0.31f, 1f);
    private static readonly Color ObjectiveCompletedTextColor = new Color(0.84f, 0.97f, 0.88f, 1f);
    private static readonly Color ObjectiveFailedTextColor = new Color(1f, 0.86f, 0.86f, 1f);

    [Header("References")]
    [SerializeField]
    private IncidentMissionSystem missionSystem;

    [SerializeField]
    private Canvas targetCanvas;

    [Header("Behavior")]
    [SerializeField]
    private bool pauseGameplayOnMissionEnd = true;

    [SerializeField]
    private bool unlockCursorOnMissionEnd = true;

    [SerializeField]
    private bool showRetryButton = true;

    [SerializeField]
    private bool showMainMenuButton = true;

    [SerializeField]
    private string mainMenuSceneName = "MainMenu";

    [Header("Labels")]
    [SerializeField]
    private string resultLabel = "RESULT";

    [SerializeField]
    private string completedTitle = "Mission Complete";

    [SerializeField]
    private string failedTitle = "Mission Failed";

    [SerializeField]
    private string retryButtonLabel = "Retry";

    [SerializeField]
    private string mainMenuButtonLabel = "Main Menu";

    [SerializeField]
    private string performanceHeader = "PERFORMANCE";

    [SerializeField]
    private string objectivesHeader = "OBJECTIVES";

    [SerializeField]
    private string noObjectivesLabel = "No tracked objectives.";

    [SerializeField]
    private string timeLabel = "Time";

    [SerializeField]
    private string firesLabel = "Fires";

    [SerializeField]
    private string rescuesLabel = "Rescues";

    [SerializeField]
    private string victimsLabel = "Victims";

    [SerializeField]
    private string rankLabel = "RANK";

    [SerializeField]
    private string scoreLabel = "Score";

    [SerializeField]
    private string victimsValueFormat = "U:{0} C:{1} S:{2} X:{3} D:{4}";

    [Header("Scene UI Names")]
    [SerializeField]
    private string resultPopupObjectName = "ResultPopup";

    private GameObject overlayRoot;
    private CanvasGroup overlayCanvasGroup;
    private MissionResultPopupSequence overlaySequence;
    private Transform overlayContentRoot;
    private GameObject gameSummaryPanelRoot;
    private GameObject legacyResultPanelRoot;
    private TMP_Text resultLabelText;
    private TMP_Text resultStateText;
    private TMP_Text missionNameText;
    private TMP_Text performanceHeaderText;
    private TMP_Text timeLabelText;
    private TMP_Text timeValueText;
    private TMP_Text scoreLabelText;
    private GameObject scoreRowRoot;
    private TMP_Text scoreValueText;
    private TMP_Text rescuesLabelText;
    private TMP_Text rescuesValueText;
    private TMP_Text victimsLabelText;
    private TMP_Text victimsValueText;
    private TMP_Text firesLabelText;
    private TMP_Text firesValueText;
    private TMP_Text objectivesHeaderText;
    private RectTransform objectivesListRectTransform;
    private ObjectiveRowView[] objectiveRows;
    private GameObject rankStampRoot;
    private TMP_Text rankLabelText;
    private TMP_Text rankValueText;
    private GameObject retryButtonRoot;
    private Button retryButton;
    private TMP_Text retryButtonText;
    private GameObject mainMenuButtonRoot;
    private Button mainMenuButton;
    private TMP_Text mainMenuButtonText;
    private bool hasOpenedResult;
    private float previousTimeScale = 1f;
    private bool timeScaleOverridden;
    private bool missingOverlayWarningLogged;
    private bool callbacksBound;
    private bool usingCompletionIntroSequence;
    private Coroutine rankStampCoroutine;
    private Coroutine rankStampDelayCoroutine;

    private sealed class ObjectiveRowView
    {
        public GameObject Root;
        public TMP_Text Text;
        public Image Icon;
        public LayoutElement LayoutElement;
    }

    private void Awake()
    {
        ResolveReferences();
        ResolveSceneOverlay();
        BindButtonCallbacks();
        HideOverlayImmediate();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ResolveSceneOverlay();
        BindButtonCallbacks();
        LanguageManager.LanguageChanged -= HandleLanguageChanged;
        LanguageManager.LanguageChanged += HandleLanguageChanged;
    }

    private void OnDisable()
    {
        LanguageManager.LanguageChanged -= HandleLanguageChanged;
        RestoreTimeScale();
    }

    private void OnDestroy()
    {
        LanguageManager.LanguageChanged -= HandleLanguageChanged;
        RestoreTimeScale();
    }

    private void Update()
    {
        ResolveReferences();
        ResolveSceneOverlay();
        BindButtonCallbacks();

        if (missionSystem == null || hasOpenedResult)
        {
            return;
        }

        if (
            missionSystem.State == IncidentMissionSystem.MissionState.Completed
            || missionSystem.State == IncidentMissionSystem.MissionState.Failed
        )
        {
            OpenResultOverlay();
        }
    }

    private void ResolveReferences()
    {
        if (missionSystem == null)
        {
            missionSystem = GetComponent<IncidentMissionSystem>();
            if (missionSystem == null)
            {
                missionSystem = FindAnyObjectByType<IncidentMissionSystem>();
            }
        }
    }

    private void ResolveSceneOverlay()
    {
        if (overlayRoot != null)
        {
            return;
        }

        Transform overlayTransform = FindSceneTransformByName(resultPopupObjectName);
        if (overlayTransform == null)
        {
            if (!missingOverlayWarningLogged)
            {
                Debug.LogWarning(
                    $"[MissionEndOverlayController] Could not find '{resultPopupObjectName}' in scene '{SceneManager.GetActiveScene().name}'."
                );
                missingOverlayWarningLogged = true;
            }

            return;
        }

        overlayRoot = overlayTransform.gameObject;
        overlayCanvasGroup = overlayRoot.GetComponent<CanvasGroup>();
        overlaySequence = overlayRoot.GetComponent<MissionResultPopupSequence>();
        if (overlaySequence == null && CanAutoAttachCompletionSequence(overlayTransform))
        {
            overlaySequence = overlayRoot.AddComponent<MissionResultPopupSequence>();
        }

        Transform gameSummaryTransform = FindDescendantByName(overlayTransform, "GameSummaryPanel");
        Transform legacyResultTransform = FindDescendantByName(overlayTransform, "ResultPanel");
        overlayContentRoot = gameSummaryTransform ?? legacyResultTransform ?? overlayTransform;
        gameSummaryPanelRoot = gameSummaryTransform?.gameObject;
        legacyResultPanelRoot = legacyResultTransform?.gameObject;

        if (gameSummaryPanelRoot != null && legacyResultPanelRoot != null)
        {
            legacyResultPanelRoot.SetActive(false);
        }

        resultLabelText =
            FindText(overlayContentRoot, "Txt_Result")
            ?? FindText(overlayContentRoot, "ResultLabelText");
        resultStateText =
            FindText(overlayContentRoot, "Txt_MissionComplete")
            ?? FindText(overlayContentRoot, "ResultStateText");
        missionNameText =
            FindText(overlayContentRoot, "Txt_Subtitle")
            ?? FindText(overlayContentRoot, "MissionNameText");
        performanceHeaderText =
            FindText(overlayContentRoot, "Txt_PerformanceTitle")
            ?? FindFirstTextInNamedRow(overlayContentRoot, "PerformanceHeadingRow");
        timeLabelText =
            FindTextInNamedRow(overlayContentRoot, "StatRow_Time", "Txt_Label")
            ?? FindTextInNamedRowChild(overlayContentRoot, "TimeStatRow", 0)
            ?? FindLabelTextInNamedRow(overlayContentRoot, "TimeStatRow");
        timeValueText =
            FindTextInNamedRow(overlayContentRoot, "StatRow_Time", "Txt_Value")
            ?? FindText(overlayContentRoot, "TimerText")
            ?? FindValueTextInNamedRow(overlayContentRoot, "TimeStatRow");
        scoreLabelText =
            FindTextInNamedRow(overlayContentRoot, "ScoreRow", "Txt_Label")
            ?? FindTextInNamedRowChild(overlayContentRoot, "ScoreStatRow", 0)
            ?? FindLabelTextInNamedRow(overlayContentRoot, "ScoreStatRow");
        scoreRowRoot =
            FindDescendantByName(overlayContentRoot, "ScoreRow")?.gameObject
            ?? FindDescendantByName(overlayContentRoot, "ScoreStatRow")?.gameObject;
        scoreValueText =
            FindTextInNamedRow(overlayContentRoot, "ScoreRow", "Txt_Value")
            ?? FindTextInNamedRowChild(overlayContentRoot, "ScoreStatRow", 1)
            ?? FindValueTextInNamedRow(overlayContentRoot, "ScoreStatRow");
        rescuesLabelText =
            FindTextInNamedRow(overlayContentRoot, "StatRow_Rescues", "Txt_Label")
            ?? FindTextInNamedRowChild(overlayContentRoot, "RescuesStatRow", 0)
            ?? FindLabelTextInNamedRow(overlayContentRoot, "RescuesStatRow");
        rescuesValueText =
            FindTextInNamedRow(overlayContentRoot, "StatRow_Rescues", "Txt_Value")
            ?? FindTextInNamedRowChild(overlayContentRoot, "RescuesStatRow", 1)
            ?? FindValueTextInNamedRow(overlayContentRoot, "RescuesStatRow");
        victimsLabelText =
            FindTextInNamedRow(overlayContentRoot, "StatRow_Victims", "Txt_Label")
            ?? FindTextInNamedRowChild(overlayContentRoot, "VictimsStatRow", 0)
            ?? FindLabelTextInNamedRow(overlayContentRoot, "VictimsStatRow");
        victimsValueText =
            FindTextInNamedRow(overlayContentRoot, "StatRow_Victims", "Txt_Value")
            ?? FindTextInNamedRowChild(overlayContentRoot, "VictimsStatRow", 1)
            ?? FindValueTextInNamedRow(overlayContentRoot, "VictimsStatRow");
        firesLabelText =
            FindTextInNamedRow(overlayContentRoot, "StatRow_Fires", "Txt_Label")
            ?? FindTextInNamedRowChild(overlayContentRoot, "FiresStatRow", 0)
            ?? FindLabelTextInNamedRow(overlayContentRoot, "FiresStatRow");
        firesValueText =
            FindTextInNamedRow(overlayContentRoot, "StatRow_Fires", "Txt_Value")
            ?? FindTextInNamedRowChild(overlayContentRoot, "FiresStatRow", 1)
            ?? FindValueTextInNamedRow(overlayContentRoot, "FiresStatRow");
        objectivesHeaderText =
            FindText(overlayContentRoot, "Txt_ObjectivesTitle")
            ?? FindText(overlayContentRoot, "ObjectivesHeadingText");
        Transform objectivesAreaTransform =
            FindDescendantByName(overlayContentRoot, "Objectives_Area")
            ?? FindDescendantByName(overlayContentRoot, "ObjectivesList");
        objectivesListRectTransform = ResolveObjectivesListRoot(objectivesAreaTransform);
        rankStampRoot = FindDescendantByName(overlayContentRoot, "Rank_Stamp_Group")?.gameObject;
        rankLabelText = FindText(overlayContentRoot, "Txt_RankLabel");
        rankValueText = FindText(overlayContentRoot, "Txt_RankGrade");
        objectiveRows = BuildObjectiveRows(overlayContentRoot, objectivesAreaTransform);

        retryButtonRoot =
            FindDescendantByName(overlayContentRoot, "btnRetry")?.gameObject
            ?? FindDescendantByName(overlayContentRoot, "RetryButtonRoot")?.gameObject;
        retryButton =
            FindButton(overlayContentRoot, "btnRetry")
            ?? FindButton(overlayContentRoot, "RetryButton");
        retryButtonText =
            FindFirstTextInTransform(
                retryButton != null ? retryButton.transform : retryButtonRoot?.transform
            ) ?? FindText(overlayContentRoot, "RetryButtonText");

        mainMenuButtonRoot =
            FindDescendantByName(overlayContentRoot, "btnBackToMain")?.gameObject
            ?? FindDescendantByName(overlayContentRoot, "MainMenuButtonRoot")?.gameObject;
        mainMenuButton =
            FindButton(overlayContentRoot, "btnBackToMain")
            ?? FindButton(overlayContentRoot, "MainMenuButton");
        mainMenuButtonText =
            FindFirstTextInTransform(
                mainMenuButton != null ? mainMenuButton.transform : mainMenuButtonRoot?.transform
            ) ?? FindText(overlayContentRoot, "MainMenuButtonText");
    }

    private void BindButtonCallbacks()
    {
        if (callbacksBound)
        {
            return;
        }

        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(HandleRetryPressed);
            retryButton.onClick.AddListener(HandleRetryPressed);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(HandleMainMenuPressed);
            mainMenuButton.onClick.AddListener(HandleMainMenuPressed);
        }

        callbacksBound = retryButton != null || mainMenuButton != null;
    }

    private void OpenResultOverlay()
    {
        ResolveSceneOverlay();
        if (overlayRoot == null || missionSystem == null)
        {
            return;
        }

        hasOpenedResult = true;
        usingCompletionIntroSequence = false;

        overlayRoot.SetActive(true);
        EnsurePreferredSummaryPanelActive();

        if (pauseGameplayOnMissionEnd)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            timeScaleOverridden = true;
        }

        if (unlockCursorOnMissionEnd)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        if (
            missionSystem.State == IncidentMissionSystem.MissionState.Completed
            && overlaySequence != null
            && overlaySequence.CanPlayCompletionIntro
        )
        {
            usingCompletionIntroSequence = true;
            PopulateOverlay();
            RefreshButtonLabels();

            if (retryButtonRoot != null)
            {
                retryButtonRoot.SetActive(showRetryButton);
            }

            if (mainMenuButtonRoot != null)
            {
                mainMenuButtonRoot.SetActive(showMainMenuButton);
            }

            overlaySequence.PlayCompletionIntro(HandleCompletionIntroFinished);
            return;
        }

        if (overlaySequence != null)
        {
            overlaySequence.PrepareSummaryLayout();
        }

        PopulateOverlay();
        RefreshButtonLabels();

        if (retryButtonRoot != null)
        {
            retryButtonRoot.SetActive(showRetryButton);
        }

        if (mainMenuButtonRoot != null)
        {
            mainMenuButtonRoot.SetActive(showMainMenuButton);
        }

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 1f;
            overlayCanvasGroup.interactable = true;
            overlayCanvasGroup.blocksRaycasts = true;
        }
    }

    private void HideOverlayImmediate()
    {
        usingCompletionIntroSequence = false;

        if (overlaySequence != null)
        {
            overlaySequence.HideImmediate();
        }

        if (overlayRoot == null)
        {
            return;
        }

        overlayRoot.SetActive(true);
        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.interactable = false;
            overlayCanvasGroup.blocksRaycasts = false;
        }
        else
        {
            overlayRoot.SetActive(false);
        }

        // Stop any pending or running rank stamp coroutines when hiding overlay
        StopRankStampDelay();
        if (rankStampCoroutine != null)
        {
            StopCoroutine(rankStampCoroutine);
            rankStampCoroutine = null;
        }
    }

    private void PopulateOverlay()
    {
        if (missionSystem == null)
        {
            return;
        }

        if (resultLabelText != null)
        {
            resultLabelText.text = MissionLocalization.Get("mission.end.result_label", resultLabel);
        }

        if (resultStateText != null)
        {
            resultStateText.text =
                missionSystem.State == IncidentMissionSystem.MissionState.Completed
                    ? MissionLocalization.Get("mission.end.completed_title", completedTitle)
                    : MissionLocalization.Get("mission.end.failed_title", failedTitle);
        }

        if (missionNameText != null)
        {
            missionNameText.text = missionSystem.MissionTitle;
        }

        if (performanceHeaderText != null)
        {
            performanceHeaderText.text = MissionLocalization.Get(
                "mission.end.performance_header",
                MissionLocalization.Get("mission.end.stats_header", performanceHeader)
            );
        }

        if (timeLabelText != null)
        {
            timeLabelText.text = MissionLocalization.Get("mission.end.time_label", timeLabel);
        }

        if (firesLabelText != null)
        {
            firesLabelText.text = MissionLocalization.Get("mission.end.fires_label", firesLabel);
        }

        if (rescuesLabelText != null)
        {
            rescuesLabelText.text = MissionLocalization.Get(
                "mission.end.rescues_label",
                rescuesLabel
            );
        }

        if (victimsLabelText != null)
        {
            victimsLabelText.text = MissionLocalization.Get(
                "mission.end.victims_label",
                victimsLabel
            );
        }

        if (scoreLabelText != null)
        {
            scoreLabelText.text = MissionLocalization.Get("mission.end.score_label", scoreLabel);
        }

        if (rankLabelText != null)
        {
            rankLabelText.text = MissionLocalization.Get("mission.end.rank_label", rankLabel);
        }

        if (timeValueText != null)
        {
            timeValueText.text = BuildTimeValue();
        }

        if (scoreRowRoot != null)
        {
            bool hasScore = missionSystem.DisplayedMaximumScore > 0;
            scoreRowRoot.SetActive(hasScore);
            if (hasScore && scoreValueText != null)
            {
                scoreValueText.text = BuildScoreValue(rankValueText == null);
            }
        }

        if (rankStampRoot != null)
        {
            bool hasRank = !string.IsNullOrWhiteSpace(missionSystem.DisplayedScoreRank);
            bool showRank = missionSystem.DisplayedMaximumScore > 0 || hasRank;
            rankStampRoot.SetActive(showRank);

            if (showRank && rankValueText != null)
            {
                rankValueText.text = hasRank ? missionSystem.DisplayedScoreRank : "-";
            }

            if (showRank)
            {
                StartRankStampDelay();
            }
            else
            {
                StopRankStampDelay();
                if (rankStampRoot != null)
                {
                    rankStampRoot.SetActive(false);
                }
            }
        }

        if (rescuesValueText != null)
        {
            rescuesValueText.text =
                $"{missionSystem.DisplayedRescuedCount} / {missionSystem.DisplayedTotalTrackedRescuables}";
        }

        if (victimsValueText != null)
        {
            victimsValueText.text = MissionLocalization.Format(
                "mission.end.victims_format",
                victimsValueFormat,
                missionSystem.DisplayedUrgentVictimCount,
                missionSystem.DisplayedCriticalVictimCount,
                missionSystem.DisplayedStabilizedVictimCount,
                missionSystem.DisplayedExtractedVictimCount,
                missionSystem.DisplayedDeceasedVictimCount
            );
        }

        if (firesValueText != null)
        {
            firesValueText.text =
                $"{missionSystem.DisplayedExtinguishedFireCount} / {missionSystem.DisplayedTotalTrackedFires}";
        }

        if (objectivesHeaderText != null)
        {
            objectivesHeaderText.text = MissionLocalization.Get(
                "mission.end.objectives_header",
                objectivesHeader
            );
        }

        RefreshObjectiveRows();
    }

    private void HandleLanguageChanged(AppLanguage _)
    {
        if (!hasOpenedResult || usingCompletionIntroSequence)
        {
            return;
        }

        ResolveSceneOverlay();
        PopulateOverlay();
        RefreshButtonLabels();
    }

    private void HandleCompletionIntroFinished()
    {
        usingCompletionIntroSequence = false;
        ResolveSceneOverlay();
        PopulateOverlay();
        RefreshButtonLabels();

        if (retryButtonRoot != null)
        {
            retryButtonRoot.SetActive(showRetryButton);
        }

        if (mainMenuButtonRoot != null)
        {
            mainMenuButtonRoot.SetActive(showMainMenuButton);
        }

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 1f;
            overlayCanvasGroup.interactable = true;
            overlayCanvasGroup.blocksRaycasts = true;
        }
    }

    private void RefreshObjectiveRows()
    {
        if (objectiveRows == null || objectiveRows.Length == 0)
        {
            return;
        }

        int visibleCount = 0;
        for (int i = 0; i < objectiveRows.Length; i++)
        {
            ObjectiveRowView row = objectiveRows[i];
            if (row?.Root != null)
            {
                row.Root.SetActive(false);
            }
        }

        for (
            int i = 0;
            i < missionSystem.ResultObjectiveStatusCount && visibleCount < objectiveRows.Length;
            i++
        )
        {
            if (
                !missionSystem.TryGetResultObjectiveStatus(
                    i,
                    out MissionObjectiveStatusSnapshot status
                )
            )
            {
                continue;
            }

            ObjectiveRowView row = objectiveRows[visibleCount];
            if (row?.Root == null)
            {
                visibleCount++;
                continue;
            }

            row.Root.SetActive(true);
            if (row.Text != null)
            {
                row.Text.text = BuildObjectiveText(status, row.Icon == null);
            }

            ApplyObjectiveVisuals(row, status);
            RefreshObjectiveRowLayout(row);
            visibleCount++;
        }

        RefreshObjectivesLayoutRoot();

        if (visibleCount > 0)
        {
            return;
        }

        ObjectiveRowView firstRow = objectiveRows[0];
        if (firstRow?.Root == null)
        {
            return;
        }

        firstRow.Root.SetActive(true);
        if (firstRow.Text != null)
        {
            firstRow.Text.text = MissionLocalization.Get(
                "mission.end.no_objectives",
                noObjectivesLabel
            );
            firstRow.Text.color = ObjectivePendingColor;
        }

        if (firstRow.Icon != null)
        {
            firstRow.Icon.color = ObjectivePendingColor;
        }

        RefreshObjectiveRowLayout(firstRow);
        RefreshObjectivesLayoutRoot();
    }

    private void ApplyObjectiveVisuals(ObjectiveRowView row, MissionObjectiveStatusSnapshot status)
    {
        Color iconColor = ObjectivePendingColor;
        Color textColor = ObjectivePendingColor;

        if (status.HasFailed)
        {
            iconColor = ObjectiveFailedColor;
            textColor = ObjectiveFailedTextColor;
        }
        else if (status.IsComplete)
        {
            iconColor = ObjectiveCompletedColor;
            textColor = ObjectiveCompletedTextColor;
        }

        if (row.Icon != null)
        {
            row.Icon.color = iconColor;
        }

        if (row.Text != null)
        {
            row.Text.color = textColor;
        }
    }

    private void RefreshObjectiveRowLayout(ObjectiveRowView row)
    {
        if (row?.Root == null || row.LayoutElement == null)
        {
            return;
        }

        float preferredHeight = 36f;
        if (row.Text != null)
        {
            // Disable auto-sizing to prevent the text component from changing font size at runtime.
            row.Text.enableAutoSizing = false;
            row.Text.textWrappingMode = TextWrappingModes.Normal;
            row.Text.overflowMode = TextOverflowModes.Overflow;

            // Do not compute preferredHeight from GetPreferredValues to avoid dynamic row resizing.
            // Keep the default/fallback preferredHeight value above.
        }

        row.LayoutElement.minHeight = preferredHeight;
        row.LayoutElement.preferredHeight = preferredHeight;
        row.LayoutElement.flexibleHeight = 0f;
    }

    private void RefreshObjectivesLayoutRoot()
    {
        if (objectivesListRectTransform == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(objectivesListRectTransform);
        Canvas.ForceUpdateCanvases();
    }

    private string BuildScoreValue(bool includeRank)
    {
        string scoreValue = $"{missionSystem.DisplayedScore}/{missionSystem.DisplayedMaximumScore}";
        if (!includeRank || string.IsNullOrWhiteSpace(missionSystem.DisplayedScoreRank))
        {
            return scoreValue;
        }

        return $"{scoreValue} [{missionSystem.DisplayedScoreRank}]";
    }

    private static string BuildObjectiveText(
        MissionObjectiveStatusSnapshot status,
        bool includePrefix
    )
    {
        string text = !string.IsNullOrWhiteSpace(status.Summary) ? status.Summary : status.Title;
        if (status.MaxScore > 0)
        {
            text = $"{text} ({status.Score}/{status.MaxScore})";
        }

        if (!includePrefix)
        {
            return text;
        }

        string prefix =
            status.HasFailed ? MissionLocalization.Get("mission.hud.prefix.failed", "[FAILED]")
            : status.IsComplete ? MissionLocalization.Get("mission.hud.prefix.completed", "[DONE]")
            : MissionLocalization.Get("mission.hud.prefix.pending", "[ ]");
        return $"{prefix} {text}";
    }

    private void RefreshButtonLabels()
    {
        if (retryButtonText != null)
        {
            retryButtonText.text = MissionLocalization.Get(
                "mission.end.retry_button",
                retryButtonLabel
            );
        }

        if (mainMenuButtonText != null)
        {
            mainMenuButtonText.text = MissionLocalization.Get(
                "mission.end.main_menu_button",
                mainMenuButtonLabel
            );
        }
    }

    private void HandleRetryPressed()
    {
        RestoreTimeScale();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void HandleMainMenuPressed()
    {
        RestoreTimeScale();
        if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName.Trim());
        }
    }

    private void RestoreTimeScale()
    {
        if (!timeScaleOverridden)
        {
            return;
        }

        Time.timeScale = previousTimeScale;
        timeScaleOverridden = false;
    }

    private string BuildTimeValue()
    {
        if (missionSystem == null)
        {
            return string.Empty;
        }

        float seconds =
            missionSystem.TimeLimitSeconds > 0f
                ? missionSystem.RemainingTimeSeconds
                : missionSystem.ElapsedTime;
        return FormatClock(seconds);
    }

    private static string FormatClock(float elapsedSeconds)
    {
        int roundedSeconds = Mathf.Max(0, Mathf.RoundToInt(elapsedSeconds));
        int hours = roundedSeconds / 3600;
        int minutes = (roundedSeconds % 3600) / 60;
        int seconds = roundedSeconds % 60;

        if (hours > 0)
        {
            return $"{hours:00}:{minutes:00}:{seconds:00}";
        }

        return $"{minutes:00}:{seconds:00}";
    }

    private static Transform FindSceneTransformByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindDescendantByName(roots[i].transform, objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindDescendantByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendantByName(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static TMP_Text FindText(Transform root, string objectName)
    {
        return FindDescendantByName(root, objectName)?.GetComponent<TMP_Text>();
    }

    private static TMP_Text FindTextInNamedRow(Transform root, string rowName, string textName)
    {
        Transform row = FindDescendantByName(root, rowName);
        return FindText(row, textName);
    }

    private static Button FindButton(Transform root, string objectName)
    {
        return FindDescendantByName(root, objectName)?.GetComponent<Button>();
    }

    private static TMP_Text FindFirstTextInNamedRow(Transform root, string rowName)
    {
        Transform row = FindDescendantByName(root, rowName);
        return FindFirstTextInTransform(row);
    }

    private static TMP_Text FindTextInNamedRowChild(Transform root, string rowName, int childIndex)
    {
        Transform row = FindDescendantByName(root, rowName);
        if (row == null || childIndex < 0 || childIndex >= row.childCount)
        {
            return null;
        }

        return FindFirstTextInTransform(row.GetChild(childIndex));
    }

    private static TMP_Text FindFirstTextInTransform(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        return texts.Length > 0 ? texts[0] : null;
    }

    private static TMP_Text FindValueTextInNamedRow(Transform root, string rowName)
    {
        Transform row = FindDescendantByName(root, rowName);
        if (row == null)
        {
            return null;
        }

        TMP_Text directChildValueText = FindBoundaryTextInRow(row, false);
        if (directChildValueText != null)
        {
            return directChildValueText;
        }

        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
        if (texts.Length == 0)
        {
            return null;
        }

        return texts.Length == 1 ? texts[0] : texts[texts.Length - 1];
    }

    private static TMP_Text FindLabelTextInNamedRow(Transform root, string rowName)
    {
        Transform row = FindDescendantByName(root, rowName);
        if (row == null)
        {
            return null;
        }

        TMP_Text directChildLabelText = FindBoundaryTextInRow(row, true);
        if (directChildLabelText != null)
        {
            return directChildLabelText;
        }

        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
        if (texts.Length == 0)
        {
            return null;
        }

        return texts[0];
    }

    private static TMP_Text FindBoundaryTextInRow(Transform row, bool firstChild)
    {
        if (row == null || row.childCount == 0)
        {
            return null;
        }

        int childIndex = firstChild ? 0 : row.childCount - 1;
        Transform child = row.GetChild(childIndex);
        TMP_Text childText = FindFirstTextInTransform(child);
        if (childText != null)
        {
            return childText;
        }

        return row.childCount >= 2
            ? FindFirstTextInTransform(row.GetChild(firstChild ? 1 : row.childCount - 2))
            : null;
    }

    private static Image FindFirstImageInTransform(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Image[] images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].gameObject != root.gameObject)
            {
                return images[i];
            }
        }

        return null;
    }

    private static ObjectiveRowView[] BuildObjectiveRows(Transform root, Transform objectivesArea)
    {
        if (objectivesArea != null)
        {
            List<ObjectiveRowView> summaryRows = new List<ObjectiveRowView>();
            CollectObjectiveRows(objectivesArea, summaryRows);

            if (summaryRows.Count > 0)
            {
                return summaryRows.ToArray();
            }
        }

        ObjectiveRowView[] legacyRows = new ObjectiveRowView[ObjectiveRowNames.Length];
        for (int i = 0; i < ObjectiveRowNames.Length; i++)
        {
            Transform rowTransform = FindDescendantByName(root, ObjectiveRowNames[i]);
            if (rowTransform == null)
            {
                continue;
            }

            legacyRows[i] = new ObjectiveRowView
            {
                Root = rowTransform.gameObject,
                Text =
                    FindText(root, ObjectiveTextNames[i])
                    ?? FindTextInNamedRowChild(root, ObjectiveRowNames[i], 1)
                    ?? FindFirstTextInTransform(rowTransform),
                Icon = FindFirstImageInTransform(rowTransform),
                LayoutElement = rowTransform.GetComponent<LayoutElement>(),
            };
        }

        return legacyRows;
    }

    private static bool CanAutoAttachCompletionSequence(Transform overlayTransform)
    {
        if (overlayTransform == null)
        {
            return false;
        }

        return FindDescendantByName(overlayTransform, "MissionComplete") != null
            && FindDescendantByName(overlayTransform, "GameSummaryPanel") != null;
    }

    private static void CollectObjectiveRows(Transform root, List<ObjectiveRowView> rows)
    {
        if (root == null || rows == null)
        {
            return;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (child.name.StartsWith("ObjectiveRow_", StringComparison.Ordinal))
            {
                rows.Add(
                    new ObjectiveRowView
                    {
                        Root = child.gameObject,
                        Text =
                            FindText(child, "Txt_ObjectiveDesc")
                            ?? FindFirstTextInTransform(child),
                        Icon =
                            FindDescendantByName(child, "Icon_Checkmark")?.GetComponent<Image>()
                            ?? FindFirstImageInTransform(child),
                        LayoutElement =
                            child.GetComponent<LayoutElement>()
                            ?? child.GetComponentInChildren<LayoutElement>(true),
                    }
                );
                continue;
            }

            CollectObjectiveRows(child, rows);
        }
    }

    private static RectTransform ResolveObjectivesListRoot(Transform objectivesArea)
    {
        if (objectivesArea == null)
        {
            return null;
        }

        Transform explicitListRoot =
            FindDescendantByName(objectivesArea, "ObjectivesList")
            ?? FindDescendantByName(objectivesArea, "Objectives_Wrapper")
            ?? FindDescendantByName(objectivesArea, "Objectives_Content")
            ?? FindDescendantByName(objectivesArea, "Objectives_VisualRoot")
            ?? FindDescendantByName(objectivesArea, "Objectives_Body");
        if (explicitListRoot is RectTransform explicitRect)
        {
            return explicitRect;
        }

        Transform firstObjectiveRow = FindFirstDescendantByPrefix(objectivesArea, "ObjectiveRow_");
        if (firstObjectiveRow is RectTransform firstRowRect && firstRowRect.parent is RectTransform rowParent)
        {
            return rowParent;
        }

        return objectivesArea as RectTransform;
    }

    private static Transform FindFirstDescendantByPrefix(Transform root, string prefix)
    {
        if (root == null || string.IsNullOrEmpty(prefix))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (child.name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return child;
            }

            Transform found = FindFirstDescendantByPrefix(child, prefix);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void EnsurePreferredSummaryPanelActive()
    {
        if (gameSummaryPanelRoot == null)
        {
            return;
        }

        gameSummaryPanelRoot.SetActive(true);
        if (legacyResultPanelRoot != null)
        {
            legacyResultPanelRoot.SetActive(false);
        }
    }

    private void StartRankStampAnimation()
    {
        if (rankStampCoroutine != null)
        {
            StopCoroutine(rankStampCoroutine);
            rankStampCoroutine = null;
        }

        if (rankStampRoot == null)
        {
            return;
        }

        // Ensure the stamp GameObject is active before animating.
        rankStampRoot.SetActive(true);
        rankStampCoroutine = StartCoroutine(RankStampRoutine());
    }

    private IEnumerator RankStampRoutine()
    {
        Transform t = rankStampRoot.transform;
        Vector3 originalScale = t.localScale;
        Quaternion originalRot = t.localRotation;

        t.localScale = Vector3.zero;
        t.localRotation = Quaternion.Euler(0f, 0f, -30f);

        float phase1 = 0.12f;
        float phase2 = 0.18f;
        float elapsed = 0f;

        // pop in to slightly larger than original
        while (elapsed < phase1)
        {
            float p = elapsed / phase1;
            float s = Mathf.SmoothStep(0f, 1.25f, p);
            t.localScale = originalScale * s;
            t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-30f, 10f, p));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // settle back to original with small bounce
        elapsed = 0f;
        Vector3 overshoot = originalScale * 1.05f;
        while (elapsed < phase2)
        {
            float p = elapsed / phase2;
            float s = Mathf.SmoothStep(1.25f, 1f, p);
            t.localScale = originalScale * s;
            t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(10f, 0f, p));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        t.localScale = originalScale;
        t.localRotation = originalRot;
        rankStampCoroutine = null;
    }

    private void StartRankStampDelay()
    {
        StopRankStampDelay();
        if (rankStampRoot == null)
        {
            return;
        }
        // Ensure it's hidden until delay completes
        rankStampRoot.SetActive(false);
        rankStampDelayCoroutine = StartCoroutine(RankStampDelayRoutine());
    }

    private void StopRankStampDelay()
    {
        if (rankStampDelayCoroutine != null)
        {
            StopCoroutine(rankStampDelayCoroutine);
            rankStampDelayCoroutine = null;
        }
    }

    private IEnumerator RankStampDelayRoutine()
    {
        // wait until GameSummaryPanel is active
        if (gameSummaryPanelRoot == null)
        {
            yield break;
        }

        // Wait until panel becomes active in hierarchy
        while (!gameSummaryPanelRoot.activeInHierarchy)
        {
            yield return null;
        }

        // Require 3 seconds of continuous visible time (unscaled)
        float required = 3f;
        float timer = 0f;
        while (timer < required)
        {
            if (!gameSummaryPanelRoot.activeInHierarchy)
            {
                // aborted, panel hidden again
                yield break;
            }
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        rankStampDelayCoroutine = null;
        StartRankStampAnimation();
    }
}
