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
        "ReachExitObjectiveRow"
    };

    private static readonly Color ObjectivePendingColor = new Color(0.92f, 0.95f, 1f, 1f);
    private static readonly Color ObjectiveCompletedColor = new Color(0.33f, 0.86f, 0.56f, 1f);
    private static readonly Color ObjectiveFailedColor = new Color(0.94f, 0.34f, 0.31f, 1f);
    private static readonly Color ObjectiveCompletedTextColor = new Color(0.84f, 0.97f, 0.88f, 1f);
    private static readonly Color ObjectiveFailedTextColor = new Color(1f, 0.86f, 0.86f, 1f);

    [Header("References")]
    [SerializeField] private IncidentMissionSystem missionSystem;
    [SerializeField] private Canvas targetCanvas;

    [Header("Behavior")]
    [SerializeField] private bool pauseGameplayOnMissionEnd = true;
    [SerializeField] private bool unlockCursorOnMissionEnd = true;
    [SerializeField] private bool showRetryButton = true;
    [SerializeField] private bool showMainMenuButton = true;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Labels")]
    [SerializeField] private string resultLabel = "RESULT";
    [SerializeField] private string completedTitle = "Mission Complete";
    [SerializeField] private string failedTitle = "Mission Failed";
    [SerializeField] private string retryButtonLabel = "Retry";
    [SerializeField] private string mainMenuButtonLabel = "Main Menu";
    [SerializeField] private string performanceHeader = "PERFORMANCE";
    [SerializeField] private string objectivesHeader = "OBJECTIVES";
    [SerializeField] private string noObjectivesLabel = "No tracked objectives.";
    [SerializeField] private string timeLabel = "Time";
    [SerializeField] private string firesLabel = "Fires";
    [SerializeField] private string rescuesLabel = "Rescues";
    [SerializeField] private string victimsLabel = "Victims";
    [SerializeField] private string scoreLabel = "Score";
    [SerializeField] private string victimsValueFormat = "U:{0} C:{1} S:{2} X:{3} D:{4}";

    [Header("Scene UI Names")]
    [SerializeField] private string resultPopupObjectName = "ResultPopup";

    private GameObject overlayRoot;
    private CanvasGroup overlayCanvasGroup;
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

        if (missionSystem.State == IncidentMissionSystem.MissionState.Completed ||
            missionSystem.State == IncidentMissionSystem.MissionState.Failed)
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
                Debug.LogWarning($"[MissionEndOverlayController] Could not find '{resultPopupObjectName}' in scene '{SceneManager.GetActiveScene().name}'.");
                missingOverlayWarningLogged = true;
            }

            return;
        }

        overlayRoot = overlayTransform.gameObject;
        overlayCanvasGroup = overlayRoot.GetComponent<CanvasGroup>();

        resultLabelText = FindText(overlayTransform, "ResultLabelText");
        resultStateText = FindText(overlayTransform, "ResultStateText");
        missionNameText = FindText(overlayTransform, "MissionNameText");
        performanceHeaderText = FindFirstTextInNamedRow(overlayTransform, "PerformanceHeadingRow");
        timeLabelText = FindLabelTextInNamedRow(overlayTransform, "TimeStatRow");
        timeValueText = FindValueTextInNamedRow(overlayTransform, "TimeStatRow");
        scoreLabelText = FindLabelTextInNamedRow(overlayTransform, "ScoreStatRow");
        scoreRowRoot = FindDescendantByName(overlayTransform, "ScoreStatRow")?.gameObject;
        scoreValueText = FindValueTextInNamedRow(overlayTransform, "ScoreStatRow");
        rescuesLabelText = FindLabelTextInNamedRow(overlayTransform, "RescuesStatRow");
        rescuesValueText = FindValueTextInNamedRow(overlayTransform, "RescuesStatRow");
        victimsLabelText = FindLabelTextInNamedRow(overlayTransform, "VictimsStatRow");
        victimsValueText = FindValueTextInNamedRow(overlayTransform, "VictimsStatRow");
        firesLabelText = FindLabelTextInNamedRow(overlayTransform, "FiresStatRow");
        firesValueText = FindValueTextInNamedRow(overlayTransform, "FiresStatRow");
        objectivesHeaderText = FindText(overlayTransform, "ObjectivesHeadingText");
        objectivesListRectTransform = FindDescendantByName(overlayTransform, "ObjectivesList") as RectTransform;

        objectiveRows = new ObjectiveRowView[ObjectiveRowNames.Length];
        for (int i = 0; i < ObjectiveRowNames.Length; i++)
        {
            Transform rowTransform = FindDescendantByName(overlayTransform, ObjectiveRowNames[i]);
            if (rowTransform == null)
            {
                continue;
            }

            objectiveRows[i] = new ObjectiveRowView
            {
                Root = rowTransform.gameObject,
                Text = FindFirstTextInTransform(rowTransform),
                Icon = FindFirstImageInTransform(rowTransform),
                LayoutElement = rowTransform.GetComponent<LayoutElement>() ?? rowTransform.gameObject.AddComponent<LayoutElement>()
            };
        }

        retryButtonRoot = FindDescendantByName(overlayTransform, "RetryButtonRoot")?.gameObject;
        retryButton = FindButton(overlayTransform, "RetryButton");
        retryButtonText = FindText(overlayTransform, "RetryButtonText");

        mainMenuButtonRoot = FindDescendantByName(overlayTransform, "MainMenuButtonRoot")?.gameObject;
        mainMenuButton = FindButton(overlayTransform, "MainMenuButton");
        mainMenuButtonText = FindText(overlayTransform, "MainMenuButtonText");
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

        overlayRoot.SetActive(true);
        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 1f;
            overlayCanvasGroup.interactable = true;
            overlayCanvasGroup.blocksRaycasts = true;
        }

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
    }

    private void HideOverlayImmediate()
    {
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
            resultStateText.text = missionSystem.State == IncidentMissionSystem.MissionState.Completed
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
                MissionLocalization.Get("mission.end.stats_header", performanceHeader));
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
            rescuesLabelText.text = MissionLocalization.Get("mission.end.rescues_label", rescuesLabel);
        }

        if (victimsLabelText != null)
        {
            victimsLabelText.text = MissionLocalization.Get("mission.end.victims_label", victimsLabel);
        }

        if (scoreLabelText != null)
        {
            scoreLabelText.text = MissionLocalization.Get("mission.end.score_label", scoreLabel);
        }

        if (timeValueText != null)
        {
            timeValueText.text = FormatElapsedTime(missionSystem.ElapsedTime);
        }

        if (scoreRowRoot != null)
        {
            bool hasScore = missionSystem.DisplayedMaximumScore > 0;
            scoreRowRoot.SetActive(hasScore);
            if (hasScore && scoreValueText != null)
            {
                scoreValueText.text = BuildScoreValue();
            }
        }

        if (rescuesValueText != null)
        {
            rescuesValueText.text = $"{missionSystem.DisplayedRescuedCount} / {missionSystem.DisplayedTotalTrackedRescuables}";
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
                missionSystem.DisplayedDeceasedVictimCount);
        }

        if (firesValueText != null)
        {
            firesValueText.text = $"{missionSystem.DisplayedExtinguishedFireCount} / {missionSystem.DisplayedTotalTrackedFires}";
        }

        if (objectivesHeaderText != null)
        {
            objectivesHeaderText.text = MissionLocalization.Get("mission.end.objectives_header", objectivesHeader);
        }

        RefreshObjectiveRows();
    }

    private void HandleLanguageChanged(AppLanguage _)
    {
        if (!hasOpenedResult)
        {
            return;
        }

        ResolveSceneOverlay();
        PopulateOverlay();
        RefreshButtonLabels();
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

        for (int i = 0; i < missionSystem.ResultObjectiveStatusCount && visibleCount < objectiveRows.Length; i++)
        {
            if (!missionSystem.TryGetResultObjectiveStatus(i, out MissionObjectiveStatusSnapshot status))
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
            firstRow.Text.text = MissionLocalization.Get("mission.end.no_objectives", noObjectivesLabel);
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
            row.Text.enableAutoSizing = true;
            row.Text.fontSizeMin = 12f;
            row.Text.enableWordWrapping = true;
            row.Text.overflowMode = TextOverflowModes.Overflow;

            RectTransform rowRectTransform = row.Root.transform as RectTransform;
            float textWidth = rowRectTransform != null ? Mathf.Max(140f, rowRectTransform.rect.width - 32f) : 220f;
            preferredHeight = Mathf.Max(preferredHeight, row.Text.GetPreferredValues(row.Text.text, textWidth, 0f).y + 10f);
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

    private string BuildScoreValue()
    {
        string scoreValue = $"{missionSystem.DisplayedScore}/{missionSystem.DisplayedMaximumScore}";
        if (string.IsNullOrWhiteSpace(missionSystem.DisplayedScoreRank))
        {
            return scoreValue;
        }

        return $"{scoreValue} [{missionSystem.DisplayedScoreRank}]";
    }

    private static string BuildObjectiveText(MissionObjectiveStatusSnapshot status, bool includePrefix)
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

        string prefix = status.HasFailed
            ? MissionLocalization.Get("mission.hud.prefix.failed", "[FAILED]")
            : status.IsComplete
                ? MissionLocalization.Get("mission.hud.prefix.completed", "[DONE]")
                : MissionLocalization.Get("mission.hud.prefix.pending", "[ ]");
        return $"{prefix} {text}";
    }

    private void RefreshButtonLabels()
    {
        if (retryButtonText != null)
        {
            retryButtonText.text = MissionLocalization.Get("mission.end.retry_button", retryButtonLabel);
        }

        if (mainMenuButtonText != null)
        {
            mainMenuButtonText.text = MissionLocalization.Get("mission.end.main_menu_button", mainMenuButtonLabel);
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

    private static string FormatElapsedTime(float elapsedSeconds)
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

    private static Button FindButton(Transform root, string objectName)
    {
        return FindDescendantByName(root, objectName)?.GetComponent<Button>();
    }

    private static TMP_Text FindFirstTextInNamedRow(Transform root, string rowName)
    {
        Transform row = FindDescendantByName(root, rowName);
        return FindFirstTextInTransform(row);
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

        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
        if (texts.Length == 0)
        {
            return null;
        }

        return texts[0];
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
}
