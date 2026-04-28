using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class LevelSelectSceneController
{
    private Button recordsButton;
    private TMP_Text recordsButtonText;
    private RectTransform recordsPanelRoot;
    private CanvasGroup recordsPanelCanvasGroup;
    private TMP_Text recordsTitleText;
    private ScrollRect recordsListScrollRect;
    private RectTransform recordsListContentRoot;
    private Button recordsListItemTemplate;
    private ScrollRect recordsDetailScrollRect;
    private RectTransform recordsDetailContentRoot;
    private TMP_Text recordsDetailText;
    private Button recordsCloseButton;
    private Button recordsDeleteButton;
    private TMP_Text recordsDeleteButtonText;
    private readonly List<Button> recordsListButtons = new List<Button>();
    private readonly Dictionary<Button, Color> recordsListButtonBaseColors = new Dictionary<Button, Color>();
    private readonly Dictionary<Button, Vector3> recordsListButtonBaseScales = new Dictionary<Button, Vector3>();
    private readonly HashSet<Button> configuredRecordListButtons = new HashSet<Button>();
    private readonly HashSet<Button> hoveredRecordListButtons = new HashSet<Button>();
    private List<PlayerCompletionRecord> visibleRecords = new List<PlayerCompletionRecord>();
    private PlayerCompletionRecord selectedRecord;
    private Coroutine recordsPanelTweenCoroutine;
    private Coroutine recordSelectionPulseCoroutine;
    private Vector3 recordsPanelDefaultScale = Vector3.one;

    private void EnsureRecordsRuntimeUi()
    {
        RectTransform canvasRect = GetCanvasRect();
        if (canvasRect == null)
        {
            return;
        }

        EnsureRecordsButton(canvasRect);
        EnsureRecordsPanel(canvasRect);
        RefreshRecordsRuntimeUi();
        HideRecordsPanelImmediate();
    }

    private void EnsureRecordsButton(RectTransform canvasRect)
    {
        if (recordsButton != null)
        {
            return;
        }

        Transform buttonTransform =
            FindDeepChild(canvasRect, "btnRecords")
            ?? FindDeepChild(canvasRect, "RecordsButton")
            ?? FindDeepChild(canvasRect, "ButtonRecords");

        recordsButton = buttonTransform != null
            ? buttonTransform.GetComponent<Button>() ?? buttonTransform.GetComponentInChildren<Button>(true)
            : null;
        recordsButton ??= FindButtonByLabel(canvasRect, "Records");

        if (recordsButton == null)
        {
            return;
        }

        recordsButton.onClick.RemoveListener(OpenRecordsPanel);
        recordsButton.onClick.AddListener(OpenRecordsPanel);
        recordsButtonText = recordsButton.GetComponentInChildren<TMP_Text>(true);
    }

    private void EnsureRecordsPanel(RectTransform canvasRect)
    {
        if (recordsPanelRoot != null)
        {
            return;
        }

        recordsPanelRoot =
            FindDeepChild(canvasRect, "RecordsPanelCustom") as RectTransform
            ?? FindDeepChild(canvasRect, "RecordsPanel") as RectTransform;
        if (recordsPanelRoot == null)
        {
            return;
        }

        recordsPanelCanvasGroup = recordsPanelRoot.GetComponent<CanvasGroup>();
        recordsPanelDefaultScale = recordsPanelRoot.localScale;
        recordsTitleText = FindNestedText(recordsPanelRoot, "Title");

        recordsCloseButton = FindNestedButton(recordsPanelRoot, "CloseButton");
        if (recordsCloseButton != null)
        {
            recordsCloseButton.onClick.RemoveListener(CloseRecordsPanel);
            recordsCloseButton.onClick.AddListener(CloseRecordsPanel);
        }

        Transform listPanel = FindDeepChild(recordsPanelRoot, "RecordsListPanel");
        recordsListScrollRect = ResolveScrollRectWithContent(listPanel);
        recordsListContentRoot = ResolveRecordsListContentRoot(listPanel);
        recordsListItemTemplate = recordsListContentRoot != null
            ? recordsListContentRoot.GetComponentInChildren<Button>(true)
            : null;
        if (recordsListItemTemplate != null && !recordsListButtons.Contains(recordsListItemTemplate))
        {
            recordsListButtons.Add(recordsListItemTemplate);
        }

        recordsDetailText = FindNestedText(recordsPanelRoot, "DetailText");
        recordsDetailScrollRect = ResolveParentScrollRectWithContent(recordsDetailText);
        recordsDetailContentRoot = recordsDetailScrollRect != null
            ? recordsDetailScrollRect.content
            : recordsDetailText != null ? recordsDetailText.rectTransform.parent as RectTransform : null;
        if (recordsDetailText != null)
        {
            recordsDetailText.textWrappingMode = TextWrappingModes.Normal;
            recordsDetailText.overflowMode = TextOverflowModes.Truncate;
        }

        recordsDeleteButton = FindNestedButton(recordsPanelRoot, "DeleteRecordButton");
        recordsDeleteButtonText = recordsDeleteButton != null
            ? recordsDeleteButton.GetComponentInChildren<TMP_Text>(true)
            : null;
        if (recordsDeleteButton != null)
        {
            recordsDeleteButton.onClick.RemoveListener(DeleteSelectedRecord);
            recordsDeleteButton.onClick.AddListener(DeleteSelectedRecord);
        }
    }

    private static RectTransform ResolveRecordsListContentRoot(Transform listPanel)
    {
        if (listPanel == null)
        {
            return null;
        }

        ScrollRect[] scrollRects = listPanel.GetComponentsInChildren<ScrollRect>(true);
        for (int i = 0; i < scrollRects.Length; i++)
        {
            ScrollRect scrollRect = scrollRects[i];
            if (scrollRect != null && scrollRect.content != null)
            {
                return scrollRect.content;
            }
        }

        return FindDeepChild(listPanel, "Content") as RectTransform;
    }

    private static ScrollRect ResolveScrollRectWithContent(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        ScrollRect[] scrollRects = root.GetComponentsInChildren<ScrollRect>(true);
        for (int i = 0; i < scrollRects.Length; i++)
        {
            ScrollRect scrollRect = scrollRects[i];
            if (scrollRect != null && scrollRect.content != null)
            {
                return scrollRect;
            }
        }

        return null;
    }

    private static ScrollRect ResolveParentScrollRectWithContent(Component component)
    {
        if (component == null)
        {
            return null;
        }

        ScrollRect[] scrollRects = component.GetComponentsInParent<ScrollRect>(true);
        for (int i = 0; i < scrollRects.Length; i++)
        {
            ScrollRect scrollRect = scrollRects[i];
            if (scrollRect != null && scrollRect.content != null)
            {
                return scrollRect;
            }
        }

        return null;
    }

    private void RefreshRecordsRuntimeUi()
    {
        if (recordsButtonText != null)
        {
            recordsButtonText.text = LanguageManager.Tr("levelselect.btn.records", "Records");
            ApplyLanguageFont(recordsButtonText, LanguageFontRole.Default);
        }

        if (recordsTitleText != null)
        {
            recordsTitleText.text = LanguageManager.Tr("records.title", "Completion Records");
            ApplyLanguageFont(recordsTitleText, LanguageFontRole.Default);
        }

        if (recordsCloseButton != null)
        {
            SetRecordsButtonLabel(recordsCloseButton, LanguageManager.Tr("common.btn.close", "Close"));
        }

        if (recordsDeleteButtonText != null)
        {
            recordsDeleteButtonText.text = LanguageManager.Tr("records.btn.delete", "Delete Record");
            ApplyLanguageFont(recordsDeleteButtonText, LanguageFontRole.Default);
        }

        if (IsRecordsPanelOpen())
        {
            RefreshRecordsList();
        }
    }

    private void OpenRecordsPanel()
    {
        EnsureRecordsRuntimeUi();
        CloseLevelInfo();
        CloseSubMenu();

        if (recordsPanelRoot == null)
        {
            return;
        }

        if (recordsPanelCanvasGroup == null)
        {
            recordsPanelRoot.gameObject.SetActive(true);
            RefreshRecordsList();
            RefreshRecordsLayouts(resetScroll: true);
            return;
        }

        recordsPanelRoot.gameObject.SetActive(true);
        recordsPanelRoot.SetAsLastSibling();
        RefreshRecordsList();
        PlayRecordsPanelTween(show: true);
    }

    private void CloseRecordsPanel()
    {
        if (recordsPanelRoot == null)
        {
            return;
        }

        if (recordsPanelCanvasGroup == null)
        {
            recordsPanelRoot.gameObject.SetActive(false);
            return;
        }

        PlayRecordsPanelTween(show: false);
    }

    private void HideRecordsPanelImmediate()
    {
        if (recordsPanelRoot == null)
        {
            return;
        }

        if (recordsPanelTweenCoroutine != null)
        {
            StopCoroutine(recordsPanelTweenCoroutine);
            recordsPanelTweenCoroutine = null;
        }

        recordsPanelRoot.localScale = recordsPanelDefaultScale;
        if (recordsPanelCanvasGroup != null)
        {
            recordsPanelCanvasGroup.alpha = 0f;
            recordsPanelCanvasGroup.interactable = false;
            recordsPanelCanvasGroup.blocksRaycasts = false;
            return;
        }

        recordsPanelRoot.gameObject.SetActive(false);
    }

    private void PlayRecordsPanelTween(bool show)
    {
        if (recordsPanelCanvasGroup == null || recordsPanelRoot == null)
        {
            return;
        }

        if (recordsPanelTweenCoroutine != null)
        {
            StopCoroutine(recordsPanelTweenCoroutine);
        }

        recordsPanelTweenCoroutine = StartCoroutine(PlayRecordsPanelTweenRoutine(show));
    }

    private IEnumerator PlayRecordsPanelTweenRoutine(bool show)
    {
        recordsPanelRoot.gameObject.SetActive(true);
        recordsPanelCanvasGroup.interactable = false;
        recordsPanelCanvasGroup.blocksRaycasts = false;

        float duration = show ? 0.16f : 0.11f;
        float startAlpha = recordsPanelCanvasGroup.alpha;
        float endAlpha = show ? 1f : 0f;
        Vector3 hiddenScale = recordsPanelDefaultScale * 0.965f;
        Vector3 startScale = recordsPanelRoot.localScale;
        Vector3 endScale = show ? recordsPanelDefaultScale : hiddenScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float eased = show ? EaseOutBack(t) : EaseInQuad(t);
            recordsPanelCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
            recordsPanelRoot.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        recordsPanelCanvasGroup.alpha = endAlpha;
        recordsPanelRoot.localScale = endScale;
        recordsPanelCanvasGroup.interactable = show;
        recordsPanelCanvasGroup.blocksRaycasts = show;
        recordsPanelTweenCoroutine = null;
    }

    private bool IsRecordsPanelOpen()
    {
        if (recordsPanelRoot == null)
        {
            return false;
        }

        return recordsPanelCanvasGroup != null
            ? recordsPanelCanvasGroup.alpha > 0.001f
            : recordsPanelRoot.gameObject.activeSelf;
    }

    private void RefreshRecordsList()
    {
        string playerName = LoadingFlowState.GetPlayerName();
        visibleRecords = PlayerCompletionRecordStore.GetRecords(playerName);

        EnsureRecordButtonCapacity(visibleRecords.Count);
        for (int i = 0; i < recordsListButtons.Count; i++)
        {
            Button button = recordsListButtons[i];
            ConfigureRecordListButton(button);
            bool visible = i < visibleRecords.Count;
            button.gameObject.SetActive(visible);
            if (!visible)
            {
                hoveredRecordListButtons.Remove(button);
                ApplyRecordItemVisualState(button, animateColor: false);
                continue;
            }

            PlayerCompletionRecord record = visibleRecords[i];
            SetRecordsButtonLabel(button, BuildRecordListItemText(record));
            int recordIndex = i;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectRecord(recordIndex));
            button.onClick.AddListener(() => PlayRecordSelectionPulse(button.transform));
        }

        if (visibleRecords.Count == 0)
        {
            selectedRecord = null;
            SetText(recordsDetailText, string.IsNullOrWhiteSpace(playerName)
                ? LanguageManager.Tr("records.empty.no_player", "No player profile selected.")
                : LanguageManager.Tr("records.empty", "No completion records saved yet."));
            SetDeleteRecordButtonEnabled(false);
            RefreshRecordItemVisualStates();
            RefreshRecordsLayouts(resetScroll: true);
            return;
        }

        if (selectedRecord == null || !ContainsRecord(visibleRecords, selectedRecord.recordId))
        {
            selectedRecord = visibleRecords[0];
        }

        SelectRecordById(selectedRecord.recordId);
        RefreshRecordsLayouts(resetScroll: true);
    }

    private void EnsureRecordButtonCapacity(int count)
    {
        if (recordsListContentRoot == null || recordsListItemTemplate == null)
        {
            return;
        }

        while (recordsListButtons.Count < count)
        {
            GameObject itemObject = Instantiate(recordsListItemTemplate.gameObject, recordsListContentRoot);
            itemObject.name = $"RecordItem_{recordsListButtons.Count:00}";
            EventTrigger clonedTrigger = itemObject.GetComponent<EventTrigger>();
            if (clonedTrigger != null && clonedTrigger.triggers != null)
            {
                clonedTrigger.triggers.Clear();
            }

            Button button = itemObject.GetComponent<Button>() ?? itemObject.GetComponentInChildren<Button>(true);
            if (button == null)
            {
                itemObject.SetActive(false);
                return;
            }

            Image background = GetRecordButtonBackground(button);
            if (
                background != null
                && recordsListButtonBaseColors.TryGetValue(recordsListItemTemplate, out Color templateBaseColor)
            )
            {
                background.color = templateBaseColor;
            }

            ConfigureRecordListButton(button);
            recordsListButtons.Add(button);
        }
    }

    private void SelectRecord(int index)
    {
        if (index < 0 || index >= visibleRecords.Count)
        {
            return;
        }

        selectedRecord = visibleRecords[index];
        SelectRecordById(selectedRecord.recordId);
    }

    private void SelectRecordById(string recordId)
    {
        selectedRecord = null;
        for (int i = 0; i < visibleRecords.Count; i++)
        {
            PlayerCompletionRecord record = visibleRecords[i];
            if (record != null && string.Equals(record.recordId, recordId, StringComparison.OrdinalIgnoreCase))
            {
                selectedRecord = record;
                break;
            }
        }

        SetText(recordsDetailText, BuildRecordDetailText(selectedRecord));
        SetDeleteRecordButtonEnabled(selectedRecord != null);
        RefreshRecordItemVisualStates();
        RefreshRecordsLayouts(resetScroll: false);
        if (recordsDetailScrollRect != null && recordsDetailScrollRect.content != null)
        {
            recordsDetailScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void DeleteSelectedRecord()
    {
        if (selectedRecord == null)
        {
            return;
        }

        string playerName = LoadingFlowState.GetPlayerName();
        PlayerCompletionRecordStore.DeleteRecord(playerName, selectedRecord.recordId);
        selectedRecord = null;
        RefreshRecordsList();
    }

    private void SetDeleteRecordButtonEnabled(bool enabled)
    {
        if (recordsDeleteButton != null)
        {
            recordsDeleteButton.interactable = enabled;
        }
    }

    private void ConfigureRecordListButton(Button button)
    {
        if (button == null || configuredRecordListButtons.Contains(button))
        {
            return;
        }

        Image background = GetRecordButtonBackground(button);
        if (background != null && !recordsListButtonBaseColors.ContainsKey(button))
        {
            recordsListButtonBaseColors[button] = background.color;
        }

        if (!recordsListButtonBaseScales.ContainsKey(button))
        {
            recordsListButtonBaseScales[button] = button.transform.localScale;
        }

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = button.gameObject.AddComponent<EventTrigger>();
        }

        AddEventTriggerListener(trigger, EventTriggerType.PointerEnter, _ =>
        {
            hoveredRecordListButtons.Add(button);
            ApplyRecordItemVisualState(button, animateColor: true);
        });
        AddEventTriggerListener(trigger, EventTriggerType.PointerExit, _ =>
        {
            hoveredRecordListButtons.Remove(button);
            ApplyRecordItemVisualState(button, animateColor: true);
        });

        configuredRecordListButtons.Add(button);
        ApplyRecordItemVisualState(button, animateColor: false);
    }

    private static void AddEventTriggerListener(
        EventTrigger trigger,
        EventTriggerType eventType,
        UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = eventType
        };
        if (trigger.triggers == null)
        {
            trigger.triggers = new List<EventTrigger.Entry>();
        }

        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    private void RefreshRecordItemVisualStates()
    {
        for (int i = 0; i < recordsListButtons.Count; i++)
        {
            ApplyRecordItemVisualState(recordsListButtons[i], animateColor: true);
        }
    }

    private void ApplyRecordItemVisualState(Button button, bool animateColor)
    {
        if (button == null)
        {
            return;
        }

        bool selected = IsRecordButtonSelected(button);
        bool hovered = hoveredRecordListButtons.Contains(button);
        Image background = GetRecordButtonBackground(button);
        if (background != null)
        {
            Color baseColor = recordsListButtonBaseColors.TryGetValue(button, out Color storedColor)
                ? storedColor
                : background.color;
            Color accent = new Color(0.12f, 0.42f, 0.54f, baseColor.a);
            Color hover = Color.Lerp(baseColor, Color.white, 0.12f);
            Color target = selected ? Color.Lerp(baseColor, accent, 0.82f) : hovered ? hover : baseColor;
            if (animateColor)
            {
                background.CrossFadeColor(target, 0.08f, ignoreTimeScale: true, useAlpha: true);
            }
            else
            {
                background.color = target;
            }
        }

        Vector3 baseScale = recordsListButtonBaseScales.TryGetValue(button, out Vector3 storedScale)
            ? storedScale
            : Vector3.one;
        float scale = selected ? 1.018f : hovered ? 1.008f : 1f;
        button.transform.localScale = baseScale * scale;
    }

    private bool IsRecordButtonSelected(Button button)
    {
        if (button == null || selectedRecord == null)
        {
            return false;
        }

        int index = recordsListButtons.IndexOf(button);
        return index >= 0
            && index < visibleRecords.Count
            && visibleRecords[index] != null
            && string.Equals(
                visibleRecords[index].recordId,
                selectedRecord.recordId,
                StringComparison.OrdinalIgnoreCase);
    }

    private static Image GetRecordButtonBackground(Button button)
    {
        if (button == null)
        {
            return null;
        }

        return button.targetGraphic as Image ?? button.GetComponent<Image>();
    }

    private void PlayRecordSelectionPulse(Transform target)
    {
        if (target == null)
        {
            return;
        }

        if (recordSelectionPulseCoroutine != null)
        {
            StopCoroutine(recordSelectionPulseCoroutine);
        }

        recordSelectionPulseCoroutine = StartCoroutine(PlayRecordSelectionPulseRoutine(target));
    }

    private IEnumerator PlayRecordSelectionPulseRoutine(Transform target)
    {
        Button button = target.GetComponent<Button>();
        Vector3 baseScale = button != null && recordsListButtonBaseScales.TryGetValue(button, out Vector3 storedScale)
            ? storedScale
            : target.localScale;
        float duration = 0.12f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float pulse = Mathf.Sin(t * Mathf.PI) * 0.035f;
            target.localScale = baseScale * (1.018f + pulse);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (button != null)
        {
            ApplyRecordItemVisualState(button, animateColor: false);
        }
        else
        {
            target.localScale = baseScale;
        }

        recordSelectionPulseCoroutine = null;
    }

    private void RefreshRecordsLayouts(bool resetScroll)
    {
        if (recordsDetailText != null)
        {
            recordsDetailText.ForceMeshUpdate(ignoreActiveState: true);
            ForceRebuildLayout(recordsDetailText.rectTransform);
        }

        ForceRebuildLayout(recordsDetailContentRoot);
        ForceRebuildLayout(recordsListContentRoot);
        Canvas.ForceUpdateCanvases();

        ForceRebuildLayout(recordsDetailContentRoot);
        ForceRebuildLayout(recordsListContentRoot);
        Canvas.ForceUpdateCanvases();

        if (!resetScroll)
        {
            return;
        }

        if (recordsListScrollRect != null && recordsListScrollRect.content != null)
        {
            recordsListScrollRect.verticalNormalizedPosition = 1f;
        }

        if (recordsDetailScrollRect != null && recordsDetailScrollRect.content != null)
        {
            recordsDetailScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private static void ForceRebuildLayout(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        if (root.parent is RectTransform parent)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        }
    }

    private static float EaseInQuad(float t)
    {
        return t * t;
    }

    private static float EaseOutBack(float t)
    {
        const float overshoot = 1.35f;
        float p = t - 1f;
        return 1f + p * p * ((overshoot + 1f) * p + overshoot);
    }

    private static bool ContainsRecord(List<PlayerCompletionRecord> records, string recordId)
    {
        if (records == null || string.IsNullOrWhiteSpace(recordId))
        {
            return false;
        }

        for (int i = 0; i < records.Count; i++)
        {
            if (records[i] != null && string.Equals(records[i].recordId, recordId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildRecordListItemText(PlayerCompletionRecord record)
    {
        if (record == null)
        {
            return string.Empty;
        }

        string date = FormatRecordDate(record.savedUtcTicks);
        string level = !string.IsNullOrWhiteSpace(record.levelId) ? record.levelId : record.missionTitle;
        string rank = !string.IsNullOrWhiteSpace(record.onsiteRank) ? record.onsiteRank : "-";
        string score = record.totalMaximumScore > 0 ? $"{record.totalScore}/{record.totalMaximumScore}" : "-";
        string best = record.isBest ? "  Best" : string.Empty;
        return $"{level}\n{date}  |  {score}  |  {rank}{best}";
    }

    private static string BuildRecordDetailText(PlayerCompletionRecord record)
    {
        if (record == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        CallPhaseResultSnapshot callPhase = record.callPhase ?? new CallPhaseResultSnapshot();
        builder.AppendLine(record.missionTitle);
        builder.AppendLine($"{record.levelId}  |  {FormatRecordDate(record.savedUtcTicks)}");
        builder.AppendLine(record.isBest ? "Best record for this level" : string.Empty);
        builder.AppendLine();

        builder.AppendLine("Call Phase");
        builder.AppendLine($"Score: {callPhase.finalScore}/{callPhase.maximumScore}");
        builder.AppendLine($"Call Time: {FormatClock(callPhase.callDurationSeconds)} / Target {FormatClock(callPhase.targetCallTimeSeconds)}");
        builder.AppendLine($"Severity: {callPhase.severityChosenDisplay} / Expected {callPhase.expectedSeverityDisplay}");
        builder.AppendLine($"Follow-up: {callPhase.followUpQualityLabel} ({callPhase.followUpOptimalCount} optimal, {callPhase.followUpAcceptableCount} acceptable, {callPhase.followUpPoorCount} poor)");
        if (callPhase.feedbackLines != null && callPhase.feedbackLines.Count > 0)
        {
            builder.AppendLine("Feedback:");
            for (int i = 0; i < callPhase.feedbackLines.Count; i++)
            {
                builder.AppendLine($"- {callPhase.feedbackLines[i]}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Onsite Phase");
        builder.AppendLine($"Score: {record.onsiteScore}/{record.onsiteMaximumScore}  |  Rank: {(!string.IsNullOrWhiteSpace(record.onsiteRank) ? record.onsiteRank : "-")}");
        builder.AppendLine($"Time: {FormatClock(Mathf.RoundToInt(record.onsiteElapsedSeconds))}");
        builder.AppendLine($"Fires: {record.extinguishedFireCount}/{record.totalTrackedFires}");
        builder.AppendLine($"Rescues: {record.rescuedCount}/{record.totalTrackedRescuables}");
        builder.AppendLine($"Victims: U:{record.urgentVictimCount} C:{record.criticalVictimCount} S:{record.stabilizedVictimCount} X:{record.extractedVictimCount} D:{record.deceasedVictimCount}");

        if (record.objectives != null && record.objectives.Count > 0)
        {
            builder.AppendLine("Objectives:");
            for (int i = 0; i < record.objectives.Count; i++)
            {
                PlayerCompletionObjectiveRecord objective = record.objectives[i];
                if (objective == null)
                {
                    continue;
                }

                string state = objective.hasFailed ? "Failed" : objective.isComplete ? "Done" : "Pending";
                string summary = !string.IsNullOrWhiteSpace(objective.summary) ? objective.summary : objective.title;
                builder.AppendLine($"- [{state}] {summary} ({objective.score}/{objective.maximumScore})");
            }
        }

        builder.AppendLine();
        builder.AppendLine($"Total: {record.totalScore}/{record.totalMaximumScore}");
        return builder.ToString();
    }

    private static string FormatRecordDate(long utcTicks)
    {
        if (utcTicks <= 0)
        {
            return "-";
        }

        DateTime utc = new DateTime(utcTicks, DateTimeKind.Utc);
        DateTime local = utc.ToLocalTime();
        return local.ToString("yyyy-MM-dd HH:mm");
    }

    private static string FormatClock(int totalSeconds)
    {
        int safeSeconds = Mathf.Max(0, totalSeconds);
        int hours = safeSeconds / 3600;
        int minutes = (safeSeconds % 3600) / 60;
        int seconds = safeSeconds % 60;
        return hours > 0 ? $"{hours:00}:{minutes:00}:{seconds:00}" : $"{minutes:00}:{seconds:00}";
    }

    private static void SetRecordsButtonLabel(Button button, string label)
    {
        TMP_Text text = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (text == null)
        {
            return;
        }

        text.text = label ?? string.Empty;
        ApplyLanguageFont(text, LanguageFontRole.Default);
    }
}
