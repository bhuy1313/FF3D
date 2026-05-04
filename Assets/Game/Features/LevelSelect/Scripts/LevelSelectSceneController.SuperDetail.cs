using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

public partial class LevelSelectSceneController
{
    [Header("Super Detail Custom UI")]
    [SerializeField] private CanvasGroup levelSuperDetailCanvasGroup;
    [SerializeField] private RectTransform levelSuperDetailContentRoot;
    [SerializeField] private Image levelSuperDetailMapImage;
    [SerializeField] private TMP_Text levelSuperDetailMapCaptionText;
    [SerializeField] private TMP_Text levelSuperDetailTitleText;
    [SerializeField] private TMP_Text levelSuperDetailMetaText;
    [SerializeField] private TMP_Text levelSuperDetailBriefingText;
    [SerializeField] private TMP_Text levelSuperDetailScenarioText;
    [SerializeField] private TMP_Text levelSuperDetailRecordsText;
    [SerializeField] private TMP_Text levelSuperDetailTipsText;

    private bool superDetailButtonsBound = false;
    private Button superDetailBtnStart;
    private Button superDetailBtnScenario;
    private Button superDetailBtnBack;
    private Button superDetailBtnClose;
    private CanvasGroup superDetailScenarioButtonCanvasGroup;
    private RectTransform superDetailScenarioDropdownRoot;
    private CanvasGroup superDetailScenarioDropdownCanvasGroup;
    private RectTransform superDetailScenarioDropdownContentRoot;
    private Button superDetailScenarioDropdownTemplateButton;
    private Button superDetailScenarioToggleProxyButton;
    private bool superDetailLayoutConfigured;
    private Tween superDetailScenarioButtonPulseTween;

    private void EnsureSuperDetailButtons()
    {
        if (superDetailButtonsBound || levelSuperDetailContentRoot == null) return;

        Button[] buttons = levelSuperDetailContentRoot.parent.GetComponentsInChildren<Button>(true);
        foreach (Button b in buttons)
        {
            if (b.name == "btnStart")
            {
                superDetailBtnStart = b;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(PlaySelectedLevel);
                ConfigureSuperDetailButtonVisual(b, SuperDetailButtonRole.Start);
            }
            else if (b.name == "btnScenario")
            {
                superDetailBtnScenario = b;
                superDetailScenarioButtonCanvasGroup = GetOrAddComponent<CanvasGroup>(b.gameObject);
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(ToggleSuperDetailScenarioDropdown);
                ConfigureSuperDetailButtonVisual(b, SuperDetailButtonRole.Scenario);
            }
            else if (b.name == "btnBack")
            {
                superDetailBtnBack = b;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(BackFromSuperDetail);
                ConfigureSuperDetailButtonVisual(b, SuperDetailButtonRole.Back);
            }
            else if (b.name == "btnClose")
            {
                superDetailBtnClose = b;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => CloseLevelInfo());
                ConfigureSuperDetailButtonVisual(b, SuperDetailButtonRole.Close);
            }
        }
        
        superDetailButtonsBound = true;
    }

    private enum SuperDetailButtonRole
    {
        Start,
        Scenario,
        Back,
        Close
    }

    private static void ConfigureSuperDetailButtonVisual(Button button, SuperDetailButtonRole role)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.targetGraphic as Image;
        if (image == null)
        {
            image = button.GetComponent<Image>();
            button.targetGraphic = image;
        }

        Color normal;
        Color highlighted;
        Color pressed;
        Color disabled;
        Color label;

        switch (role)
        {
            case SuperDetailButtonRole.Start:
                normal = new Color32(0x18, 0x9A, 0x58, 0xFF);
                highlighted = new Color32(0x20, 0xBF, 0x6B, 0xFF);
                pressed = new Color32(0x0E, 0x6F, 0x40, 0xFF);
                disabled = new Color32(0x35, 0x45, 0x3D, 0xA8);
                label = Color.white;
                break;
            case SuperDetailButtonRole.Scenario:
                normal = new Color32(0x1F, 0x6F, 0xB8, 0xFF);
                highlighted = new Color32(0x2E, 0x9A, 0xFF, 0xFF);
                pressed = new Color32(0x14, 0x4D, 0x86, 0xFF);
                disabled = new Color32(0x2D, 0x3B, 0x49, 0xA0);
                label = Color.white;
                break;
            case SuperDetailButtonRole.Close:
                normal = new Color32(0x8E, 0x35, 0x35, 0xFF);
                highlighted = new Color32(0xC7, 0x45, 0x45, 0xFF);
                pressed = new Color32(0x62, 0x23, 0x23, 0xFF);
                disabled = new Color32(0x44, 0x32, 0x32, 0xA0);
                label = Color.white;
                break;
            default:
                normal = new Color32(0x3B, 0x46, 0x56, 0xFF);
                highlighted = new Color32(0x55, 0x66, 0x7A, 0xFF);
                pressed = new Color32(0x27, 0x30, 0x3C, 0xFF);
                disabled = new Color32(0x34, 0x38, 0x40, 0xA0);
                label = new Color32(0xE8, 0xF0, 0xF8, 0xFF);
                break;
        }

        if (image != null)
        {
            image.color = normal;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = highlighted;
        colors.selectedColor = highlighted;
        colors.pressedColor = pressed;
        colors.disabledColor = disabled;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.11f;
        button.colors = colors;
        button.transition = Selectable.Transition.ColorTint;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.color = label;
            text.fontStyle |= FontStyles.Bold;
        }

        RectTransform rect = button.transform as RectTransform;
        if (rect != null)
        {
            rect.DOKill();
            rect.localScale = Vector3.one;
        }

        ConfigureSuperDetailButtonMotion(button, role);
    }

    private static void ConfigureSuperDetailButtonMotion(Button button, SuperDetailButtonRole role)
    {
        EventTrigger trigger = GetOrAddComponent<EventTrigger>(button.gameObject);
        trigger.triggers.Clear();

        AddSuperDetailButtonTrigger(trigger, EventTriggerType.PointerEnter, _ =>
        {
            if (!button.interactable) return;
            button.transform.DOKill();
            button.transform.DOScale(role == SuperDetailButtonRole.Scenario ? 1.075f : 1.045f, 0.12f).SetEase(Ease.OutCubic);
        });

        AddSuperDetailButtonTrigger(trigger, EventTriggerType.PointerExit, _ =>
        {
            button.transform.DOKill();
            button.transform.DOScale(1f, 0.14f).SetEase(Ease.OutCubic);
        });

        AddSuperDetailButtonTrigger(trigger, EventTriggerType.PointerDown, _ =>
        {
            if (!button.interactable) return;
            button.transform.DOKill();
            button.transform.DOScale(0.965f, 0.08f).SetEase(Ease.OutCubic);
        });

        AddSuperDetailButtonTrigger(trigger, EventTriggerType.PointerUp, _ =>
        {
            if (!button.interactable) return;
            button.transform.DOKill();
            button.transform.DOScale(1.045f, 0.1f).SetEase(Ease.OutBack);
        });
    }

    private static void AddSuperDetailButtonTrigger(EventTrigger trigger, EventTriggerType eventType, UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = eventType
        };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    private void BackFromSuperDetail()
    {
        LevelPanelAnimation anim = UnityEngine.Object.FindAnyObjectByType<LevelPanelAnimation>();
        if (anim != null)
        {
            anim.CloseSuperDetail();
        }
        else
        {
            HideLevelSuperDetailImmediate();
        }
    }

    private void RefreshLevelSuperDetail()
    {
        if (levelSuperDetailContentRoot == null || selectedLevelDefinition == null)
        {
            return;
        }

        EnsureSuperDetailButtons();
        RefreshSuperDetailScenarioButtonVisibility();

        ScenarioDefinition scenario = GetDisplayedScenario(selectedLevelDefinition);
        string title = ResolveDisplayedLevelName(selectedLevelDefinition, scenario, selectedLevelSourceButton);
        string area = ResolveDisplayedAreaSummary(selectedLevelCard, selectedLevelDefinition, scenario);
        string difficulty = ResolveDisplayedDifficultySummary(selectedLevelDefinition, scenario);
        string description = ResolveDisplayedDescription(selectedLevelDefinition, scenario);
        string objective = ResolveDisplayedObjective(selectedLevelDefinition, scenario);
        string levelId = ResolveLevelIdForSuperDetail(selectedLevelDefinition);
        List<PlayerCompletionRecord> levelRecords = GetLevelRecordsForSuperDetail(levelId);
        PlayerCompletionRecord bestRecord = GetBestLevelRecord(levelRecords);

        SetText(levelSuperDetailTitleText, title);
        SetText(levelSuperDetailMetaText, BuildSuperDetailMetaText(area, difficulty, levelId, selectedLevelDefinition));
        SetText(levelSuperDetailBriefingText, BuildSuperDetailBriefingText(description, objective));
        SetText(levelSuperDetailScenarioText, BuildSuperDetailScenarioText(selectedLevelDefinition, scenario));
        SetText(levelSuperDetailRecordsText, BuildSuperDetailRecordsText(levelRecords, bestRecord));
        SetText(levelSuperDetailTipsText, BuildSuperDetailTipsText(selectedLevelDefinition, scenario));
        RefreshSuperDetailMapPreview(selectedLevelCard, title);

        ApplyLanguageFont(levelSuperDetailTitleText, LanguageFontRole.Heading);
        ApplyLanguageFont(levelSuperDetailMapCaptionText, LanguageFontRole.Default);
        ApplyLanguageFont(levelSuperDetailMetaText, LanguageFontRole.Default);
        ApplyLanguageFont(levelSuperDetailBriefingText, LanguageFontRole.Default);
        ApplyLanguageFont(levelSuperDetailScenarioText, LanguageFontRole.Default);
        ApplyLanguageFont(levelSuperDetailRecordsText, LanguageFontRole.Default);
        ApplyLanguageFont(levelSuperDetailTipsText, LanguageFontRole.Default);
        
        ForceSuperDetailLayout();
        AnimateSuperDetailShow();
    }

    private void RefreshSuperDetailScenarioButtonVisibility()
    {
        if (superDetailBtnScenario == null)
        {
            return;
        }

        bool shouldShow = HasMultipleConfiguredScenarios(selectedLevelDefinition);
        if (!shouldShow)
        {
            SetSuperDetailScenarioDropdownVisible(false);
        }

        if (superDetailScenarioButtonCanvasGroup == null)
        {
            superDetailScenarioButtonCanvasGroup = GetOrAddComponent<CanvasGroup>(superDetailBtnScenario.gameObject);
        }

        superDetailScenarioButtonCanvasGroup.DOKill();
        superDetailScenarioButtonCanvasGroup.alpha = shouldShow ? 1f : 0f;
        superDetailScenarioButtonCanvasGroup.interactable = shouldShow;
        superDetailScenarioButtonCanvasGroup.blocksRaycasts = shouldShow;
        superDetailBtnScenario.interactable = shouldShow;

        LayoutElement layoutElement = superDetailBtnScenario.GetComponent<LayoutElement>();
        if (layoutElement != null)
        {
            layoutElement.ignoreLayout = !shouldShow;
        }

        if (!shouldShow)
        {
            UpdateSuperDetailScenarioButtonState(false);
        }
    }

    private void AnimateSuperDetailShow()
    {
        // Chỉ chạy hiệu ứng trượt nội dung (Slide) khi refresh data, 
        // TRÁNH đụng chạm đến CanvasGroup alpha vì đã được script LevelInfoAnimation quản lý.
        if (levelSuperDetailContentRoot != null)
        {
            levelSuperDetailContentRoot.DOKill();
            // Start from the right side of the screen (e.g., X = 1920)
            levelSuperDetailContentRoot.anchoredPosition = new Vector2(1920f, 0f);
            levelSuperDetailContentRoot.DOAnchorPosX(0f, 0.5f).SetEase(Ease.OutQuint);
        }
    }

    private void HideLevelSuperDetailImmediate()
    {
        UpdateSuperDetailScenarioButtonState(false);

        if (levelSuperDetailContentRoot != null) 
        {
            levelSuperDetailContentRoot.DOKill();
        }
        
        if (levelSuperDetailCanvasGroup != null)
        {
            levelSuperDetailCanvasGroup.DOKill();
            levelSuperDetailCanvasGroup.alpha = 0f;
            levelSuperDetailCanvasGroup.interactable = false;
            levelSuperDetailCanvasGroup.blocksRaycasts = false;
        }
        
        SetSuperDetailScenarioDropdownVisible(false);
    }

    private void RefreshSuperDetailMapPreview(RegionCard card, string title)
    {
        if (levelSuperDetailMapImage == null)
        {
            return;
        }

        Sprite sprite = selectedLevelDefinition != null && selectedLevelDefinition.mapSprite != null
            ? selectedLevelDefinition.mapSprite
            : card != null ? card.mapSprite : null;
        levelSuperDetailMapImage.sprite = sprite;
        levelSuperDetailMapImage.preserveAspect = true;
        levelSuperDetailMapImage.color = sprite != null
            ? Color.white
            : new Color(0.15f, 0.20f, 0.25f, 0.94f);

        SetText(
            levelSuperDetailMapCaptionText,
            sprite != null
                ? title
                : LanguageManager.Tr("levelselect.super_detail.map_placeholder", "Map preview image slot"));
    }

    private string BuildSuperDetailMetaText(
        string area,
        string difficulty,
        string levelId,
        LevelDefinition definition)
    {
        StringBuilder builder = new StringBuilder();
        AppendSuperDetailLine(builder, "Area", area);
        AppendSuperDetailLine(builder, "Difficulty", difficulty);
        AppendSuperDetailLine(builder, "Level ID", levelId);
        AppendSuperDetailLine(builder, "Call Scene", ResolveTargetSceneName(definition, GetDisplayedScenario(definition)));
        AppendSuperDetailLine(builder, "Onsite Scene", ResolveOnsiteSceneName(definition, GetDisplayedScenario(definition)));
        return builder.ToString().TrimEnd();
    }

    private static string BuildSuperDetailBriefingText(string description, string objective)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("<color=#87CEFA>■ Briefing:</color>"); 
        builder.AppendLine(string.IsNullOrWhiteSpace(description) ? "<color=#808080>-</color>" : description.Trim());
        builder.AppendLine();
        builder.AppendLine("<color=#87CEFA>■ Primary objective:</color>");
        builder.AppendLine(string.IsNullOrWhiteSpace(objective) ? "<color=#808080>-</color>" : objective.Trim());
        return builder.ToString().TrimEnd();
    }

    private string BuildSuperDetailScenarioText(LevelDefinition definition, ScenarioDefinition selectedScenario)
    {
        StringBuilder builder = new StringBuilder();
        if (selectedScenario != null)
        {
            builder.AppendLine($"<color=#87CEFA>Selected:</color> <color=#FFFFFF>{ResolveScenarioDisplayName(selectedScenario, definition)}</color>");
            AppendSuperDetailLine(builder, "Case ID", selectedScenario.caseId);
            AppendSuperDetailLine(builder, "Scenario ID", selectedScenario.scenarioId);
            AppendSuperDetailLine(builder, "Resource", selectedScenario.scenarioResourcePath);
            return builder.ToString().TrimEnd();
        }

        builder.AppendLine("<color=#87CEFA>Selected:</color> <color=#FFD700>Random incident</color>");
        ScenarioDefinition[] scenarios = GetConfiguredScenarios(definition);
        if (scenarios == null || scenarios.Length == 0)
        {
            AppendSuperDetailLine(builder, "Case ID", definition != null ? definition.caseId : string.Empty);
            AppendSuperDetailLine(builder, "Resource", definition != null ? definition.scenarioResourcePath : string.Empty);
            return builder.ToString().TrimEnd();
        }

        builder.AppendLine("<color=#87CEFA>Possible incidents:</color>");
        for (int i = 0; i < scenarios.Length; i++)
        {
            ScenarioDefinition scenario = scenarios[i];
            if (scenario == null)
            {
                continue;
            }

            string caseId = string.IsNullOrWhiteSpace(scenario.caseId) ? "No case" : scenario.caseId.Trim();
            builder.AppendLine($"  - {ResolveScenarioDisplayName(scenario, definition)} <color=#808080>({caseId})</color>");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildSuperDetailRecordsText(
        List<PlayerCompletionRecord> records,
        PlayerCompletionRecord bestRecord)
    {
        if (records == null || records.Count == 0)
        {
            return "<color=#808080><i>No saved completion records for this level yet.</i></color>";
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"<color=#87CEFA>Saved records:</color> {records.Count}");
        if (bestRecord != null)
        {
            string score = bestRecord.totalMaximumScore > 0
                ? $"<color=#00FF00>{bestRecord.totalScore}</color>/{bestRecord.totalMaximumScore}"
                : "<color=#808080>-</color>";
            string rank = !string.IsNullOrWhiteSpace(bestRecord.onsiteRank) ? $"<color=#FFD700>{bestRecord.onsiteRank}</color>" : "<color=#808080>-</color>";
            builder.AppendLine($"<color=#87CEFA>Best score:</color> {score}");
            builder.AppendLine($"<color=#87CEFA>Best rank:</color> {rank}");
            builder.AppendLine($"<color=#87CEFA>Best onsite time:</color> {FormatClock(Mathf.RoundToInt(bestRecord.onsiteElapsedSeconds))}");
        }

        PlayerCompletionRecord latest = records[0];
        if (latest != null)
        {
            builder.AppendLine($"<color=#87CEFA>Latest save:</color> <color=#A9A9A9>{FormatSuperDetailDate(latest.savedUtcTicks)}</color>");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildSuperDetailTipsText(LevelDefinition definition, ScenarioDefinition scenario)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("<color=#A9A9A9>• Review caller details carefully before dispatching onsite.</color>");
        builder.AppendLine("<color=#A9A9A9>• The call phase can affect onsite setup and context.</color>");
        builder.AppendLine(scenario != null
            ? "<color=#A9A9A9>• Manual scenario selection locks this briefing to one incident.</color>"
            : "<color=#A9A9A9>• Random incident mode can pick any configured scenario for this level.</color>");
        if (definition != null && GetConfiguredScenarioCount(definition) > 1)
        {
            builder.AppendLine("<color=#A9A9A9>• Double-click a scenario in the dropdown to pin or unpin it.</color>");
        }

        return builder.ToString().TrimEnd();
    }

    private List<PlayerCompletionRecord> GetLevelRecordsForSuperDetail(string levelId)
    {
        List<PlayerCompletionRecord> records = PlayerCompletionRecordStore.GetRecords(LoadingFlowState.GetPlayerName());
        if (records == null || string.IsNullOrWhiteSpace(levelId))
        {
            return new List<PlayerCompletionRecord>();
        }

        List<PlayerCompletionRecord> filtered = new List<PlayerCompletionRecord>();
        for (int i = 0; i < records.Count; i++)
        {
            PlayerCompletionRecord record = records[i];
            if (record != null && string.Equals(record.levelId, levelId, StringComparison.OrdinalIgnoreCase))
            {
                filtered.Add(record);
            }
        }

        return filtered;
    }

    private static PlayerCompletionRecord GetBestLevelRecord(List<PlayerCompletionRecord> records)
    {
        if (records == null || records.Count == 0)
        {
            return null;
        }

        PlayerCompletionRecord best = records[0];
        for (int i = 1; i < records.Count; i++)
        {
            PlayerCompletionRecord record = records[i];
            if (CompareSuperDetailRecord(record, best) > 0)
            {
                best = record;
            }
        }

        return best;
    }

    private static int CompareSuperDetailRecord(PlayerCompletionRecord left, PlayerCompletionRecord right)
    {
        float leftRatio = left != null && left.totalMaximumScore > 0 ? left.totalScore / (float)left.totalMaximumScore : 0f;
        float rightRatio = right != null && right.totalMaximumScore > 0 ? right.totalScore / (float)right.totalMaximumScore : 0f;
        int scoreCompare = leftRatio.CompareTo(rightRatio);
        if (scoreCompare != 0)
        {
            return scoreCompare;
        }

        float leftTime = left != null ? left.onsiteElapsedSeconds : float.MaxValue;
        float rightTime = right != null ? right.onsiteElapsedSeconds : float.MaxValue;
        return rightTime.CompareTo(leftTime);
    }

    private static string ResolveLevelIdForSuperDetail(LevelDefinition definition)
    {
        if (definition == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(definition.levelId))
        {
            return definition.levelId.Trim();
        }

        return !string.IsNullOrWhiteSpace(definition.buttonName)
            ? definition.buttonName.Trim()
            : string.Empty;
    }

    private static void AppendSuperDetailLine(StringBuilder builder, string label, string value)
    {
        builder.AppendLine($"<color=#87CEFA>{label}:</color> {(!string.IsNullOrWhiteSpace(value) ? value.Trim() : "<color=#808080>-</color>")}");
    }

    private static string FormatSuperDetailDate(long utcTicks)
    {
        if (utcTicks <= 0)
        {
            return "-";
        }

        DateTime utc = new DateTime(utcTicks, DateTimeKind.Utc);
        return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }

    private void ForceSuperDetailLayout()
    {
        if (levelSuperDetailContentRoot == null)
        {
            return;
        }

        ConfigureSuperDetailLayout();
        RefreshSuperDetailTextLayout(levelSuperDetailTitleText);
        RefreshSuperDetailTextLayout(levelSuperDetailMetaText);
        RefreshSuperDetailTextLayout(levelSuperDetailBriefingText);
        RefreshSuperDetailTextLayout(levelSuperDetailScenarioText);
        RefreshSuperDetailTextLayout(levelSuperDetailRecordsText);
        RefreshSuperDetailTextLayout(levelSuperDetailTipsText);

        Canvas.ForceUpdateCanvases();
        ForceRebuildChain(levelSuperDetailBriefingText != null ? levelSuperDetailBriefingText.rectTransform : null, levelSuperDetailContentRoot);
        ForceRebuildChain(levelSuperDetailScenarioText != null ? levelSuperDetailScenarioText.rectTransform : null, levelSuperDetailContentRoot);
        ForceRebuildChain(levelSuperDetailRecordsText != null ? levelSuperDetailRecordsText.rectTransform : null, levelSuperDetailContentRoot);
        ForceRebuildChain(levelSuperDetailTipsText != null ? levelSuperDetailTipsText.rectTransform : null, levelSuperDetailContentRoot);
        LayoutRebuilder.ForceRebuildLayoutImmediate(levelSuperDetailContentRoot);
        Canvas.ForceUpdateCanvases();
    }

    private void ConfigureSuperDetailLayout()
    {
        if (superDetailLayoutConfigured || levelSuperDetailContentRoot == null)
        {
            return;
        }

        ScrollRect scrollRect = levelSuperDetailContentRoot.GetComponentInChildren<ScrollRect>(true);
        if (scrollRect != null)
        {
            ConfigureSuperDetailScrollRect(scrollRect);
            ConfigureSuperDetailVerticalLayout(scrollRect.content);
        }

        ConfigureSuperDetailSection(levelSuperDetailBriefingText);
        ConfigureSuperDetailSection(levelSuperDetailScenarioText);
        ConfigureSuperDetailSection(levelSuperDetailRecordsText);
        ConfigureSuperDetailSection(levelSuperDetailTipsText);
        superDetailLayoutConfigured = true;
    }

    private static void ConfigureSuperDetailScrollRect(ScrollRect scrollRect)
    {
        RectTransform scrollTransform = scrollRect.transform as RectTransform;
        if (scrollTransform != null)
        {
            scrollTransform.anchorMin = Vector2.zero;
            scrollTransform.anchorMax = Vector2.one;
            scrollTransform.offsetMin = Vector2.zero;
            scrollTransform.offsetMax = Vector2.zero;
        }

        if (scrollRect.viewport != null)
        {
            scrollRect.viewport.anchorMin = Vector2.zero;
            scrollRect.viewport.anchorMax = Vector2.one;
            scrollRect.viewport.offsetMin = Vector2.zero;
            scrollRect.viewport.offsetMax = Vector2.zero;
        }

        if (scrollRect.content != null)
        {
            scrollRect.content.anchorMin = new Vector2(0f, 1f);
            scrollRect.content.anchorMax = new Vector2(1f, 1f);
            scrollRect.content.pivot = new Vector2(0f, 1f);
            scrollRect.content.anchoredPosition = Vector2.zero;
            scrollRect.content.sizeDelta = new Vector2(0f, scrollRect.content.sizeDelta.y);
        }
    }

    private static void ConfigureSuperDetailSection(TMP_Text bodyText)
    {
        if (bodyText == null)
        {
            return;
        }

        RectTransform bodyRect = bodyText.rectTransform;
        ConfigureSuperDetailBodyText(bodyText);
        ConfigureSuperDetailVerticalLayout(bodyRect.parent as RectTransform);
    }

    private static void ConfigureSuperDetailBodyText(TMP_Text bodyText)
    {
        RectTransform bodyRect = bodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0f, 1f);
        bodyRect.anchoredPosition = Vector2.zero;
        bodyRect.sizeDelta = new Vector2(0f, bodyRect.sizeDelta.y);

        ContentSizeFitter fitter = GetOrAddComponent<ContentSizeFitter>(bodyText.gameObject);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement layoutElement = GetOrAddComponent<LayoutElement>(bodyText.gameObject);
        layoutElement.ignoreLayout = false;
        layoutElement.flexibleWidth = 1f;
        layoutElement.flexibleHeight = 0f;
    }

    private static void ConfigureSuperDetailVerticalLayout(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            return;
        }

        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childScaleWidth = false;
        layout.childScaleHeight = false;
    }

    private static void RefreshSuperDetailTextLayout(TMP_Text text)
    {
        if (text == null)
        {
            return;
        }

        text.ForceMeshUpdate();
        LayoutElement layoutElement = text.GetComponent<LayoutElement>();
        if (layoutElement != null)
        {
            layoutElement.preferredHeight = text.preferredHeight;
        }

        LayoutRebuilder.MarkLayoutForRebuild(text.rectTransform);
    }

    private void EnsureSuperDetailScenarioDropdown()
    {
        if (superDetailScenarioDropdownRoot != null) return;
        
        EnsureLevelInfoPopup(); // Make sure levelInfoPopup is initialized first
        if (levelInfoPopup.scenarioDropdownRoot == null) return;

        GameObject clone = UnityEngine.Object.Instantiate(levelInfoPopup.scenarioDropdownRoot.gameObject, levelSuperDetailContentRoot.parent);
        clone.name = "SuperDetailScenarioDropdown";
        superDetailScenarioDropdownRoot = clone.GetComponent<RectTransform>();
        superDetailScenarioDropdownCanvasGroup = GetOrAddComponent<CanvasGroup>(clone);
        
        Transform contentTransform = superDetailScenarioDropdownRoot.Find("Content");
        if (contentTransform == null && superDetailScenarioDropdownRoot.childCount > 0)
        {
            // If "Content" doesn't exist by name, fallback to the first child or search deeper
            contentTransform = superDetailScenarioDropdownRoot.GetChild(0);
        }
        
        if (contentTransform != null)
        {
            superDetailScenarioDropdownContentRoot = contentTransform as RectTransform;
            superDetailScenarioDropdownTemplateButton = superDetailScenarioDropdownContentRoot.GetComponentInChildren<Button>(true);
        }

        GameObject proxy = new GameObject("SuperDetailScenarioToggleProxy", typeof(RectTransform), typeof(Button));
        RectTransform proxyRect = proxy.GetComponent<RectTransform>();
        proxyRect.SetParent(levelSuperDetailContentRoot.parent, false);
        proxyRect.anchorMin = Vector2.zero;
        proxyRect.anchorMax = Vector2.one;
        proxyRect.sizeDelta = Vector2.zero;
        superDetailScenarioToggleProxyButton = proxy.GetComponent<Button>();
        superDetailScenarioToggleProxyButton.onClick.AddListener(() => SetSuperDetailScenarioDropdownVisible(false));
        proxy.SetActive(false);

        SetSuperDetailScenarioDropdownVisible(false);
    }

    private void ToggleSuperDetailScenarioDropdown()
    {
        if (selectedLevelDefinition == null)
        {
            return;
        }

        if (!HasMultipleConfiguredScenarios(selectedLevelDefinition))
        {
            Debug.Log("[SuperDetail] No multiple scenarios for this level, cannot open dropdown.");
            return;
        }

        EnsureSuperDetailScenarioDropdown();
        BuildSuperDetailScenarioDropdown(selectedLevelDefinition);
        
        bool isCurrentlyOpen = superDetailScenarioDropdownCanvasGroup != null && superDetailScenarioDropdownCanvasGroup.alpha > 0.001f;
        SetSuperDetailScenarioDropdownVisible(!isCurrentlyOpen);
    }

    private void SetSuperDetailScenarioDropdownVisible(bool visible)
    {
        if (superDetailScenarioDropdownRoot == null || superDetailScenarioDropdownCanvasGroup == null) return;

        superDetailScenarioDropdownRoot.gameObject.SetActive(true);
        if (visible)
        {
            superDetailScenarioDropdownRoot.SetAsLastSibling();
            PositionSuperDetailScenarioDropdown();
        }

        superDetailScenarioDropdownCanvasGroup.alpha = visible ? 1f : 0f;
        superDetailScenarioDropdownCanvasGroup.interactable = visible;
        superDetailScenarioDropdownCanvasGroup.blocksRaycasts = visible;

        if (superDetailScenarioToggleProxyButton != null)
        {
            if (visible)
            {
                superDetailScenarioToggleProxyButton.transform.SetAsLastSibling();
                superDetailScenarioDropdownRoot.SetAsLastSibling();
            }
            superDetailScenarioToggleProxyButton.gameObject.SetActive(visible);
        }

        UpdateSuperDetailScenarioButtonState(visible);
    }

    private void UpdateSuperDetailScenarioButtonState(bool dropdownVisible)
    {
        if (superDetailBtnScenario == null)
        {
            return;
        }

        RectTransform rect = superDetailBtnScenario.transform as RectTransform;
        Image image = superDetailBtnScenario.targetGraphic as Image;

        superDetailScenarioButtonPulseTween?.Kill();
        superDetailScenarioButtonPulseTween = null;

        if (dropdownVisible)
        {
            if (image != null)
            {
                image.DOColor(new Color32(0x37, 0xB4, 0xFF, 0xFF), 0.12f);
            }

            if (rect != null)
            {
                rect.DOKill();
                rect.localScale = Vector3.one;
                superDetailScenarioButtonPulseTween = rect
                    .DOScale(1.055f, 0.55f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);
            }
        }
        else
        {
            if (image != null)
            {
                image.DOColor(new Color32(0x1F, 0x6F, 0xB8, 0xFF), 0.14f);
            }

            if (rect != null)
            {
                rect.DOKill();
                rect.DOScale(1f, 0.12f).SetEase(Ease.OutCubic);
            }
        }
    }

    private void PositionSuperDetailScenarioDropdown()
    {
        if (superDetailScenarioDropdownRoot == null || superDetailBtnScenario == null) return;
        
        // Make the dropdown bottom-center anchored so it grows upwards
        superDetailScenarioDropdownRoot.anchorMin = new Vector2(0.5f, 0f);
        superDetailScenarioDropdownRoot.anchorMax = new Vector2(0.5f, 0f);
        superDetailScenarioDropdownRoot.pivot = new Vector2(0.5f, 0f);
        
        // Align pivot (bottom-center) exactly to the button's position (center)
        superDetailScenarioDropdownRoot.position = superDetailBtnScenario.transform.position;
        
        // Offset UP by half the button's height plus a small margin
        RectTransform btnRect = superDetailBtnScenario.transform as RectTransform;
        float yOffset = btnRect != null ? (btnRect.rect.height * 0.5f) + 10f : 40f;
        superDetailScenarioDropdownRoot.anchoredPosition += new Vector2(0, yOffset);
    }

    private void BuildSuperDetailScenarioDropdown(LevelDefinition definition)
    {
        ScenarioDefinition[] scenarios = GetConfiguredScenarios(definition);

        if (superDetailScenarioDropdownContentRoot == null || superDetailScenarioDropdownTemplateButton == null)
        {
            return;
        }

        for (int i = 0; i < scenarios.Length; i++)
        {
            Button itemButton = GetOrCreateSuperDetailScenarioDropdownItem(i);
            if (itemButton == null) continue;

            TMP_Text label = itemButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = ResolveScenarioDisplayName(scenarios[i], definition);
            }

            ScenarioDefinition capturedScenario = scenarios[i];
            itemButton.onClick.RemoveAllListeners();
            itemButton.onClick.AddListener(() => 
            {
                OnScenarioDropdownItemClicked(definition, capturedScenario);
                SetSuperDetailScenarioDropdownVisible(false);
                RefreshLevelSuperDetail(); 
            });
            itemButton.gameObject.SetActive(true);
            ApplyScenarioDropdownItemVisual(itemButton, capturedScenario);
        }

        for (int i = scenarios.Length; i < superDetailScenarioDropdownContentRoot.childCount; i++)
        {
            Transform extra = superDetailScenarioDropdownContentRoot.GetChild(i);
            if (extra != null)
            {
                extra.gameObject.SetActive(false);
            }
        }
    }

    private Button GetOrCreateSuperDetailScenarioDropdownItem(int index)
    {
        if (index == 0)
        {
            return superDetailScenarioDropdownTemplateButton;
        }

        if (index < superDetailScenarioDropdownContentRoot.childCount)
        {
            Transform child = superDetailScenarioDropdownContentRoot.GetChild(index);
            Button btn = child.GetComponent<Button>();
            if (btn != null)
            {
                return btn;
            }
        }

        GameObject newItem = UnityEngine.Object.Instantiate(superDetailScenarioDropdownTemplateButton.gameObject, superDetailScenarioDropdownContentRoot);
        return newItem.GetComponent<Button>();
    }
}
