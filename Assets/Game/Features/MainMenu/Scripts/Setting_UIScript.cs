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
    [SerializeField] private TMP_Dropdown antiAliasingDropdown;
    [SerializeField] private Toggle vsyncToggle;
    [SerializeField] private ThreeStepSlider shadowQualitySlider;
    [SerializeField] private Toggle fpsToggle;
    [SerializeField] private Slider fovSlider;
    [SerializeField] private SliderPercentText fovSliderValueText;
    [SerializeField] private TMP_Text fovValueText;
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private SliderPercentText mouseSensitivitySliderValueText;

    [Header("Audio Settings")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private SliderPercentText masterVolumeSliderValueText;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private SliderPercentText musicVolumeSliderValueText;
    [SerializeField] private Slider ambienceVolumeSlider;
    [SerializeField] private SliderPercentText ambienceVolumeSliderValueText;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private SliderPercentText sfxVolumeSliderValueText;
    [SerializeField] private Slider voiceVolumeSlider;
    [SerializeField] private SliderPercentText voiceVolumeSliderValueText;
    [SerializeField] private Slider uiVolumeSlider;
    [SerializeField] private SliderPercentText uiVolumeSliderValueText;

    [Header("Call Phase Settings")]
    [SerializeField] private ThreeStepSlider responseSpeedSlider;
    [SerializeField] private Toggle autoQuestionToggle;
    [SerializeField] private Toggle autoValidateToggle;
    [SerializeField] private Toggle minimapToggle;
    [SerializeField] private TMP_Dropdown minimapTypeDropdown;

    [Header("Tab Visual")]
    [SerializeField] private Color hoverTabBackgroundColor = new Color32(0x34, 0x34, 0x34, 0xFF);
    [SerializeField] private Color selectedTabBackgroundColor = new Color32(0x4C, 0x4C, 0x4C, 0xFF);
    [SerializeField] private Color hoverTabAccentColor = new Color32(0xFF, 0xB0, 0x4A, 0xFF);
    [SerializeField] private Color selectedTabAccentColor = new Color32(0xFF, 0x8A, 0x00, 0xFF);
    [SerializeField] private Color hoverTabLabelColor = new Color32(0xFF, 0xB0, 0x4A, 0xFF);
    [SerializeField] private Color selectedTabLabelColor = new Color32(0xFF, 0x8A, 0x00, 0xFF);

    [Header("Action Button Hover")]
    [SerializeField] private Color hoverActionButtonBackgroundColor = new Color32(0x66, 0x66, 0x66, 0xFF);
    [SerializeField] private Color hoverActionButtonAccentColor = new Color32(0xFF, 0xB0, 0x4A, 0xFF);
    [SerializeField] private Color hoverActionButtonLabelColor = Color.white;
    [SerializeField] private Vector3 hoverActionButtonScale = new Vector3(1.02f, 1.02f, 1f);

    [Header("Default Settings")]
    [SerializeField] private AppLanguage defaultLanguage = AppLanguage.Vietnamese;

    private CanvasGroup cgGen, cgGrap, cgAudio, cgCont;
    private SettingTabButtonVisual btnGenVisual;
    private SettingTabButtonVisual btnGrapVisual;
    private SettingTabButtonVisual btnAudVisual;
    private SettingTabButtonVisual btnContVisual;
    private SettingActionButtonHoverVisual btnRestoreDefaultHoverVisual;
    private SettingActionButtonHoverVisual btnSaveHoverVisual;
    private SettingActionButtonHoverVisual btnExitHoverVisual;
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
    private readonly List<DisplaySettingsService.AntiAliasingOption> antiAliasingOptions = new List<DisplaySettingsService.AntiAliasingOption>();
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
        ConfigureAntiAliasingDropdown();
        ConfigureFovSlider();
        ConfigureMouseSensitivitySlider();
        ConfigureAudioSliders();
        InitializeResponseSpeedDefaultSelection();
        InitializeFovDefaultSelection();
        InitializeMouseSensitivityDefaultSelection();
        InitializeAudioDefaultSelection();
        InitializeAutoQuestionDefaultSelection();
        InitializeAutoValidateDefaultSelection();
        InitializeMinimapToggleDefaultSelection();
        InitializeMinimapTypeDefaultSelection();
        ValidateResolvedReferences();
        CaptureDefaultState();
        Debug.Log($"Setting_UIScript[{GetInstanceLabel()}]: Awake", this);

        cgGen = GetOrAddCanvasGroup(panelGen);
        cgGrap = GetOrAddCanvasGroup(panelGrap);
        cgAudio = GetOrAddCanvasGroup(panelAudio);
        cgCont = GetOrAddCanvasGroup(panelCont);
        btnGenVisual = ConfigureTabVisual(btnGen);
        btnGrapVisual = ConfigureTabVisual(btnGrap);
        btnAudVisual = ConfigureTabVisual(btnAud);
        btnContVisual = ConfigureTabVisual(btnCont);
        btnRestoreDefaultHoverVisual = ConfigureActionButtonHoverVisual(btnRestoreDefault);
        btnSaveHoverVisual = ConfigureActionButtonHoverVisual(btnSave);
        btnExitHoverVisual = ConfigureActionButtonHoverVisual(btnExit);

        if (btnGen != null) btnGen.onClick.AddListener(() => SelectOnly(cgGen));
        if (btnGrap != null) btnGrap.onClick.AddListener(() => SelectOnly(cgGrap));
        if (btnAud != null) btnAud.onClick.AddListener(() => SelectOnly(cgAudio));
        if (btnCont != null) btnCont.onClick.AddListener(() => SelectOnly(cgCont));
        if (btnRestoreDefault != null) btnRestoreDefault.onClick.AddListener(OnRestoreDefaultsClicked);
        if (btnSave != null) btnSave.onClick.AddListener(OnSaveButtonClicked);
        if (btnExit != null) btnExit.onClick.AddListener(OnExitButtonClicked);
        if (antiAliasingDropdown != null) antiAliasingDropdown.onValueChanged.AddListener(OnAntiAliasingDropdownValueChanged);
        if (fpsToggle != null) fpsToggle.onValueChanged.AddListener(OnFpsToggleValueChanged);
        if (shadowQualitySlider != null) shadowQualitySlider.AddStepChangedListener(OnShadowQualityStepChanged);
        if (fovSlider != null) fovSlider.onValueChanged.AddListener(OnFovSliderValueChanged);
        if (mouseSensitivitySlider != null) mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivitySliderValueChanged);
        RegisterAudioSliderListener(masterVolumeSlider, AudioBus.Master);
        RegisterAudioSliderListener(musicVolumeSlider, AudioBus.Music);
        RegisterAudioSliderListener(ambienceVolumeSlider, AudioBus.Ambience);
        RegisterAudioSliderListener(sfxVolumeSlider, AudioBus.Sfx);
        RegisterAudioSliderListener(voiceVolumeSlider, AudioBus.Voice);
        RegisterAudioSliderListener(uiVolumeSlider, AudioBus.Ui);
        if (minimapToggle != null) minimapToggle.onValueChanged.AddListener(OnMinimapToggleValueChanged);
        if (minimapTypeDropdown != null) minimapTypeDropdown.onValueChanged.AddListener(OnMinimapTypeDropdownValueChanged);

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
        if (cgGen != null) SelectOnly(cgGen);
        else if (cgGrap != null) SelectOnly(cgGrap);
        else if (cgAudio != null) SelectOnly(cgAudio);
        else if (cgCont != null) SelectOnly(cgCont);

        LoadSavedResolutionSelection();
        LoadSavedAntiAliasingSelection();
        LoadSavedVSyncSelection();
        LoadSavedShadowQualitySelection();
        LoadSavedFpsSelection();
        LoadSavedFovSelection();
        LoadSavedMouseSensitivitySelection();
        LoadSavedAudioSelection();
        LoadSavedResponseSpeedSelection();
        LoadSavedAutoQuestionSelection();
        LoadSavedAutoValidateSelection();
        LoadSavedMinimapToggleSelection();
        LoadSavedMinimapTypeSelection();
        ApplyFpsTogglePreview();
        BindDirtyTracking();
        BeginEditSession();
    }

    private void SelectOnly(CanvasGroup target)
    {
        if (target == null) return;

        SetTabState(cgGen, btnGenVisual, target == cgGen);
        SetTabState(cgGrap, btnGrapVisual, target == cgGrap);
        SetTabState(cgAudio, btnAudVisual, target == cgAudio);
        SetTabState(cgCont, btnContVisual, target == cgCont);
    }

    private void SetTabState(CanvasGroup cg, SettingTabButtonVisual visual, bool isActive)
    {
        if (isActive) Show(cg);
        else Hide(cg);

        if (visual != null)
        {
            visual.SetSelected(isActive);
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

    private SettingTabButtonVisual ConfigureTabVisual(Button button)
    {
        if (button == null)
        {
            return null;
        }

        SettingTabButtonVisual visual = button.GetComponent<SettingTabButtonVisual>();
        if (visual == null)
        {
            return null;
        }

        visual.RefreshBindings();
        return visual;
    }

    private SettingActionButtonHoverVisual ConfigureActionButtonHoverVisual(Button button)
    {
        if (button == null)
        {
            return null;
        }

        SettingActionButtonHoverVisual visual = button.GetComponent<SettingActionButtonHoverVisual>();
        if (visual == null)
        {
            return null;
        }

        visual.Configure(
            hoverActionButtonBackgroundColor,
            hoverActionButtonAccentColor,
            hoverActionButtonLabelColor,
            hoverActionButtonScale);
        visual.RefreshBindings();
        return visual;
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

        antiAliasingDropdown ??= FindTmpDropdownInNamedPanelChild(panelGrap, "DropdownAniDropdownAntiAliasing");
        antiAliasingDropdown ??= FindTmpDropdownByObjectName("DropdownAniDropdownAntiAliasing");
        antiAliasingDropdown ??= FindTmpDropdownInNamedPanelChild(panelGrap, "DropdownAntiAliasing");
        antiAliasingDropdown ??= FindTmpDropdownByObjectName("DropdownAntiAliasing");
        antiAliasingDropdown ??= FindTmpDropdownInNamedPanelChild(panelGrap, "AntiAliasing");
        antiAliasingDropdown ??= FindTmpDropdownByKeyword(panelGrap, "anti");

        if (resolutionDropdown != null && antiAliasingDropdown != null && ReferenceEquals(resolutionDropdown, antiAliasingDropdown))
        {
            resolutionDropdown = FindResolutionDropdownExcluding(antiAliasingDropdown);
        }

        if (vsyncToggle == null)
        {
            vsyncToggle = FindToggleByName("ToggVsync");
        }

        if (fpsToggle == null)
        {
            fpsToggle = FindToggleByName("ToggFPS");
        }

        shadowQualitySlider ??= FindThreeStepSliderInNamedPanelChild(panelGrap, "ShadowSlider");
        shadowQualitySlider ??= FindThreeStepSliderByObjectName("ShadowSlider");

        fovSlider = FindSliderInNamedPanelChild(panelGrap, "FOV");
        fovSliderValueText = FindSliderPercentTextInNamedPanelChild(panelGrap, "FOV");
        fovValueText = FindTextInNamedPanelChild(panelGrap, "FOV", "FOVValueText");
        mouseSensitivitySlider = FindSliderInLocalizedPanelChild(panelCont, MouseSensitivityLocalizationKey);
        mouseSensitivitySliderValueText = FindSliderPercentTextInLocalizedPanelChild(panelCont, MouseSensitivityLocalizationKey);
        masterVolumeSlider = FindSliderInNamedPanelChild(panelAudio, "Master");
        masterVolumeSliderValueText = FindSliderPercentTextInNamedPanelChild(panelAudio, "Master");
        musicVolumeSlider = FindSliderInNamedPanelChild(panelAudio, "Music");
        musicVolumeSliderValueText = FindSliderPercentTextInNamedPanelChild(panelAudio, "Music");
        ambienceVolumeSlider = FindSliderInNamedPanelChild(panelAudio, "Ambience");
        ambienceVolumeSliderValueText = FindSliderPercentTextInNamedPanelChild(panelAudio, "Ambience");
        sfxVolumeSlider = FindSliderInNamedPanelChild(panelAudio, "SFX");
        sfxVolumeSliderValueText = FindSliderPercentTextInNamedPanelChild(panelAudio, "SFX");
        voiceVolumeSlider = FindSliderInNamedPanelChild(panelAudio, "Radio");
        voiceVolumeSliderValueText = FindSliderPercentTextInNamedPanelChild(panelAudio, "Radio");
        uiVolumeSlider = FindSliderInNamedPanelChild(panelAudio, "UI");
        uiVolumeSliderValueText = FindSliderPercentTextInNamedPanelChild(panelAudio, "UI");

        if (responseSpeedSlider == null)
        {
            responseSpeedSlider = FindResponseSpeedSlider();
        }

        if (minimapToggle == null)
        {
            minimapToggle = FindToggleByName("ToggMinimap");
        }

        if (minimapTypeDropdown == null)
        {
            minimapTypeDropdown = FindMinimapTypeDropdown();
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

        if (panelGrap != null && antiAliasingDropdown == null)
        {
            Debug.LogWarning("Setting_UIScript: Anti-aliasing dropdown could not be resolved from the graphics panel.", this);
        }

        if (panelGrap != null && vsyncToggle == null)
        {
            Debug.LogWarning("Setting_UIScript: VSync toggle could not be resolved from the graphics panel.", this);
        }

        if (fpsToggle == null)
        {
            Debug.LogWarning("Setting_UIScript: FPS toggle could not be resolved from the settings UI.", this);
        }

        if (panelGrap != null && shadowQualitySlider == null)
        {
            Debug.LogWarning("Setting_UIScript: Shadow quality slider could not be resolved from the graphics panel.", this);
        }

        if (panelGrap != null && fovSlider == null)
        {
            Debug.LogWarning("Setting_UIScript: FOV slider could not be resolved from the graphics panel.", this);
        }

        if (panelCont != null && mouseSensitivitySlider == null)
        {
            Debug.LogWarning("Setting_UIScript: Mouse sensitivity slider could not be resolved from the control panel.", this);
        }

        if (panelAudio != null && masterVolumeSlider == null)
        {
            Debug.LogWarning("Setting_UIScript: Master volume slider could not be resolved from the audio panel.", this);
        }

        if (panelAudio != null && musicVolumeSlider == null)
        {
            Debug.LogWarning("Setting_UIScript: Music volume slider could not be resolved from the audio panel.", this);
        }

        if (panelAudio != null && ambienceVolumeSlider == null)
        {
            Debug.LogWarning("Setting_UIScript: Ambience volume slider could not be resolved from the audio panel.", this);
        }

        if (panelAudio != null && sfxVolumeSlider == null)
        {
            Debug.LogWarning("Setting_UIScript: SFX volume slider could not be resolved from the audio panel.", this);
        }

        if (panelAudio != null && voiceVolumeSlider == null)
        {
            Debug.LogWarning("Setting_UIScript: Voice / Radio volume slider could not be resolved from the audio panel.", this);
        }

        if (panelAudio != null && uiVolumeSlider == null)
        {
            Debug.LogWarning("Setting_UIScript: UI volume slider could not be resolved from the audio panel.", this);
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

        if (panelGen != null && minimapTypeDropdown == null)
        {
            Debug.LogWarning("Setting_UIScript: Minimap type dropdown could not be resolved from the general panel.", this);
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

    private TMP_Dropdown FindMinimapTypeDropdown()
    {
        if (panelGen == null)
        {
            return null;
        }

        Transform minimapTypeRoot = FindNamedPanelChild(panelGen, "MinimapType");
        if (minimapTypeRoot == null)
        {
            return null;
        }

        return minimapTypeRoot.GetComponentInChildren<TMP_Dropdown>(true);
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

    private ThreeStepSlider FindThreeStepSliderInNamedPanelChild(GameObject panelRoot, string childName)
    {
        Transform namedChild = FindNamedPanelChild(panelRoot, childName);
        return namedChild != null ? namedChild.GetComponentInChildren<ThreeStepSlider>(true) : null;
    }

    private ThreeStepSlider FindThreeStepSliderByObjectName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        ThreeStepSlider[] sliders = GetComponentsInChildren<ThreeStepSlider>(true);
        foreach (ThreeStepSlider candidate in sliders)
        {
            if (candidate != null && string.Equals(candidate.gameObject.name, objectName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private TMP_Dropdown FindResolutionDropdownExcluding(TMP_Dropdown excludedDropdown)
    {
        if (panelGrap == null)
        {
            return null;
        }

        TMP_Dropdown[] dropdowns = panelGrap.GetComponentsInChildren<TMP_Dropdown>(true);
        foreach (TMP_Dropdown dropdown in dropdowns)
        {
            if (dropdown != null && !ReferenceEquals(dropdown, excludedDropdown))
            {
                return dropdown;
            }
        }

        return null;
    }

    private TMP_Dropdown FindTmpDropdownInNamedPanelChild(GameObject panelRoot, string childName)
    {
        Transform namedChild = FindNamedPanelChild(panelRoot, childName);
        return namedChild != null ? namedChild.GetComponentInChildren<TMP_Dropdown>(true) : null;
    }

    private TMP_Dropdown FindTmpDropdownByObjectName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        TMP_Dropdown[] dropdowns = GetComponentsInChildren<TMP_Dropdown>(true);
        foreach (TMP_Dropdown candidate in dropdowns)
        {
            if (candidate != null && string.Equals(candidate.gameObject.name, objectName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private TMP_Dropdown FindTmpDropdownByKeyword(GameObject panelRoot, string keyword)
    {
        if (panelRoot == null || string.IsNullOrWhiteSpace(keyword))
        {
            return null;
        }

        TMP_Dropdown[] dropdowns = panelRoot.GetComponentsInChildren<TMP_Dropdown>(true);
        for (int index = 0; index < dropdowns.Length; index++)
        {
            TMP_Dropdown dropdown = dropdowns[index];
            if (dropdown == null)
            {
                continue;
            }

            string objectName = dropdown.gameObject.name;
            if (objectName != null && objectName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return dropdown;
            }
        }

        return null;
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
        return ContainsNamedTransform(candidate.transform, "ShadowSlider") ||
               ContainsNamedTransform(candidate.transform, "DropdownAniDropdownAntiAliasing") ||
               ContainsNamedTransform(candidate.transform, "DropdownAntiAliasing") ||
               ContainsNamedTransform(candidate.transform, "AntiAliasing") ||
               ContainsNamedTransform(candidate.transform, "FOV") ||
               ContainsNamedTransform(candidate.transform, "ToggVsync");
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

    private void RegisterAudioSliderListener(Slider slider, AudioBus bus)
    {
        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.AddListener(_ => ApplyAudioSliderPreview(bus, slider));
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
