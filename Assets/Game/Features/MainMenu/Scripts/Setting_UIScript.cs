using System;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class Setting_UIScript : MonoBehaviour
{
    private const string MouseSensitivityLocalizationKey = "setting.control.mousesensitivity";

    [Header("Buttons")]
    [SerializeField] private Button btnGen;
    [SerializeField] private Button btnGrap;
    [SerializeField] private Button btnAud;
    [SerializeField] private Button btnCont;
    [SerializeField] private Button btnRestoreDefault;
    [SerializeField] private Button btnSave;
    [SerializeField] private Button btnExit;

    [Header("Panels (Root GameObject of each panel)")]
    [SerializeField] private GameObject panelGen;
    [SerializeField] private GameObject panelGrap;
    [SerializeField] private GameObject panelAudio;
    [SerializeField] private GameObject panelCont;

    [Header("Confirm Toast")]
    [SerializeField] private ToastContainerController toastContainer;
    [SerializeField] private LanguageToggleBinder languageToggleBinder;
    [SerializeField] private string confirmTitle = "Thong bao";
    [SerializeField] private string saveConfirmMessage = "Ban co muon luu cau hinh khong?";
    [SerializeField] private string backUnsavedConfirmMessage = "Co su thay doi, ban co muon luu khong?";
    [SerializeField] private string restoreConfirmMessage = "Ban co muon khoi phuc cai dat mac dinh khong?";
    [SerializeField] private string yesLabel = "Yes";
    [SerializeField] private string noLabel = "No";
    [SerializeField] private bool confirmUsesSavedLanguage = true;

    [Header("Confirm Toast Localization Keys (Optional)")]
    [SerializeField] private string confirmTitleKey = "common.toast.confirm.title";
    [SerializeField] private string saveConfirmMessageKey = "settings.confirm.save";
    [SerializeField] private string backUnsavedConfirmMessageKey = "settings.confirm.unsaved";
    [SerializeField] private string restoreConfirmMessageKey = "settings.confirm.restore";
    [SerializeField] private string yesLabelKey = "common.btn.yes";
    [SerializeField] private string noLabelKey = "common.btn.no";

    [Header("Graphics Settings")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle vsyncToggle;
    [SerializeField] private Toggle fpsToggle;
    [SerializeField] private Slider fovSlider;
    [SerializeField] private SliderPercentText fovSliderValueText;
    [SerializeField] private TMP_Text fovValueText;
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private SliderPercentText mouseSensitivitySliderValueText;

    [Header("Call Phase Settings")]
    [SerializeField] private ThreeStepSlider responseSpeedSlider;
    [SerializeField] private Toggle autoQuestionToggle;
    [SerializeField] private Toggle autoValidateToggle;

    [Header("Active Visual")]
    [Range(1f, 1.5f)] [SerializeField] private float activeBrightness = 1.12f;
    [Range(0.6f, 1f)] [SerializeField] private float inactiveBrightness = 0.95f;

    [Header("Default Settings")]
    [SerializeField] private AppLanguage defaultLanguage = AppLanguage.Vietnamese;

    private CanvasGroup cgGen, cgGrap, cgAudio, cgCont;
    private Image btnGenImage, btnGrapImage, btnAudImage, btnContImage;
    private Image panelGenImage, panelGrapImage, panelAudioImage, panelContImage;
    private readonly Dictionary<Image, Color> baseImageColors = new Dictionary<Image, Color>();
    private bool hasUnsavedChanges;
    private bool dirtyTrackingBound;
    private bool isRestoringSnapshot;
    private bool hasSnapshot;
    private bool hasDefaultState;
    private AppLanguage languageSnapshot = AppLanguage.Vietnamese;
    private readonly Dictionary<Toggle, bool> toggleSnapshot = new Dictionary<Toggle, bool>();
    private readonly Dictionary<Slider, float> sliderSnapshot = new Dictionary<Slider, float>();
    private readonly Dictionary<Dropdown, int> dropdownSnapshot = new Dictionary<Dropdown, int>();
    private readonly Dictionary<TMP_Dropdown, int> tmpDropdownSnapshot = new Dictionary<TMP_Dropdown, int>();
    private readonly Dictionary<InputField, string> inputFieldSnapshot = new Dictionary<InputField, string>();
    private readonly Dictionary<TMP_InputField, string> tmpInputFieldSnapshot = new Dictionary<TMP_InputField, string>();
    private readonly Dictionary<Toggle, bool> toggleDefaults = new Dictionary<Toggle, bool>();
    private readonly Dictionary<Slider, float> sliderDefaults = new Dictionary<Slider, float>();
    private readonly Dictionary<Dropdown, int> dropdownDefaults = new Dictionary<Dropdown, int>();
    private readonly Dictionary<TMP_Dropdown, int> tmpDropdownDefaults = new Dictionary<TMP_Dropdown, int>();
    private readonly Dictionary<InputField, string> inputFieldDefaults = new Dictionary<InputField, string>();
    private readonly Dictionary<TMP_InputField, string> tmpInputFieldDefaults = new Dictionary<TMP_InputField, string>();
    private readonly List<DisplaySettingsService.ResolutionOption> supportedResolutions = new List<DisplaySettingsService.ResolutionOption>();
    private Coroutine saveFinalizeCoroutine;
    private bool isFinalizingSave;

    private void Awake()
    {
        if (TryDisableIfContainerDuplicate())
        {
            return;
        }

        ResolveRuntimeReferences();
        InitializeControlsSettings();
        ConfigureResolutionDropdown(useSavedSelection: false);
        ConfigureFovSlider();
        ConfigureMouseSensitivitySlider();
        InitializeResponseSpeedDefaultSelection();
        InitializeFovDefaultSelection();
        InitializeMouseSensitivityDefaultSelection();
        InitializeAutoQuestionDefaultSelection();
        InitializeAutoValidateDefaultSelection();
        ValidateResolvedReferences();
        CaptureDefaultState();
        Debug.Log($"Setting_UIScript[{GetInstanceLabel()}]: Awake", this);

        cgGen = GetOrAddCanvasGroup(panelGen);
        cgGrap = GetOrAddCanvasGroup(panelGrap);
        cgAudio = GetOrAddCanvasGroup(panelAudio);
        cgCont = GetOrAddCanvasGroup(panelCont);

        btnGenImage = GetButtonImage(btnGen);
        btnGrapImage = GetButtonImage(btnGrap);
        btnAudImage = GetButtonImage(btnAud);
        btnContImage = GetButtonImage(btnCont);

        panelGenImage = GetPanelImage(panelGen);
        panelGrapImage = GetPanelImage(panelGrap);
        panelAudioImage = GetPanelImage(panelAudio);
        panelContImage = GetPanelImage(panelCont);

        CacheBaseColor(btnGenImage);
        CacheBaseColor(btnGrapImage);
        CacheBaseColor(btnAudImage);
        CacheBaseColor(btnContImage);
        CacheBaseColor(panelGenImage);
        CacheBaseColor(panelGrapImage);
        CacheBaseColor(panelAudioImage);
        CacheBaseColor(panelContImage);

        if (btnGen != null) btnGen.onClick.AddListener(() => SelectOnly(cgGen, btnGenImage, panelGenImage));
        if (btnGrap != null) btnGrap.onClick.AddListener(() => SelectOnly(cgGrap, btnGrapImage, panelGrapImage));
        if (btnAud != null) btnAud.onClick.AddListener(() => SelectOnly(cgAudio, btnAudImage, panelAudioImage));
        if (btnCont != null) btnCont.onClick.AddListener(() => SelectOnly(cgCont, btnContImage, panelContImage));
        if (btnRestoreDefault != null) btnRestoreDefault.onClick.AddListener(OnRestoreDefaultsClicked);
        if (btnSave != null) btnSave.onClick.AddListener(OnSaveButtonClicked);
        if (btnExit != null) btnExit.onClick.AddListener(OnExitButtonClicked);
        if (fpsToggle != null) fpsToggle.onValueChanged.AddListener(OnFpsToggleValueChanged);
        if (fovSlider != null) fovSlider.onValueChanged.AddListener(OnFovSliderValueChanged);
        if (mouseSensitivitySlider != null) mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivitySliderValueChanged);

    }

    private bool TryDisableIfContainerDuplicate()
    {
        Setting_UIScript[] nestedInstances = GetComponentsInChildren<Setting_UIScript>(true);
        for (int index = 0; index < nestedInstances.Length; index++)
        {
            Setting_UIScript nestedInstance = nestedInstances[index];
            if (nestedInstance != null && nestedInstance != this)
            {
                Debug.LogWarning(
                    $"Setting_UIScript[{GetInstanceLabel()}]: Disabled because a nested Setting_UIScript exists at {GetHierarchyPath(nestedInstance.transform)}.",
                    this);
                enabled = false;
                return true;
            }
        }

        return false;
    }

    private void Start()
    {
        // Keep exactly one panel open at all times. Prefer General as default.
        if (cgGen != null) SelectOnly(cgGen, btnGenImage, panelGenImage);
        else if (cgGrap != null) SelectOnly(cgGrap, btnGrapImage, panelGrapImage);
        else if (cgAudio != null) SelectOnly(cgAudio, btnAudImage, panelAudioImage);
        else if (cgCont != null) SelectOnly(cgCont, btnContImage, panelContImage);

        LoadSavedResolutionSelection();
        LoadSavedVSyncSelection();
        LoadSavedFpsSelection();
        LoadSavedFovSelection();
        LoadSavedMouseSensitivitySelection();
        LoadSavedResponseSpeedSelection();
        LoadSavedAutoQuestionSelection();
        LoadSavedAutoValidateSelection();
        ApplyFpsTogglePreview();
        BindDirtyTracking();
        BeginEditSession();
    }

    private void SelectOnly(CanvasGroup target, Image activeButtonImage, Image activePanelImage)
    {
        if (target == null) return;

        SetTabState(cgGen, btnGenImage, panelGenImage, target == cgGen, activeButtonImage, activePanelImage);
        SetTabState(cgGrap, btnGrapImage, panelGrapImage, target == cgGrap, activeButtonImage, activePanelImage);
        SetTabState(cgAudio, btnAudImage, panelAudioImage, target == cgAudio, activeButtonImage, activePanelImage);
        SetTabState(cgCont, btnContImage, panelContImage, target == cgCont, activeButtonImage, activePanelImage);
    }

    private void SetTabState(
        CanvasGroup cg,
        Image buttonImage,
        Image panelImage,
        bool isActive,
        Image activeButtonImage,
        Image activePanelImage)
    {
        if (isActive) Show(cg);
        else Hide(cg);

        if (buttonImage != null)
        {
            float brightness = buttonImage == activeButtonImage ? activeBrightness : inactiveBrightness;
            ApplyBrightness(buttonImage, brightness);
        }

        if (panelImage != null)
        {
            float brightness = panelImage == activePanelImage ? activeBrightness : inactiveBrightness;
            ApplyBrightness(panelImage, brightness);
        }
    }

    private void Show(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
    }

    private void Hide(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        if (go == null) return null;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    private Image GetButtonImage(Button button)
    {
        if (button == null) return null;
        var image = button.targetGraphic as Image;
        if (image != null) return image;
        return button.GetComponent<Image>();
    }

    private Image GetPanelImage(GameObject panel)
    {
        if (panel == null) return null;
        return panel.GetComponent<Image>();
    }

    private void CacheBaseColor(Image image)
    {
        if (image == null || baseImageColors.ContainsKey(image)) return;
        baseImageColors.Add(image, image.color);
    }

    private void ApplyBrightness(Image image, float brightness)
    {
        if (image == null) return;

        if (!baseImageColors.TryGetValue(image, out Color baseColor))
        {
            baseColor = image.color;
            baseImageColors[image] = baseColor;
        }

        image.color = new Color(
            Mathf.Clamp01(baseColor.r * brightness),
            Mathf.Clamp01(baseColor.g * brightness),
            Mathf.Clamp01(baseColor.b * brightness),
            baseColor.a);
    }

    public bool HandleBackRequest(Action continueBackAction)
    {
        Debug.Log($"Setting_UIScript[{GetInstanceLabel()}]: HandleBackRequest start. isFinalizingSave={isFinalizingSave} hasSnapshot={hasSnapshot} hasUnsavedChanges={hasUnsavedChanges}", this);

        if (isFinalizingSave)
        {
            continueBackAction?.Invoke();
            return true;
        }

        RefreshDirtyState();

        if (!hasUnsavedChanges)
        {
            return false;
        }

        if (TryGetDifferenceDescription(out string unsavedDifference))
        {
            Debug.Log($"Setting_UIScript: Unsaved change detected before back. {unsavedDifference}", this);
        }

        ShowConfirmation(
            LocalizeText(backUnsavedConfirmMessageKey, backUnsavedConfirmMessage, GetConfirmationLanguage()),
            () =>
            {
                SaveChanges();
                continueBackAction?.Invoke();
            },
            () =>
            {
                RestoreSnapshot();
                continueBackAction?.Invoke();
            });

        return true;
    }

    public void BeginEditSession()
    {
        if (languageToggleBinder != null)
        {
            languageToggleBinder.RevertToCurrentLanguage();
        }

        CaptureSnapshot("BeginEditSession");
        hasUnsavedChanges = false;
        Debug.Log($"Setting_UIScript[{GetInstanceLabel()}]: BeginEditSession complete.", this);
    }

    public void ShowConfirmation(
        string message,
        Action onYes,
        Action onNo = null,
        string title = null,
        string customYesLabel = null,
        string customNoLabel = null)
    {
        if (toastContainer == null)
        {
            toastContainer = FindAnyObjectByType<ToastContainerController>();
        }

        if (toastContainer == null)
        {
            Debug.LogError("ToastContainerController is missing in scene.", this);
            return;
        }

        AppLanguage confirmLanguage = GetConfirmationLanguage();

        string resolvedTitle = string.IsNullOrEmpty(title)
            ? LocalizeText(confirmTitleKey, confirmTitle, confirmLanguage)
            : title;
        string resolvedMessage = string.IsNullOrEmpty(message)
            ? LocalizeText(saveConfirmMessageKey, saveConfirmMessage, confirmLanguage)
            : message;
        string resolvedYesLabel = string.IsNullOrEmpty(customYesLabel)
            ? LocalizeText(yesLabelKey, yesLabel, confirmLanguage)
            : customYesLabel;
        string resolvedNoLabel = string.IsNullOrEmpty(customNoLabel)
            ? LocalizeText(noLabelKey, noLabel, confirmLanguage)
            : customNoLabel;

        toastContainer.ShowConfirmation(
            resolvedTitle,
            resolvedMessage,
            onYes,
            onNo,
            resolvedYesLabel,
            resolvedNoLabel);
    }

    public void MarkDirty()
    {
        if (isRestoringSnapshot || isFinalizingSave)
        {
            return;
        }

        if (!hasSnapshot)
        {
            CaptureSnapshot();
        }

        hasUnsavedChanges = HasDifferencesFromSnapshot();
    }

    private void ResolveRuntimeReferences()
    {
        if (btnGen == null)
        {
            btnGen = FindButtonByName("btnGen");
        }

        if (btnGrap == null)
        {
            btnGrap = FindButtonByName("btnGrap");
        }

        if (btnAud == null)
        {
            btnAud = FindButtonByName("btnAud");
        }

        if (btnCont == null)
        {
            btnCont = FindButtonByName("btnCont");
        }

        if (btnRestoreDefault == null)
        {
            btnRestoreDefault = FindButtonByName("btnRestoreDefault");
        }

        if (btnSave == null)
        {
            btnSave = FindButtonByName("btnSave");
        }

        if (btnExit == null)
        {
            btnExit = FindButtonByName("btnExit");
        }

        if (toastContainer == null)
        {
            toastContainer = FindAnyObjectByType<ToastContainerController>();
        }

        if (languageToggleBinder == null)
        {
            languageToggleBinder = GetComponentInChildren<LanguageToggleBinder>(true);
        }

        ResolvePanelReferences();

        if (resolutionDropdown == null)
        {
            resolutionDropdown = FindResolutionDropdown();
        }

        if (vsyncToggle == null)
        {
            vsyncToggle = FindToggleByName("ToggVsync");
        }

        if (fpsToggle == null)
        {
            fpsToggle = FindToggleByName("ToggFPS");
        }

        fovSlider = FindSliderInNamedPanelChild(panelGrap, "FOV");
        fovSliderValueText = FindSliderPercentTextInNamedPanelChild(panelGrap, "FOV");
        fovValueText = FindTextInNamedPanelChild(panelGrap, "FOV", "FOVValueText");
        mouseSensitivitySlider = FindSliderInLocalizedPanelChild(panelCont, MouseSensitivityLocalizationKey);
        mouseSensitivitySliderValueText = FindSliderPercentTextInLocalizedPanelChild(panelCont, MouseSensitivityLocalizationKey);

        if (responseSpeedSlider == null)
        {
            responseSpeedSlider = FindResponseSpeedSlider();
        }

        autoQuestionToggle = FindFirstToggleInNamedPanelChild("AutoQuestion");
        autoValidateToggle = FindFirstToggleInNamedPanelChild("AutoConfirm");
    }

    private void ValidateResolvedReferences()
    {
        if (btnGen == null || btnGrap == null || btnAud == null || btnCont == null || btnSave == null || btnRestoreDefault == null || btnExit == null)
        {
            Debug.LogWarning("Setting_UIScript: One or more navigation/action buttons could not be resolved from the Setting prefab.", this);
        }

        if (panelGen == null || panelGrap == null || panelAudio == null || panelCont == null)
        {
            Debug.LogWarning("Setting_UIScript: One or more content panels could not be resolved from the Setting prefab.", this);
        }

        if (panelGrap != null && resolutionDropdown == null)
        {
            Debug.LogWarning("Setting_UIScript: Resolution dropdown could not be resolved from the graphics panel.", this);
        }

        if (panelGrap != null && vsyncToggle == null)
        {
            Debug.LogWarning("Setting_UIScript: VSync toggle could not be resolved from the graphics panel.", this);
        }

        if (fpsToggle == null)
        {
            Debug.LogWarning("Setting_UIScript: FPS toggle could not be resolved from the settings UI.", this);
        }

        if (panelGrap != null && fovSlider == null)
        {
            Debug.LogWarning("Setting_UIScript: FOV slider could not be resolved from the graphics panel.", this);
        }

        if (panelCont != null && mouseSensitivitySlider == null)
        {
            Debug.LogWarning("Setting_UIScript: Mouse sensitivity slider could not be resolved from the control panel.", this);
        }

        if (panelGen != null && responseSpeedSlider == null)
        {
            Debug.LogWarning("Setting_UIScript: Call Phase response speed slider could not be resolved from the general panel.", this);
        }

        if (panelGen != null && autoQuestionToggle == null)
        {
            Debug.LogWarning("Setting_UIScript: Call Phase auto-question toggle could not be resolved from the general panel.", this);
        }

        if (panelGen != null && autoValidateToggle == null)
        {
            Debug.LogWarning("Setting_UIScript: Call Phase auto-validate toggle could not be resolved from the general panel.", this);
        }
    }

    private Button FindButtonByName(string buttonName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button != null && string.Equals(button.name, buttonName, StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }
        }

        return null;
    }

    private TMP_Dropdown FindResolutionDropdown()
    {
        if (panelGrap == null)
        {
            return null;
        }

        TMP_Dropdown[] dropdowns = panelGrap.GetComponentsInChildren<TMP_Dropdown>(true);
        foreach (TMP_Dropdown dropdown in dropdowns)
        {
            if (dropdown != null)
            {
                return dropdown;
            }
        }

        return null;
    }

    private Toggle FindToggleByName(string toggleName)
    {
        Toggle[] toggles = GetComponentsInChildren<Toggle>(true);
        foreach (Toggle toggle in toggles)
        {
            if (toggle != null && string.Equals(toggle.name, toggleName, StringComparison.OrdinalIgnoreCase))
            {
                return toggle;
            }
        }

        return null;
    }

    private ThreeStepSlider FindResponseSpeedSlider()
    {
        if (panelGen == null)
        {
            return null;
        }

        Transform[] transforms = panelGen.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in transforms)
        {
            if (child == null || !string.Equals(child.name, "ResponseSpeed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ThreeStepSlider namedSlider = child.GetComponentInChildren<ThreeStepSlider>(true);
            if (namedSlider != null)
            {
                return namedSlider;
            }
        }

        return panelGen.GetComponentInChildren<ThreeStepSlider>(true);
    }

    private Toggle FindFirstToggleInNamedPanelChild(string childName)
    {
        if (panelGen == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        Transform[] transforms = panelGen.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in transforms)
        {
            if (child == null || !string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Toggle namedToggle = child.GetComponentInChildren<Toggle>(true);
            if (namedToggle != null)
            {
                return namedToggle;
            }
        }

        return null;
    }

    private Slider FindSliderInNamedPanelChild(GameObject panelRoot, string childName)
    {
        Transform namedChild = FindNamedPanelChild(panelRoot, childName);
        return namedChild != null ? namedChild.GetComponentInChildren<Slider>(true) : null;
    }

    private SliderPercentText FindSliderPercentTextInNamedPanelChild(GameObject panelRoot, string childName)
    {
        Transform namedChild = FindNamedPanelChild(panelRoot, childName);
        return namedChild != null ? namedChild.GetComponentInChildren<SliderPercentText>(true) : null;
    }

    private TMP_Text FindTextInNamedPanelChild(GameObject panelRoot, string childName, string textObjectName)
    {
        Transform namedChild = FindNamedPanelChild(panelRoot, childName);
        if (namedChild == null || string.IsNullOrWhiteSpace(textObjectName))
        {
            return null;
        }

        Transform[] transforms = namedChild.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in transforms)
        {
            if (child == null || !string.Equals(child.name, textObjectName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            TMP_Text text = child.GetComponent<TMP_Text>();
            if (text != null)
            {
                return text;
            }
        }

        return null;
    }

    private Slider FindSliderInLocalizedPanelChild(GameObject panelRoot, string localizationKey)
    {
        Transform section = FindPanelSectionByLocalizationKey(panelRoot, localizationKey);
        return section != null ? section.GetComponentInChildren<Slider>(true) : null;
    }

    private SliderPercentText FindSliderPercentTextInLocalizedPanelChild(GameObject panelRoot, string localizationKey)
    {
        Transform section = FindPanelSectionByLocalizationKey(panelRoot, localizationKey);
        return section != null ? section.GetComponentInChildren<SliderPercentText>(true) : null;
    }

    private Transform FindNamedPanelChild(GameObject panelRoot, string childName)
    {
        if (panelRoot == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        Transform[] transforms = panelRoot.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in transforms)
        {
            if (child != null && string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private Transform FindPanelSectionByLocalizationKey(GameObject panelRoot, string localizationKey)
    {
        if (panelRoot == null || string.IsNullOrWhiteSpace(localizationKey))
        {
            return null;
        }

        LocalizedText[] localizedTexts = panelRoot.GetComponentsInChildren<LocalizedText>(true);
        Transform panelRootTransform = panelRoot.transform;

        foreach (LocalizedText localizedText in localizedTexts)
        {
            if (localizedText == null || !string.Equals(localizedText.LocalizationKey, localizationKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Transform current = localizedText.transform;
            while (current != null)
            {
                if (current.GetComponentInChildren<Slider>(true) != null)
                {
                    return current;
                }

                if (current == panelRootTransform)
                {
                    break;
                }

                current = current.parent;
            }
        }

        return null;
    }

    private void ResolvePanelReferences()
    {
        if (panelGen != null && panelGrap != null && panelAudio != null && panelCont != null)
        {
            return;
        }

        Button[] navigationButtons = new[] { btnGen, btnGrap, btnAud, btnCont };
        Transform navigationMain = FindNavigationMain(navigationButtons);

        List<GameObject> panelCandidates = new List<GameObject>();
        RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform rect in rects)
        {
            if (rect == null || !string.Equals(rect.name, "Main", StringComparison.Ordinal))
            {
                continue;
            }

            if (navigationMain != null && rect == navigationMain)
            {
                continue;
            }

            panelCandidates.Add(rect.gameObject);
        }

        if (panelCandidates.Count == 0)
        {
            return;
        }

        panelGen ??= FindBestPanel(panelCandidates, IsGeneralPanelCandidate);
        panelGrap ??= FindBestPanel(panelCandidates, IsGraphicsPanelCandidate);
        panelAudio ??= FindBestPanel(panelCandidates, IsAudioPanelCandidate);
        panelCont ??= FindBestPanel(panelCandidates, IsControlPanelCandidate);

        if (panelGen != null)
        {
            panelCandidates.Remove(panelGen);
        }

        if (panelGrap != null)
        {
            panelCandidates.Remove(panelGrap);
        }

        if (panelAudio != null)
        {
            panelCandidates.Remove(panelAudio);
        }

        if (panelCont != null)
        {
            panelCandidates.Remove(panelCont);
        }

        if (panelGen == null && panelCandidates.Count > 0)
        {
            panelGen = panelCandidates[0];
            panelCandidates.RemoveAt(0);
        }

        if (panelGrap == null && panelCandidates.Count > 0)
        {
            panelGrap = panelCandidates[0];
            panelCandidates.RemoveAt(0);
        }

        if (panelAudio == null && panelCandidates.Count > 0)
        {
            panelAudio = panelCandidates[0];
            panelCandidates.RemoveAt(0);
        }

        if (panelCont == null && panelCandidates.Count > 0)
        {
            panelCont = panelCandidates[0];
        }
    }

    private Transform FindNavigationMain(Button[] navigationButtons)
    {
        foreach (Button button in navigationButtons)
        {
            if (button == null)
            {
                continue;
            }

            Transform current = button.transform;
            while (current != null && current != transform)
            {
                if (string.Equals(current.name, "Main", StringComparison.Ordinal))
                {
                    return current;
                }

                current = current.parent;
            }
        }

        return null;
    }

    private GameObject FindBestPanel(List<GameObject> candidates, Func<GameObject, bool> predicate)
    {
        foreach (GameObject candidate in candidates)
        {
            if (candidate != null && predicate(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private bool IsGeneralPanelCandidate(GameObject candidate)
    {
        return ContainsNamedTransform(candidate.transform, "Toggle_VI") ||
               ContainsNamedTransform(candidate.transform, "Toggle_EN") ||
               candidate.GetComponentInChildren<LanguageToggleBinder>(true) != null;
    }

    private bool IsGraphicsPanelCandidate(GameObject candidate)
    {
        return ContainsNamedTransform(candidate.transform, "Toggle_Graphic_Low") ||
               ContainsNamedTransform(candidate.transform, "Toggle_Graphic_Medium") ||
               ContainsNamedTransform(candidate.transform, "Toggle_Graphic_High") ||
               ContainsNamedTransform(candidate.transform, "Toggle_Graphic_Ultra");
    }

    private bool IsAudioPanelCandidate(GameObject candidate)
    {
        return candidate.GetComponentsInChildren<Slider>(true).Length >= 2;
    }

    private bool IsControlPanelCandidate(GameObject candidate)
    {
        return ContainsNamedTransform(candidate.transform, "Toggle_GameMode_Simulation") ||
               ContainsNamedTransform(candidate.transform, "Toggle_GameMode_Acade") ||
               candidate.GetComponentInChildren<SwitchToggleUI>(true) != null;
    }

    private bool ContainsNamedTransform(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in transforms)
        {
            if (child != null && string.Equals(child.name, objectName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void BindDirtyTracking()
    {
        if (dirtyTrackingBound)
        {
            return;
        }

        dirtyTrackingBound = true;

        RegisterDirtyForPanel(panelGen);
        RegisterDirtyForPanel(panelGrap);
        RegisterDirtyForPanel(panelAudio);
        RegisterDirtyForPanel(panelCont);
    }

    private void RegisterDirtyForPanel(GameObject panelRoot)
    {
        if (panelRoot == null)
        {
            return;
        }

        Toggle[] toggles = panelRoot.GetComponentsInChildren<Toggle>(true);
        foreach (Toggle toggle in toggles)
        {
            toggle.onValueChanged.AddListener(_ => MarkDirty());
        }

        Slider[] sliders = panelRoot.GetComponentsInChildren<Slider>(true);
        foreach (Slider slider in sliders)
        {
            slider.onValueChanged.AddListener(_ => MarkDirty());
        }

        Dropdown[] dropdowns = panelRoot.GetComponentsInChildren<Dropdown>(true);
        foreach (Dropdown dropdown in dropdowns)
        {
            dropdown.onValueChanged.AddListener(_ => MarkDirty());
        }

        TMP_Dropdown[] tmpDropdowns = panelRoot.GetComponentsInChildren<TMP_Dropdown>(true);
        foreach (TMP_Dropdown dropdown in tmpDropdowns)
        {
            dropdown.onValueChanged.AddListener(_ => MarkDirty());
        }

        InputField[] inputFields = panelRoot.GetComponentsInChildren<InputField>(true);
        foreach (InputField inputField in inputFields)
        {
            inputField.onValueChanged.AddListener(_ => MarkDirty());
        }

        TMP_InputField[] tmpInputFields = panelRoot.GetComponentsInChildren<TMP_InputField>(true);
        foreach (TMP_InputField inputField in tmpInputFields)
        {
            inputField.onValueChanged.AddListener(_ => MarkDirty());
        }
    }

    private void OnSaveButtonClicked()
    {
        RefreshDirtyState();

        if (!hasUnsavedChanges)
        {
            return;
        }

        ShowConfirmation(LocalizeText(saveConfirmMessageKey, saveConfirmMessage, GetConfirmationLanguage()), SaveChanges);
    }

    private void OnRestoreDefaultsClicked()
    {
        if (!hasDefaultState)
        {
            CaptureDefaultState();
        }

        ShowConfirmation(
            LocalizeText(restoreConfirmMessageKey, restoreConfirmMessage, GetConfirmationLanguage()),
            RestoreDefaultsAndSave);
    }

    private void OnExitButtonClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void RestoreDefaultsAndSave()
    {
        RestoreDefaults();
        SaveChanges();
    }

    private string GetInstanceLabel()
    {
        return $"{GetHierarchyPath(transform)}#{GetHashCode()}";
    }

}
