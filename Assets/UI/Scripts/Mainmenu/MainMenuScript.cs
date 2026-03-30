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
    private const string ContinueLocalizationKey = "mainmenu.btn.continue";

    [Header("Panels")]
    [SerializeField] private CanvasGroup mainMenuPanel;
    [SerializeField] private CanvasGroup settingPanel;
    [SerializeField] private CanvasGroup newGamePopupPanel;

    [Header("New Game")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Button confirmNewGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private string loadingSceneName = "LoadingScene";
    [FormerlySerializedAs("tutorialSceneName")]
    [SerializeField] private string nextSceneAfterNewGameName = "LevelSelectScene";
    [SerializeField] private string defaultPlayerName = "Player";
    [SerializeField] private int playerNameCharacterLimit = DefaultPlayerNameCharacterLimit;

    // Add any extra panels here if they should be hidden on load.
    [SerializeField] private CanvasGroup[] otherPanelsToHideOnLoad;
    private Setting_UIScript settingUI;
    private bool isStartingNewGame;
    private bool isStartingContinue;
    private CanvasGroup continuePopupPanel;
    private RectTransform continuePopupListRoot;
    private TMP_Text continuePopupEmptyText;
    private Button continuePopupCloseButton;
    private readonly List<Button> continueProfileButtons = new List<Button>();

    private bool IsPrimaryController => mainMenuPanel != null;

    private void Awake()
    {
        settingUI = GetComponent<Setting_UIScript>();

        if (!IsPrimaryController)
        {
            return;
        }

        ResolveNewGamePopupControls();
        BindNewGameControls();
        ResolveContinueControls();
        EnsureContinuePopup();
        BindContinueControls();

        // On load: MainMenu is visible, the other panels are hidden.
        SetPanelActive(mainMenuPanel, true);
        SetPanelActive(settingPanel, false);
        SetPanelActive(newGamePopupPanel, false);
        SetPanelActive(continuePopupPanel, false);
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
        if (settingUI == null)
        {
            settingUI = GetComponent<Setting_UIScript>();
        }

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
        LoadingFlowState.SetPendingTargetScene(nextSceneAfterNewGameName);

        SceneManager.LoadScene(loadingSceneName);
    }

    // Bind this to the Back button.
    public void BackToMain()
    {
        if (settingUI == null)
        {
            settingUI = GetComponent<Setting_UIScript>();
        }

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

        UpdateContinueButtonState();
    }

    private void ResolveNewGamePopupControls()
    {
        if (newGamePopupPanel == null)
        {
            return;
        }

        Transform popupRoot = newGamePopupPanel.transform;

        if (playerNameInput == null)
        {
            playerNameInput = FindComponentByName<TMP_InputField>(popupRoot, "InputField (TMP)");

            if (playerNameInput == null)
            {
                playerNameInput = popupRoot.GetComponentInChildren<TMP_InputField>(true);
            }
        }

        if (confirmNewGameButton == null)
        {
            confirmNewGameButton = FindComponentByName<Button>(popupRoot, "btnConfirm");

            if (confirmNewGameButton == null)
            {
                Button[] buttons = popupRoot.GetComponentsInChildren<Button>(true);
                foreach (Button button in buttons)
                {
                    if (button != null && button.name != "btnBack")
                    {
                        confirmNewGameButton = button;
                        break;
                    }
                }
            }
        }

        if (playerNameInput != null)
        {
            playerNameInput.characterLimit = Mathf.Max(1, playerNameCharacterLimit);
        }
    }

    private void ResolveContinueControls()
    {
        if (continueButton != null)
        {
            return;
        }

        Transform searchRoot = mainMenuPanel != null ? mainMenuPanel.transform : transform;
        continueButton = FindButtonByLocalizationKey(searchRoot, ContinueLocalizationKey);
        if (continueButton == null)
        {
            continueButton = FindComponentByName<Button>(searchRoot, "btnContinue");
        }
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

        UpdateNewGameConfirmState();
    }

    private void UpdateNewGameConfirmState()
    {
        if (confirmNewGameButton == null)
        {
            return;
        }

        bool hasValidPlayerName = !string.IsNullOrWhiteSpace(SanitizePlayerName(playerNameInput != null ? playerNameInput.text : string.Empty));
        confirmNewGameButton.interactable = !isStartingNewGame && IsNewGamePopupOpen && hasValidPlayerName;
    }

    private void UpdateContinueButtonState()
    {
        if (continueButton == null)
        {
            return;
        }

        continueButton.interactable = !isStartingContinue && PlayerProgressProfileStore.HasAnyProfiles();
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
    }

    public void CloseContinuePopup()
    {
        if (isStartingContinue)
        {
            return;
        }

        SetPanelActive(continuePopupPanel, false);
        SetPanelInteraction(mainMenuPanel, true);
        UpdateContinueButtonState();
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

        LoadingFlowState.SetPlayerName(resolvedName);
        LoadingFlowState.ClearCurrentLevelId();
        LoadingFlowState.ClearPendingScenarioResourcePath();
        LoadingFlowState.ClearPendingCaseId();
        LoadingFlowState.SetPendingTargetScene(nextSceneAfterNewGameName);
        PlayerProgressProfileStore.TouchProfile(resolvedName);

        SceneManager.LoadScene(loadingSceneName);
    }

    private void EnsureContinuePopup()
    {
        if (continuePopupPanel != null)
        {
            return;
        }

        RectTransform canvasRoot = GetCanvasRoot();
        if (canvasRoot == null)
        {
            return;
        }

        GameObject popupRoot = new GameObject("ContinuePopup", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        RectTransform popupRect = popupRoot.GetComponent<RectTransform>();
        popupRect.SetParent(canvasRoot, false);
        popupRect.anchorMin = Vector2.zero;
        popupRect.anchorMax = Vector2.one;
        popupRect.offsetMin = Vector2.zero;
        popupRect.offsetMax = Vector2.zero;
        popupRect.SetAsLastSibling();

        Image overlay = popupRoot.GetComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.72f);
        overlay.raycastTarget = true;

        continuePopupPanel = popupRoot.GetComponent<CanvasGroup>();

        GameObject cardObject = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.SetParent(popupRect, false);
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(560f, 440f);

        Image cardImage = cardObject.GetComponent<Image>();
        cardImage.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);

        VerticalLayoutGroup cardLayout = cardObject.GetComponent<VerticalLayoutGroup>();
        cardLayout.padding = new RectOffset(28, 28, 24, 24);
        cardLayout.spacing = 16f;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = false;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;

        TMP_FontAsset popupFont = ResolvePopupFont();

        TMP_Text titleText = CreatePopupText("Title", cardRect, "Continue", popupFont, 30f, FontStyles.Bold, TextAlignmentOptions.Center);
        LayoutElement titleLayout = titleText.gameObject.AddComponent<LayoutElement>();
        titleLayout.preferredHeight = 42f;

        TMP_Text subtitleText = CreatePopupText("Subtitle", cardRect, "Select a saved profile", popupFont, 20f, FontStyles.Normal, TextAlignmentOptions.Center);
        LayoutElement subtitleLayout = subtitleText.gameObject.AddComponent<LayoutElement>();
        subtitleLayout.preferredHeight = 32f;

        GameObject listObject = new GameObject("ProfileList", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        continuePopupListRoot = listObject.GetComponent<RectTransform>();
        continuePopupListRoot.SetParent(cardRect, false);
        continuePopupListRoot.anchorMin = new Vector2(0f, 1f);
        continuePopupListRoot.anchorMax = new Vector2(1f, 1f);
        continuePopupListRoot.pivot = new Vector2(0.5f, 1f);

        VerticalLayoutGroup listLayout = listObject.GetComponent<VerticalLayoutGroup>();
        listLayout.spacing = 10f;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = false;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;

        ContentSizeFitter listFitter = listObject.GetComponent<ContentSizeFitter>();
        listFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        listFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        continuePopupEmptyText = CreatePopupText("EmptyText", cardRect, "No save profiles found.", popupFont, 18f, FontStyles.Italic, TextAlignmentOptions.Center);
        LayoutElement emptyLayout = continuePopupEmptyText.gameObject.AddComponent<LayoutElement>();
        emptyLayout.preferredHeight = 28f;

        continuePopupCloseButton = CreatePopupButton(cardRect, "Back", popupFont, CloseContinuePopup);
        LayoutElement closeLayout = continuePopupCloseButton.gameObject.AddComponent<LayoutElement>();
        closeLayout.preferredHeight = 56f;
    }

    private void PopulateContinuePopupList()
    {
        if (continuePopupListRoot == null)
        {
            return;
        }

        for (int i = continuePopupListRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(continuePopupListRoot.GetChild(i).gameObject);
        }

        continueProfileButtons.Clear();

        List<PlayerProgressProfileStore.ProfileSummary> profiles = PlayerProgressProfileStore.GetAllProfileSummaries();
        if (continuePopupEmptyText != null)
        {
            continuePopupEmptyText.gameObject.SetActive(profiles.Count <= 0);
        }

        TMP_FontAsset popupFont = ResolvePopupFont();
        for (int i = 0; i < profiles.Count; i++)
        {
            PlayerProgressProfileStore.ProfileSummary profile = profiles[i];
            string buttonLabel = $"{profile.PlayerName}  ({profile.CompletedLevelCount} completed)";
            Button button = CreatePopupButton(
                continuePopupListRoot,
                buttonLabel,
                popupFont,
                () => ContinueWithProfile(profile.PlayerName));
            continueProfileButtons.Add(button);
        }
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

    private TMP_Text CreatePopupText(
        string objectName,
        RectTransform parent,
        string text,
        TMP_FontAsset font,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        TMP_Text label = go.GetComponent<TMP_Text>();
        label.text = text;
        label.font = font != null ? font : label.font;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.color = Color.white;
        return label;
    }

    private Button CreatePopupButton(RectTransform parent, string labelText, TMP_FontAsset font, UnityEngine.Events.UnityAction clickAction)
    {
        GameObject buttonObject = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(parent, false);

        Image background = buttonObject.GetComponent<Image>();
        background.color = new Color(0.93f, 0.72f, 0.38f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = background.color;
        colors.highlightedColor = new Color(1f, 0.82f, 0.50f, 1f);
        colors.pressedColor = new Color(0.84f, 0.60f, 0.22f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.onClick.AddListener(clickAction);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 54f;

        TMP_Text label = CreatePopupText("Label", buttonRect, labelText, font, 20f, FontStyles.Normal, TextAlignmentOptions.Center);
        label.color = new Color(0.17f, 0.17f, 0.17f, 1f);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(14f, 8f);
        label.rectTransform.offsetMax = new Vector2(-14f, -8f);
        return button;
    }

    private RectTransform GetCanvasRoot()
    {
        Canvas canvas = GetComponentInParent<Canvas>(true);
        return canvas != null ? canvas.transform as RectTransform : null;
    }

    private Button FindButtonByLocalizationKey(Transform root, string localizationKey)
    {
        if (root == null || string.IsNullOrWhiteSpace(localizationKey))
        {
            return null;
        }

        LocalizedText[] localizedTexts = root.GetComponentsInChildren<LocalizedText>(true);
        foreach (LocalizedText localizedText in localizedTexts)
        {
            if (localizedText == null || !string.Equals(localizedText.LocalizationKey, localizationKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Button button = localizedText.GetComponentInParent<Button>(true);
            if (button != null)
            {
                return button;
            }
        }

        return null;
    }

    private T FindComponentByName<T>(Transform root, string objectName) where T : Component
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in transforms)
        {
            if (child != null && child.name == objectName)
            {
                T component = child.GetComponent<T>();
                if (component != null)
                {
                    return component;
                }
            }
        }

        return null;
    }

    private bool IsNewGamePopupOpen => newGamePopupPanel != null && newGamePopupPanel.alpha > 0.99f;
    private bool IsContinuePopupOpen => continuePopupPanel != null && continuePopupPanel.alpha > 0.99f;
}
