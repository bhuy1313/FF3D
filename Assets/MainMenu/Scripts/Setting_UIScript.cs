using System;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Setting_UIScript : MonoBehaviour
{
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
        ConfigureResolutionDropdown(useSavedSelection: false);
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
            toastContainer = FindFirstObjectByType<ToastContainerController>();
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
            toastContainer = FindFirstObjectByType<ToastContainerController>();
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

    private void SaveChanges()
    {
        isFinalizingSave = true;
        Debug.Log($"Setting_UIScript[{GetInstanceLabel()}]: SaveChanges start.", this);

        SaveResolutionSelection();
        SaveVSyncSelection();
        SaveFpsSelection();
        SaveLanguageSelection();

        PlayerPrefs.Save();
        CaptureSnapshot("SaveChanges immediate");
        hasUnsavedChanges = false;
        LogSaveBaselineState("immediate");

        if (saveFinalizeCoroutine != null)
        {
            StopCoroutine(saveFinalizeCoroutine);
        }

        saveFinalizeCoroutine = StartCoroutine(FinalizeSaveChangesAfterFrame());
    }

    private void ConfigureResolutionDropdown(bool useSavedSelection)
    {
        supportedResolutions.Clear();

        if (resolutionDropdown == null)
        {
            return;
        }

        supportedResolutions.AddRange(DisplaySettingsService.GetSupportedResolutions());
        if (supportedResolutions.Count == 0)
        {
            return;
        }

        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>(supportedResolutions.Count);
        foreach (DisplaySettingsService.ResolutionOption option in supportedResolutions)
        {
            options.Add(new TMP_Dropdown.OptionData(option.Label));
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);

        int selectedIndex = DisplaySettingsService.GetRecommendedIndex(supportedResolutions);
        if (useSavedSelection && DisplaySettingsService.TryGetSavedResolution(out int savedWidth, out int savedHeight))
        {
            int savedIndex = DisplaySettingsService.FindBestIndex(supportedResolutions, savedWidth, savedHeight);
            if (savedIndex >= 0)
            {
                selectedIndex = savedIndex;
            }
        }

        SetResolutionDropdownValue(selectedIndex);
    }

    private void LoadSavedResolutionSelection()
    {
        if (resolutionDropdown == null || supportedResolutions.Count == 0)
        {
            return;
        }

        if (!DisplaySettingsService.TryGetSavedResolution(out int savedWidth, out int savedHeight))
        {
            return;
        }

        int savedIndex = DisplaySettingsService.FindBestIndex(supportedResolutions, savedWidth, savedHeight);
        if (savedIndex >= 0)
        {
            SetResolutionDropdownValue(savedIndex);
        }
    }

    private void SaveResolutionSelection()
    {
        if (resolutionDropdown == null || supportedResolutions.Count == 0)
        {
            return;
        }

        int selectedIndex = Mathf.Clamp(resolutionDropdown.value, 0, supportedResolutions.Count - 1);
        DisplaySettingsService.ResolutionOption selectedResolution = supportedResolutions[selectedIndex];
        bool hasSavedResolution = DisplaySettingsService.TryGetSavedResolution(out int savedWidth, out int savedHeight);
        if (!hasSavedResolution || savedWidth != selectedResolution.Width || savedHeight != selectedResolution.Height)
        {
            DisplaySettingsService.SaveResolution(selectedResolution.Width, selectedResolution.Height);
        }

        if (Screen.width != selectedResolution.Width || Screen.height != selectedResolution.Height)
        {
            DisplaySettingsService.ApplyResolution(selectedResolution);
        }
    }

    private void LoadSavedVSyncSelection()
    {
        if (vsyncToggle == null)
        {
            return;
        }

        bool vsyncEnabled = DisplaySettingsService.GetCurrentVSyncEnabled();
        DisplaySettingsService.TryGetSavedVSyncEnabled(out vsyncEnabled);

        vsyncToggle.SetIsOnWithoutNotify(vsyncEnabled);
    }

    private void SaveVSyncSelection()
    {
        if (vsyncToggle == null)
        {
            return;
        }

        bool hasSavedVSync = DisplaySettingsService.TryGetSavedVSyncEnabled(out bool savedVSyncEnabled);
        if (!hasSavedVSync || savedVSyncEnabled != vsyncToggle.isOn)
        {
            DisplaySettingsService.SaveVSyncEnabled(vsyncToggle.isOn);
        }

        if (DisplaySettingsService.GetCurrentVSyncEnabled() != vsyncToggle.isOn)
        {
            DisplaySettingsService.ApplyVSync(vsyncToggle.isOn);
        }
    }

    private void LoadSavedFpsSelection()
    {
        if (fpsToggle == null)
        {
            return;
        }

        if (!DisplaySettingsService.TryGetSavedShowFpsOverlay(out bool showFps))
        {
            return;
        }

        fpsToggle.SetIsOnWithoutNotify(showFps);
    }

    private void SaveFpsSelection()
    {
        if (fpsToggle == null)
        {
            return;
        }

        bool hasSavedFpsPreference = DisplaySettingsService.TryGetSavedShowFpsOverlay(out bool savedShowFps);
        if (!hasSavedFpsPreference || savedShowFps != fpsToggle.isOn)
        {
            DisplaySettingsService.SaveShowFpsOverlay(fpsToggle.isOn);
        }

        FPSOverlayRuntimeController.SetOverlayVisible(fpsToggle.isOn);
    }

    private void SaveLanguageSelection()
    {
        if (LanguageManager.Instance == null)
        {
            return;
        }

        if (languageToggleBinder != null && languageToggleBinder.TryGetSelectedLanguage(out AppLanguage selectedLanguage))
        {
            if (LanguageManager.Instance.CurrentLanguage != selectedLanguage)
            {
                LanguageManager.Instance.SetLanguage(selectedLanguage, true, true);
            }

            return;
        }

        LanguageManager.Instance.SetLanguage(LanguageManager.Instance.CurrentLanguage, true, false);
    }

    private void OnFpsToggleValueChanged(bool _)
    {
        ApplyFpsTogglePreview();
    }

    private void ApplyFpsTogglePreview()
    {
        if (fpsToggle == null)
        {
            return;
        }

        FPSOverlayRuntimeController.SetOverlayVisible(fpsToggle.isOn);
    }

    private void SetResolutionDropdownValue(int index)
    {
        if (resolutionDropdown == null || supportedResolutions.Count == 0)
        {
            return;
        }

        int clampedIndex = Mathf.Clamp(index, 0, supportedResolutions.Count - 1);
        resolutionDropdown.SetValueWithoutNotify(clampedIndex);
        resolutionDropdown.RefreshShownValue();
    }

    private void CaptureDefaultState()
    {
        toggleDefaults.Clear();
        sliderDefaults.Clear();
        dropdownDefaults.Clear();
        tmpDropdownDefaults.Clear();
        inputFieldDefaults.Clear();
        tmpInputFieldDefaults.Clear();

        CapturePanelDefaults(panelGen);
        CapturePanelDefaults(panelGrap);
        CapturePanelDefaults(panelAudio);
        CapturePanelDefaults(panelCont);

        hasDefaultState = true;
    }

    private void CapturePanelDefaults(GameObject panelRoot)
    {
        if (panelRoot == null)
        {
            return;
        }

        Toggle[] toggles = panelRoot.GetComponentsInChildren<Toggle>(true);
        foreach (Toggle toggle in toggles)
        {
            if (toggle != null && !toggleDefaults.ContainsKey(toggle))
            {
                toggleDefaults.Add(toggle, toggle.isOn);
            }
        }

        Slider[] sliders = panelRoot.GetComponentsInChildren<Slider>(true);
        foreach (Slider slider in sliders)
        {
            if (slider != null && !sliderDefaults.ContainsKey(slider))
            {
                sliderDefaults.Add(slider, slider.value);
            }
        }

        Dropdown[] dropdowns = panelRoot.GetComponentsInChildren<Dropdown>(true);
        foreach (Dropdown dropdown in dropdowns)
        {
            if (dropdown != null && !dropdownDefaults.ContainsKey(dropdown))
            {
                dropdownDefaults.Add(dropdown, dropdown.value);
            }
        }

        TMP_Dropdown[] tmpDropdowns = panelRoot.GetComponentsInChildren<TMP_Dropdown>(true);
        foreach (TMP_Dropdown dropdown in tmpDropdowns)
        {
            if (dropdown != null && !tmpDropdownDefaults.ContainsKey(dropdown))
            {
                tmpDropdownDefaults.Add(dropdown, dropdown.value);
            }
        }

        InputField[] inputFields = panelRoot.GetComponentsInChildren<InputField>(true);
        foreach (InputField inputField in inputFields)
        {
            if (inputField != null && !inputFieldDefaults.ContainsKey(inputField))
            {
                inputFieldDefaults.Add(inputField, inputField.text);
            }
        }

        TMP_InputField[] tmpInputFields = panelRoot.GetComponentsInChildren<TMP_InputField>(true);
        foreach (TMP_InputField inputField in tmpInputFields)
        {
            if (inputField != null && !tmpInputFieldDefaults.ContainsKey(inputField))
            {
                tmpInputFieldDefaults.Add(inputField, inputField.text);
            }
        }
    }

    private void CaptureSnapshot(string reason = null)
    {
        toggleSnapshot.Clear();
        sliderSnapshot.Clear();
        dropdownSnapshot.Clear();
        tmpDropdownSnapshot.Clear();
        inputFieldSnapshot.Clear();
        tmpInputFieldSnapshot.Clear();

        CapturePanelSnapshot(panelGen);
        CapturePanelSnapshot(panelGrap);
        CapturePanelSnapshot(panelAudio);
        CapturePanelSnapshot(panelCont);

        if (LanguageManager.Instance != null)
        {
            languageSnapshot = LanguageManager.Instance.CurrentLanguage;
        }

        hasSnapshot = true;

        if (vsyncToggle != null && toggleSnapshot.TryGetValue(vsyncToggle, out bool vsyncSnapshotValue))
        {
            Debug.Log(
                $"Setting_UIScript[{GetInstanceLabel()}]: CaptureSnapshot reason='{reason ?? "<none>"}' VSync current={vsyncToggle.isOn} snapshot={vsyncSnapshotValue}",
                this);
        }
    }

    private void CapturePanelSnapshot(GameObject panelRoot)
    {
        if (panelRoot == null)
        {
            return;
        }

        Toggle[] toggles = panelRoot.GetComponentsInChildren<Toggle>(true);
        foreach (Toggle toggle in toggles)
        {
            if (toggle != null && !toggleSnapshot.ContainsKey(toggle))
            {
                toggleSnapshot.Add(toggle, toggle.isOn);
            }
        }

        Slider[] sliders = panelRoot.GetComponentsInChildren<Slider>(true);
        foreach (Slider slider in sliders)
        {
            if (slider != null && !sliderSnapshot.ContainsKey(slider))
            {
                sliderSnapshot.Add(slider, slider.value);
            }
        }

        Dropdown[] dropdowns = panelRoot.GetComponentsInChildren<Dropdown>(true);
        foreach (Dropdown dropdown in dropdowns)
        {
            if (dropdown != null && !dropdownSnapshot.ContainsKey(dropdown))
            {
                dropdownSnapshot.Add(dropdown, dropdown.value);
            }
        }

        TMP_Dropdown[] tmpDropdowns = panelRoot.GetComponentsInChildren<TMP_Dropdown>(true);
        foreach (TMP_Dropdown dropdown in tmpDropdowns)
        {
            if (dropdown != null && !tmpDropdownSnapshot.ContainsKey(dropdown))
            {
                tmpDropdownSnapshot.Add(dropdown, dropdown.value);
            }
        }

        InputField[] inputFields = panelRoot.GetComponentsInChildren<InputField>(true);
        foreach (InputField inputField in inputFields)
        {
            if (inputField != null && !inputFieldSnapshot.ContainsKey(inputField))
            {
                inputFieldSnapshot.Add(inputField, inputField.text);
            }
        }

        TMP_InputField[] tmpInputFields = panelRoot.GetComponentsInChildren<TMP_InputField>(true);
        foreach (TMP_InputField inputField in tmpInputFields)
        {
            if (inputField != null && !tmpInputFieldSnapshot.ContainsKey(inputField))
            {
                tmpInputFieldSnapshot.Add(inputField, inputField.text);
            }
        }
    }

    private bool HasDifferencesFromSnapshot()
    {
        return TryGetDifferenceDescription(out _);
    }

    private bool TryGetDifferenceDescription(out string description)
    {
        description = null;

        if (!hasSnapshot)
        {
            return false;
        }

        foreach (KeyValuePair<Toggle, bool> entry in toggleSnapshot)
        {
            if (entry.Key == null)
            {
                description = $"Toggle snapshot entry is missing.";
                return true;
            }

            if (entry.Key.isOn != entry.Value)
            {
                description = $"Toggle [{GetHierarchyPath(entry.Key.transform)}] current={entry.Key.isOn} snapshot={entry.Value}";
                return true;
            }
        }

        foreach (KeyValuePair<Slider, float> entry in sliderSnapshot)
        {
            if (entry.Key == null)
            {
                description = $"Slider snapshot entry is missing.";
                return true;
            }

            if (!Mathf.Approximately(entry.Key.value, entry.Value))
            {
                description = $"Slider [{GetHierarchyPath(entry.Key.transform)}] current={entry.Key.value} snapshot={entry.Value}";
                return true;
            }
        }

        foreach (KeyValuePair<Dropdown, int> entry in dropdownSnapshot)
        {
            if (entry.Key == null)
            {
                description = $"Dropdown snapshot entry is missing.";
                return true;
            }

            if (entry.Key.value != entry.Value)
            {
                description = $"Dropdown [{GetHierarchyPath(entry.Key.transform)}] current={entry.Key.value} snapshot={entry.Value}";
                return true;
            }
        }

        foreach (KeyValuePair<TMP_Dropdown, int> entry in tmpDropdownSnapshot)
        {
            if (entry.Key == null)
            {
                description = $"TMP_Dropdown snapshot entry is missing.";
                return true;
            }

            if (entry.Key.value != entry.Value)
            {
                description = $"TMP_Dropdown [{GetHierarchyPath(entry.Key.transform)}] current={entry.Key.value} snapshot={entry.Value}";
                return true;
            }
        }

        foreach (KeyValuePair<InputField, string> entry in inputFieldSnapshot)
        {
            if (entry.Key == null)
            {
                description = $"InputField snapshot entry is missing.";
                return true;
            }

            if (!string.Equals(entry.Key.text, entry.Value, StringComparison.Ordinal))
            {
                description = $"InputField [{GetHierarchyPath(entry.Key.transform)}] current='{entry.Key.text}' snapshot='{entry.Value}'";
                return true;
            }
        }

        foreach (KeyValuePair<TMP_InputField, string> entry in tmpInputFieldSnapshot)
        {
            if (entry.Key == null)
            {
                description = $"TMP_InputField snapshot entry is missing.";
                return true;
            }

            if (!string.Equals(entry.Key.text, entry.Value, StringComparison.Ordinal))
            {
                description = $"TMP_InputField [{GetHierarchyPath(entry.Key.transform)}] current='{entry.Key.text}' snapshot='{entry.Value}'";
                return true;
            }
        }

        if (LanguageManager.Instance != null && LanguageManager.Instance.CurrentLanguage != languageSnapshot)
        {
            description = $"Language current={LanguageManager.Instance.CurrentLanguage} snapshot={languageSnapshot}";
            return true;
        }

        return false;
    }

    private void RestoreSnapshot()
    {
        if (!hasSnapshot)
        {
            return;
        }

        isRestoringSnapshot = true;

        foreach (KeyValuePair<Toggle, bool> entry in toggleSnapshot)
        {
            if (entry.Key != null && entry.Key.isOn != entry.Value)
            {
                entry.Key.isOn = entry.Value;
            }
        }

        foreach (KeyValuePair<Slider, float> entry in sliderSnapshot)
        {
            if (entry.Key != null && !Mathf.Approximately(entry.Key.value, entry.Value))
            {
                entry.Key.value = entry.Value;
            }
        }

        foreach (KeyValuePair<Dropdown, int> entry in dropdownSnapshot)
        {
            if (entry.Key != null && entry.Key.value != entry.Value)
            {
                entry.Key.value = entry.Value;
            }
        }

        foreach (KeyValuePair<TMP_Dropdown, int> entry in tmpDropdownSnapshot)
        {
            if (entry.Key != null && entry.Key.value != entry.Value)
            {
                entry.Key.value = entry.Value;
            }
        }

        foreach (KeyValuePair<InputField, string> entry in inputFieldSnapshot)
        {
            if (entry.Key != null && !string.Equals(entry.Key.text, entry.Value, StringComparison.Ordinal))
            {
                entry.Key.text = entry.Value;
            }
        }

        foreach (KeyValuePair<TMP_InputField, string> entry in tmpInputFieldSnapshot)
        {
            if (entry.Key != null && !string.Equals(entry.Key.text, entry.Value, StringComparison.Ordinal))
            {
                entry.Key.text = entry.Value;
            }
        }

        if (LanguageManager.Instance != null && LanguageManager.Instance.CurrentLanguage != languageSnapshot)
        {
            LanguageManager.Instance.SetLanguage(languageSnapshot, false, true);
        }

        isRestoringSnapshot = false;
        hasUnsavedChanges = false;
    }

    private void RestoreDefaults()
    {
        if (!hasDefaultState)
        {
            return;
        }

        isRestoringSnapshot = true;

        foreach (KeyValuePair<Toggle, bool> entry in toggleDefaults)
        {
            if (entry.Key != null && entry.Key.isOn != entry.Value)
            {
                entry.Key.isOn = entry.Value;
            }
        }

        foreach (KeyValuePair<Slider, float> entry in sliderDefaults)
        {
            if (entry.Key != null && !Mathf.Approximately(entry.Key.value, entry.Value))
            {
                entry.Key.value = entry.Value;
            }
        }

        foreach (KeyValuePair<Dropdown, int> entry in dropdownDefaults)
        {
            if (entry.Key != null && entry.Key.value != entry.Value)
            {
                entry.Key.value = entry.Value;
            }
        }

        foreach (KeyValuePair<TMP_Dropdown, int> entry in tmpDropdownDefaults)
        {
            if (entry.Key != null && entry.Key.value != entry.Value)
            {
                entry.Key.value = entry.Value;
            }
        }

        foreach (KeyValuePair<InputField, string> entry in inputFieldDefaults)
        {
            if (entry.Key != null && !string.Equals(entry.Key.text, entry.Value, StringComparison.Ordinal))
            {
                entry.Key.text = entry.Value;
            }
        }

        foreach (KeyValuePair<TMP_InputField, string> entry in tmpInputFieldDefaults)
        {
            if (entry.Key != null && !string.Equals(entry.Key.text, entry.Value, StringComparison.Ordinal))
            {
                entry.Key.text = entry.Value;
            }
        }

        if (languageToggleBinder != null)
        {
            languageToggleBinder.SetSelectedLanguage(defaultLanguage);
        }
        else if (LanguageManager.Instance != null && LanguageManager.Instance.CurrentLanguage != defaultLanguage)
        {
            LanguageManager.Instance.SetLanguage(defaultLanguage, false, true);
        }

        isRestoringSnapshot = false;
        hasUnsavedChanges = HasDifferencesFromSnapshot();
    }

    private AppLanguage GetConfirmationLanguage()
    {
        if (confirmUsesSavedLanguage && hasSnapshot)
        {
            return languageSnapshot;
        }

        if (LanguageManager.Instance != null)
        {
            return LanguageManager.Instance.CurrentLanguage;
        }

        if (languageToggleBinder != null && languageToggleBinder.TryGetSelectedLanguage(out AppLanguage selectedLanguage))
        {
            return selectedLanguage;
        }

        return AppLanguage.Vietnamese;
    }

    private string LocalizeText(string key, string fallback, AppLanguage language)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return fallback;
        }

        return LanguageManager.Tr(key, language, fallback);
    }

    private void RefreshDirtyState()
    {
        if (isFinalizingSave)
        {
            hasUnsavedChanges = false;
            return;
        }

        hasUnsavedChanges = HasDifferencesFromSnapshot();
    }

    private IEnumerator FinalizeSaveChangesAfterFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        CaptureSnapshot("SaveChanges finalized");
        isFinalizingSave = false;
        RefreshDirtyState();
        saveFinalizeCoroutine = null;
        LogSaveBaselineState("finalized");

        if (hasUnsavedChanges && TryGetDifferenceDescription(out string remainingDifference))
        {
            Debug.LogWarning($"Setting_UIScript: State is still dirty after SaveChanges. {remainingDifference}", this);
        }
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return "<null>";
        }

        List<string> segments = new List<string>();
        Transform current = target;
        while (current != null)
        {
            segments.Add(current.name);
            current = current.parent;
        }

        segments.Reverse();
        return string.Join("/", segments);
    }

    private void LogSaveBaselineState(string stage)
    {
        if (vsyncToggle != null && toggleSnapshot.TryGetValue(vsyncToggle, out bool vsyncSnapshotValue))
        {
            Debug.Log(
                $"Setting_UIScript[{GetInstanceLabel()}]: Save baseline ({stage}) VSync current={vsyncToggle.isOn} snapshot={vsyncSnapshotValue} savedPref={ReadBoolPref(DisplaySettingsService.VSyncEnabledKey)} currentRuntimeVSync={DisplaySettingsService.GetCurrentVSyncEnabled()}",
                this);
        }

        if (resolutionDropdown != null && tmpDropdownSnapshot.TryGetValue(resolutionDropdown, out int resolutionSnapshotValue))
        {
            Debug.Log(
                $"Setting_UIScript[{GetInstanceLabel()}]: Save baseline ({stage}) Resolution current={resolutionDropdown.value} snapshot={resolutionSnapshotValue}",
                this);
        }
    }

    private static string ReadBoolPref(string key)
    {
        if (!PlayerPrefs.HasKey(key))
        {
            return "<missing>";
        }

        return PlayerPrefs.GetInt(key, 0) != 0 ? "true" : "false";
    }

    private string GetInstanceLabel()
    {
        return $"{GetHierarchyPath(transform)}#{GetHashCode()}";
    }

}
