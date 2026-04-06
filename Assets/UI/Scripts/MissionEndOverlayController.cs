using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MissionEndOverlayController : MonoBehaviour
{
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
    [SerializeField] private string completedTitle = "Mission Complete";
    [SerializeField] private string failedTitle = "Mission Failed";
    [SerializeField] private string retryButtonLabel = "Retry";
    [SerializeField] private string mainMenuButtonLabel = "Main Menu";
    [SerializeField] private string statsHeader = "Summary";
    [SerializeField] private string objectivesHeader = "Objectives";

    private GameObject overlayRoot;
    private CanvasGroup overlayCanvasGroup;
    private TMP_Text titleText;
    private TMP_Text summaryText;
    private TMP_Text objectivesText;
    private Button retryButton;
    private Button mainMenuButton;
    private bool hasOpenedResult;
    private float previousTimeScale = 1f;
    private bool timeScaleOverridden;

    private void Awake()
    {
        ResolveReferences();
        EnsureOverlayCreated();
        HideOverlayImmediate();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        RestoreTimeScale();
    }

    private void OnDestroy()
    {
        RestoreTimeScale();
    }

    private void Update()
    {
        ResolveReferences();
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

        if (targetCanvas == null)
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas candidate = canvases[i];
                if (candidate != null && candidate.isRootCanvas && candidate.gameObject.activeInHierarchy)
                {
                    targetCanvas = candidate;
                    break;
                }
            }
        }
    }

    private void EnsureOverlayCreated()
    {
        if (overlayRoot != null)
        {
            return;
        }

        ResolveReferences();
        if (targetCanvas == null)
        {
            return;
        }

        TMP_FontAsset fontAsset = TMP_Settings.defaultFontAsset;

        overlayRoot = new GameObject("MissionEndOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        overlayRoot.transform.SetParent(targetCanvas.transform, false);
        overlayRoot.transform.SetAsLastSibling();

        RectTransform overlayRect = overlayRoot.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image background = overlayRoot.GetComponent<Image>();
        background.color = new Color(0.03f, 0.05f, 0.08f, 0.88f);

        overlayCanvasGroup = overlayRoot.GetComponent<CanvasGroup>();

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(overlayRoot.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(680f, 0f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.1f, 0.13f, 0.17f, 0.96f);

        VerticalLayoutGroup panelLayout = panel.GetComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(28, 28, 28, 28);
        panelLayout.spacing = 18f;
        panelLayout.childControlHeight = false;
        panelLayout.childControlWidth = true;
        panelLayout.childForceExpandHeight = false;
        panelLayout.childForceExpandWidth = true;

        ContentSizeFitter panelFitter = panel.GetComponent<ContentSizeFitter>();
        panelFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        titleText = CreateText("Title", panel.transform, fontAsset, 34f, FontStyles.Bold, TextAlignmentOptions.Center);
        summaryText = CreateText("Summary", panel.transform, fontAsset, 22f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        objectivesText = CreateText("Objectives", panel.transform, fontAsset, 20f, FontStyles.Normal, TextAlignmentOptions.TopLeft);

        LayoutElement summaryLayout = summaryText.gameObject.AddComponent<LayoutElement>();
        summaryLayout.preferredHeight = 200f;
        LayoutElement objectivesLayout = objectivesText.gameObject.AddComponent<LayoutElement>();
        objectivesLayout.preferredHeight = 180f;

        GameObject buttonRow = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        buttonRow.transform.SetParent(panel.transform, false);

        HorizontalLayoutGroup buttonLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 18f;
        buttonLayout.childControlHeight = false;
        buttonLayout.childControlWidth = true;
        buttonLayout.childForceExpandHeight = false;
        buttonLayout.childForceExpandWidth = true;

        retryButton = CreateButton("RetryButton", buttonRow.transform, retryButtonLabel, fontAsset, HandleRetryPressed);
        mainMenuButton = CreateButton("MainMenuButton", buttonRow.transform, mainMenuButtonLabel, fontAsset, HandleMainMenuPressed);
    }

    private void OpenResultOverlay()
    {
        EnsureOverlayCreated();
        if (overlayRoot == null || overlayCanvasGroup == null || missionSystem == null)
        {
            return;
        }

        hasOpenedResult = true;
        PopulateTexts();

        retryButton.gameObject.SetActive(showRetryButton);
        mainMenuButton.gameObject.SetActive(showMainMenuButton);

        overlayCanvasGroup.alpha = 1f;
        overlayCanvasGroup.interactable = true;
        overlayCanvasGroup.blocksRaycasts = true;

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
        if (overlayCanvasGroup == null)
        {
            return;
        }

        overlayCanvasGroup.alpha = 0f;
        overlayCanvasGroup.interactable = false;
        overlayCanvasGroup.blocksRaycasts = false;
    }

    private void PopulateTexts()
    {
        if (missionSystem == null)
        {
            return;
        }

        if (titleText != null)
        {
            titleText.text = missionSystem.State == IncidentMissionSystem.MissionState.Completed
                ? completedTitle
                : failedTitle;
        }

        if (summaryText != null)
        {
            summaryText.text = BuildSummaryText();
        }

        if (objectivesText != null)
        {
            objectivesText.text = BuildObjectivesText();
        }
    }

    private string BuildSummaryText()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(statsHeader);
        builder.Append('\n');
        builder.Append("Mission: ");
        builder.Append(missionSystem.MissionTitle);
        builder.Append('\n');
        builder.Append("State: ");
        builder.Append(missionSystem.State);
        builder.Append('\n');

        if (missionSystem.DisplayedMaximumScore > 0)
        {
            builder.Append("Score: ");
            builder.Append(missionSystem.DisplayedScore);
            builder.Append('/');
            builder.Append(missionSystem.DisplayedMaximumScore);
            if (!string.IsNullOrWhiteSpace(missionSystem.DisplayedScoreRank))
            {
                builder.Append("  [");
                builder.Append(missionSystem.DisplayedScoreRank);
                builder.Append(']');
            }

            builder.Append('\n');
        }

        builder.Append("Time: ");
        builder.Append(missionSystem.ElapsedTime.ToString("F1"));
        builder.Append("s\n");
        builder.Append("Fires: ");
        builder.Append(missionSystem.ExtinguishedFireCount);
        builder.Append('/');
        builder.Append(missionSystem.TotalTrackedFires);
        builder.Append('\n');
        builder.Append("Rescues: ");
        builder.Append(missionSystem.RescuedCount);
        builder.Append('/');
        builder.Append(missionSystem.TotalTrackedRescuables);
        builder.Append('\n');
        builder.Append("Victims: U ");
        builder.Append(missionSystem.UrgentVictimCount);
        builder.Append(" | C ");
        builder.Append(missionSystem.CriticalVictimCount);
        builder.Append(" | S ");
        builder.Append(missionSystem.StabilizedVictimCount);
        builder.Append(" | X ");
        builder.Append(missionSystem.ExtractedVictimCount);
        builder.Append(" | D ");
        builder.Append(missionSystem.DeceasedVictimCount);
        return builder.ToString();
    }

    private string BuildObjectivesText()
    {
        if (missionSystem.ObjectiveStatusCount <= 0)
        {
            return objectivesHeader + "\nNo tracked objectives.";
        }

        StringBuilder builder = new StringBuilder();
        builder.Append(objectivesHeader);
        for (int i = 0; i < missionSystem.ObjectiveStatusCount; i++)
        {
            if (!missionSystem.TryGetObjectiveStatus(i, out MissionObjectiveStatusSnapshot status))
            {
                continue;
            }

            builder.Append('\n');
            builder.Append(status.HasFailed ? "[FAILED] " : status.IsComplete ? "[DONE] " : "[ ] ");
            builder.Append(status.Summary);
            if (status.MaxScore > 0)
            {
                builder.Append(" (");
                builder.Append(status.Score);
                builder.Append('/');
                builder.Append(status.MaxScore);
                builder.Append(')');
            }
        }

        return builder.ToString();
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

    private static TMP_Text CreateText(string objectName, Transform parent, TMP_FontAsset fontAsset, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = fontAsset;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.color = new Color(0.95f, 0.97f, 1f, 1f);
        return text;
    }

    private static Button CreateButton(string objectName, Transform parent, string label, TMP_FontAsset fontAsset, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.24f, 0.37f, 0.5f, 1f);

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 54f;
        layout.flexibleWidth = 1f;

        Button button = buttonObject.GetComponent<Button>();
        if (action != null)
        {
            button.onClick.AddListener(action);
        }

        TMP_Text labelText = CreateText("Label", buttonObject.transform, fontAsset, 22f, FontStyles.Bold, TextAlignmentOptions.Center);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 8f);
        labelRect.offsetMax = new Vector2(-12f, -8f);
        labelText.text = label ?? string.Empty;
        return button;
    }
}
