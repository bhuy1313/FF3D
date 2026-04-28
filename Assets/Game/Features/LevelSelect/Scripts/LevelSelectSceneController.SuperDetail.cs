using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
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

    private void RefreshLevelSuperDetail()
    {
        if (levelSuperDetailContentRoot == null || selectedLevelDefinition == null)
        {
            return;
        }

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

    private void AnimateSuperDetailShow()
    {
        // Chỉ chạy hiệu ứng trượt nội dung (Slide) khi refresh data, 
        // TRÁNH đụng chạm đến CanvasGroup alpha vì đã được script LevelInfoAnimation quản lý.
        if (levelSuperDetailContentRoot != null)
        {
            levelSuperDetailContentRoot.DOKill();
            levelSuperDetailContentRoot.anchoredPosition = new Vector2(0f, -40f);
            levelSuperDetailContentRoot.DOAnchorPosY(0f, 0.5f).SetEase(Ease.OutBack);
        }
    }

    private void HideLevelSuperDetailImmediate()
    {
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
    }

    private void RefreshSuperDetailMapPreview(RegionCard card, string title)
    {
        if (levelSuperDetailMapImage == null)
        {
            return;
        }

        Sprite sprite = card != null ? card.mapSprite : null;
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

        LayoutRebuilder.ForceRebuildLayoutImmediate(levelSuperDetailContentRoot);
    }
}