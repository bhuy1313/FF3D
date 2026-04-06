using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LevelSelectSceneController : MonoBehaviour
{
    private const string RegionSuburbanLocalizationKey = "levelselect.region.suburban";
    private const string RegionCityLocalizationKey = "levelselect.region.city";
    private const string PlayButtonLocalizationKey = "levelselect.btn.play";
    private const string NoDescriptionLocalizationKey = "levelselect.placeholder.description";
    private const string NoObjectiveLocalizationKey = "levelselect.placeholder.objective";
    private const string FallbackLevelNameLocalizationKey = "levelselect.placeholder.level_name";
    private const string DifficultyNormalLocalizationKey = "levelselect.difficulty.normal";
    private const string DifficultyOptionalLocalizationKey = "levelselect.difficulty.optional";
    private const string DifficultyTbdLocalizationKey = "levelselect.difficulty.tbd";
    private const string RandomIncidentLocalizationKey = "levelselect.info.random_incident";
    private const string PossibleIncidentsLocalizationKey = "levelselect.info.possible_incidents";
    private const string SelectedScenarioLocalizationKey = "levelselect.info.selected_scenario";
    private const string SelectedScenarioObjectiveLocalizationKey = "levelselect.info.selected_scenario_objective";
    private const float ScenarioDropdownDoubleClickWindow = 0.35f;
    private static readonly Color ScenarioDropdownItemColor = new Color(0f, 0f, 0f, 1f);
    private static readonly Color ScenarioDropdownSelectedItemColor = new Color(0.15f, 0.58f, 0.22f, 1f);
    private static readonly Color ScenarioToggleSelectedTextColor = new Color32(0x77, 0xFF, 0x00, 0xFF);

    private enum RegionSelection
    {
        None = 0,
        Suburban = 1,
        City = 2
    }

    [Serializable]
    private sealed class LevelDefinition
    {
        public string buttonName;
        public string levelId;
        public string levelName;
        public string levelNameLocalizationKey;
        [TextArea(2, 4)] public string description;
        public string descriptionLocalizationKey;
        [TextArea(2, 4)] public string objective;
        public string objectiveLocalizationKey;
        public string difficulty = "Normal";
        public string difficultyLocalizationKey;
        public string caseId;
        public string targetSceneName;
        public string scenarioResourcePath;
        public ScenarioDefinition[] scenarioDefinitions = Array.Empty<ScenarioDefinition>();

        [NonSerialized] public Button button;
    }

    [Serializable]
    private sealed class ScenarioDefinition
    {
        public string scenarioId;
        public string displayName;
        public string caseId;
        public string targetSceneName;
        public string scenarioResourcePath;
    }

    [Serializable]
    private sealed class RegionCard
    {
        public string name;
        public string titleLocalizationKey;
        public RectTransform root;
        public TMP_Text title;
        public Sprite mapSprite;
        public Color placeholderColor = new Color(0.18f, 0.28f, 0.36f, 1f);
        public string[] levels = new[] { "Level 01", "Level 02", "Level 03" };
        public LevelDefinition[] levelDefinitions = Array.Empty<LevelDefinition>();

        [NonSerialized] public SlantedPanelGraphic shapeGraphic;
        [NonSerialized] public Mask mask;
        [NonSerialized] public Button button;
        [NonSerialized] public Image mapImage;
        [NonSerialized] public Image dimOverlay;
        [NonSerialized] public RectTransform levelListRoot;
        [NonSerialized] public RectTransform levelListContentRoot;
        [NonSerialized] public CanvasGroup levelListGroup;
        [NonSerialized] public RectTransform levelButtonsRoot;
        [NonSerialized] public CanvasGroup levelButtonsGroup;
        [NonSerialized] public bool isLeftCard;
        [NonSerialized] public readonly List<TMP_Text> levelLabels = new List<TMP_Text>();
        [NonSerialized] public readonly List<Button> levelButtons = new List<Button>();
        [NonSerialized] public readonly List<Vector2> levelButtonNormalizedPositions = new List<Vector2>();
    }

    [Serializable]
    private sealed class LevelInfoPopupReferences
    {
        public RectTransform root;
        public RectTransform contentRoot;
        public CanvasGroup canvasGroup;
        public TMP_Text levelNameText;
        public TMP_Text areaText;
        public TMP_Text descriptionText;
        public TMP_Text objectiveText;
        public TMP_Text difficultyText;
        public Button playButton;
        public TMP_Text playButtonLabel;
        public RectTransform scenarioDropdownRoot;
        public RectTransform scenarioDropdownContentRoot;
        public Button scenarioDropdownTemplateButton;
        public TMP_Text scenarioDropdownTemplateLabel;
        [NonSerialized] public CanvasGroup scenarioDropdownCanvasGroup;
        [NonSerialized] public Button scenarioDropdownToggleButton;
        [NonSerialized] public TMP_Text scenarioDropdownToggleLabel;
        [NonSerialized] public Color scenarioDropdownToggleDefaultTextColor;
        [NonSerialized] public CanvasGroup levelNameCanvasGroup;
        [NonSerialized] public CanvasGroup areaCanvasGroup;
        [NonSerialized] public CanvasGroup descriptionCanvasGroup;
        [NonSerialized] public CanvasGroup objectiveCanvasGroup;
        [NonSerialized] public CanvasGroup difficultyCanvasGroup;
        [NonSerialized] public CanvasGroup playButtonCanvasGroup;
        [NonSerialized] public CanvasGroup scenarioToggleButtonCanvasGroup;
        [NonSerialized] public RectTransform scenarioDropdownToggleProxyRoot;
        [NonSerialized] public Button scenarioDropdownToggleProxyButton;
    }

    [Header("Scene References")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private Image dividerImage;
    [SerializeField] private RegionCard suburbanCard;
    [SerializeField] private RegionCard cityCard;
    [SerializeField] private LevelInfoPopupReferences levelInfoPopup;
    [SerializeField] private SubMenuPanelController subMenuPanelController;

    [Header("Settings Popup")]
    [SerializeField] private GameObject settingPanelPrefab;
    [SerializeField] private GameObject toastPrefab;
    [SerializeField] private RectTransform settingsHostRoot;
    [SerializeField] private RectTransform toastHostRoot;

    [Header("Layout")]
    [SerializeField] private float expandedRatio = 0.88f;
    [SerializeField] private float collapsedRatio = 0.12f;
    [SerializeField] private float neutralRatio = 0.5f;
    [SerializeField] private float cardSpacing = 0f;
    [SerializeField] private Vector4 panelPadding = new Vector4(24f, 24f, 24f, 24f);
    [SerializeField] private float minimumRegionWidth = 96f;

    [Header("Animation")]
    [SerializeField] private float widthSmoothTime = 0.18f;
    [SerializeField] private float overlayAlphaIdle = 0.22f;
    [SerializeField] private float overlayAlphaCollapsed = 0.48f;
    [SerializeField] private float overlayAlphaSelected = 0.08f;
    [SerializeField] private float dividerAlphaIdle = 0.78f;
    [SerializeField] private float dividerAlphaSelected = 0.56f;
    [SerializeField] private float mapScaleIdle = 1.06f;
    [SerializeField] private float mapScaleCollapsed = 1.12f;
    [SerializeField] private float mapScaleSelected = 1.01f;

    [Header("Scene Intro")]
    [SerializeField] private bool playSceneIntro = true;
    [SerializeField] private float sceneIntroDelay = 0.04f;
    [SerializeField] private float sceneIntroDuration = 0.48f;
    [SerializeField] private float sceneIntroVerticalOffset = 26f;
    [SerializeField] private float sceneIntroCardHorizontalOffset = 36f;
    [SerializeField] [Range(0.85f, 1f)] private float sceneIntroStartScale = 0.985f;

    [Header("Content")]
    [SerializeField] private float outerContentPadding = 28f;
    [SerializeField] private float seamContentPadding = 34f;
    [SerializeField] private float titleTopOffset = 30f;
    [SerializeField] private float titleHeight = 72f;
    [SerializeField] private float levelListBottomOffset = 28f;
    [SerializeField] private float contentWidthPadding = 48f;
    [SerializeField] private bool preferSceneLevelTexts = true;

    [Header("Divider")]
    [SerializeField] private bool showDivider;
    [SerializeField] private float dividerAngle = 35f;
    [SerializeField] private float dividerWidth = 10f;
    [SerializeField] private float dividerHeight = 520f;
    [SerializeField] private Color dividerColor = new Color(1f, 1f, 1f, 0.78f);

    [Header("Level Info Popup")]
    [SerializeField] private float popupHorizontalOffset = 20f;
    [SerializeField] private float popupVerticalOffset = 12f;
    [SerializeField] private float popupScreenMargin = 24f;

    [Header("Level Info Motion")]
    [SerializeField] private bool animateLevelInfoPopup = true;
    [SerializeField] private float levelInfoOpenDuration = 0.26f;
    [SerializeField] private float levelInfoCloseDuration = 0.16f;
    [SerializeField] private float levelInfoVerticalIntroOffset = 20f;
    [SerializeField] [Range(0.85f, 1f)] private float levelInfoStartScale = 0.965f;
    [SerializeField] private float levelInfoContentStagger = 0.04f;
    [SerializeField] private float levelInfoContentFadeDuration = 0.14f;
    [SerializeField] private float levelInfoButtonsDelay = 0.08f;

    private const string LoadingSceneName = "LoadingScene";
    private const string CompletedLevelMarker = "✓";

    private float currentLeftRatio;
    private float targetLeftRatio;
    private float ratioVelocity;
    private RegionSelection currentSelection;
    private Vector2 lastPanelSize = Vector2.negativeInfinity;
    private LevelDefinition selectedLevelDefinition;
    private RegionCard selectedLevelCard;
    private Button selectedLevelSourceButton;
    private GameObject settingsInstance;
    private CanvasGroup settingsCanvasGroup;
    private Setting_UIScript settingsUI;
    private Button settingsBackButton;
    private ToastContainerController runtimeToastContainer;
    private CanvasGroup panelCanvasGroup;
    private Coroutine sceneIntroCoroutine;
    private Coroutine levelInfoPopupCoroutine;
    private Vector2 panelRootBaseAnchoredPosition;
    private Vector3 panelRootBaseScale = Vector3.one;
    private Vector3 levelInfoPopupBaseScale = Vector3.one;
    private float sceneIntroProgress = 1f;
    private bool isSceneIntroPlaying;
    private ScenarioDefinition selectedScenarioOverride;
    private string lastScenarioDropdownClickKey = string.Empty;
    private float lastScenarioDropdownClickTime = -10f;

    private void Awake()
    {
        if (panelRoot == null)
        {
            panelRoot = transform as RectTransform;
        }

        if (panelRoot != null)
        {
            panelRootBaseAnchoredPosition = panelRoot.anchoredPosition;
            panelRootBaseScale = panelRoot.localScale;
            panelCanvasGroup = GetOrAddComponent<CanvasGroup>(panelRoot.gameObject);
        }

        EnsureDivider();
        EnsureLevelInfoPopup();
        EnsureSubMenuPopup();
        SetupCard(suburbanCard, RegionSelection.Suburban);
        SetupCard(cityCard, RegionSelection.City);
        CloseLevelInfo(instant: true);
        CloseSubMenu(instant: true);

        currentLeftRatio = neutralRatio;
        targetLeftRatio = neutralRatio;
        InitializeSceneIntroState();
        ApplyAnimatedState(instant: true);
        RefreshLocalizedContent();
    }

    private void Start()
    {
        if (!playSceneIntro || !Application.isPlaying)
        {
            CompleteSceneIntro();
            return;
        }

        if (sceneIntroCoroutine != null)
        {
            StopCoroutine(sceneIntroCoroutine);
        }

        sceneIntroCoroutine = StartCoroutine(PlaySceneIntro());
    }

    private void OnEnable()
    {
        LanguageManager.LanguageChanged -= OnLanguageChanged;
        LanguageManager.LanguageChanged += OnLanguageChanged;
        RefreshLocalizedContent();
    }

    private void OnDisable()
    {
        LanguageManager.LanguageChanged -= OnLanguageChanged;
        StopLevelInfoPopupAnimation();
    }

    private void Update()
    {
        if (isSceneIntroPlaying)
        {
            if (panelRoot != null)
            {
                Vector2 currentSize = panelRoot.rect.size;
                if (!Mathf.Approximately(currentSize.x, lastPanelSize.x) ||
                    !Mathf.Approximately(currentSize.y, lastPanelSize.y))
                {
                    lastPanelSize = currentSize;
                    ApplyAnimatedState(instant: true);
                }
            }

            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleEscapePressed();
            return;
        }

        if (panelRoot != null)
        {
            Vector2 currentSize = panelRoot.rect.size;
            if (!Mathf.Approximately(currentSize.x, lastPanelSize.x) ||
                !Mathf.Approximately(currentSize.y, lastPanelSize.y))
            {
                lastPanelSize = currentSize;
                ApplyAnimatedState(instant: true);
                return;
            }
        }

        if (Mathf.Abs(currentLeftRatio - targetLeftRatio) < 0.0001f)
        {
            return;
        }

        ApplyAnimatedState(instant: false);
    }

    private void InitializeSceneIntroState()
    {
        sceneIntroProgress = playSceneIntro && Application.isPlaying ? 0f : 1f;
        isSceneIntroPlaying = sceneIntroProgress < 0.999f;
        ApplySceneIntroVisualState();
    }

    private IEnumerator PlaySceneIntro()
    {
        isSceneIntroPlaying = true;
        sceneIntroProgress = 0f;
        ApplyAnimatedState(instant: true);

        if (sceneIntroDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(sceneIntroDelay);
        }

        float duration = Mathf.Max(0.01f, sceneIntroDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            sceneIntroProgress = Mathf.Clamp01(elapsed / duration);
            ApplyAnimatedState(instant: true);
            yield return null;
        }

        CompleteSceneIntro();
    }

    private void CompleteSceneIntro()
    {
        if (sceneIntroCoroutine != null)
        {
            StopCoroutine(sceneIntroCoroutine);
            sceneIntroCoroutine = null;
        }

        sceneIntroProgress = 1f;
        isSceneIntroPlaying = false;
        ApplyAnimatedState(instant: true);
    }

    private void ApplySceneIntroVisualState()
    {
        float introVisibility = GetSceneIntroVisibility();

        if (panelRoot != null)
        {
            float scale = Mathf.Lerp(sceneIntroStartScale, 1f, introVisibility);
            panelRoot.anchoredPosition = panelRootBaseAnchoredPosition +
                                         new Vector2(0f, Mathf.Lerp(sceneIntroVerticalOffset, 0f, introVisibility));
            panelRoot.localScale = new Vector3(
                panelRootBaseScale.x * scale,
                panelRootBaseScale.y * scale,
                panelRootBaseScale.z);
        }

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = introVisibility;
            panelCanvasGroup.interactable = !isSceneIntroPlaying;
            panelCanvasGroup.blocksRaycasts = !isSceneIntroPlaying;
        }
    }

    private float GetSceneIntroVisibility()
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sceneIntroProgress));
    }

    private float GetSceneIntroOffset()
    {
        return (1f - GetSceneIntroVisibility()) * sceneIntroCardHorizontalOffset;
    }

    public void SelectSuburban()
    {
        SelectRegion(RegionSelection.Suburban);
    }

    public void SelectCity()
    {
        SelectRegion(RegionSelection.City);
    }

    public void ResetSelection()
    {
        SelectRegion(RegionSelection.None);
    }

    private void SelectRegion(RegionSelection selection)
    {
        CloseLevelInfo();
        currentSelection = selection;

        switch (selection)
        {
            case RegionSelection.Suburban:
                targetLeftRatio = expandedRatio;
                break;
            case RegionSelection.City:
                targetLeftRatio = collapsedRatio;
                break;
            default:
                targetLeftRatio = neutralRatio;
                break;
        }
    }

    private void EnsureDivider()
    {
        if (panelRoot == null)
        {
            return;
        }

        if (!showDivider)
        {
            if (dividerImage != null)
            {
                dividerImage.gameObject.SetActive(false);
            }

            return;
        }

        if (dividerImage == null)
        {
            Transform existing = panelRoot.Find("DividerLine");
            if (existing == null)
            {
                GameObject go = new GameObject("DividerLine", typeof(RectTransform), typeof(Image));
                RectTransform rect = go.GetComponent<RectTransform>();
                rect.SetParent(panelRoot, false);
                dividerImage = go.GetComponent<Image>();
            }
            else
            {
                dividerImage = existing.GetComponent<Image>();
                if (dividerImage == null)
                {
                    dividerImage = existing.gameObject.AddComponent<Image>();
                }
            }
        }

        if (dividerImage == null)
        {
            return;
        }

        RectTransform rectTransform = dividerImage.rectTransform;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(dividerWidth, dividerHeight);
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, dividerAngle);
        rectTransform.SetAsLastSibling();

        dividerImage.color = dividerColor;
        dividerImage.raycastTarget = false;
        dividerImage.type = Image.Type.Simple;
        dividerImage.gameObject.SetActive(true);
    }

    private void SetupCard(RegionCard card, RegionSelection selection)
    {
        if (card == null || card.root == null)
        {
            return;
        }

        card.root.anchorMin = new Vector2(0f, 0f);
        card.root.anchorMax = new Vector2(0f, 1f);
        card.root.pivot = new Vector2(0f, 0.5f);
        card.root.localScale = Vector3.one;

        card.shapeGraphic = GetOrAddComponent<SlantedPanelGraphic>(card.root.gameObject);
        card.shapeGraphic.color = new Color(0.08f, 0.1f, 0.12f, 0.96f);
        card.shapeGraphic.raycastTarget = true;
        card.isLeftCard = selection == RegionSelection.Suburban;

        card.mask = GetOrAddComponent<Mask>(card.root.gameObject);
        card.mask.showMaskGraphic = true;

        card.button = GetOrAddComponent<Button>(card.root.gameObject);
        card.button.targetGraphic = card.shapeGraphic;
        card.button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = card.button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.98f);
        colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 0.98f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        card.button.colors = colors;
        card.button.onClick.RemoveAllListeners();
        card.button.onClick.AddListener(() => SelectRegion(selection));

        card.mapImage = EnsureMapImage(card);
        card.dimOverlay = EnsureDimOverlay(card);

        if (card.title == null)
        {
            card.title = card.root.GetComponentInChildren<TMP_Text>(true);
        }

        ConfigureTitle(card.title);
        EnsureLevelList(card);
        EnsureLevelButtonsPanel(card);
        RegisterLevelButtons(card);
    }

    private Image EnsureMapImage(RegionCard card)
    {
        Transform existing = card.root.Find("MapImage");
        Image image;
        RectTransform rect;

        if (existing == null)
        {
            GameObject go = new GameObject("MapImage", typeof(RectTransform), typeof(Image));
            rect = go.GetComponent<RectTransform>();
            rect.SetParent(card.root, false);
            image = go.GetComponent<Image>();
        }
        else
        {
            rect = existing as RectTransform;
            image = existing.GetComponent<Image>();
            if (image == null)
            {
                image = existing.gameObject.AddComponent<Image>();
            }
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetSiblingIndex(0);

        if (card.mapSprite != null)
        {
            image.sprite = card.mapSprite;
        }

        image.preserveAspect = false;
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        image.color = image.sprite != null ? Color.white : card.placeholderColor;

        return image;
    }

    private Image EnsureDimOverlay(RegionCard card)
    {
        Transform existing = card.root.Find("DimOverlay");
        Image image;
        RectTransform rect;

        if (existing == null)
        {
            GameObject go = new GameObject("DimOverlay", typeof(RectTransform), typeof(Image));
            rect = go.GetComponent<RectTransform>();
            rect.SetParent(card.root, false);
            image = go.GetComponent<Image>();
        }
        else
        {
            rect = existing as RectTransform;
            image = existing.GetComponent<Image>();
            if (image == null)
            {
                image = existing.gameObject.AddComponent<Image>();
            }
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetSiblingIndex(1);

        image.color = new Color(0f, 0f, 0f, overlayAlphaIdle);
        image.raycastTarget = false;

        return image;
    }

    private void EnsureLevelList(RegionCard card)
    {
        Transform existing = card.root.Find("LevelList");
        RectTransform listRoot;

        if (existing == null)
        {
            GameObject go = new GameObject(
                "LevelList",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            listRoot = go.GetComponent<RectTransform>();
            listRoot.SetParent(card.root, false);
        }
        else
        {
            listRoot = existing as RectTransform;
            if (listRoot.GetComponent<CanvasGroup>() == null)
            {
                listRoot.gameObject.AddComponent<CanvasGroup>();
            }

            if (listRoot.GetComponent<VerticalLayoutGroup>() == null)
            {
                listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            if (listRoot.GetComponent<ContentSizeFitter>() == null)
            {
                listRoot.gameObject.AddComponent<ContentSizeFitter>();
            }
        }

        RectTransform listContentRoot = EnsureLevelListContentRoot(listRoot);

        listRoot.anchorMin = new Vector2(0f, 0f);
        listRoot.anchorMax = new Vector2(0f, 0f);
        listRoot.pivot = new Vector2(0f, 0f);
        listRoot.anchoredPosition = new Vector2(outerContentPadding, levelListBottomOffset);
        listRoot.sizeDelta = Vector2.zero;
        listRoot.SetAsLastSibling();

        card.levelListRoot = listRoot;
        card.levelListContentRoot = listContentRoot;
        card.levelListGroup = listRoot.GetComponent<CanvasGroup>();
        card.levelListGroup.alpha = 0f;
        card.levelListGroup.interactable = false;
        card.levelListGroup.blocksRaycasts = false;

        VerticalLayoutGroup rootLayout = listRoot.GetComponent<VerticalLayoutGroup>();
        rootLayout.childAlignment = TextAnchor.LowerLeft;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = false;
        rootLayout.childForceExpandHeight = false;
        rootLayout.spacing = 0f;
        rootLayout.padding = new RectOffset(24, 24, 12, 12);

        ContentSizeFitter rootFitter = listRoot.GetComponent<ContentSizeFitter>();
        rootFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        VerticalLayoutGroup contentLayout = GetOrAddComponent<VerticalLayoutGroup>(listContentRoot.gameObject);
        contentLayout.childAlignment = TextAnchor.LowerLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = false;
        contentLayout.childForceExpandHeight = false;
        contentLayout.spacing = 10f;
        contentLayout.padding = new RectOffset(0, 0, 0, 0);

        ContentSizeFitter contentFitter = GetOrAddComponent<ContentSizeFitter>(listContentRoot.gameObject);
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        card.levelLabels.Clear();

        string[] levels = GetDisplayLevels(listContentRoot, card);

        for (int i = 0; i < levels.Length; i++)
        {
            TMP_Text label = EnsureLevelLabel(listContentRoot, card.title, i);
            LevelDefinition definition = GetLevelDefinitionByIndex(card, i);
            string rawLabelText = !string.IsNullOrWhiteSpace(label.text) && preferSceneLevelTexts
                ? label.text
                : levels[i];
            label.text = FormatLevelLabel(card, definition, rawLabelText, i);

            card.levelLabels.Add(label);
        }

        for (int i = levels.Length; i < listContentRoot.childCount; i++)
        {
            Transform extra = listContentRoot.GetChild(i);
            if (extra != null)
            {
                extra.gameObject.SetActive(false);
            }
        }
    }

    private RectTransform EnsureLevelListContentRoot(RectTransform listRoot)
    {
        Transform existing = listRoot.Find("LevelListContent");
        RectTransform contentRoot;

        if (existing == null)
        {
            GameObject go = new GameObject(
                "LevelListContent",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            contentRoot = go.GetComponent<RectTransform>();
            contentRoot.SetParent(listRoot, false);
        }
        else
        {
            contentRoot = existing as RectTransform;
            if (contentRoot.GetComponent<VerticalLayoutGroup>() == null)
            {
                contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            if (contentRoot.GetComponent<ContentSizeFitter>() == null)
            {
                contentRoot.gameObject.AddComponent<ContentSizeFitter>();
            }
        }

        contentRoot.anchorMin = new Vector2(0f, 0f);
        contentRoot.anchorMax = new Vector2(0f, 0f);
        contentRoot.pivot = new Vector2(0f, 0f);
        contentRoot.anchoredPosition = Vector2.zero;
        contentRoot.sizeDelta = Vector2.zero;

        return contentRoot;
    }

    private void EnsureLevelButtonsPanel(RegionCard card)
    {
        Transform existing = card.root.Find("LevelButtonsPanel");
        if (existing == null)
        {
            card.levelButtonsRoot = null;
            card.levelButtonsGroup = null;
            return;
        }

        RectTransform panelRoot = existing as RectTransform;
        if (panelRoot == null)
        {
            card.levelButtonsRoot = null;
            card.levelButtonsGroup = null;
            return;
        }

        panelRoot.anchorMin = Vector2.zero;
        panelRoot.anchorMax = Vector2.one;
        panelRoot.offsetMin = Vector2.zero;
        panelRoot.offsetMax = Vector2.zero;
        panelRoot.SetSiblingIndex(Mathf.Max(2, panelRoot.GetSiblingIndex()));

        card.levelButtonsRoot = panelRoot;
        card.levelButtonsGroup = GetOrAddComponent<CanvasGroup>(panelRoot.gameObject);
        card.levelButtonsGroup.alpha = 0f;
        card.levelButtonsGroup.interactable = false;
        card.levelButtonsGroup.blocksRaycasts = false;
    }

    private void RegisterLevelButtons(RegionCard card)
    {
        card.levelButtons.Clear();
        card.levelButtonNormalizedPositions.Clear();
        EnsureDefaultLevelDefinitions(card);

        if (card.levelButtonsRoot == null)
        {
            return;
        }

        int definitionIndex = 0;
        for (int i = 0; i < card.levelButtonsRoot.childCount; i++)
        {
            Transform child = card.levelButtonsRoot.GetChild(i);
            if (child == null)
            {
                continue;
            }

            Button levelButton = child.GetComponent<Button>();
            if (levelButton == null)
            {
                continue;
            }

            LevelDefinition definition = GetLevelDefinition(card, levelButton, definitionIndex);
            if (definition != null)
            {
                definition.button = levelButton;
            }

            int capturedIndex = definitionIndex;
            levelButton.onClick.RemoveAllListeners();
            levelButton.onClick.AddListener(() => OnLevelButtonPressed(card, capturedIndex));
            card.levelButtons.Add(levelButton);
            card.levelButtonNormalizedPositions.Add(CaptureNormalizedButtonPosition(levelButton.transform as RectTransform, card.levelButtonsRoot));
            definitionIndex++;
        }
    }

    private void EnsureDefaultLevelDefinitions(RegionCard card)
    {
        if (card.levelDefinitions != null && card.levelDefinitions.Length > 0)
        {
            return;
        }

        int levelCount = card.levelButtonsRoot != null ? card.levelButtonsRoot.childCount : 0;
        if (levelCount <= 0)
        {
            levelCount = card.levels != null ? card.levels.Length : 0;
        }

        if (levelCount <= 0)
        {
            return;
        }

        card.levelDefinitions = new LevelDefinition[levelCount];
        for (int i = 0; i < levelCount; i++)
        {
            string fallbackLevelName = GetFallbackLevelName(card, i);

            card.levelDefinitions[i] = new LevelDefinition
            {
                buttonName = $"LevelButton_{i + 1:00}",
                levelId = $"{card.name.ToLowerInvariant()}_{i + 1:00}",
                levelName = fallbackLevelName,
                description = string.Empty,
                objective = string.Empty,
                difficulty = string.Empty,
                caseId = string.Empty,
                targetSceneName = string.Empty,
                scenarioResourcePath = string.Empty
            };
        }
    }

    private string GetFallbackLevelName(RegionCard card, int index)
    {
        if (card.levels != null && index >= 0 && index < card.levels.Length)
        {
            return card.levels[index];
        }

        if (card.levelButtonsRoot != null && index >= 0 && index < card.levelButtonsRoot.childCount)
        {
            Button button = card.levelButtonsRoot.GetChild(index).GetComponent<Button>();
            string label = GetButtonLabel(button);
            if (!string.IsNullOrWhiteSpace(label))
            {
                return SanitizeLevelName(label);
            }
        }

        return $"{LanguageManager.Tr(FallbackLevelNameLocalizationKey, "Level")} {index + 1:00}";
    }

    private LevelDefinition GetLevelDefinition(RegionCard card, Button levelButton, int fallbackIndex)
    {
        if (card.levelDefinitions == null || card.levelDefinitions.Length == 0)
        {
            return null;
        }

        string buttonName = levelButton != null ? levelButton.name : string.Empty;
        for (int i = 0; i < card.levelDefinitions.Length; i++)
        {
            LevelDefinition definition = card.levelDefinitions[i];
            if (definition == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(definition.buttonName) &&
                string.Equals(definition.buttonName, buttonName, StringComparison.Ordinal))
            {
                return definition;
            }
        }

        if (fallbackIndex >= 0 && fallbackIndex < card.levelDefinitions.Length)
        {
            return card.levelDefinitions[fallbackIndex];
        }

        return null;
    }

    private LevelDefinition GetLevelDefinitionByIndex(RegionCard card, int index)
    {
        if (card == null || card.levelDefinitions == null || index < 0 || index >= card.levelDefinitions.Length)
        {
            return null;
        }

        return card.levelDefinitions[index];
    }

    private void OnLevelButtonPressed(RegionCard card, int definitionIndex)
    {
        if (card == null || card.levelButtons == null || definitionIndex < 0)
        {
            return;
        }

        Button sourceButton = definitionIndex < card.levelButtons.Count ? card.levelButtons[definitionIndex] : null;
        LevelDefinition definition = GetLevelDefinition(card, sourceButton, definitionIndex);
        if (definition == null)
        {
            return;
        }

        OpenLevelInfo(card, definition, sourceButton);
    }

    private void HandleEscapePressed()
    {
        if (IsSettingsOpen())
        {
            RequestCloseSettings();
            return;
        }

        if (IsScenarioDropdownOpen())
        {
            SetScenarioDropdownVisible(false);
            return;
        }

        if (IsSubMenuOpen())
        {
            CloseSubMenu();
            return;
        }

        if (IsLevelInfoOpen())
        {
            CloseLevelInfo();
            return;
        }

        OpenSubMenu();
    }

    private void EnsureLevelInfoPopup()
    {
        if (levelInfoPopup.root == null)
        {
            levelInfoPopup.root = FindPopupRect("LevelInfoPanel");
        }

        if (levelInfoPopup.root == null)
        {
            return;
        }

        if (levelInfoPopup.contentRoot == null)
        {
            levelInfoPopup.contentRoot = FindNestedRect(levelInfoPopup.root, "InfoContentPanel");
        }

        if (levelInfoPopup.levelNameText == null)
        {
            levelInfoPopup.levelNameText = FindNestedText(levelInfoPopup.root, "LevelName");
        }

        if (levelInfoPopup.areaText == null)
        {
            levelInfoPopup.areaText = FindNestedText(levelInfoPopup.root, "Area");
        }

        if (levelInfoPopup.descriptionText == null)
        {
            levelInfoPopup.descriptionText = FindNestedText(levelInfoPopup.root, "Description");
        }

        if (levelInfoPopup.objectiveText == null)
        {
            levelInfoPopup.objectiveText = FindNestedText(levelInfoPopup.root, "Objective");
        }

        if (levelInfoPopup.difficultyText == null)
        {
            levelInfoPopup.difficultyText = FindNestedText(levelInfoPopup.root, "Difficulty");
        }

        if (levelInfoPopup.playButton == null)
        {
            levelInfoPopup.playButton = FindNestedButton(levelInfoPopup.root, "BtnPlay");
        }

        if (levelInfoPopup.playButtonLabel == null && levelInfoPopup.playButton != null)
        {
            levelInfoPopup.playButtonLabel = levelInfoPopup.playButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (levelInfoPopup.scenarioDropdownRoot == null)
        {
            levelInfoPopup.scenarioDropdownRoot = FindNestedRect(levelInfoPopup.root, "customDropdown");
        }

        if (levelInfoPopup.scenarioDropdownContentRoot == null && levelInfoPopup.scenarioDropdownRoot != null)
        {
            levelInfoPopup.scenarioDropdownContentRoot = FindNestedRect(levelInfoPopup.scenarioDropdownRoot, "Content");
        }

        if (levelInfoPopup.scenarioDropdownTemplateButton == null && levelInfoPopup.scenarioDropdownContentRoot != null)
        {
            levelInfoPopup.scenarioDropdownTemplateButton = levelInfoPopup.scenarioDropdownContentRoot.GetComponentInChildren<Button>(true);
        }

        if (levelInfoPopup.scenarioDropdownTemplateLabel == null && levelInfoPopup.scenarioDropdownTemplateButton != null)
        {
            levelInfoPopup.scenarioDropdownTemplateLabel = levelInfoPopup.scenarioDropdownTemplateButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (levelInfoPopup.scenarioDropdownToggleButton == null)
        {
            levelInfoPopup.scenarioDropdownToggleButton = FindNestedButton(levelInfoPopup.root, "btnSelectManual");
        }

        if (levelInfoPopup.scenarioDropdownToggleLabel == null && levelInfoPopup.scenarioDropdownToggleButton != null)
        {
            levelInfoPopup.scenarioDropdownToggleLabel = levelInfoPopup.scenarioDropdownToggleButton.GetComponentInChildren<TMP_Text>(true);
            if (levelInfoPopup.scenarioDropdownToggleLabel != null)
            {
                levelInfoPopup.scenarioDropdownToggleDefaultTextColor = levelInfoPopup.scenarioDropdownToggleLabel.color;
            }
        }

        levelInfoPopup.root.gameObject.SetActive(true);
        levelInfoPopupBaseScale = levelInfoPopup.root.localScale;
        levelInfoPopup.canvasGroup = GetOrAddComponent<CanvasGroup>(levelInfoPopup.root.gameObject);
        levelInfoPopup.canvasGroup.alpha = 0f;
        levelInfoPopup.canvasGroup.interactable = false;
        levelInfoPopup.canvasGroup.blocksRaycasts = false;

        if (levelInfoPopup.scenarioDropdownRoot != null)
        {
            levelInfoPopup.scenarioDropdownRoot.gameObject.SetActive(true);
            levelInfoPopup.scenarioDropdownCanvasGroup = GetOrAddComponent<CanvasGroup>(levelInfoPopup.scenarioDropdownRoot.gameObject);
            SetScenarioDropdownVisible(false);
        }

        if (levelInfoPopup.playButton != null)
        {
            levelInfoPopup.playButton.onClick.RemoveAllListeners();
            levelInfoPopup.playButton.onClick.AddListener(PlaySelectedLevel);
            levelInfoPopup.playButtonCanvasGroup = GetOrAddComponent<CanvasGroup>(levelInfoPopup.playButton.gameObject);
        }

        levelInfoPopup.levelNameCanvasGroup = GetCanvasGroup(levelInfoPopup.levelNameText);
        levelInfoPopup.areaCanvasGroup = GetCanvasGroup(levelInfoPopup.areaText);
        levelInfoPopup.descriptionCanvasGroup = GetCanvasGroup(levelInfoPopup.descriptionText);
        levelInfoPopup.objectiveCanvasGroup = GetCanvasGroup(levelInfoPopup.objectiveText);
        levelInfoPopup.difficultyCanvasGroup = GetCanvasGroup(levelInfoPopup.difficultyText);
        levelInfoPopup.scenarioToggleButtonCanvasGroup = levelInfoPopup.scenarioDropdownToggleButton != null
            ? GetOrAddComponent<CanvasGroup>(levelInfoPopup.scenarioDropdownToggleButton.gameObject)
            : null;

        EnsureScenarioDropdownToggleProxy();
        EnsureScenarioDropdownToggleButton();
        ApplyLevelInfoContentAlpha(0f, 0f);
        RefreshScenarioDropdownToggleVisualState();
    }

    private void EnsureSubMenuPopup()
    {
        if (subMenuPanelController == null)
        {
            RectTransform subMenuRoot = FindPopupRect("SubMenuPanel");
            if (subMenuRoot != null)
            {
                subMenuPanelController = GetOrAddComponent<SubMenuPanelController>(subMenuRoot.gameObject);
            }
        }

        if (subMenuPanelController == null)
        {
            return;
        }

        // LevelSelect has its own Escape flow (submenu, settings, level info).
        // Disable the prefab-level Escape host here to avoid double toggles in one frame.
        SubMenuEscapeHost escapeHost = subMenuPanelController.GetComponent<SubMenuEscapeHost>();
        if (escapeHost != null)
        {
            escapeHost.enabled = false;
        }

        subMenuPanelController.EnsureInitialized();
        subMenuPanelController.SetResumeAction(CloseSubMenu);
        subMenuPanelController.SetQuitAction(QuitApplication);
        subMenuPanelController.SetMainMenuAction(ReturnToMainMenu);
        subMenuPanelController.SetSettingsAction(OpenSettingsPanel);

        if (subMenuPanelController.SettingsButton == null)
        {
            Debug.LogWarning("LevelSelectSceneController: Could not resolve Settings button in SubMenuPanel.", this);
        }
    }

    private void OpenLevelInfo(RegionCard card, LevelDefinition definition, Button sourceButton)
    {
        EnsureLevelInfoPopup();
        if (levelInfoPopup.root == null || definition == null)
        {
            return;
        }

        selectedLevelCard = card;
        selectedLevelDefinition = definition;
        selectedLevelSourceButton = sourceButton;
        selectedScenarioOverride = null;
        ResetScenarioDropdownClickState();

        BuildScenarioDropdown(definition);
        ApplyLevelInfoTexts(card, definition, sourceButton);
        RefreshLevelInfoLayout();
        RefreshPlayButtonState(definition);
        RefreshScenarioDropdownToggleVisualState();

        PositionLevelInfoPopupNearSource(sourceButton);
        Vector2 targetPosition = levelInfoPopup.root.anchoredPosition;
        StopLevelInfoPopupAnimation();

        if (!Application.isPlaying || !animateLevelInfoPopup)
        {
            ShowLevelInfoImmediately(targetPosition);
            return;
        }

        levelInfoPopupCoroutine = StartCoroutine(AnimateOpenLevelInfoPopup(targetPosition));
    }

    private void CloseLevelInfo(bool instant = false)
    {
        if (levelInfoPopup.root == null || levelInfoPopup.canvasGroup == null)
        {
            return;
        }

        StopLevelInfoPopupAnimation();
        levelInfoPopup.canvasGroup.interactable = false;
        levelInfoPopup.canvasGroup.blocksRaycasts = false;

        if (!instant)
        {
            selectedLevelCard = null;
            selectedLevelDefinition = null;
        }

        selectedScenarioOverride = null;
        ResetScenarioDropdownClickState();
        SetScenarioDropdownVisible(false);
        selectedLevelSourceButton = null;
        RefreshScenarioDropdownToggleVisualState();

        if (instant || !Application.isPlaying || !animateLevelInfoPopup)
        {
            HideLevelInfoImmediately();
            return;
        }

        levelInfoPopupCoroutine = StartCoroutine(AnimateCloseLevelInfoPopup());
    }

    private void StopLevelInfoPopupAnimation()
    {
        if (levelInfoPopupCoroutine == null)
        {
            return;
        }

        StopCoroutine(levelInfoPopupCoroutine);
        levelInfoPopupCoroutine = null;
    }

    private void ShowLevelInfoImmediately(Vector2 targetPosition)
    {
        if (levelInfoPopup.root == null || levelInfoPopup.canvasGroup == null)
        {
            return;
        }

        levelInfoPopup.root.anchoredPosition = targetPosition;
        levelInfoPopup.root.localScale = levelInfoPopupBaseScale;
        levelInfoPopup.canvasGroup.alpha = 1f;
        levelInfoPopup.canvasGroup.interactable = true;
        levelInfoPopup.canvasGroup.blocksRaycasts = true;
        ApplyLevelInfoContentAlpha(1f, 1f);
    }

    private void HideLevelInfoImmediately()
    {
        if (levelInfoPopup.root == null || levelInfoPopup.canvasGroup == null)
        {
            return;
        }

        levelInfoPopup.root.localScale = levelInfoPopupBaseScale;
        levelInfoPopup.canvasGroup.alpha = 0f;
        levelInfoPopup.canvasGroup.interactable = false;
        levelInfoPopup.canvasGroup.blocksRaycasts = false;
        ApplyLevelInfoContentAlpha(0f, 0f);
    }

    private IEnumerator AnimateOpenLevelInfoPopup(Vector2 targetPosition)
    {
        levelInfoPopup.canvasGroup.alpha = 0f;
        levelInfoPopup.canvasGroup.interactable = false;
        levelInfoPopup.canvasGroup.blocksRaycasts = false;
        levelInfoPopup.root.localScale = levelInfoPopupBaseScale * levelInfoStartScale;
        levelInfoPopup.root.anchoredPosition = targetPosition + Vector2.down * levelInfoVerticalIntroOffset;
        ApplyLevelInfoContentAlpha(0f, 0f);

        float duration = Mathf.Max(0.01f, levelInfoOpenDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutQuart(progress);
            levelInfoPopup.canvasGroup.alpha = eased;
            levelInfoPopup.root.localScale = Vector3.LerpUnclamped(levelInfoPopupBaseScale * levelInfoStartScale, levelInfoPopupBaseScale, eased);
            levelInfoPopup.root.anchoredPosition = Vector2.LerpUnclamped(
                targetPosition + Vector2.down * levelInfoVerticalIntroOffset,
                targetPosition,
                eased);

            ApplyLevelInfoSequenceAlpha(GetLevelInfoTextGroups(), elapsed, 0f);
            ApplyLevelInfoSequenceAlpha(GetLevelInfoButtonGroups(), elapsed, levelInfoButtonsDelay);
            yield return null;
        }

        ShowLevelInfoImmediately(targetPosition);
        levelInfoPopupCoroutine = null;
    }

    private IEnumerator AnimateCloseLevelInfoPopup()
    {
        Vector2 startPosition = levelInfoPopup.root.anchoredPosition;
        Vector2 endPosition = startPosition + Vector2.down * (levelInfoVerticalIntroOffset * 0.35f);
        Vector3 startScale = levelInfoPopup.root.localScale;
        Vector3 endScale = levelInfoPopupBaseScale * Mathf.Lerp(levelInfoStartScale, 1f, 0.55f);
        float startAlpha = levelInfoPopup.canvasGroup.alpha;
        float duration = Mathf.Max(0.01f, levelInfoCloseDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutCubic(progress);
            float visibility = 1f - eased;
            levelInfoPopup.canvasGroup.alpha = startAlpha * visibility;
            levelInfoPopup.root.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            levelInfoPopup.root.anchoredPosition = Vector2.LerpUnclamped(startPosition, endPosition, eased);
            ApplyLevelInfoContentAlpha(visibility, visibility);
            yield return null;
        }

        HideLevelInfoImmediately();
        levelInfoPopupCoroutine = null;
    }

    private void ApplyLevelInfoContentAlpha(float textAlpha, float buttonAlpha)
    {
        SetCanvasGroupAlpha(levelInfoPopup.levelNameCanvasGroup, textAlpha);
        SetCanvasGroupAlpha(levelInfoPopup.areaCanvasGroup, textAlpha);
        SetCanvasGroupAlpha(levelInfoPopup.descriptionCanvasGroup, textAlpha);
        SetCanvasGroupAlpha(levelInfoPopup.objectiveCanvasGroup, textAlpha);
        SetCanvasGroupAlpha(levelInfoPopup.difficultyCanvasGroup, textAlpha);
        SetCanvasGroupAlpha(levelInfoPopup.scenarioToggleButtonCanvasGroup, buttonAlpha);
        SetCanvasGroupAlpha(levelInfoPopup.playButtonCanvasGroup, buttonAlpha);
    }

    private void ApplyLevelInfoSequenceAlpha(CanvasGroup[] groups, float elapsed, float baseDelay)
    {
        float fadeDuration = Mathf.Max(0.01f, levelInfoContentFadeDuration);
        float stagger = Mathf.Max(0f, levelInfoContentStagger);

        for (int i = 0; i < groups.Length; i++)
        {
            float visibility = EaseOutCubic(Mathf.Clamp01((elapsed - baseDelay - i * stagger) / fadeDuration));
            SetCanvasGroupAlpha(groups[i], visibility);
        }
    }

    private CanvasGroup[] GetLevelInfoTextGroups()
    {
        return new[]
        {
            levelInfoPopup.levelNameCanvasGroup,
            levelInfoPopup.areaCanvasGroup,
            levelInfoPopup.descriptionCanvasGroup,
            levelInfoPopup.objectiveCanvasGroup,
            levelInfoPopup.difficultyCanvasGroup
        };
    }

    private CanvasGroup[] GetLevelInfoButtonGroups()
    {
        return new[]
        {
            levelInfoPopup.scenarioToggleButtonCanvasGroup,
            levelInfoPopup.playButtonCanvasGroup
        };
    }

    private void OpenSubMenu()
    {
        EnsureSubMenuPopup();
        if (subMenuPanelController == null)
        {
            return;
        }

        subMenuPanelController.Open();
    }

    private void CloseSubMenu()
    {
        CloseSubMenu(false);
    }

    private void CloseSubMenu(bool instant)
    {
        if (subMenuPanelController == null)
        {
            return;
        }

        subMenuPanelController.Close();
    }

    private bool IsLevelInfoOpen()
    {
        return levelInfoPopup != null &&
               levelInfoPopup.canvasGroup != null &&
               levelInfoPopup.canvasGroup.alpha > 0.001f;
    }

    private bool IsSubMenuOpen()
    {
        return subMenuPanelController != null && subMenuPanelController.IsOpen;
    }

    private void OpenSettingsPanel()
    {
        if (!EnsureSettingsPanel())
        {
            return;
        }

        CloseLevelInfo();
        CloseSubMenu();

        if (settingsUI != null)
        {
            settingsUI.BeginEditSession();
        }

        SetSettingsVisible(true);
    }

    private void RequestCloseSettings()
    {
        if (settingsUI != null && settingsUI.HandleBackRequest(CloseSettingsImmediate))
        {
            return;
        }

        CloseSettingsImmediate();
    }

    private void CloseSettingsImmediate()
    {
        SetSettingsVisible(false);
        OpenSubMenu();
    }

    private bool IsSettingsOpen()
    {
        return settingsCanvasGroup != null && settingsCanvasGroup.alpha > 0.001f;
    }

    private bool EnsureSettingsPanel()
    {
        if (settingsInstance == null)
        {
            if (settingPanelPrefab == null)
            {
                Debug.LogWarning("LevelSelectSceneController: Setting panel prefab is not assigned.", this);
                return false;
            }

            RectTransform host = settingsHostRoot != null ? settingsHostRoot : GetCanvasRect();
            if (host == null)
            {
                Debug.LogWarning("LevelSelectSceneController: Could not resolve a host canvas for Setting panel.", this);
                return false;
            }

            settingsInstance = Instantiate(settingPanelPrefab, host);
            settingsInstance.name = settingPanelPrefab.name;
            settingsInstance.transform.SetAsLastSibling();
        }

        if (settingsCanvasGroup == null)
        {
            settingsCanvasGroup = settingsInstance.GetComponent<CanvasGroup>();
            if (settingsCanvasGroup == null)
            {
                settingsCanvasGroup = settingsInstance.AddComponent<CanvasGroup>();
            }
        }

        if (settingsUI == null)
        {
            settingsUI = settingsInstance.GetComponentInChildren<Setting_UIScript>(true);
            if (settingsUI == null)
            {
                Debug.LogWarning("LevelSelectSceneController: Setting panel instance is missing Setting_UIScript.", settingsInstance);
            }
        }

        if (settingsBackButton == null)
        {
            settingsBackButton = FindNestedButton(settingsInstance.transform, "btnBack");
            if (settingsBackButton == null)
            {
                Debug.LogWarning("LevelSelectSceneController: Could not find btnBack on Setting panel instance.", settingsInstance);
            }
            else
            {
                settingsBackButton.onClick.RemoveAllListeners();
                settingsBackButton.onClick.AddListener(RequestCloseSettings);
            }
        }

        EnsureRuntimeToastContainer();
        SetSettingsVisible(false);
        return true;
    }

    private void EnsureRuntimeToastContainer()
    {
        if (runtimeToastContainer != null)
        {
            return;
        }

        runtimeToastContainer = FindAnyObjectByType<ToastContainerController>();
        if (runtimeToastContainer != null)
        {
            return;
        }

        if (toastPrefab == null)
        {
            Debug.LogWarning("LevelSelectSceneController: Toast prefab is not assigned. Settings back confirmation will be unavailable if changes are unsaved.", this);
            return;
        }

        RectTransform host = toastHostRoot != null ? toastHostRoot : GetCanvasRect();
        if (host == null)
        {
            Debug.LogWarning("LevelSelectSceneController: Could not resolve a host canvas for Toast container.", this);
            return;
        }

        GameObject go = new GameObject("Toast Container", typeof(RectTransform), typeof(ToastContainerController));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(host, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetAsLastSibling();

        runtimeToastContainer = go.GetComponent<ToastContainerController>();
        runtimeToastContainer.Configure(toastPrefab, rect);
    }

    private void SetSettingsVisible(bool visible)
    {
        if (settingsInstance == null || settingsCanvasGroup == null)
        {
            return;
        }

        if (visible)
        {
            settingsInstance.transform.SetAsLastSibling();
        }

        settingsCanvasGroup.alpha = visible ? 1f : 0f;
        settingsCanvasGroup.interactable = visible;
        settingsCanvasGroup.blocksRaycasts = visible;
    }

    private void PlaySelectedLevel()
    {
        if (selectedLevelDefinition == null || !HasPlayableRoute(selectedLevelDefinition))
        {
            return;
        }

        ScenarioDefinition scenario = IsPlayableScenario(selectedLevelDefinition, selectedScenarioOverride)
            ? selectedScenarioOverride
            : GetRandomPlayableScenario(selectedLevelDefinition);
        string targetSceneName = ResolveTargetSceneName(selectedLevelDefinition, scenario);
        if (!IsPlayableScene(targetSceneName))
        {
            return;
        }

        LoadingFlowState.SetPendingTargetScene(targetSceneName);
        if (!string.IsNullOrWhiteSpace(selectedLevelDefinition.levelId))
        {
            LoadingFlowState.SetCurrentLevelId(selectedLevelDefinition.levelId);
        }
        else
        {
            LoadingFlowState.ClearCurrentLevelId();
        }

        string scenarioResourcePath = ResolveScenarioResourcePath(selectedLevelDefinition, scenario);
        if (!string.IsNullOrWhiteSpace(scenarioResourcePath))
        {
            LoadingFlowState.SetPendingScenarioResourcePath(scenarioResourcePath);
        }
        else
        {
            LoadingFlowState.ClearPendingScenarioResourcePath();
        }

        string caseId = ResolveCaseId(selectedLevelDefinition, scenario);
        if (!string.IsNullOrWhiteSpace(caseId))
        {
            LoadingFlowState.SetPendingCaseId(caseId);
        }
        else
        {
            LoadingFlowState.ClearPendingCaseId();
        }

        CloseLevelInfo();
        SceneManager.LoadScene(LoadingSceneName);
    }

    private bool HasPlayableRoute(LevelDefinition definition)
    {
        if (definition == null)
        {
            return false;
        }

        if (HasPlayableScenarioRoute(definition))
        {
            return true;
        }

        return IsPlayableScene(definition.targetSceneName);
    }

    private bool HasPlayableScenarioRoute(LevelDefinition definition)
    {
        if (definition == null || definition.scenarioDefinitions == null || definition.scenarioDefinitions.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < definition.scenarioDefinitions.Length; i++)
        {
            if (IsPlayableScenario(definition, definition.scenarioDefinitions[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasMultipleConfiguredScenarios(LevelDefinition definition)
    {
        return GetConfiguredScenarioCount(definition) > 1;
    }

    private ScenarioDefinition GetRandomPlayableScenario(LevelDefinition definition)
    {
        if (!HasPlayableScenarioRoute(definition))
        {
            return null;
        }

        int playableScenarioCount = 0;
        for (int i = 0; i < definition.scenarioDefinitions.Length; i++)
        {
            if (IsPlayableScenario(definition, definition.scenarioDefinitions[i]))
            {
                playableScenarioCount++;
            }
        }

        if (playableScenarioCount <= 0)
        {
            return null;
        }

        int selectedIndex = UnityEngine.Random.Range(0, playableScenarioCount);
        int currentIndex = 0;

        for (int i = 0; i < definition.scenarioDefinitions.Length; i++)
        {
            ScenarioDefinition scenario = definition.scenarioDefinitions[i];
            if (!IsPlayableScenario(definition, scenario))
            {
                continue;
            }

            if (currentIndex == selectedIndex)
            {
                return scenario;
            }

            currentIndex++;
        }

        return null;
    }

    private bool IsPlayableScenario(LevelDefinition definition, ScenarioDefinition scenario)
    {
        return scenario != null && IsPlayableScene(ResolveTargetSceneName(definition, scenario));
    }

    private static bool IsPlayableScene(string sceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName) &&
               Application.CanStreamedLevelBeLoaded(sceneName.Trim());
    }

    private static string ResolveTargetSceneName(LevelDefinition definition, ScenarioDefinition scenario)
    {
        if (scenario != null && !string.IsNullOrWhiteSpace(scenario.targetSceneName))
        {
            return scenario.targetSceneName.Trim();
        }

        return definition != null && !string.IsNullOrWhiteSpace(definition.targetSceneName)
            ? definition.targetSceneName.Trim()
            : string.Empty;
    }

    private static string ResolveScenarioResourcePath(LevelDefinition definition, ScenarioDefinition scenario)
    {
        if (scenario != null && !string.IsNullOrWhiteSpace(scenario.scenarioResourcePath))
        {
            return scenario.scenarioResourcePath.Trim();
        }

        return definition != null && !string.IsNullOrWhiteSpace(definition.scenarioResourcePath)
            ? definition.scenarioResourcePath.Trim()
            : string.Empty;
    }

    private static string ResolveCaseId(LevelDefinition definition, ScenarioDefinition scenario)
    {
        if (scenario != null && !string.IsNullOrWhiteSpace(scenario.caseId))
        {
            return scenario.caseId.Trim();
        }

        return definition != null && !string.IsNullOrWhiteSpace(definition.caseId)
            ? definition.caseId.Trim()
            : string.Empty;
    }

    private void EnsureScenarioDropdownToggleButton()
    {
        if (levelInfoPopup.scenarioDropdownToggleButton == null)
        {
            return;
        }

        Button toggleButton = levelInfoPopup.scenarioDropdownToggleButton;
        toggleButton.onClick.RemoveAllListeners();
        toggleButton.onClick.AddListener(ToggleScenarioDropdown);
    }

    private void EnsureScenarioDropdownToggleProxy()
    {
        if (levelInfoPopup == null || levelInfoPopup.root == null)
        {
            return;
        }

        if (levelInfoPopup.scenarioDropdownToggleProxyRoot == null)
        {
            Transform existing = levelInfoPopup.root.Find("ScenarioDropdownToggleProxy");
            if (existing != null)
            {
                levelInfoPopup.scenarioDropdownToggleProxyRoot = existing as RectTransform;
            }
        }

        if (levelInfoPopup.scenarioDropdownToggleProxyRoot == null)
        {
            GameObject proxyObject = new GameObject("ScenarioDropdownToggleProxy", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            RectTransform proxyRect = proxyObject.GetComponent<RectTransform>();
            proxyRect.SetParent(levelInfoPopup.root, false);
            proxyRect.anchorMin = new Vector2(0.5f, 0.5f);
            proxyRect.anchorMax = new Vector2(0.5f, 0.5f);
            proxyRect.pivot = new Vector2(0.5f, 0.5f);
            proxyRect.sizeDelta = new Vector2(30f, 30f);

            Image proxyImage = proxyObject.GetComponent<Image>();
            proxyImage.color = new Color(1f, 1f, 1f, 0f);
            proxyImage.raycastTarget = true;

            LayoutElement layoutElement = proxyObject.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
            layoutElement.minWidth = 30f;
            layoutElement.minHeight = 30f;
            layoutElement.preferredWidth = 30f;
            layoutElement.preferredHeight = 30f;

            levelInfoPopup.scenarioDropdownToggleProxyRoot = proxyRect;
        }

        levelInfoPopup.scenarioDropdownToggleProxyButton = levelInfoPopup.scenarioDropdownToggleProxyRoot.GetComponent<Button>();
        if (levelInfoPopup.scenarioDropdownToggleProxyButton == null)
        {
            levelInfoPopup.scenarioDropdownToggleProxyButton = levelInfoPopup.scenarioDropdownToggleProxyRoot.gameObject.AddComponent<Button>();
        }

        levelInfoPopup.scenarioDropdownToggleProxyButton.onClick.RemoveAllListeners();
        levelInfoPopup.scenarioDropdownToggleProxyButton.onClick.AddListener(CloseScenarioDropdownFromProxy);
        levelInfoPopup.scenarioDropdownToggleProxyRoot.gameObject.SetActive(false);
    }

    private void ToggleScenarioDropdown()
    {
        if (selectedLevelDefinition == null || !HasMultipleConfiguredScenarios(selectedLevelDefinition))
        {
            return;
        }

        BuildScenarioDropdown(selectedLevelDefinition);
        SetScenarioDropdownVisible(!IsScenarioDropdownOpen());
    }

    private bool IsScenarioDropdownOpen()
    {
        return levelInfoPopup != null &&
               levelInfoPopup.scenarioDropdownCanvasGroup != null &&
               levelInfoPopup.scenarioDropdownCanvasGroup.alpha > 0.001f;
    }

    private void SetScenarioDropdownVisible(bool visible)
    {
        if (levelInfoPopup == null ||
            levelInfoPopup.scenarioDropdownRoot == null ||
            levelInfoPopup.scenarioDropdownCanvasGroup == null)
        {
            return;
        }

        levelInfoPopup.scenarioDropdownRoot.gameObject.SetActive(true);
        if (visible)
        {
            levelInfoPopup.scenarioDropdownRoot.SetAsLastSibling();
        }

        levelInfoPopup.scenarioDropdownCanvasGroup.alpha = visible ? 1f : 0f;
        levelInfoPopup.scenarioDropdownCanvasGroup.interactable = visible;
        levelInfoPopup.scenarioDropdownCanvasGroup.blocksRaycasts = visible;
        SetScenarioDropdownToggleProxyVisible(visible);
    }

    private void SetScenarioDropdownToggleProxyVisible(bool visible)
    {
        if (levelInfoPopup == null ||
            levelInfoPopup.scenarioDropdownToggleProxyRoot == null ||
            levelInfoPopup.scenarioDropdownToggleButton == null)
        {
            return;
        }

        if (!visible)
        {
            levelInfoPopup.scenarioDropdownToggleProxyRoot.gameObject.SetActive(false);
            return;
        }

        RectTransform toggleRect = levelInfoPopup.scenarioDropdownToggleButton.transform as RectTransform;
        if (toggleRect == null)
        {
            return;
        }

        RectTransform proxyRect = levelInfoPopup.scenarioDropdownToggleProxyRoot;
        RectTransform toggleParent = toggleRect.parent as RectTransform;
        if (toggleParent == null)
        {
            return;
        }

        if (proxyRect.parent != toggleParent)
        {
            proxyRect.SetParent(toggleParent, false);
        }

        proxyRect.anchorMin = toggleRect.anchorMin;
        proxyRect.anchorMax = toggleRect.anchorMax;
        proxyRect.pivot = toggleRect.pivot;
        proxyRect.anchoredPosition = toggleRect.anchoredPosition;
        proxyRect.sizeDelta = new Vector2(30f, 30f);
        proxyRect.localRotation = toggleRect.localRotation;
        proxyRect.localScale = Vector3.one;
        proxyRect.SetAsLastSibling();
        proxyRect.gameObject.SetActive(true);
    }

    private void CloseScenarioDropdownFromProxy()
    {
        if (!IsScenarioDropdownOpen())
        {
            return;
        }

        ResetScenarioDropdownClickState();
        SetScenarioDropdownVisible(false);
    }

    private void BuildScenarioDropdown(LevelDefinition definition)
    {
        ScenarioDefinition[] scenarios = GetConfiguredScenarios(definition);
        bool hasManualChoices = scenarios.Length > 1;

        if (levelInfoPopup.scenarioDropdownToggleButton != null)
        {
            levelInfoPopup.scenarioDropdownToggleButton.interactable = hasManualChoices;
        }

        RefreshScenarioDropdownToggleVisualState();

        if (levelInfoPopup.scenarioDropdownContentRoot == null || levelInfoPopup.scenarioDropdownTemplateButton == null)
        {
            SetScenarioDropdownVisible(false);
            return;
        }

        for (int i = 0; i < scenarios.Length; i++)
        {
            Button itemButton = GetOrCreateScenarioDropdownItem(i);
            if (itemButton == null)
            {
                continue;
            }

            TMP_Text label = itemButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = ResolveScenarioDisplayName(scenarios[i], definition);
            }

            ScenarioDefinition capturedScenario = scenarios[i];
            itemButton.onClick.RemoveAllListeners();
            itemButton.onClick.AddListener(() => OnScenarioDropdownItemClicked(definition, capturedScenario));
            itemButton.gameObject.SetActive(true);
            ApplyScenarioDropdownItemVisual(itemButton, capturedScenario);
        }

        for (int i = scenarios.Length; i < levelInfoPopup.scenarioDropdownContentRoot.childCount; i++)
        {
            Transform extra = levelInfoPopup.scenarioDropdownContentRoot.GetChild(i);
            if (extra != null)
            {
                extra.gameObject.SetActive(false);
            }
        }

        SetScenarioDropdownVisible(false);
    }

    private Button GetOrCreateScenarioDropdownItem(int index)
    {
        if (levelInfoPopup.scenarioDropdownContentRoot == null || levelInfoPopup.scenarioDropdownTemplateButton == null)
        {
            return null;
        }

        Transform existing = index < levelInfoPopup.scenarioDropdownContentRoot.childCount
            ? levelInfoPopup.scenarioDropdownContentRoot.GetChild(index)
            : null;

        GameObject itemObject;
        if (existing == null)
        {
            itemObject = Instantiate(levelInfoPopup.scenarioDropdownTemplateButton.gameObject, levelInfoPopup.scenarioDropdownContentRoot);
            itemObject.name = $"{levelInfoPopup.scenarioDropdownTemplateButton.gameObject.name}_{index + 1:00}";
        }
        else
        {
            itemObject = existing.gameObject;
        }

        itemObject.SetActive(true);
        return GetOrAddComponent<Button>(itemObject);
    }

    private void OnScenarioDropdownItemClicked(LevelDefinition definition, ScenarioDefinition scenario)
    {
        if (definition == null || scenario == null || definition != selectedLevelDefinition)
        {
            return;
        }

        string clickKey = GetScenarioClickKey(definition, scenario);
        float now = Time.unscaledTime;
        bool isDoubleClick = string.Equals(lastScenarioDropdownClickKey, clickKey, StringComparison.Ordinal) &&
                             now - lastScenarioDropdownClickTime <= ScenarioDropdownDoubleClickWindow;

        lastScenarioDropdownClickKey = clickKey;
        lastScenarioDropdownClickTime = now;

        if (!isDoubleClick)
        {
            return;
        }

        bool clearManualSelection = IsSameScenarioSelection(selectedScenarioOverride, scenario);
        selectedScenarioOverride = clearManualSelection ? null : scenario;
        ResetScenarioDropdownClickState();
        SetScenarioDropdownVisible(false);
        ApplyLevelInfoTexts(selectedLevelCard, selectedLevelDefinition, selectedLevelSourceButton);
        RefreshLevelInfoLayout();
        RefreshPlayButtonState(selectedLevelDefinition);
        RefreshScenarioDropdownToggleVisualState();
        PositionLevelInfoPopupNearSource(selectedLevelSourceButton);
    }

    private void RefreshScenarioDropdownToggleVisualState()
    {
        if (levelInfoPopup == null || levelInfoPopup.scenarioDropdownToggleLabel == null)
        {
            return;
        }

        bool hasManualSelection = selectedLevelDefinition != null && selectedScenarioOverride != null;
        levelInfoPopup.scenarioDropdownToggleLabel.color = hasManualSelection
            ? ScenarioToggleSelectedTextColor
            : levelInfoPopup.scenarioDropdownToggleDefaultTextColor;
    }

    private void ResetScenarioDropdownClickState()
    {
        lastScenarioDropdownClickKey = string.Empty;
        lastScenarioDropdownClickTime = -10f;
    }

    private static string GetScenarioClickKey(LevelDefinition definition, ScenarioDefinition scenario)
    {
        string levelId = definition != null ? definition.levelId : string.Empty;
        string scenarioId = scenario != null && !string.IsNullOrWhiteSpace(scenario.scenarioId)
            ? scenario.scenarioId
            : ResolveScenarioResourcePath(definition, scenario);
        return $"{levelId}::{scenarioId}";
    }

    private ScenarioDefinition[] GetConfiguredScenarios(LevelDefinition definition)
    {
        if (definition == null || definition.scenarioDefinitions == null || definition.scenarioDefinitions.Length == 0)
        {
            return Array.Empty<ScenarioDefinition>();
        }

        List<ScenarioDefinition> scenarios = new List<ScenarioDefinition>(definition.scenarioDefinitions.Length);
        for (int i = 0; i < definition.scenarioDefinitions.Length; i++)
        {
            ScenarioDefinition scenario = definition.scenarioDefinitions[i];
            if (scenario == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(scenario.targetSceneName) &&
                string.IsNullOrWhiteSpace(scenario.scenarioResourcePath) &&
                string.IsNullOrWhiteSpace(scenario.caseId) &&
                string.IsNullOrWhiteSpace(scenario.displayName))
            {
                continue;
            }

            scenarios.Add(scenario);
        }

        return scenarios.Count > 0 ? scenarios.ToArray() : Array.Empty<ScenarioDefinition>();
    }

    private void ApplyScenarioDropdownItemVisual(Button itemButton, ScenarioDefinition scenario)
    {
        if (itemButton == null)
        {
            return;
        }

        Image targetGraphic = itemButton.targetGraphic as Image;
        if (targetGraphic == null)
        {
            return;
        }

        targetGraphic.color = IsSameScenarioSelection(selectedScenarioOverride, scenario)
            ? ScenarioDropdownSelectedItemColor
            : ScenarioDropdownItemColor;
    }

    private static bool IsSameScenarioSelection(ScenarioDefinition left, ScenarioDefinition right)
    {
        if (left == null || right == null)
        {
            return false;
        }

        return string.Equals(left.scenarioId, right.scenarioId, StringComparison.Ordinal) &&
               string.Equals(left.scenarioResourcePath, right.scenarioResourcePath, StringComparison.Ordinal) &&
               string.Equals(left.targetSceneName, right.targetSceneName, StringComparison.Ordinal);
    }

    private void RefreshPlayButtonState(LevelDefinition definition)
    {
        if (levelInfoPopup.playButton == null)
        {
            return;
        }

        bool canPlay = selectedScenarioOverride != null
            ? IsPlayableScenario(definition, selectedScenarioOverride)
            : HasPlayableRoute(definition);
        levelInfoPopup.playButton.interactable = canPlay;
    }

    private RectTransform FindPopupRect(string objectName)
    {
        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (canvas == null)
        {
            return null;
        }

        Transform direct = canvas.transform.Find(objectName);
        return direct as RectTransform;
    }

    private RectTransform GetCanvasRect()
    {
        Canvas canvas = GetComponentInParent<Canvas>(true);
        return canvas != null ? canvas.transform as RectTransform : null;
    }

    private static RectTransform FindNestedRect(Transform root, string objectName)
    {
        Transform target = FindDeepChild(root, objectName);
        return target as RectTransform;
    }

    private static TMP_Text FindNestedText(Transform root, string objectName)
    {
        Transform target = FindDeepChild(root, objectName);
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private static Button FindNestedButton(Transform root, string objectName)
    {
        Transform target = FindDeepChild(root, objectName);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private static Button FindButtonByLabel(Transform root, string labelText)
    {
        if (root == null || string.IsNullOrWhiteSpace(labelText))
        {
            return null;
        }

        TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == null || !string.Equals(label.text?.Trim(), labelText, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Button button = label.GetComponentInParent<Button>(true);
            if (button != null)
            {
                return button;
            }
        }

        return null;
    }

    private static Transform FindDeepChild(Transform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        if (string.Equals(parent.name, objectName, StringComparison.Ordinal))
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            Transform result = FindDeepChild(child, objectName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private void OnLanguageChanged(AppLanguage _)
    {
        RefreshLocalizedContent();
    }

    private void RefreshLocalizedContent()
    {
        RefreshRegionCardFonts(suburbanCard);
        RefreshRegionCardFonts(cityCard);
        RefreshLevelInfoFonts();
        RefreshRegionCardTexts(suburbanCard);
        RefreshRegionCardTexts(cityCard);
        RefreshRegionCardLayout(suburbanCard);
        RefreshRegionCardLayout(cityCard);
        RefreshPlayButtonText();

        if (selectedLevelDefinition != null)
        {
            ApplyLevelInfoTexts(selectedLevelCard, selectedLevelDefinition, selectedLevelSourceButton);
            RefreshLevelInfoLayout();
        }
    }

    private void RefreshRegionCardTexts(RegionCard card)
    {
        if (card == null)
        {
            return;
        }

        SetText(card.title, ResolveRegionTitle(card));

        if (card.levelListContentRoot == null || card.levelLabels == null || card.levelLabels.Count <= 0)
        {
            return;
        }

        for (int i = 0; i < card.levelLabels.Count; i++)
        {
            TMP_Text label = card.levelLabels[i];
            if (label == null)
            {
                continue;
            }

            LevelDefinition definition = GetLevelDefinitionByIndex(card, i);
            string resolvedBaseName = ResolveLevelListBaseName(card, definition, i);
            label.text = FormatLevelLabel(card, definition, resolvedBaseName, i);
        }
    }

    private void RefreshPlayButtonText()
    {
        if (levelInfoPopup.playButtonLabel == null)
        {
            return;
        }

        SetText(levelInfoPopup.playButtonLabel, LanguageManager.Tr(PlayButtonLocalizationKey, "Play"));
    }

    private void ApplyLevelInfoTexts(RegionCard card, LevelDefinition definition, Button sourceButton)
    {
        if (definition == null)
        {
            return;
        }

        ScenarioDefinition scenario = GetDisplayedScenario(definition);
        SetText(levelInfoPopup.levelNameText, ResolveDisplayedLevelName(definition, scenario, sourceButton));
        SetText(levelInfoPopup.areaText, ResolveDisplayedAreaSummary(card, definition, scenario));
        SetText(levelInfoPopup.descriptionText, ResolveDisplayedDescription(definition, scenario));
        SetText(levelInfoPopup.objectiveText, ResolveDisplayedObjective(definition, scenario));
        SetText(levelInfoPopup.difficultyText, ResolveDisplayedDifficultySummary(definition, scenario));
        RefreshPlayButtonText();
    }

    private void RefreshRegionCardFonts(RegionCard card)
    {
        if (card == null)
        {
            return;
        }

        ApplyLanguageFontToChildren(card.root, LanguageFontRole.Default);
        ApplyLanguageFont(card.title, LanguageFontRole.Heading);

        if (card.levelLabels == null)
        {
            return;
        }

        for (int i = 0; i < card.levelLabels.Count; i++)
        {
            ApplyLanguageFont(card.levelLabels[i], LanguageFontRole.Default);
        }
    }

    private void RefreshLevelInfoFonts()
    {
        ApplyLanguageFontToChildren(levelInfoPopup.root, LanguageFontRole.Default);
        ApplyLanguageFont(levelInfoPopup.levelNameText, LanguageFontRole.Heading);
        ApplyLanguageFont(levelInfoPopup.playButtonLabel, LanguageFontRole.Default);
    }

    private void RefreshRegionCardLayout(RegionCard card)
    {
        if (card == null)
        {
            return;
        }

        ForceTextLayoutUpdate(card.title);

        if (card.levelLabels != null)
        {
            for (int i = 0; i < card.levelLabels.Count; i++)
            {
                ForceTextLayoutUpdate(card.levelLabels[i]);
            }
        }

        if (card.levelListContentRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(card.levelListContentRoot);
        }

        if (card.levelListRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(card.levelListRoot);
        }

        if (card.root != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(card.root);
        }
    }

    private void RefreshLevelInfoLayout()
    {
        Canvas.ForceUpdateCanvases();

        ForceTextLayoutUpdate(levelInfoPopup.levelNameText);
        ForceTextLayoutUpdate(levelInfoPopup.areaText);
        ForceTextLayoutUpdate(levelInfoPopup.descriptionText);
        ForceTextLayoutUpdate(levelInfoPopup.objectiveText);
        ForceTextLayoutUpdate(levelInfoPopup.difficultyText);

        ForceRebuildChain(levelInfoPopup.levelNameText != null ? levelInfoPopup.levelNameText.rectTransform : null, levelInfoPopup.contentRoot);
        ForceRebuildChain(levelInfoPopup.areaText != null ? levelInfoPopup.areaText.rectTransform : null, levelInfoPopup.contentRoot);
        ForceRebuildChain(levelInfoPopup.descriptionText != null ? levelInfoPopup.descriptionText.rectTransform : null, levelInfoPopup.contentRoot);
        ForceRebuildChain(levelInfoPopup.objectiveText != null ? levelInfoPopup.objectiveText.rectTransform : null, levelInfoPopup.contentRoot);
        ForceRebuildChain(levelInfoPopup.difficultyText != null ? levelInfoPopup.difficultyText.rectTransform : null, levelInfoPopup.contentRoot);

        if (levelInfoPopup.contentRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(levelInfoPopup.contentRoot);
        }

        if (levelInfoPopup.root != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(levelInfoPopup.root);
        }

        Canvas.ForceUpdateCanvases();
    }

    private static void ForceTextLayoutUpdate(TMP_Text target)
    {
        if (target == null)
        {
            return;
        }

        target.ForceMeshUpdate();
        LayoutRebuilder.MarkLayoutForRebuild(target.rectTransform);
    }

    private static void ForceRebuildChain(RectTransform start, RectTransform stopInclusive)
    {
        RectTransform current = start;
        while (current != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(current);
            if (current == stopInclusive)
            {
                break;
            }

            current = current.parent as RectTransform;
        }
    }

    private static Vector2 CaptureNormalizedButtonPosition(RectTransform buttonRect, RectTransform parentRect)
    {
        if (buttonRect == null || parentRect == null)
        {
            return new Vector2(-1f, -1f);
        }

        Rect rect = parentRect.rect;
        if (rect.width <= Mathf.Epsilon || rect.height <= Mathf.Epsilon)
        {
            return new Vector2(-1f, -1f);
        }

        Vector3[] corners = new Vector3[4];
        buttonRect.GetWorldCorners(corners);

        Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;
        Vector3 localCenter3 = parentRect.InverseTransformPoint(worldCenter);
        Vector2 localCenter = new Vector2(localCenter3.x, localCenter3.y);

        float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, localCenter.x);
        float normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, localCenter.y);
        return new Vector2(normalizedX, normalizedY);
    }

    private static void ApplyLevelButtonPositions(RegionCard card)
    {
        if (card == null || card.levelButtonsRoot == null || card.levelButtons.Count == 0)
        {
            return;
        }

        Rect rect = card.levelButtonsRoot.rect;
        if (rect.width <= Mathf.Epsilon || rect.height <= Mathf.Epsilon)
        {
            return;
        }

        int count = Mathf.Min(card.levelButtons.Count, card.levelButtonNormalizedPositions.Count);
        for (int i = 0; i < count; i++)
        {
            Button button = card.levelButtons[i];
            RectTransform buttonRect = button != null ? button.transform as RectTransform : null;
            if (buttonRect == null)
            {
                continue;
            }

            Vector2 normalizedPosition = card.levelButtonNormalizedPositions[i];
            if (normalizedPosition.x < 0f || normalizedPosition.y < 0f)
            {
                normalizedPosition = CaptureNormalizedButtonPosition(buttonRect, card.levelButtonsRoot);
                if (normalizedPosition.x < 0f || normalizedPosition.y < 0f)
                {
                    continue;
                }

                card.levelButtonNormalizedPositions[i] = normalizedPosition;
            }

            float localCenterX = Mathf.Lerp(rect.xMin, rect.xMax, normalizedPosition.x);
            float localCenterY = Mathf.Lerp(rect.yMin, rect.yMax, normalizedPosition.y);
            Vector2 pivotOffset = new Vector2(
                (buttonRect.pivot.x - 0.5f) * buttonRect.rect.width,
                (buttonRect.pivot.y - 0.5f) * buttonRect.rect.height);

            Vector3 localPosition = buttonRect.localPosition;
            buttonRect.localPosition = new Vector3(
                localCenterX + pivotOffset.x,
                localCenterY + pivotOffset.y,
                localPosition.z);
        }
    }

    private void PositionLevelInfoPopupNearSource(Button sourceButton)
    {
        if (sourceButton == null || levelInfoPopup.root == null)
        {
            return;
        }

        RectTransform sourceRect = sourceButton.transform as RectTransform;
        RectTransform canvasRect = GetCanvasRect();
        if (sourceRect == null || canvasRect == null)
        {
            return;
        }

        Canvas canvas = canvasRect.GetComponent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector3[] corners = new Vector3[4];
        sourceRect.GetWorldCorners(corners);

        Vector2 localMin = Vector2.positiveInfinity;
        Vector2 localMax = Vector2.negativeInfinity;
        Vector2 localCenter = Vector2.zero;

        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, eventCamera, out Vector2 localPoint);
            localCenter += localPoint;
            localMin = Vector2.Min(localMin, localPoint);
            localMax = Vector2.Max(localMax, localPoint);
        }

        localCenter *= 0.25f;
        Vector2 buttonExtents = (localMax - localMin) * 0.5f;

        bool placeOnRight = localCenter.x <= 0f;
        bool placeAbove = localCenter.y <= 0f;

        levelInfoPopup.root.anchorMin = new Vector2(0.5f, 0.5f);
        levelInfoPopup.root.anchorMax = new Vector2(0.5f, 0.5f);
        levelInfoPopup.root.pivot = new Vector2(placeOnRight ? 0f : 1f, placeAbove ? 0f : 1f);

        Vector2 popupSize = levelInfoPopup.root.rect.size;
        Vector2 anchoredPosition = localCenter + new Vector2(
            placeOnRight ? buttonExtents.x + popupHorizontalOffset : -(buttonExtents.x + popupHorizontalOffset),
            placeAbove ? buttonExtents.y + popupVerticalOffset : -(buttonExtents.y + popupVerticalOffset));

        Rect canvasBounds = canvasRect.rect;
        float left = anchoredPosition.x - popupSize.x * levelInfoPopup.root.pivot.x;
        float right = left + popupSize.x;
        float bottom = anchoredPosition.y - popupSize.y * levelInfoPopup.root.pivot.y;
        float top = bottom + popupSize.y;

        if (left < canvasBounds.xMin + popupScreenMargin)
        {
            anchoredPosition.x += (canvasBounds.xMin + popupScreenMargin) - left;
        }

        if (right > canvasBounds.xMax - popupScreenMargin)
        {
            anchoredPosition.x -= right - (canvasBounds.xMax - popupScreenMargin);
        }

        if (bottom < canvasBounds.yMin + popupScreenMargin)
        {
            anchoredPosition.y += (canvasBounds.yMin + popupScreenMargin) - bottom;
        }

        if (top > canvasBounds.yMax - popupScreenMargin)
        {
            anchoredPosition.y -= top - (canvasBounds.yMax - popupScreenMargin);
        }

        levelInfoPopup.root.anchoredPosition = anchoredPosition;
    }

    private static string GetButtonLabel(Button button)
    {
        TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        return label != null ? label.text : string.Empty;
    }

    private void ReturnToMainMenu()
    {
        CloseSubMenu();
        SceneManager.LoadScene("MainMenu");
    }

    private static void QuitApplication()
    {
        Application.Quit();
    }

    private static string SanitizeLevelName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        if (trimmed.Length >= 2 &&
            char.IsDigit(trimmed[0]) &&
            char.IsDigit(trimmed[1]))
        {
            int firstLetterIndex = 2;
            while (firstLetterIndex < trimmed.Length &&
                   !char.IsLetterOrDigit(trimmed[firstLetterIndex]))
            {
                firstLetterIndex++;
            }

            if (firstLetterIndex < trimmed.Length)
            {
                return trimmed.Substring(firstLetterIndex).Trim();
            }
        }

        return trimmed;
    }

    private string[] GetDisplayLevels(RectTransform listRoot, RegionCard card)
    {
        if (preferSceneLevelTexts)
        {
            List<string> sceneLevels = new List<string>();
            for (int i = 0; i < listRoot.childCount; i++)
            {
                TMP_Text existingLabel = listRoot.GetChild(i).GetComponent<TMP_Text>();
                if (existingLabel == null || string.IsNullOrWhiteSpace(existingLabel.text))
                {
                    continue;
                }

                sceneLevels.Add(existingLabel.text.Trim());
            }

            if (sceneLevels.Count > 0)
            {
                return sceneLevels.ToArray();
            }
        }

        if (card.levels != null && card.levels.Length > 0)
        {
            return card.levels;
        }

        string levelFallback = LanguageManager.Tr(FallbackLevelNameLocalizationKey, "Level");
        return new[] { $"{levelFallback} 01", $"{levelFallback} 02", $"{levelFallback} 03" };
    }

    private string FormatLevelLabel(RegionCard card, LevelDefinition definition, string rawText, int index)
    {
        string sequence = $"{index + 1:00}";

        string baseLabel;
        if (string.IsNullOrWhiteSpace(rawText))
        {
            string levelFallback = LanguageManager.Tr(FallbackLevelNameLocalizationKey, "Level");
            baseLabel = card != null && card.isLeftCard
                ? $"{sequence}  {levelFallback} {sequence}"
                : $"{levelFallback} {sequence}  {sequence}";
            return AppendCompletedMarker(baseLabel, definition);
        }

        string normalized = NormalizeLevelBaseName(rawText);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = LanguageManager.Tr(FallbackLevelNameLocalizationKey, "Level");
        }

        baseLabel = card != null && card.isLeftCard
            ? $"{sequence}  {normalized}"
            : $"{normalized}  {sequence}";
        return AppendCompletedMarker(baseLabel, definition);
    }

    private string AppendCompletedMarker(string baseLabel, LevelDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(baseLabel))
        {
            return baseLabel;
        }

        string playerName = LoadingFlowState.GetPlayerName();
        if (definition == null
            || string.IsNullOrWhiteSpace(playerName)
            || string.IsNullOrWhiteSpace(definition.levelId)
            || !PlayerProgressProfileStore.IsLevelCompleted(playerName, definition.levelId))
        {
            return baseLabel;
        }

        return $"{baseLabel}  {CompletedLevelMarker}";
    }

    private TMP_Text EnsureLevelLabel(RectTransform listRoot, TMP_Text title, int index)
    {
        Transform existing = index < listRoot.childCount ? listRoot.GetChild(index) : null;
        TMP_Text label;
        RectTransform rect;

        if (existing == null)
        {
            GameObject go = new GameObject("LevelItem", typeof(RectTransform), typeof(TextMeshProUGUI));
            rect = go.GetComponent<RectTransform>();
            rect.SetParent(listRoot, false);
            label = go.GetComponent<TMP_Text>();
        }
        else
        {
            rect = existing as RectTransform;
            label = existing.GetComponent<TMP_Text>();
            if (label == null)
            {
                label = existing.gameObject.AddComponent<TextMeshProUGUI>();
            }

            existing.gameObject.SetActive(true);
        }

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(0f, 40f);

        label.font = title != null ? title.font : label.font;
        label.fontSize = 22f;
        label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.alignment = TextAlignmentOptions.Left;
        label.color = new Color(1f, 1f, 1f, 0.92f);
        label.margin = Vector4.zero;
        label.raycastTarget = false;

        return label;
    }

    private string ResolveRegionTitle(RegionCard card)
    {
        if (card == null)
        {
            return string.Empty;
        }

        string localizationKey = !string.IsNullOrWhiteSpace(card.titleLocalizationKey)
            ? card.titleLocalizationKey
            : GetDefaultRegionLocalizationKey(card.name);
        string fallback = card.title != null ? card.title.text : card.name;
        return ResolveLocalizedText(localizationKey, fallback);
    }

    private string ResolveLevelName(LevelDefinition definition, Button sourceButton)
    {
        string buttonLabel = GetButtonLabel(sourceButton);
        string fallback = !string.IsNullOrWhiteSpace(definition.levelName)
            ? definition.levelName
            : SanitizeLevelName(buttonLabel);
        string localizationKey = GetLevelFieldLocalizationKey(definition, definition.levelNameLocalizationKey, "name");
        return ResolveLocalizedText(localizationKey, fallback);
    }

    private string ResolveDescription(LevelDefinition definition)
    {
        string fallback = !string.IsNullOrWhiteSpace(definition.description)
            ? definition.description
            : LanguageManager.Tr(NoDescriptionLocalizationKey, "No description configured.");
        string localizationKey = GetLevelFieldLocalizationKey(definition, definition.descriptionLocalizationKey, "description");
        return ResolveLocalizedText(localizationKey, fallback);
    }

    private string ResolveObjective(LevelDefinition definition)
    {
        string fallback = !string.IsNullOrWhiteSpace(definition.objective)
            ? definition.objective
            : LanguageManager.Tr(NoObjectiveLocalizationKey, "No objective configured.");
        string localizationKey = GetLevelFieldLocalizationKey(definition, definition.objectiveLocalizationKey, "objective");
        return ResolveLocalizedText(localizationKey, fallback);
    }

    private string ResolveDifficulty(LevelDefinition definition)
    {
        string localizationKey = !string.IsNullOrWhiteSpace(definition.difficultyLocalizationKey)
            ? definition.difficultyLocalizationKey
            : GetDefaultDifficultyLocalizationKey(definition.difficulty);
        string fallback = !string.IsNullOrWhiteSpace(definition.difficulty)
            ? definition.difficulty
            : LanguageManager.Tr(DifficultyTbdLocalizationKey, "TBD");
        return ResolveLocalizedText(localizationKey, fallback);
    }

    private ScenarioDefinition GetDisplayedScenario(LevelDefinition definition)
    {
        return definition != null && definition == selectedLevelDefinition ? selectedScenarioOverride : null;
    }

    private string ResolveDisplayedLevelName(LevelDefinition definition, ScenarioDefinition scenario, Button sourceButton)
    {
        return scenario != null
            ? ResolveScenarioDisplayName(scenario, definition)
            : ResolveLevelName(definition, sourceButton);
    }

    private string ResolveDisplayedAreaSummary(RegionCard card, LevelDefinition definition, ScenarioDefinition scenario)
    {
        return scenario != null
            ? ResolveSelectedScenarioAreaSummary(card)
            : ResolveAreaSummary(card, definition);
    }

    private string ResolveDisplayedDescription(LevelDefinition definition, ScenarioDefinition scenario)
    {
        return scenario != null
            ? ResolveSelectedScenarioDescription(definition, scenario)
            : ResolveDescription(definition);
    }

    private string ResolveDisplayedObjective(LevelDefinition definition, ScenarioDefinition scenario)
    {
        return scenario != null
            ? ResolveSelectedScenarioObjective(definition, scenario)
            : ResolveObjective(definition);
    }

    private string ResolveDisplayedDifficultySummary(LevelDefinition definition, ScenarioDefinition scenario)
    {
        return scenario != null
            ? ResolveSelectedScenarioDifficultySummary(definition, scenario)
            : ResolveDifficultySummary(definition);
    }

    private string ResolveAreaSummary(RegionCard card, LevelDefinition definition)
    {
        string regionTitle = ResolveRegionTitle(card);
        string incidentMode = ResolveRandomIncidentLabel(definition);
        if (string.IsNullOrWhiteSpace(regionTitle))
        {
            return incidentMode;
        }

        if (string.IsNullOrWhiteSpace(incidentMode))
        {
            return regionTitle;
        }

        return $"{regionTitle} • {incidentMode}";
    }

    private string ResolveDifficultySummary(LevelDefinition definition)
    {
        string difficulty = ResolveDifficulty(definition);
        string possibleIncidents = ResolvePossibleIncidentsLabel(definition);
        if (string.IsNullOrWhiteSpace(difficulty))
        {
            return possibleIncidents;
        }

        if (string.IsNullOrWhiteSpace(possibleIncidents))
        {
            return difficulty;
        }

        return $"{difficulty} • {possibleIncidents}";
    }

    private string ResolveSelectedScenarioAreaSummary(RegionCard card)
    {
        string regionTitle = ResolveRegionTitle(card);
        string scenarioLabel = LanguageManager.Tr(SelectedScenarioLocalizationKey, "Selected Scenario");
        return CombineSummaryParts(regionTitle, scenarioLabel);
    }

    private string ResolveSelectedScenarioDescription(LevelDefinition definition, ScenarioDefinition scenario)
    {
        string fallback = ResolveDescription(definition);
        CallPhaseScenarioData scenarioData = LoadScenarioData(scenario);
        if (scenarioData != null)
        {
            string localizedDescription = scenarioData.GetLocalizedDescription();
            if (!string.IsNullOrWhiteSpace(localizedDescription))
            {
                fallback = localizedDescription.Trim();
            }
        }

        return ResolveLocalizedText(GetScenarioFieldLocalizationKey(scenario, "description"), fallback);
    }

    private string ResolveSelectedScenarioObjective(LevelDefinition definition, ScenarioDefinition scenario)
    {
        string fallback = !string.IsNullOrWhiteSpace(definition?.objective)
            ? ResolveObjective(definition)
            : LanguageManager.Tr(
                SelectedScenarioObjectiveLocalizationKey,
                "Deploy the selected scenario on the next run.");
        return ResolveLocalizedText(GetScenarioFieldLocalizationKey(scenario, "objective"), fallback);
    }

    private string ResolveSelectedScenarioDifficultySummary(LevelDefinition definition, ScenarioDefinition scenario)
    {
        string difficulty = ResolveDifficulty(definition);
        CallPhaseScenarioData scenarioData = LoadScenarioData(scenario);
        string category = scenarioData != null && !string.IsNullOrWhiteSpace(scenarioData.GetLocalizedCategory())
            ? scenarioData.GetLocalizedCategory().Trim()
            : LanguageManager.Tr(SelectedScenarioLocalizationKey, "Selected Scenario");
        return CombineSummaryParts(difficulty, category);
    }

    private string ResolveScenarioDisplayName(ScenarioDefinition scenario, LevelDefinition definition)
    {
        if (scenario == null)
        {
            return ResolveLevelName(definition, null);
        }

        string fallback;
        if (!string.IsNullOrWhiteSpace(scenario.displayName))
        {
            fallback = scenario.displayName.Trim();
        }
        else
        {
            CallPhaseScenarioData scenarioData = LoadScenarioData(scenario);
            if (scenarioData != null && !string.IsNullOrWhiteSpace(scenarioData.GetLocalizedDisplayName()))
            {
                fallback = scenarioData.GetLocalizedDisplayName().Trim();
            }
            else if (!string.IsNullOrWhiteSpace(scenario.scenarioId))
            {
                fallback = scenario.scenarioId.Trim();
            }
            else
            {
                fallback = ResolveLevelName(definition, null);
            }

            return ResolveLocalizedText(GetScenarioFieldLocalizationKey(scenario, "name"), fallback);
        }

        return ResolveLocalizedText(GetScenarioFieldLocalizationKey(scenario, "name"), fallback);
    }

    private static string CombineSummaryParts(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return right;
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return left;
        }

        return $"{left} • {right}";
    }

    private static CallPhaseScenarioData LoadScenarioData(ScenarioDefinition scenario)
    {
        if (scenario == null || string.IsNullOrWhiteSpace(scenario.scenarioResourcePath))
        {
            return null;
        }

        return Resources.Load<CallPhaseScenarioData>(scenario.scenarioResourcePath.Trim());
    }

    private string ResolveRandomIncidentLabel(LevelDefinition definition)
    {
        if (definition == null)
        {
            return string.Empty;
        }

        if (GetConfiguredScenarioCount(definition) <= 0 && string.IsNullOrWhiteSpace(definition.scenarioResourcePath))
        {
            return string.Empty;
        }

        return LanguageManager.Tr(RandomIncidentLocalizationKey, "Random Incident");
    }

    private string ResolvePossibleIncidentsLabel(LevelDefinition definition)
    {
        int scenarioCount = GetConfiguredScenarioCount(definition);
        if (scenarioCount <= 0 && !string.IsNullOrWhiteSpace(definition?.scenarioResourcePath))
        {
            scenarioCount = 1;
        }

        string format = LanguageManager.Tr(PossibleIncidentsLocalizationKey, "Possible incidents: {0}");
        return string.Format(format, scenarioCount);
    }

    private static int GetConfiguredScenarioCount(LevelDefinition definition)
    {
        if (definition == null || definition.scenarioDefinitions == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < definition.scenarioDefinitions.Length; i++)
        {
            ScenarioDefinition scenario = definition.scenarioDefinitions[i];
            if (scenario == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(scenario.targetSceneName) &&
                string.IsNullOrWhiteSpace(scenario.scenarioResourcePath) &&
                string.IsNullOrWhiteSpace(scenario.caseId))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private string ResolveLevelListBaseName(RegionCard card, LevelDefinition definition, int index)
    {
        if (definition != null)
        {
            string localizedName = ResolveLevelName(definition, definition.button);
            if (!string.IsNullOrWhiteSpace(localizedName))
            {
                return localizedName;
            }
        }

        string[] levels = GetDisplayLevels(card.levelListContentRoot, card);
        if (levels != null && index >= 0 && index < levels.Length)
        {
            return levels[index];
        }

        return LanguageManager.Tr(FallbackLevelNameLocalizationKey, "Level");
    }

    private static string GetDefaultRegionLocalizationKey(string regionName)
    {
        if (string.Equals(regionName, "Suburban", StringComparison.OrdinalIgnoreCase))
        {
            return RegionSuburbanLocalizationKey;
        }

        if (string.Equals(regionName, "City", StringComparison.OrdinalIgnoreCase))
        {
            return RegionCityLocalizationKey;
        }

        return string.Empty;
    }

    private static string GetLevelFieldLocalizationKey(LevelDefinition definition, string explicitKey, string suffix)
    {
        if (!string.IsNullOrWhiteSpace(explicitKey))
        {
            return explicitKey;
        }

        if (definition == null || string.IsNullOrWhiteSpace(definition.levelId))
        {
            return string.Empty;
        }

        return $"levelselect.level.{NormalizeLocalizationToken(definition.levelId)}.{suffix}";
    }

    private static string GetScenarioFieldLocalizationKey(ScenarioDefinition scenario, string suffix)
    {
        if (scenario == null || string.IsNullOrWhiteSpace(scenario.scenarioId) || string.IsNullOrWhiteSpace(suffix))
        {
            return string.Empty;
        }

        return $"levelselect.scenario.{NormalizeLocalizationToken(scenario.scenarioId)}.{suffix}";
    }

    private static string GetDefaultDifficultyLocalizationKey(string difficulty)
    {
        if (string.IsNullOrWhiteSpace(difficulty))
        {
            return DifficultyTbdLocalizationKey;
        }

        switch (difficulty.Trim().ToLowerInvariant())
        {
            case "normal":
                return DifficultyNormalLocalizationKey;
            case "optional":
                return DifficultyOptionalLocalizationKey;
            case "tbd":
                return DifficultyTbdLocalizationKey;
            default:
                return string.Empty;
        }
    }

    private static string ResolveLocalizedText(string key, string fallback)
    {
        return string.IsNullOrWhiteSpace(key)
            ? fallback
            : LanguageManager.Tr(key, fallback);
    }

    private static void ApplyLanguageFontToChildren(Transform root, LanguageFontRole role)
    {
        if (root == null || LanguageManager.Instance == null)
        {
            return;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            ApplyLanguageFont(texts[i], role);
        }
    }

    private static void ApplyLanguageFont(TMP_Text target, LanguageFontRole role)
    {
        if (target == null || LanguageManager.Instance == null)
        {
            return;
        }

        TMP_FontAsset font = LanguageManager.Instance.GetCurrentTMPFont(role);
        if (font != null)
        {
            target.font = font;
        }
    }

    private static string NormalizeLocalizationToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().ToLowerInvariant().Replace(' ', '_');
    }

    private static string NormalizeLevelBaseName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();

        if (trimmed.Length >= 2 && char.IsDigit(trimmed[0]) && char.IsDigit(trimmed[1]))
        {
            int firstContentIndex = 2;
            while (firstContentIndex < trimmed.Length && !char.IsLetter(trimmed[firstContentIndex]))
            {
                firstContentIndex++;
            }

            if (firstContentIndex < trimmed.Length)
            {
                trimmed = trimmed.Substring(firstContentIndex).Trim();
            }
        }

        int lastSpaceIndex = trimmed.LastIndexOf(' ');
        if (lastSpaceIndex >= 0 && lastSpaceIndex + 3 <= trimmed.Length)
        {
            string suffix = trimmed.Substring(lastSpaceIndex + 1);
            if (suffix.Length == 2 && char.IsDigit(suffix[0]) && char.IsDigit(suffix[1]))
            {
                trimmed = trimmed.Substring(0, lastSpaceIndex).TrimEnd();
            }
        }

        return trimmed;
    }

    private void ConfigureTitle(TMP_Text title)
    {
        if (title == null)
        {
            return;
        }

        RectTransform rect = title.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -titleTopOffset);
        rect.sizeDelta = new Vector2(-contentWidthPadding, titleHeight);
        rect.SetSiblingIndex(Mathf.Max(2, rect.GetSiblingIndex()));

        title.margin = Vector4.zero;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.fontSize = 34f;
        title.enableAutoSizing = false;
    }

    private void ApplyAnimatedState(bool instant)
    {
        if (panelRoot != null)
        {
            lastPanelSize = panelRoot.rect.size;
        }

        currentLeftRatio = instant
            ? targetLeftRatio
            : Mathf.SmoothDamp(currentLeftRatio, targetLeftRatio, ref ratioVelocity, widthSmoothTime);

        float contentWidth = Mathf.Max(0f, panelRoot.rect.width - panelPadding.x - panelPadding.y);
        float contentHeight = Mathf.Max(0f, panelRoot.rect.height - panelPadding.z - panelPadding.w);
        float splitX = contentWidth * currentLeftRatio;

        float rawInset = Mathf.Tan(Mathf.Deg2Rad * dividerAngle) * contentHeight;
        float maxInsetLeft = Mathf.Max(0f, (splitX - minimumRegionWidth) * 2f);
        float maxInsetRight = Mathf.Max(0f, ((contentWidth - splitX) - minimumRegionWidth) * 2f);
        float seamInset = Mathf.Min(rawInset, maxInsetLeft, maxInsetRight);

        PositionCards(contentHeight, contentWidth, splitX, seamInset);

        float suburbanFocus = Mathf.InverseLerp(neutralRatio, expandedRatio, currentLeftRatio);
        float cityFocus = Mathf.InverseLerp(neutralRatio, expandedRatio, 1f - currentLeftRatio);
        ApplyLevelButtonPositions(suburbanCard);
        ApplyLevelButtonPositions(cityCard);
        UpdateCardVisuals(suburbanCard, suburbanFocus);
        UpdateCardVisuals(cityCard, cityFocus);
        UpdateDivider(Mathf.Max(suburbanFocus, cityFocus), contentWidth, splitX, seamInset);
        ApplySceneIntroVisualState();

        if (levelInfoPopup != null &&
            levelInfoPopup.root != null &&
            levelInfoPopup.canvasGroup != null &&
            levelInfoPopup.canvasGroup.alpha > 0f &&
            selectedLevelSourceButton != null)
        {
            PositionLevelInfoPopupNearSource(selectedLevelSourceButton);
        }
    }

    private void PositionCards(float contentHeight, float contentWidth, float splitX, float seamInset)
    {
        float topSeamX = splitX + seamInset * 0.5f;
        float bottomSeamX = splitX - seamInset * 0.5f;
        float introOffset = GetSceneIntroOffset();

        if (suburbanCard != null && suburbanCard.root != null)
        {
            RectTransform root = suburbanCard.root;
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 0.5f);
            root.sizeDelta = new Vector2(Mathf.Max(0f, topSeamX - cardSpacing * 0.5f), -(panelPadding.z + panelPadding.w));
            root.anchoredPosition = new Vector2(panelPadding.x - introOffset, (panelPadding.w - panelPadding.z) * 0.5f);

            if (suburbanCard.shapeGraphic != null)
            {
                suburbanCard.shapeGraphic.SetShape(SlantedPanelGraphic.SlantSide.Right, 0f, seamInset);
            }

            ApplyContentInsets(suburbanCard, 0f, seamInset);
        }

        if (cityCard != null && cityCard.root != null)
        {
            RectTransform root = cityCard.root;
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 0.5f);
            root.sizeDelta = new Vector2(Mathf.Max(0f, contentWidth - bottomSeamX - cardSpacing * 0.5f), -(panelPadding.z + panelPadding.w));
            root.anchoredPosition = new Vector2(panelPadding.x + bottomSeamX + cardSpacing * 0.5f + introOffset, (panelPadding.w - panelPadding.z) * 0.5f);

            if (cityCard.shapeGraphic != null)
            {
                cityCard.shapeGraphic.SetShape(SlantedPanelGraphic.SlantSide.Left, seamInset, 0f);
            }

            ApplyContentInsets(cityCard, seamInset, 0f);
        }

        if (suburbanCard != null && cityCard != null && suburbanCard.root != null && cityCard.root != null)
        {
            suburbanCard.root.SetSiblingIndex(0);
            cityCard.root.SetSiblingIndex(1);
        }
    }

    private void ApplyContentInsets(RegionCard card, float topInset, float bottomInset)
    {
        if (card == null)
        {
            return;
        }

        float seamInset = Mathf.Max(topInset, bottomInset);
        float seamPadding = seamContentPadding + seamInset * 0.35f;
        float leftInset = card.isLeftCard ? outerContentPadding : seamPadding;
        float rightInset = card.isLeftCard ? seamPadding : outerContentPadding;

        if (card.title != null)
        {
            RectTransform titleRect = card.title.rectTransform;
            titleRect.offsetMin = new Vector2(leftInset, titleRect.offsetMin.y);
            titleRect.offsetMax = new Vector2(-rightInset, titleRect.offsetMax.y);
            card.title.alignment = card.isLeftCard ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
        }

        if (card.levelListRoot != null)
        {
            if (card.isLeftCard)
            {
                card.levelListRoot.anchorMin = new Vector2(0f, 0f);
                card.levelListRoot.anchorMax = new Vector2(0f, 0f);
                card.levelListRoot.pivot = new Vector2(0f, 0f);
                card.levelListRoot.anchoredPosition = new Vector2(leftInset, levelListBottomOffset);
            }
            else
            {
                card.levelListRoot.anchorMin = new Vector2(1f, 0f);
                card.levelListRoot.anchorMax = new Vector2(1f, 0f);
                card.levelListRoot.pivot = new Vector2(1f, 0f);
                card.levelListRoot.anchoredPosition = new Vector2(-rightInset, levelListBottomOffset);
            }
        }

        if (card.levelListRoot != null)
        {
            VerticalLayoutGroup rootLayout = card.levelListRoot.GetComponent<VerticalLayoutGroup>();
            if (rootLayout != null)
            {
                rootLayout.childAlignment = card.isLeftCard ? TextAnchor.LowerLeft : TextAnchor.LowerRight;
            }
        }

        if (card.levelListContentRoot != null)
        {
            card.levelListContentRoot.anchorMin = card.isLeftCard ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
            card.levelListContentRoot.anchorMax = card.levelListContentRoot.anchorMin;
            card.levelListContentRoot.pivot = card.isLeftCard ? new Vector2(0f, 0f) : new Vector2(1f, 0f);

            VerticalLayoutGroup contentLayout = card.levelListContentRoot.GetComponent<VerticalLayoutGroup>();
            if (contentLayout != null)
            {
                contentLayout.childAlignment = card.isLeftCard ? TextAnchor.LowerLeft : TextAnchor.LowerRight;
            }
        }

        for (int i = 0; i < card.levelLabels.Count; i++)
        {
            TMP_Text label = card.levelLabels[i];
            if (label == null)
            {
                continue;
            }

            label.alignment = card.isLeftCard ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
        }
    }

    private void UpdateCardVisuals(RegionCard card, float focus)
    {
        if (card == null)
        {
            return;
        }

        float overlayAlpha = Mathf.Lerp(overlayAlphaIdle, overlayAlphaSelected, focus);
        if (focus <= 0.001f && currentSelection != RegionSelection.None)
        {
            overlayAlpha = overlayAlphaCollapsed;
        }

        if (card.dimOverlay != null)
        {
            Color color = card.dimOverlay.color;
            color.a = overlayAlpha;
            card.dimOverlay.color = color;
        }

        if (card.mapImage != null)
        {
            bool hasSprite = card.mapImage.sprite != null;
            float targetScale = currentSelection == RegionSelection.None
                ? mapScaleIdle
                : Mathf.Lerp(mapScaleCollapsed, mapScaleSelected, focus);

            card.mapImage.rectTransform.localScale = new Vector3(targetScale, targetScale, 1f);
            card.mapImage.color = hasSprite
                ? Color.Lerp(new Color(0.86f, 0.88f, 0.9f, 1f), Color.white, focus)
                : Color.Lerp(card.placeholderColor * 0.82f, card.placeholderColor * 1.15f, Mathf.Clamp01(0.35f + focus * 0.65f));
        }

        if (card.title != null)
        {
            card.title.color = Color.Lerp(new Color(1f, 1f, 1f, 0.72f), Color.white, Mathf.Clamp01(0.25f + focus * 0.75f));
            card.title.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(-42f, -30f, focus));
            card.title.fontSize = Mathf.Lerp(28f, 36f, focus);
        }

        if (card.levelListGroup != null)
        {
            float alpha = currentSelection == RegionSelection.None ? 0f : focus;
            card.levelListGroup.alpha = alpha;
            card.levelListGroup.interactable = alpha > 0.95f;
            card.levelListGroup.blocksRaycasts = alpha > 0.95f;
        }

        if (card.levelButtonsGroup != null)
        {
            float alpha = currentSelection == RegionSelection.None ? 0f : focus;
            card.levelButtonsGroup.alpha = alpha;
            card.levelButtonsGroup.interactable = alpha > 0.95f;
            card.levelButtonsGroup.blocksRaycasts = alpha > 0.95f;
        }

        for (int i = 0; i < card.levelLabels.Count; i++)
        {
            TMP_Text level = card.levelLabels[i];
            if (level == null)
            {
                continue;
            }

            Color color = level.color;
            color.a = Mathf.Lerp(0.2f, 0.95f, focus);
            level.color = color;
            float entryOffset = card.isLeftCard ? Mathf.Lerp(14f, 0f, focus) : Mathf.Lerp(-14f, 0f, focus);
            level.rectTransform.anchoredPosition = new Vector2(entryOffset, 0f);
        }

        if (card.shapeGraphic != null)
        {
            card.shapeGraphic.color = Color.Lerp(new Color(0.07f, 0.08f, 0.1f, 0.96f), new Color(0.1f, 0.12f, 0.14f, 0.98f), focus);
        }
    }

    private void UpdateDivider(float focus, float contentWidth, float splitX, float seamInset)
    {
        if (!showDivider || dividerImage == null)
        {
            return;
        }

        float centerX = panelPadding.x + splitX;
        float centerY = (panelPadding.w - panelPadding.z) * 0.5f;

        RectTransform rect = dividerImage.rectTransform;
        rect.anchoredPosition = new Vector2(centerX, centerY);
        rect.sizeDelta = new Vector2(dividerWidth, Mathf.Max(dividerHeight, panelRoot.rect.height - panelPadding.z - panelPadding.w));
        rect.localRotation = Quaternion.Euler(0f, 0f, dividerAngle);
        rect.SetAsLastSibling();

        Color color = dividerImage.color;
        color.a = Mathf.Lerp(dividerAlphaIdle, dividerAlphaSelected, focus);
        dividerImage.color = color;
    }

    private static CanvasGroup GetCanvasGroup(Component target)
    {
        return target != null ? GetOrAddComponent<CanvasGroup>(target.gameObject) : null;
    }

    private static void SetCanvasGroupAlpha(CanvasGroup group, float alpha)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = Mathf.Clamp01(alpha);
    }

    private static float EaseOutCubic(float value)
    {
        float t = Mathf.Clamp01(value);
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseOutQuart(float value)
    {
        float t = Mathf.Clamp01(value);
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse * inverse;
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = target.AddComponent<T>();
        }

        return component;
    }
}
