using System;
using System.Collections.Generic;
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
    [SerializeField] private Button btnSave;

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
    [SerializeField] private string yesLabel = "Yes";
    [SerializeField] private string noLabel = "No";
    [SerializeField] private bool confirmUsesSavedLanguage = true;

    [Header("Confirm Toast Localization Keys (Optional)")]
    [SerializeField] private string confirmTitleKey = "common.toast.confirm.title";
    [SerializeField] private string saveConfirmMessageKey = "settings.confirm.save";
    [SerializeField] private string yesLabelKey = "common.btn.yes";
    [SerializeField] private string noLabelKey = "common.btn.no";

    [Header("Active Visual")]
    [Range(1f, 1.5f)] [SerializeField] private float activeBrightness = 1.12f;
    [Range(0.6f, 1f)] [SerializeField] private float inactiveBrightness = 0.95f;

    private CanvasGroup cgGen, cgGrap, cgAudio, cgCont;
    private Image btnGenImage, btnGrapImage, btnAudImage, btnContImage;
    private Image panelGenImage, panelGrapImage, panelAudioImage, panelContImage;
    private readonly Dictionary<Image, Color> baseImageColors = new Dictionary<Image, Color>();
    private bool hasUnsavedChanges;
    private bool dirtyTrackingBound;
    private bool isRestoringSnapshot;
    private bool hasSnapshot;
    private AppLanguage languageSnapshot = AppLanguage.Vietnamese;
    private readonly Dictionary<Toggle, bool> toggleSnapshot = new Dictionary<Toggle, bool>();
    private readonly Dictionary<Slider, float> sliderSnapshot = new Dictionary<Slider, float>();
    private readonly Dictionary<Dropdown, int> dropdownSnapshot = new Dictionary<Dropdown, int>();
    private readonly Dictionary<TMP_Dropdown, int> tmpDropdownSnapshot = new Dictionary<TMP_Dropdown, int>();
    private readonly Dictionary<InputField, string> inputFieldSnapshot = new Dictionary<InputField, string>();
    private readonly Dictionary<TMP_InputField, string> tmpInputFieldSnapshot = new Dictionary<TMP_InputField, string>();

    private void Awake()
    {
        ResolveRuntimeReferences();
        ValidateResolvedReferences();

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
        if (btnSave != null) btnSave.onClick.AddListener(OnSaveButtonClicked);

    }

    private void Start()
    {
        // Keep exactly one panel open at all times. Prefer General as default.
        if (cgGen != null) SelectOnly(cgGen, btnGenImage, panelGenImage);
        else if (cgGrap != null) SelectOnly(cgGrap, btnGrapImage, panelGrapImage);
        else if (cgAudio != null) SelectOnly(cgAudio, btnAudImage, panelAudioImage);
        else if (cgCont != null) SelectOnly(cgCont, btnContImage, panelContImage);

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
        if (!hasUnsavedChanges)
        {
            return false;
        }

        ShowConfirmation(
            LocalizeText(saveConfirmMessageKey, saveConfirmMessage, GetConfirmationLanguage()),
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

        CaptureSnapshot();
        hasUnsavedChanges = false;
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
        if (isRestoringSnapshot)
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

        if (btnSave == null)
        {
            btnSave = FindButtonByName("btnSave");
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
    }

    private void ValidateResolvedReferences()
    {
        if (btnGen == null || btnGrap == null || btnAud == null || btnCont == null || btnSave == null)
        {
            Debug.LogWarning("Setting_UIScript: One or more navigation/save buttons could not be resolved from the Setting prefab.", this);
        }

        if (panelGen == null || panelGrap == null || panelAudio == null || panelCont == null)
        {
            Debug.LogWarning("Setting_UIScript: One or more content panels could not be resolved from the Setting prefab.", this);
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
        if (!hasUnsavedChanges)
        {
            return;
        }

        ShowConfirmation(LocalizeText(saveConfirmMessageKey, saveConfirmMessage, GetConfirmationLanguage()), SaveChanges);
    }

    private void SaveChanges()
    {
        if (languageToggleBinder != null)
        {
            languageToggleBinder.ApplySelectedLanguage(true, true);
        }
        else if (LanguageManager.Instance != null)
        {
            // Fallback if binder is missing.
            LanguageManager.Instance.SetLanguage(LanguageManager.Instance.CurrentLanguage, true, true);
        }

        PlayerPrefs.Save();
        CaptureSnapshot();
        hasUnsavedChanges = false;
    }

    private void CaptureSnapshot()
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
        if (!hasSnapshot)
        {
            return false;
        }

        foreach (KeyValuePair<Toggle, bool> entry in toggleSnapshot)
        {
            if (entry.Key == null || entry.Key.isOn != entry.Value)
            {
                return true;
            }
        }

        foreach (KeyValuePair<Slider, float> entry in sliderSnapshot)
        {
            if (entry.Key == null || !Mathf.Approximately(entry.Key.value, entry.Value))
            {
                return true;
            }
        }

        foreach (KeyValuePair<Dropdown, int> entry in dropdownSnapshot)
        {
            if (entry.Key == null || entry.Key.value != entry.Value)
            {
                return true;
            }
        }

        foreach (KeyValuePair<TMP_Dropdown, int> entry in tmpDropdownSnapshot)
        {
            if (entry.Key == null || entry.Key.value != entry.Value)
            {
                return true;
            }
        }

        foreach (KeyValuePair<InputField, string> entry in inputFieldSnapshot)
        {
            if (entry.Key == null || !string.Equals(entry.Key.text, entry.Value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        foreach (KeyValuePair<TMP_InputField, string> entry in tmpInputFieldSnapshot)
        {
            if (entry.Key == null || !string.Equals(entry.Key.text, entry.Value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (LanguageManager.Instance != null && LanguageManager.Instance.CurrentLanguage != languageSnapshot)
        {
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

}
