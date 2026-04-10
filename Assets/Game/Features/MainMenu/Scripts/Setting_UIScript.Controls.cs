using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class Setting_UIScript
{
    private const string KeyboardMouseSchemeName = "KeyboardMouse";
    private const string ControlsWaitingVietnamese = "Nhan phim...";
    private const string ControlsWaitingEnglish = "Press a key...";
    private const string ControlsUnboundVietnamese = "Chua gan";
    private const string ControlsUnboundEnglish = "Unbound";
    private const string ControlsKeyboardVietnamese = "Phim";
    private const string ControlsKeyboardEnglish = "Keyboard";

    [Serializable]
    private sealed class ControlBindingDefinition
    {
        public ControlBindingDefinition(
            string actionName,
            string vietnameseLabel,
            string englishLabel,
            string bindingId = null)
        {
            ActionName = actionName;
            VietnameseLabel = vietnameseLabel;
            EnglishLabel = englishLabel;
            BindingId = bindingId;
        }

        public string ActionName { get; }
        public string VietnameseLabel { get; }
        public string EnglishLabel { get; }
        public string BindingId { get; }
    }

    private sealed class ControlBindingSection
    {
        public ControlBindingSection(string vietnameseTitle, string englishTitle, params ControlBindingDefinition[] bindings)
        {
            VietnameseTitle = vietnameseTitle;
            EnglishTitle = englishTitle;
            Bindings = bindings ?? Array.Empty<ControlBindingDefinition>();
        }

        public string VietnameseTitle { get; }
        public string EnglishTitle { get; }
        public ControlBindingDefinition[] Bindings { get; }
    }

    private sealed class ControlBindingRow
    {
        public ControlBindingDefinition Definition;
        public RectTransform Root;
        public Button Button;
        public TMP_Text ActionLabel;
        public TMP_Text Label;
    }

    private sealed class ControlBindingSectionLabel
    {
        public ControlBindingSection Section;
        public TMP_Text Label;
    }

    [Header("Control Bindings")]
    [SerializeField] private InputActionAsset inputActionsAsset;

    private RectTransform controlsScrollContentRoot;
    private RectTransform controlsTemplateBindingRow;
    private RectTransform generatedControlsBindingsRoot;
    private InputActionRebindingExtensions.RebindingOperation activeControlsRebindOperation;
    private InputAction activeControlsRebindAction;
    private bool activeControlsRebindActionWasEnabled;
    private string bindingSnapshotJson = string.Empty;
    private string bindingDefaultJson = string.Empty;
    private bool hasBindingDefaultSnapshot;
    private TMP_Text controlsKeyboardTitleLabel;
    private TMP_FontAsset controlsFontAsset;
    private Material controlsFontMaterial;
    private Color controlsActionTextColor = Color.white;
    private Color controlsBindingTextColor = new Color32(50, 50, 50, 255);
    private Color controlsSectionTitleColor = Color.white;
    private Color controlsRowBackgroundColor = new Color(1f, 1f, 1f, 0f);
    private Color controlsSectionDividerColor = new Color(1f, 1f, 1f, 0.18f);
    private ColorBlock controlsButtonColors = ColorBlock.defaultColorBlock;
    private Sprite controlsButtonSprite;
    private readonly List<ControlBindingRow> controlBindingRows = new List<ControlBindingRow>();
    private readonly List<ControlBindingSectionLabel> controlBindingSectionLabels = new List<ControlBindingSectionLabel>();
    private readonly List<GameObject> generatedControlRowObjects = new List<GameObject>();

    private static readonly ControlBindingSection[] ControlBindingSections =
    {
        new ControlBindingSection(
            "Onsite Phase",
            "Onsite Phase",
            new ControlBindingDefinition("Move", "Tien thang", "Move Forward", "2063a8b5-6a45-43de-851b-65f3d46e7b58"),
            new ControlBindingDefinition("Move", "Lui lai", "Move Backward", "64e4d037-32e1-4fb9-80e4-fc7330404dfe"),
            new ControlBindingDefinition("Move", "Qua trai", "Move Left", "0fce8b11-5eab-4e4e-a741-b732e7b20873"),
            new ControlBindingDefinition("Move", "Qua phai", "Move Right", "7bdda0d6-57a8-47c8-8238-8aecf3110e47"),
            new ControlBindingDefinition("Jump", "Nhay", "Jump"),
            new ControlBindingDefinition("Sprint", "Chay", "Sprint"),
            new ControlBindingDefinition("Crouch", "Cui", "Crouch"),
            new ControlBindingDefinition("Interact", "Tuong tac", "Interact"),
            new ControlBindingDefinition("Pickup", "Nhat vat", "Pickup"),
            new ControlBindingDefinition("Use", "Su dung vat", "Use Item"),
            new ControlBindingDefinition("Drop", "Tha vat", "Drop Item"),
            new ControlBindingDefinition("Grab", "Giu keo vat", "Grab Object")),
        new ControlBindingSection(
            "Chi huy",
            "Command",
            new ControlBindingDefinition("ToolWheel", "Vong cong cu", "Tool Wheel"),
            new ControlBindingDefinition("CommandMove", "Lenh di chuyen", "Move Command"),
            new ControlBindingDefinition("CommandCancel", "Huy lenh", "Cancel Command"),
            new ControlBindingDefinition("CommandCancelAllFollow", "Dung tat ca follow", "Cancel All Follow"),
            new ControlBindingDefinition("ToggleBotOutline", "Bat tat vien bot", "Toggle Bot Outline")),
        new ControlBindingSection(
            "Thiet bi",
            "Equipment",
            new ControlBindingDefinition("ToggleSprayPattern", "Doi kieu phun", "Toggle Spray Pattern"),
            new ControlBindingDefinition("IncreasePressure", "Tang ap luc", "Increase Pressure"),
            new ControlBindingDefinition("DecreasePressure", "Giam ap luc", "Decrease Pressure"))
    };

    private void InitializeControlsSettings()
    {
        ResolveInputActionsAsset();
        ResolveControlsScrollContentRoot();
        CaptureControlsVisualStyle();

        if (inputActionsAsset != null && !hasBindingDefaultSnapshot)
        {
            bindingDefaultJson = InputBindingOverridesStore.GetCurrentOverridesJson(inputActionsAsset);
            hasBindingDefaultSnapshot = true;
        }

        if (inputActionsAsset != null)
        {
            InputBindingOverridesStore.ApplySavedOverrides(inputActionsAsset);
        }

        BuildControlBindingsUi();
    }

    private void OnEnable()
    {
        LanguageManager.LanguageChanged -= OnControlsLanguageChanged;
        LanguageManager.LanguageChanged += OnControlsLanguageChanged;
        RefreshControlBindingTexts();
    }

    private void OnDisable()
    {
        LanguageManager.LanguageChanged -= OnControlsLanguageChanged;
    }

    private void ResolveInputActionsAsset()
    {
        if (inputActionsAsset != null)
        {
            return;
        }

        PlayerInput playerInput = FindAnyObjectByType<PlayerInput>();
        if (playerInput != null)
        {
            inputActionsAsset = playerInput.actions;
        }
    }

    private void ResolveControlsScrollContentRoot()
    {
        controlsScrollContentRoot = null;
        if (panelCont == null)
        {
            return;
        }

        Transform scrollView = panelCont.transform.Find("Scroll View");
        if (scrollView == null)
        {
            scrollView = FindNamedPanelChild(panelCont, "Scroll View");
        }

        if (scrollView == null)
        {
            return;
        }

        Transform content = scrollView.Find("Viewport/Content");
        if (content == null)
        {
            Transform viewport = scrollView.Find("Viewport");
            if (viewport != null)
            {
                content = viewport.Find("Content");
            }
        }

        controlsScrollContentRoot = content as RectTransform;
    }

    private void CaptureControlsVisualStyle()
    {
        if (panelCont == null)
        {
            return;
        }

        TMP_Text[] texts = panelCont.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null || text.font == null)
            {
                continue;
            }

            controlsFontAsset = text.font;
            controlsFontMaterial = text.fontSharedMaterial;
            controlsActionTextColor = text.color;
            controlsSectionTitleColor = text.color;
            break;
        }

        if (controlsScrollContentRoot == null)
        {
            return;
        }

        Button buttonTemplate = controlsScrollContentRoot.GetComponentInChildren<Button>(true);
        if (buttonTemplate != null)
        {
            controlsButtonColors = buttonTemplate.colors;
            controlsButtonSprite = (buttonTemplate.targetGraphic as Image)?.sprite;

            TMP_Text bindingTextTemplate = buttonTemplate.GetComponentInChildren<TMP_Text>(true);
            if (bindingTextTemplate != null)
            {
                controlsBindingTextColor = bindingTextTemplate.color;
                controlsFontAsset = bindingTextTemplate.font != null ? bindingTextTemplate.font : controlsFontAsset;
                controlsFontMaterial = bindingTextTemplate.fontSharedMaterial != null
                    ? bindingTextTemplate.fontSharedMaterial
                    : controlsFontMaterial;
            }
        }
    }

    private void BuildControlBindingsUi()
    {
        controlBindingRows.Clear();
        controlBindingSectionLabels.Clear();
        controlsKeyboardTitleLabel = null;

        DestroyGeneratedControlRows();

        if (controlsScrollContentRoot == null)
        {
            return;
        }

        ResolveControlsTemplateBindingRow();
        if (controlsTemplateBindingRow == null)
        {
            return;
        }

        controlsTemplateBindingRow.gameObject.SetActive(false);

        if (generatedControlsBindingsRoot != null)
        {
            Destroy(generatedControlsBindingsRoot.gameObject);
            generatedControlsBindingsRoot = null;
        }

        Transform templateParent = controlsTemplateBindingRow.parent;
        int nextSiblingIndex = controlsTemplateBindingRow.GetSiblingIndex() + 1;

        for (int sectionIndex = 0; sectionIndex < ControlBindingSections.Length; sectionIndex++)
        {
            ControlBindingSection section = ControlBindingSections[sectionIndex];
            if (!SectionHasAnyAvailableBinding(section))
            {
                continue;
            }

            for (int bindingIndex = 0; bindingIndex < section.Bindings.Length; bindingIndex++)
            {
                ControlBindingDefinition definition = section.Bindings[bindingIndex];
                if (!TryResolveBinding(definition, out _, out _))
                {
                    continue;
                }

                ControlBindingRow row = CreateControlsBindingRow(definition, templateParent);
                if (row?.Button == null)
                {
                    continue;
                }

                row.Root?.SetSiblingIndex(nextSiblingIndex++);
            }
        }

        RefreshControlBindingRows();
        RefreshControlBindingTexts();
        LayoutRebuilder.ForceRebuildLayoutImmediate(controlsTemplateBindingRow.parent as RectTransform);
        LayoutRebuilder.ForceRebuildLayoutImmediate(controlsScrollContentRoot);
    }

    private void ResolveControlsTemplateBindingRow()
    {
        controlsTemplateBindingRow = null;
        if (controlsScrollContentRoot == null)
        {
            return;
        }

        Transform[] descendants = controlsScrollContentRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform descendant = descendants[i];
            if (descendant == null || !string.Equals(descendant.name, "item1", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            controlsTemplateBindingRow = descendant as RectTransform;
            return;
        }
    }

    private void DestroyGeneratedControlRows()
    {
        for (int i = 0; i < generatedControlRowObjects.Count; i++)
        {
            GameObject rowObject = generatedControlRowObjects[i];
            if (rowObject != null)
            {
                Destroy(rowObject);
            }
        }

        generatedControlRowObjects.Clear();
    }

    private ControlBindingRow CreateControlsBindingRow(ControlBindingDefinition definition, Transform parent)
    {
        if (definition == null || controlsTemplateBindingRow == null || parent == null)
        {
            return null;
        }

        RectTransform rowRect = Instantiate(controlsTemplateBindingRow, parent, false);
        rowRect.gameObject.name = $"item_{controlBindingRows.Count + 1:00}_{definition.ActionName}";
        rowRect.gameObject.SetActive(true);
        generatedControlRowObjects.Add(rowRect.gameObject);

        Button bindingButton = rowRect.GetComponentInChildren<Button>(true);
        TMP_Text bindingLabel = bindingButton != null ? bindingButton.GetComponentInChildren<TMP_Text>(true) : null;
        TMP_Text actionLabel = FindControlsActionLabel(rowRect, bindingButton);

        ControlBindingRow row = new ControlBindingRow
        {
            Definition = definition,
            Root = rowRect,
            Button = bindingButton,
            ActionLabel = actionLabel,
            Label = bindingLabel
        };

        if (actionLabel != null)
        {
            actionLabel.text = GetLocalizedLabel(definition.VietnameseLabel, definition.EnglishLabel);
        }

        if (bindingLabel != null)
        {
            bindingLabel.text = GetControlsUnboundLabel();
        }

        if (bindingButton != null)
        {
            bindingButton.onClick.RemoveAllListeners();
            bindingButton.onClick.AddListener(() => StartControlsRebind(row));
        }

        controlBindingRows.Add(row);
        return row;
    }

    private TMP_Text FindControlsActionLabel(RectTransform rowRoot, Button bindingButton)
    {
        if (rowRoot == null)
        {
            return null;
        }

        TMP_Text[] texts = rowRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            if (bindingButton != null && text.transform.IsChildOf(bindingButton.transform))
            {
                continue;
            }

            return text;
        }

        return null;
    }

    private Button CreateBindingButton(RectTransform parent)
    {
        GameObject buttonObject = new GameObject(
            "BindingButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(parent, false);
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.sizeDelta = new Vector2(220f, 40f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.sprite = controlsButtonSprite;
        buttonImage.type = controlsButtonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        buttonImage.color = Color.white;

        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.colors = controlsButtonColors;
        button.targetGraphic = buttonImage;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.minWidth = 200f;
        layout.preferredWidth = 220f;
        layout.minHeight = 40f;
        layout.preferredHeight = 40f;

        TMP_Text label = CreateText(
            buttonRect,
            "BindingLabel",
            GetControlsUnboundLabel(),
            18f,
            FontStyles.Bold,
            controlsBindingTextColor,
            TextAlignmentOptions.Center);
        label.raycastTarget = false;
        label.margin = new Vector4(8f, 0f, 8f, 0f);

        return button;
    }

    private TMP_Text CreateText(
        RectTransform parent,
        string objectName,
        string text,
        float fontSize,
        FontStyles fontStyle,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(parent, false);
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TMP_Text tmpText = textObject.GetComponent<TMP_Text>();
        tmpText.text = text;
        tmpText.color = color;
        tmpText.fontSize = fontSize;
        tmpText.fontStyle = fontStyle;
        tmpText.alignment = alignment;
        tmpText.textWrappingMode = TextWrappingModes.NoWrap;
        tmpText.overflowMode = TextOverflowModes.Ellipsis;
        tmpText.raycastTarget = false;

        if (controlsFontAsset != null)
        {
            tmpText.font = controlsFontAsset;
        }

        if (controlsFontMaterial != null)
        {
            tmpText.fontSharedMaterial = controlsFontMaterial;
        }

        return tmpText;
    }

    private bool SectionHasAnyAvailableBinding(ControlBindingSection section)
    {
        if (section == null || section.Bindings == null)
        {
            return false;
        }

        for (int i = 0; i < section.Bindings.Length; i++)
        {
            if (TryResolveBinding(section.Bindings[i], out _, out _))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryResolveBinding(ControlBindingDefinition definition, out InputAction action, out int bindingIndex)
    {
        action = null;
        bindingIndex = -1;

        if (definition == null || inputActionsAsset == null)
        {
            return false;
        }

        action = inputActionsAsset.FindAction(definition.ActionName, throwIfNotFound: false);
        if (action == null)
        {
            return false;
        }

        bindingIndex = ResolveBindingIndex(action, definition);
        return bindingIndex >= 0;
    }

    private int ResolveBindingIndex(InputAction action, ControlBindingDefinition definition)
    {
        if (action == null || definition == null)
        {
            return -1;
        }

        if (!string.IsNullOrWhiteSpace(definition.BindingId) &&
            Guid.TryParse(definition.BindingId, out Guid bindingGuid))
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].id == bindingGuid)
                {
                    return i;
                }
            }
        }

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite)
            {
                continue;
            }

            if (BindingMatchesKeyboard(binding))
            {
                return i;
            }
        }

        return -1;
    }

    private void RefreshControlBindingRows()
    {
        for (int i = 0; i < controlBindingRows.Count; i++)
        {
            RefreshControlBindingRow(controlBindingRows[i]);
        }
    }

    private void RefreshControlBindingTexts()
    {
        if (controlsKeyboardTitleLabel != null)
        {
            controlsKeyboardTitleLabel.text = GetControlsKeyboardLabel();
        }

        for (int i = 0; i < controlBindingSectionLabels.Count; i++)
        {
            ControlBindingSectionLabel sectionLabel = controlBindingSectionLabels[i];
            if (sectionLabel?.Label == null || sectionLabel.Section == null)
            {
                continue;
            }

            sectionLabel.Label.text = GetLocalizedLabel(sectionLabel.Section.VietnameseTitle, sectionLabel.Section.EnglishTitle);
        }

        for (int i = 0; i < controlBindingRows.Count; i++)
        {
            ControlBindingRow row = controlBindingRows[i];
            if (row?.ActionLabel == null || row.Definition == null)
            {
                continue;
            }

            row.ActionLabel.text = GetLocalizedLabel(row.Definition.VietnameseLabel, row.Definition.EnglishLabel);
        }

        RefreshControlBindingRows();
    }

    private void RefreshControlBindingRow(ControlBindingRow row)
    {
        if (row == null || row.Button == null || row.Label == null)
        {
            return;
        }

        if (!TryResolveBinding(row.Definition, out InputAction action, out int bindingIndex))
        {
            row.Button.interactable = false;
            row.Label.text = GetControlsUnboundLabel();
            return;
        }

        row.Button.interactable = activeControlsRebindOperation == null;
        row.Label.text = GetReadableBindingDisplay(action, bindingIndex);
    }

    private string GetReadableBindingDisplay(InputAction action, int bindingIndex)
    {
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
        {
            return GetControlsUnboundLabel();
        }

        InputBinding binding = action.bindings[bindingIndex];
        if (string.IsNullOrWhiteSpace(binding.effectivePath))
        {
            return GetControlsUnboundLabel();
        }

        string display = action.GetBindingDisplayString(bindingIndex);
        if (!string.IsNullOrWhiteSpace(display))
        {
            return display;
        }

        string humanReadable = InputControlPath.ToHumanReadableString(
            binding.effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice);

        return string.IsNullOrWhiteSpace(humanReadable) ? GetControlsUnboundLabel() : humanReadable;
    }

    private void StartControlsRebind(ControlBindingRow row)
    {
        if (row == null || activeControlsRebindOperation != null)
        {
            return;
        }

        if (!TryResolveBinding(row.Definition, out InputAction action, out int bindingIndex))
        {
            return;
        }

        activeControlsRebindAction = action;
        activeControlsRebindActionWasEnabled = action.enabled;
        SetControlBindingButtonsInteractable(false);
        row.Label.text = GetControlsWaitingLabel();

        if (action.enabled)
        {
            action.Disable();
        }

        EventSystem.current?.SetSelectedGameObject(null);

        activeControlsRebindOperation = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsHavingToMatchPath("<Keyboard>")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnCancel(operation => CompleteControlsRebind(row, canceled: true))
            .OnComplete(operation => CompleteControlsRebind(row, canceled: false));

        activeControlsRebindOperation.Start();
    }

    private void CompleteControlsRebind(ControlBindingRow row, bool canceled)
    {
        activeControlsRebindOperation?.Dispose();
        activeControlsRebindOperation = null;

        if (activeControlsRebindAction != null && activeControlsRebindActionWasEnabled)
        {
            activeControlsRebindAction.Enable();
        }

        if (!canceled && row != null && TryResolveBinding(row.Definition, out InputAction action, out int bindingIndex))
        {
            ResolveBindingConflicts(action, bindingIndex);
            InputBindingOverridesStore.ApplyCurrentOverridesToActivePlayerInputs(inputActionsAsset);
            MarkDirty();
        }

        activeControlsRebindAction = null;
        activeControlsRebindActionWasEnabled = false;
        SetControlBindingButtonsInteractable(true);
        RefreshControlBindingRows();
    }

    private void ResolveBindingConflicts(InputAction sourceAction, int sourceBindingIndex)
    {
        if (sourceAction == null || sourceBindingIndex < 0 || sourceBindingIndex >= sourceAction.bindings.Count)
        {
            return;
        }

        string sourcePath = sourceAction.bindings[sourceBindingIndex].effectivePath;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }

        InputActionMap actionMap = sourceAction.actionMap;
        if (actionMap == null)
        {
            return;
        }

        for (int actionIndex = 0; actionIndex < actionMap.actions.Count; actionIndex++)
        {
            InputAction otherAction = actionMap.actions[actionIndex];
            if (otherAction == null)
            {
                continue;
            }

            for (int bindingIndex = 0; bindingIndex < otherAction.bindings.Count; bindingIndex++)
            {
                if (otherAction == sourceAction && bindingIndex == sourceBindingIndex)
                {
                    continue;
                }

                InputBinding binding = otherAction.bindings[bindingIndex];
                if (binding.isComposite || !BindingMatchesKeyboard(binding))
                {
                    continue;
                }

                if (!string.Equals(binding.effectivePath, sourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                otherAction.ApplyBindingOverride(bindingIndex, string.Empty);
            }
        }
    }

    private static bool BindingMatchesKeyboard(InputBinding binding)
    {
        if (!string.IsNullOrWhiteSpace(binding.effectivePath) &&
            binding.effectivePath.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return GroupsContainScheme(binding.groups, KeyboardMouseSchemeName);
    }

    private static bool GroupsContainScheme(string groups, string scheme)
    {
        if (string.IsNullOrWhiteSpace(groups) || string.IsNullOrWhiteSpace(scheme))
        {
            return false;
        }

        string[] split = groups.Split(';');
        for (int i = 0; i < split.Length; i++)
        {
            if (string.Equals(split[i], scheme, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void SetControlBindingButtonsInteractable(bool interactable)
    {
        for (int i = 0; i < controlBindingRows.Count; i++)
        {
            ControlBindingRow row = controlBindingRows[i];
            if (row?.Button != null)
            {
                row.Button.interactable = interactable;
            }
        }
    }

    private string GetLocalizedLabel(string vietnamese, string english)
    {
        AppLanguage language = LanguageManager.Instance != null
            ? LanguageManager.Instance.CurrentLanguage
            : defaultLanguage;

        return language == AppLanguage.English ? english : vietnamese;
    }

    private string GetControlsWaitingLabel()
    {
        return GetLocalizedLabel(ControlsWaitingVietnamese, ControlsWaitingEnglish);
    }

    private string GetControlsUnboundLabel()
    {
        return GetLocalizedLabel(ControlsUnboundVietnamese, ControlsUnboundEnglish);
    }

    private string GetControlsKeyboardLabel()
    {
        return GetLocalizedLabel(ControlsKeyboardVietnamese, ControlsKeyboardEnglish);
    }

    private void OnControlsLanguageChanged(AppLanguage _)
    {
        RefreshControlBindingTexts();
    }

    private void SaveControlBindingOverrides()
    {
        if (inputActionsAsset == null)
        {
            return;
        }

        InputBindingOverridesStore.SaveCurrentOverrides(inputActionsAsset);
        InputBindingOverridesStore.ApplyCurrentOverridesToActivePlayerInputs(inputActionsAsset);
    }

    private void CaptureBindingSnapshot()
    {
        bindingSnapshotJson = inputActionsAsset != null
            ? InputBindingOverridesStore.GetCurrentOverridesJson(inputActionsAsset)
            : string.Empty;
    }

    private bool TryGetBindingDifferenceDescription(out string description)
    {
        description = null;

        if (inputActionsAsset == null)
        {
            return false;
        }

        string currentJson = InputBindingOverridesStore.GetCurrentOverridesJson(inputActionsAsset);
        if (string.Equals(currentJson, bindingSnapshotJson, StringComparison.Ordinal))
        {
            return false;
        }

        description = "Control binding overrides differ from the active snapshot.";
        return true;
    }

    private void RestoreBindingSnapshot()
    {
        if (inputActionsAsset == null)
        {
            return;
        }

        InputBindingOverridesStore.ApplyOverridesJson(inputActionsAsset, bindingSnapshotJson);
        InputBindingOverridesStore.ApplyCurrentOverridesToActivePlayerInputs(inputActionsAsset);
        RefreshControlBindingRows();
    }

    private void RestoreDefaultBindings()
    {
        if (inputActionsAsset == null)
        {
            return;
        }

        InputBindingOverridesStore.ApplyOverridesJson(inputActionsAsset, bindingDefaultJson);
        InputBindingOverridesStore.ApplyCurrentOverridesToActivePlayerInputs(inputActionsAsset);
        RefreshControlBindingRows();
    }

    private void DisposeActiveControlsRebindOperation()
    {
        if (activeControlsRebindOperation == null)
        {
            return;
        }

        InputActionRebindingExtensions.RebindingOperation operation = activeControlsRebindOperation;
        activeControlsRebindOperation = null;
        operation.Cancel();
        operation.Dispose();
    }

    private void OnDestroy()
    {
        LanguageManager.LanguageChanged -= OnControlsLanguageChanged;
        DisposeActiveControlsRebindOperation();
    }
}
