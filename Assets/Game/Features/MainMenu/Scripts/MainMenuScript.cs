using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System.Text;

public class MainMenuScript : MonoBehaviour
{
    private const int DefaultPlayerNameCharacterLimit = 24;
    private const string CommonToastConfirmTitleLocalizationKey = "common.toast.confirm.title";
    private const string CommonYesLocalizationKey = "common.btn.yes";
    private const string CommonNoLocalizationKey = "common.btn.no";
    private const string ContinuePopupTitleLocalizationKey = "continue.popup.title";
    private const string ContinuePopupSubtitleLocalizationKey = "continue.popup.subtitle";
    private const string ContinuePopupEmptyLocalizationKey = "continue.popup.empty";
    private const string ContinuePopupBackLocalizationKey = "continue.popup.btn.back";
    private const string ContinuePopupConfirmLocalizationKey = "continue.popup.btn.confirm";
    private const string ContinuePopupProfileProgressLocalizationKey = "continue.popup.profile.progress";
    private const string ContinueDeleteConfirmLocalizationKey = "continue.popup.delete.confirm";
    private const string NewGameNameLabelLocalizationKey = "newgame.form.label.name";
    private const string NewGameHintLocalizationKey = "newgame.form.hint";
    private const string NewGameCounterLocalizationKey = "newgame.form.counter";
    private const string NewGameErrorEmptyLocalizationKey = "newgame.form.error.empty";

    [Header("Panels")]
    [SerializeField] private CanvasGroup mainMenuPanel;
    [SerializeField] private CanvasGroup settingPanel;
    [SerializeField] private CanvasGroup newGamePopupPanel;

    [Header("New Game")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Button confirmNewGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button continueProfileItemPrefab;
    [SerializeField] private string loadingSceneName = "LoadingScene";
    [FormerlySerializedAs("tutorialSceneName")]
    [SerializeField] private string nextSceneAfterNewGameName = "LevelSelectScene";
    [SerializeField] private string defaultPlayerName = "Player";
    [SerializeField] private int playerNameCharacterLimit = DefaultPlayerNameCharacterLimit;
    [SerializeField] private TMP_Text newGameNameLabelText;
    [SerializeField] private TMP_Text newGameHintText;
    [SerializeField] private TMP_Text newGameCounterText;
    [SerializeField] private TMP_Text newGameErrorText;

    [Header("Continue Popup")]
    [SerializeField] private CanvasGroup continuePopupPanel;
    [SerializeField] private RectTransform continuePopupListRoot;
    [SerializeField] private TMP_Text continuePopupTitleText;
    [SerializeField] private TMP_Text continuePopupSubtitleText;
    [SerializeField] private TMP_Text continuePopupEmptyText;
    [SerializeField] private Button continuePopupCloseButton;
    [SerializeField] private Button continuePopupConfirmButton;
    [SerializeField] private Button continuePopupTemplateButton;
    [SerializeField] private GameObject continuePopupEmptyRoot;

    // Add any extra panels here if they should be hidden on load.
    [SerializeField] private CanvasGroup[] otherPanelsToHideOnLoad;
    private Setting_UIScript settingUI;
    private bool isStartingNewGame;
    private bool isStartingContinue;
    private readonly List<Button> continueProfileButtons = new List<Button>();
    private readonly Dictionary<Button, PlayerProgressProfileStore.ProfileSummary> continueProfileSummariesByButton = new Dictionary<Button, PlayerProgressProfileStore.ProfileSummary>();
    private string selectedContinueProfileName = string.Empty;
    private Button selectedContinueProfileButton;
    private ToastContainerController toastContainer;
    private GameObject newGameErrorRoot;
    private bool showNewGameNameError;

    private bool IsPrimaryController => mainMenuPanel != null;

#if UNITY_EDITOR
    private void OnValidate()
    {
        ValidateCriticalSerializedReferences(logAsError: false);
    }
#endif

    private void Awake()
    {
        if (Time.timeScale <= 0f)
        {
            Time.timeScale = 1f;
        }

        settingUI = ResolveSettingUI();

        if (!IsPrimaryController)
        {
            Debug.LogWarning("MainMenuScript: mainMenuPanel is not assigned. Component is inactive.", this);
            enabled = false;
            return;
        }

        if (!ValidateCriticalSerializedReferences(logAsError: true))
        {
            Debug.LogError("MainMenuScript: Missing critical serialized references. Component has been disabled.", this);
            enabled = false;
            return;
        }

        ResolveNewGamePopupControls();
        BindNewGameControls();
        ResolveContinueControls();
        ResolveToastContainer();
        EnsureContinuePopup();
        BindContinueControls();
        LanguageManager.LanguageChanged -= OnLanguageChanged;
        LanguageManager.LanguageChanged += OnLanguageChanged;

        // On load: MainMenu is visible, the other panels are hidden.
        SetPanelActive(mainMenuPanel, true);
        SetPanelActive(settingPanel, false);
        SetPanelActive(newGamePopupPanel, false);
        SetPanelActive(continuePopupPanel, false);
        RefreshNewGamePopupLocalizedTexts();
        RefreshContinuePopupLocalizedTexts();
        UpdateNewGameConfirmState();
        UpdateContinueButtonState();

        if (otherPanelsToHideOnLoad != null)
        {
            foreach (CanvasGroup panel in otherPanelsToHideOnLoad)
            {
                SetPanelActive(panel, false);
            }
        }
    }

    private void OnDestroy()
    {
        if (!IsPrimaryController)
        {
            return;
        }

        if (confirmNewGameButton != null)
        {
            confirmNewGameButton.onClick.RemoveListener(ConfirmNewGame);
        }

        if (playerNameInput != null)
        {
            playerNameInput.onValueChanged.RemoveListener(HandlePlayerNameChanged);
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OpenContinuePopup);
        }

        if (continuePopupCloseButton != null)
        {
            continuePopupCloseButton.onClick.RemoveListener(CloseContinuePopup);
        }

        if (continuePopupConfirmButton != null)
        {
            continuePopupConfirmButton.onClick.RemoveListener(ConfirmContinueSelection);
        }

        LanguageManager.LanguageChanged -= OnLanguageChanged;
    }

    private void Update()
    {
        if (!IsPrimaryController)
        {
            return;
        }

        if (IsContinuePopupOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) && !isStartingContinue)
            {
                CloseContinuePopup();
            }

            return;
        }

        if (!IsNewGamePopupOpen || isStartingNewGame)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseNewGamePopup();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ConfirmNewGame();
        }
    }

    // Bind this to the Setting button.
    public void OpenSettingPanel()
    {
        settingUI = ResolveSettingUI();

        if (settingUI != null)
        {
            settingUI.BeginEditSession();
        }

        SetPanelActive(newGamePopupPanel, false);
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(settingPanel, true);
    }

    // Bind this to the Play button.
    public void OpenNewGamePopup()
    {
        ResolveNewGamePopupControls();
        CloseContinuePopup();
        isStartingNewGame = false;
        showNewGameNameError = false;
        SetPanelInteraction(mainMenuPanel, false);
        SetPanelActive(newGamePopupPanel, true);

        if (playerNameInput != null)
        {
            string initialName = GetSuggestedPlayerName();
            playerNameInput.text = initialName;
            playerNameInput.caretPosition = initialName.Length;
            playerNameInput.selectionAnchorPosition = 0;
            playerNameInput.selectionFocusPosition = initialName.Length;
            playerNameInput.Select();
            playerNameInput.ActivateInputField();
        }

        UpdateNewGameConfirmState();
    }

    public void CloseNewGamePopup()
    {
        if (isStartingNewGame)
        {
            return;
        }

        showNewGameNameError = false;
        SetPanelActive(newGamePopupPanel, false);
        SetPanelInteraction(mainMenuPanel, true);
        UpdateNewGameConfirmState();
    }

    public void ConfirmNewGame()
    {
        if (isStartingNewGame)
        {
            return;
        }

        string playerName = string.Empty;

        if (playerNameInput != null)
        {
            string resolvedName = SanitizePlayerName(playerNameInput.text);
            if (!string.IsNullOrWhiteSpace(resolvedName))
            {
                playerName = resolvedName;
                playerNameInput.text = resolvedName;
            }
        }

        if (string.IsNullOrWhiteSpace(playerName))
        {
            showNewGameNameError = true;
            UpdateNewGameConfirmState();
            if (playerNameInput != null)
            {
                playerNameInput.Select();
                playerNameInput.ActivateInputField();
            }

            return;
        }

        isStartingNewGame = true;
        UpdateNewGameConfirmState();
        UpdateContinueButtonState();

        LoadingFlowState.SetPlayerName(playerName);
        PlayerProgressProfileStore.ResetProfile(playerName);
        LoadingFlowState.ClearCurrentLevelId();
        LoadingFlowState.ClearPendingScenarioResourcePath();
        LoadingFlowState.ClearPendingCaseId();
        LoadingFlowState.ClearPendingIncidentPayload();
        LoadingFlowState.ClearPendingOnsiteScene();
        LoadingFlowState.SetPendingTargetScene(nextSceneAfterNewGameName);

        SceneManager.LoadScene(loadingSceneName);
    }

    // Bind this to the Back button.
    public void BackToMain()
    {
        settingUI = ResolveSettingUI();

        if (settingUI != null && settingUI.HandleBackRequest(BackToMainImmediate))
        {
            return;
        }

        BackToMainImmediate();
    }

    private void BackToMainImmediate()
    {
        // Hide all secondary panels, including Settings.
        SetPanelActive(settingPanel, false);
        SetPanelActive(newGamePopupPanel, false);
        SetPanelActive(continuePopupPanel, false);

        if (otherPanelsToHideOnLoad != null)
        {
            foreach (CanvasGroup panel in otherPanelsToHideOnLoad)
            {
                SetPanelActive(panel, false);
            }
        }

        // Show the main menu again.
        SetPanelActive(mainMenuPanel, true);
    }

    private void SetPanelActive(CanvasGroup panel, bool active)
    {
        if (panel == null)
        {
            return;
        }

        panel.alpha = active ? 1f : 0f;
        panel.interactable = active;
        panel.blocksRaycasts = active;
    }

    private void SetPanelInteraction(CanvasGroup panel, bool interactable)
    {
        if (panel == null)
        {
            return;
        }

        panel.interactable = interactable;
        panel.blocksRaycasts = interactable;
    }

    private void BindNewGameControls()
    {
        if (confirmNewGameButton == null)
        {
            ResolveNewGamePopupControls();
        }

        if (confirmNewGameButton != null)
        {
            confirmNewGameButton.onClick.RemoveListener(ConfirmNewGame);
            confirmNewGameButton.onClick.AddListener(ConfirmNewGame);
        }

        if (playerNameInput != null)
        {
            playerNameInput.onValueChanged.RemoveListener(HandlePlayerNameChanged);
            playerNameInput.onValueChanged.AddListener(HandlePlayerNameChanged);
            playerNameInput.characterLimit = Mathf.Max(1, playerNameCharacterLimit);
        }

        UpdateNewGameConfirmState();
    }

    private void BindContinueControls()
    {
        ResolveContinueControls();

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OpenContinuePopup);
            continueButton.onClick.AddListener(OpenContinuePopup);
        }

        if (continuePopupCloseButton != null)
        {
            continuePopupCloseButton.onClick.RemoveListener(CloseContinuePopup);
            continuePopupCloseButton.onClick.AddListener(CloseContinuePopup);
        }

        if (continuePopupConfirmButton != null)
        {
            continuePopupConfirmButton.onClick.RemoveListener(ConfirmContinueSelection);
            continuePopupConfirmButton.onClick.AddListener(ConfirmContinueSelection);
        }

        UpdateContinueButtonState();
        UpdateContinuePopupConfirmState();
    }

    private void ResolveNewGamePopupControls()
    {
        if (playerNameInput != null)
        {
            playerNameInput.characterLimit = Mathf.Max(1, playerNameCharacterLimit);

            if (playerNameInput.textComponent != null)
            {
                LocalizedText inputTextLocalizer = playerNameInput.textComponent.GetComponent<LocalizedText>();
                if (inputTextLocalizer != null && inputTextLocalizer.enabled)
                {
                    inputTextLocalizer.enabled = false;
                }
            }
        }

        if (newGameErrorText != null && newGameErrorRoot == null && newGameErrorText.transform.parent != null)
        {
            newGameErrorRoot = newGameErrorText.transform.parent.gameObject;
        }

        ApplyPopupFont(newGameNameLabelText);
        ApplyPopupFont(newGameHintText);
        ApplyPopupFont(newGameCounterText);
        ApplyPopupFont(newGameErrorText);
    }

    private void ResolveContinueControls()
    {
        if (continuePopupEmptyText == null && continuePopupEmptyRoot != null)
        {
            continuePopupEmptyText = continuePopupEmptyRoot.GetComponentInChildren<TMP_Text>(true);
        }

        Button templateForStyling = continuePopupTemplateButton != null ? continuePopupTemplateButton : continueProfileItemPrefab;
        if (templateForStyling != null)
        {
            TMP_Text[] templateTexts = templateForStyling.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text templateText in templateTexts)
            {
                ApplyPopupFont(templateText);
            }
        }

        ApplyPopupFont(continuePopupTitleText);
        ApplyPopupFont(continuePopupSubtitleText);
        ApplyPopupFont(continuePopupEmptyText);
    }

    private void HandlePlayerNameChanged(string rawValue)
    {
        if (playerNameInput == null)
        {
            return;
        }

        string sanitizedValue = SanitizePlayerName(rawValue);
        if (!string.Equals(rawValue, sanitizedValue))
        {
            int caretPosition = Mathf.Clamp(sanitizedValue.Length, 0, sanitizedValue.Length);
            playerNameInput.SetTextWithoutNotify(sanitizedValue);
            playerNameInput.caretPosition = caretPosition;
            playerNameInput.selectionAnchorPosition = caretPosition;
            playerNameInput.selectionFocusPosition = caretPosition;
        }

        if (!string.IsNullOrWhiteSpace(sanitizedValue))
        {
            showNewGameNameError = false;
        }

        UpdateNewGameConfirmState();
    }

    private void UpdateNewGameConfirmState()
    {
        bool hasValidPlayerName = !string.IsNullOrWhiteSpace(SanitizePlayerName(playerNameInput != null ? playerNameInput.text : string.Empty));
        if (confirmNewGameButton != null)
        {
            confirmNewGameButton.interactable = !isStartingNewGame && IsNewGamePopupOpen && hasValidPlayerName;
        }

        UpdateNewGamePopupStatusTexts(hasValidPlayerName);
    }

    private void UpdateContinueButtonState()
    {
        if (continueButton == null)
        {
            return;
        }

        continueButton.interactable = !isStartingContinue;
    }

    public void OpenContinuePopup()
    {
        EnsureContinuePopup();
        PopulateContinuePopupList();
        if (continuePopupPanel == null)
        {
            return;
        }

        isStartingContinue = false;
        SetPanelInteraction(mainMenuPanel, false);
        SetPanelActive(newGamePopupPanel, false);
        SetPanelActive(continuePopupPanel, true);
        UpdateContinueButtonState();
        UpdateContinuePopupConfirmState();
    }

    public void CloseContinuePopup()
    {
        if (isStartingContinue)
        {
            return;
        }

        SetPanelActive(continuePopupPanel, false);
        SetPanelInteraction(mainMenuPanel, true);
        ClearSelectedContinueProfile();
        UpdateContinueButtonState();
    }

    private void ConfirmContinueSelection()
    {
        if (isStartingContinue || string.IsNullOrWhiteSpace(selectedContinueProfileName))
        {
            return;
        }

        ContinueWithProfile(selectedContinueProfileName);
    }

    private void ContinueWithProfile(string playerName)
    {
        string resolvedName = SanitizePlayerName(playerName);
        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            return;
        }

        isStartingContinue = true;
        UpdateContinueButtonState();
        UpdateContinuePopupConfirmState();

        LoadingFlowState.SetPlayerName(resolvedName);
        LoadingFlowState.ClearCurrentLevelId();
        LoadingFlowState.ClearPendingScenarioResourcePath();
        LoadingFlowState.ClearPendingCaseId();
        LoadingFlowState.ClearPendingIncidentPayload();
        LoadingFlowState.ClearPendingOnsiteScene();
        LoadingFlowState.SetPendingTargetScene(nextSceneAfterNewGameName);
        PlayerProgressProfileStore.TouchProfile(resolvedName);

        SceneManager.LoadScene(loadingSceneName);
    }

    private void EnsureContinuePopup()
    {
        ResolveContinueControls();
    }

    private void PopulateContinuePopupList()
    {
        ResolveContinueControls();

        bool hasSceneTemplate = continuePopupTemplateButton != null && continuePopupTemplateButton.transform.parent == continuePopupListRoot;
        Button templateSource = hasSceneTemplate ? continuePopupTemplateButton : continueProfileItemPrefab;
        if (continuePopupListRoot == null || templateSource == null)
        {
            return;
        }

        for (int i = continuePopupListRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = continuePopupListRoot.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (hasSceneTemplate && child.gameObject == continuePopupTemplateButton.gameObject)
            {
                continue;
            }

            if (continuePopupEmptyRoot != null && child.gameObject == continuePopupEmptyRoot)
            {
                continue;
            }

            Destroy(child.gameObject);
        }

        continueProfileButtons.Clear();
        continueProfileSummariesByButton.Clear();
        selectedContinueProfileName = string.Empty;
        selectedContinueProfileButton = null;
        if (hasSceneTemplate)
        {
            continuePopupTemplateButton.gameObject.SetActive(false);
        }

        List<PlayerProgressProfileStore.ProfileSummary> profiles = PlayerProgressProfileStore.GetAllProfileSummaries();
        if (continuePopupEmptyRoot != null)
        {
            continuePopupEmptyRoot.SetActive(profiles.Count <= 0);
        }

        for (int i = 0; i < profiles.Count; i++)
        {
            PlayerProgressProfileStore.ProfileSummary profile = profiles[i];
            GameObject itemObject = Instantiate(templateSource.gameObject, continuePopupListRoot);
            itemObject.name = $"ProfileItem_{i + 1}";
            itemObject.SetActive(true);

            Button button = itemObject.GetComponent<Button>();
            if (button == null)
            {
                Destroy(itemObject);
                continue;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectContinueProfile(profile.PlayerName, button));
            BindContinueProfileDeleteButton(button, profile.PlayerName);

            TMP_Text nameText;
            TMP_Text metaText;
            ResolveContinueProfileItemTexts(button, out nameText, out metaText);

            if (nameText != null)
            {
                ApplyPopupFont(nameText);
                nameText.text = profile.PlayerName;
            }

            if (metaText != null)
            {
                ApplyPopupFont(metaText);
            }

            ApplyContinueProfileTexts(button, profile);
            SetContinueProfileButtonSelected(button, false);
            continueProfileButtons.Add(button);
            continueProfileSummariesByButton[button] = profile;
        }

        UpdateContinuePopupConfirmState();
    }

    private void SelectContinueProfile(string playerName, Button selectedButton)
    {
        if (selectedButton == null || isStartingContinue)
        {
            return;
        }

        selectedContinueProfileName = SanitizePlayerName(playerName);
        selectedContinueProfileButton = selectedButton;
        RefreshContinueProfileSelectionVisuals();
        UpdateContinuePopupConfirmState();
    }

    private void ClearSelectedContinueProfile()
    {
        selectedContinueProfileName = string.Empty;
        selectedContinueProfileButton = null;
        RefreshContinueProfileSelectionVisuals();
        UpdateContinuePopupConfirmState();
    }

    private void RefreshContinueProfileSelectionVisuals()
    {
        foreach (Button profileButton in continueProfileButtons)
        {
            SetContinueProfileButtonSelected(profileButton, profileButton == selectedContinueProfileButton);
        }

        if (continuePopupTemplateButton != null)
        {
            SetContinueProfileButtonSelected(continuePopupTemplateButton, false);
        }
    }

    private void SetContinueProfileButtonSelected(Button button, bool isSelected)
    {
        if (button == null)
        {
            return;
        }

        Image background = button.targetGraphic as Image;
        if (background != null)
        {
            background.color = isSelected
                ? new Color(0.68f, 0.38f, 0.14f, 1f)
                : new Color(0.3490566f, 0.3276522f, 0.3276522f, 1f);
        }

        Transform right = button.transform.Find("Right");
        if (right != null)
        {
            right.gameObject.SetActive(isSelected);

            Image accent = right.GetComponentInChildren<Image>(true);
            if (accent != null && accent != background)
            {
                accent.color = new Color(1f, 0f, 0f, 1f);
            }

            TMP_Text deleteLabel = right.GetComponentInChildren<TMP_Text>(true);
            if (deleteLabel != null)
            {
                deleteLabel.color = Color.white;
            }
        }
    }

    private void ResolveContinueProfileItemTexts(Button button, out TMP_Text nameText, out TMP_Text metaText)
    {
        nameText = null;
        metaText = null;

        if (button == null)
        {
            return;
        }

        Transform left = button.transform.Find("Left");
        if (left != null)
        {
            TMP_Text[] leftTexts = left.GetComponentsInChildren<TMP_Text>(true);
            if (leftTexts.Length > 0)
            {
                nameText = leftTexts[0];
            }

            if (leftTexts.Length > 1)
            {
                metaText = leftTexts[1];
            }
        }
    }

    private void UpdateContinuePopupConfirmState()
    {
        if (continuePopupConfirmButton == null)
        {
            return;
        }

        continuePopupConfirmButton.interactable =
            !isStartingContinue &&
            IsContinuePopupOpen &&
            !string.IsNullOrWhiteSpace(selectedContinueProfileName);
    }

    private string GetSuggestedPlayerName()
    {
        string rememberedName = LoadingFlowState.GetPlayerName();
        string suggestedName = !string.IsNullOrWhiteSpace(rememberedName)
            ? rememberedName
            : defaultPlayerName;
        return SanitizePlayerName(suggestedName);
    }

    private string SanitizePlayerName(string rawValue)
    {
        string source = rawValue ?? string.Empty;
        StringBuilder builder = new StringBuilder(source.Length);
        bool previousWasWhitespace = false;

        for (int i = 0; i < source.Length; i++)
        {
            char character = source[i];
            if (char.IsWhiteSpace(character))
            {
                if (previousWasWhitespace || builder.Length == 0)
                {
                    continue;
                }

                builder.Append(' ');
                previousWasWhitespace = true;
                continue;
            }

            if (builder.Length >= Mathf.Max(1, playerNameCharacterLimit))
            {
                break;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        return builder.ToString().Trim();
    }

    private void OnLanguageChanged(AppLanguage _)
    {
        RefreshNewGamePopupLocalizedTexts();
        RefreshContinuePopupLocalizedTexts();
        RefreshContinueProfileLocalizedTexts();
        UpdateNewGameConfirmState();
    }

    private void RefreshNewGamePopupLocalizedTexts()
    {
        ResolveNewGamePopupControls();
        SetLocalizedPopupText(newGameNameLabelText, NewGameNameLabelLocalizationKey, "T\u00ean nh\u00e2n v\u1eadt");
        SetLocalizedPopupText(newGameHintText, NewGameHintLocalizationKey, "T\u00ean s\u1ebd d\u00f9ng cho ti\u1ebfn tr\u00ecnh ch\u01a1i.");
    }

    private void UpdateNewGamePopupStatusTexts(bool hasValidPlayerName)
    {
        ResolveNewGamePopupControls();

        int characterLimit = Mathf.Max(1, playerNameCharacterLimit);
        int currentLength = SanitizePlayerName(playerNameInput != null ? playerNameInput.text : string.Empty).Length;

        if (newGameCounterText != null)
        {
            ApplyPopupFont(newGameCounterText);
            string counterTemplate = LanguageManager.Tr(NewGameCounterLocalizationKey, "{0}/{1} k\u00fd t\u1ef1");
            newGameCounterText.text = string.Format(counterTemplate, currentLength, characterLimit);
        }

        if (newGameErrorText != null)
        {
            ApplyPopupFont(newGameErrorText);
            newGameErrorText.text = LanguageManager.Tr(NewGameErrorEmptyLocalizationKey, "Vui l\u00f2ng nh\u1eadp t\u00ean nh\u00e2n v\u1eadt");
        }

        if (newGameErrorRoot != null)
        {
            bool shouldShowError = IsNewGamePopupOpen && !isStartingNewGame && !hasValidPlayerName && showNewGameNameError;
            newGameErrorRoot.SetActive(shouldShowError);
        }
    }
    private void SetLocalizedPopupText(TMP_Text target, string key, string fallback)
    {
        if (target == null)
        {
            return;
        }

        LocalizedText localizedText = target.GetComponent<LocalizedText>();
        if (localizedText != null && localizedText.enabled)
        {
            localizedText.enabled = false;
        }

        ApplyPopupFont(target);
        target.text = LanguageManager.Tr(key, fallback);
    }

    private void ApplyPopupFont(TMP_Text target)
    {
        if (target == null)
        {
            return;
        }

        TMP_FontAsset popupFont = ResolvePopupFont();
        if (popupFont != null)
        {
            target.font = popupFont;
        }
    }

    private TMP_FontAsset ResolvePopupFont()
    {
        if (playerNameInput != null && playerNameInput.textComponent != null)
        {
            return playerNameInput.textComponent.font;
        }

        if (confirmNewGameButton != null)
        {
            TMP_Text label = confirmNewGameButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                return label.font;
            }
        }

        return null;
    }
    private void RefreshContinuePopupLocalizedTexts()
    {
        ResolveContinueControls();

        SetLocalizedPopupText(continuePopupTitleText, ContinuePopupTitleLocalizationKey, "TI\u1EBEP T\u1EE4C H\u1ED2 S\u01A0");
        SetLocalizedPopupText(continuePopupSubtitleText, ContinuePopupSubtitleLocalizationKey, "Ch\u1ECDn h\u1ED3 s\u01A1 \u0111\u00e3 l\u01b0u \u0111\u1ec3 ti\u1ebfp t\u1ee5c");
        SetLocalizedPopupText(continuePopupEmptyText, ContinuePopupEmptyLocalizationKey, "Ch\u01b0a c\u00f3 h\u1ed3 s\u01a1 l\u01b0u. H\u00e3y b\u1eaft \u0111\u1ea7u tr\u00f2 ch\u01a1i m\u1edbi.");

        if (continuePopupCloseButton != null)
        {
            SetLocalizedPopupText(
                continuePopupCloseButton.GetComponentInChildren<TMP_Text>(true),
                ContinuePopupBackLocalizationKey,
                "Tr\u1EDF v\u1EC1");
        }

        if (continuePopupConfirmButton != null)
        {
            SetLocalizedPopupText(
                continuePopupConfirmButton.GetComponentInChildren<TMP_Text>(true),
                ContinuePopupConfirmLocalizationKey,
                "Ti\u1EBFp t\u1EE5c");
        }
    }

    private void RefreshContinueProfileLocalizedTexts()
    {
        foreach (KeyValuePair<Button, PlayerProgressProfileStore.ProfileSummary> entry in continueProfileSummariesByButton)
        {
            ApplyContinueProfileTexts(entry.Key, entry.Value);
        }
    }

    private void ApplyContinueProfileTexts(Button button, PlayerProgressProfileStore.ProfileSummary profile)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text nameText;
        TMP_Text metaText;
        ResolveContinueProfileItemTexts(button, out nameText, out metaText);

        if (nameText != null)
        {
            ApplyPopupFont(nameText);
            nameText.text = profile.PlayerName;
        }

        if (metaText != null)
        {
            ApplyPopupFont(metaText);
            string progressTemplate = LanguageManager.Tr(ContinuePopupProfileProgressLocalizationKey, "{0} m\u00e0n \u0111\u00e3 ho\u00e0n th\u00e0nh");
            metaText.text = string.Format(progressTemplate, profile.CompletedLevelCount);
        }
    }

    private void BindContinueProfileDeleteButton(Button profileButton, string playerName)
    {
        if (profileButton == null)
        {
            return;
        }

        Transform right = profileButton.transform.Find("Right");
        if (right == null)
        {
            return;
        }

        Button deleteButton = right.GetComponent<Button>();
        if (deleteButton == null)
        {
            deleteButton = right.gameObject.AddComponent<Button>();
        }

        Image deleteGraphic = right.GetComponent<Image>();
        if (deleteGraphic == null)
        {
            deleteGraphic = right.GetComponentInChildren<Image>(true);
        }

        if (deleteGraphic != null)
        {
            deleteButton.targetGraphic = deleteGraphic;
        }

        deleteButton.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = deleteButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.92f, 0.92f, 1f);
        colors.pressedColor = new Color(0.9f, 0.8f, 0.8f, 1f);
        colors.selectedColor = colors.highlightedColor;
        deleteButton.colors = colors;
        deleteButton.onClick.RemoveAllListeners();
        deleteButton.onClick.AddListener(() => PromptDeleteContinueProfile(playerName, profileButton));
    }

    private void PromptDeleteContinueProfile(string playerName, Button ownerButton)
    {
        if (isStartingContinue || ownerButton == null)
        {
            return;
        }

        if (ownerButton != selectedContinueProfileButton)
        {
            SelectContinueProfile(playerName, ownerButton);
            return;
        }

        ToastContainerController resolvedToastContainer = ResolveToastContainer();
        if (resolvedToastContainer == null)
        {
            Debug.LogWarning("MainMenuScript: ToastContainerController not found. Delete confirmation cannot be shown.", this);
            return;
        }

        string title = LanguageManager.Tr(CommonToastConfirmTitleLocalizationKey, "Thong bao");
        string message = LanguageManager.Tr(ContinueDeleteConfirmLocalizationKey, "Ban co muon xoa Profile nay khong?");
        string yesLabel = LanguageManager.Tr(CommonYesLocalizationKey, "Co");
        string noLabel = LanguageManager.Tr(CommonNoLocalizationKey, "Khong");

        resolvedToastContainer.ShowConfirmation(
            title,
            message,
            () => DeleteContinueProfile(playerName),
            null,
            yesLabel,
            noLabel);
    }

    private void DeleteContinueProfile(string playerName)
    {
        if (isStartingContinue)
        {
            return;
        }

        string resolvedName = SanitizePlayerName(playerName);
        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            return;
        }

        if (!PlayerProgressProfileStore.DeleteProfile(resolvedName))
        {
            return;
        }

        if (string.Equals(selectedContinueProfileName, resolvedName, StringComparison.OrdinalIgnoreCase))
        {
            ClearSelectedContinueProfile();
        }

        PopulateContinuePopupList();
        UpdateContinueButtonState();
    }

    private ToastContainerController ResolveToastContainer()
    {
        if (toastContainer == null)
        {
            toastContainer = FindAnyObjectByType<ToastContainerController>();
        }

        return toastContainer;
    }

    private Setting_UIScript ResolveSettingUI()
    {
        if (settingPanel != null)
        {
            Setting_UIScript panelSettingUI = settingPanel.GetComponentInChildren<Setting_UIScript>(true);
            if (panelSettingUI != null)
            {
                return panelSettingUI;
            }
        }

        if (settingUI != null)
        {
            return settingUI;
        }

        return GetComponentInChildren<Setting_UIScript>(true);
    }

    private bool ValidateCriticalSerializedReferences(bool logAsError)
    {
        bool hasMissingReference = false;

        hasMissingReference |= ReportMissingReference(mainMenuPanel, nameof(mainMenuPanel), logAsError);
        hasMissingReference |= ReportMissingReference(settingPanel, nameof(settingPanel), logAsError);
        hasMissingReference |= ReportMissingReference(newGamePopupPanel, nameof(newGamePopupPanel), logAsError);
        hasMissingReference |= ReportMissingReference(playerNameInput, nameof(playerNameInput), logAsError);
        hasMissingReference |= ReportMissingReference(confirmNewGameButton, nameof(confirmNewGameButton), logAsError);
        hasMissingReference |= ReportMissingReference(continueButton, nameof(continueButton), logAsError);
        hasMissingReference |= ReportMissingReference(continueProfileItemPrefab, nameof(continueProfileItemPrefab), logAsError);
        hasMissingReference |= ReportMissingReference(continuePopupPanel, nameof(continuePopupPanel), logAsError);
        hasMissingReference |= ReportMissingReference(continuePopupListRoot, nameof(continuePopupListRoot), logAsError);
        hasMissingReference |= ReportMissingReference(continuePopupCloseButton, nameof(continuePopupCloseButton), logAsError);
        hasMissingReference |= ReportMissingReference(continuePopupConfirmButton, nameof(continuePopupConfirmButton), logAsError);
        hasMissingReference |= ReportMissingReference(continuePopupEmptyRoot, nameof(continuePopupEmptyRoot), logAsError);
        hasMissingReference |= ReportMissingString(loadingSceneName, nameof(loadingSceneName), logAsError);
        hasMissingReference |= ReportMissingString(nextSceneAfterNewGameName, nameof(nextSceneAfterNewGameName), logAsError);

        return !hasMissingReference;
    }

    private bool ReportMissingReference(UnityEngine.Object reference, string fieldName, bool logAsError)
    {
        if (reference != null)
        {
            return false;
        }

        LogMissingSerializedField($"Critical reference '{fieldName}' is not assigned.", logAsError);
        return true;
    }

    private bool ReportMissingString(string value, string fieldName, bool logAsError)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        LogMissingSerializedField($"Critical string '{fieldName}' is empty.", logAsError);
        return true;
    }

    private void LogMissingSerializedField(string message, bool logAsError)
    {
        if (logAsError)
        {
            Debug.LogError($"MainMenuScript: {message}", this);
            return;
        }

        Debug.LogWarning($"MainMenuScript: {message}", this);
    }

    private bool IsNewGamePopupOpen => newGamePopupPanel != null && newGamePopupPanel.alpha > 0.99f;
    private bool IsContinuePopupOpen => continuePopupPanel != null && continuePopupPanel.alpha > 0.99f;
}

