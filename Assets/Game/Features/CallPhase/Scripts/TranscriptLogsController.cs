using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages transcript log entries and UI instantiation for the Call Phase.
/// Supports adding logs, tracking active chunks, and hard-coded testing.
/// </summary>
public class TranscriptLogsController : MonoBehaviour
{
    private const int ScrollToBottomRetryFrames = 3;

    [Header("UI References")]
    [SerializeField] private RectTransform logContentRoot;
    [SerializeField] private TranscriptLogItem logItemPrefab;
    [SerializeField] private ScrollRect transcriptScrollRect;

    [Header("Settings")]
    [SerializeField] private float prototypeLineDelaySeconds = 1f;
    [SerializeField] private bool enableDebugLogs = false;

    [Header("Scenario")]
    [SerializeField] private CallPhaseScenarioData scenarioData;

    private List<TranscriptLogEntry> entries = new List<TranscriptLogEntry>();
    private List<TranscriptLogItem> items = new List<TranscriptLogItem>();
    private Coroutine scrollToBottomCoroutine;
    private Coroutine openingTranscriptCoroutine;

    private void Awake()
    {
        ResolveScenarioData();
    }

    private void Start()
    {
        if (transcriptScrollRect == null)
        {
            transcriptScrollRect = GetComponent<ScrollRect>();
        }
    }

    /// <summary>
    /// Clears all log entries and destroys their UI elements.
    /// </summary>
    public void ClearLogs()
    {
        if (openingTranscriptCoroutine != null)
        {
            StopCoroutine(openingTranscriptCoroutine);
            openingTranscriptCoroutine = null;
        }

        entries.Clear();

        foreach (var item in items)
        {
            if (item != null) Destroy(item.gameObject);
        }
        items.Clear();
        QueueScrollToBottom();

        if (enableDebugLogs) Debug.Log($"{nameof(TranscriptLogsController)}: Logs cleared.");
    }

    public void ResetForScenarioRun()
    {
        scenarioData = null;
        ResolveScenarioData();
        ClearLogs();
    }

    public void BeginScenarioRun()
    {
        scenarioData = null;
        ResolveScenarioData();

        if (openingTranscriptCoroutine != null)
        {
            StopCoroutine(openingTranscriptCoroutine);
        }

        openingTranscriptCoroutine = StartCoroutine(PlayInitialPrototypeTranscriptRoutine());
    }

    /// <summary>
    /// Adds a fully constructed TranscriptLogEntry and creates its UI item.
    /// </summary>
    public void AddLog(TranscriptLogEntry entry)
    {
        if (entry == null) return;

        if (entry.isActiveExtractableChunk)
        {
            ClearPreviousActiveChunk();
        }

        entries.Add(entry);
        CreateAndBindLogItem(entry);
        QueueScrollToBottom();

        if (enableDebugLogs) Debug.Log($"{nameof(TranscriptLogsController)}: Added log for {entry.speaker}: {entry.text}");
    }

    /// <summary>
    /// Convenience method to add an Operator log.
    /// </summary>
    public void AddOperatorLog(string text)
    {
        AddLog(new TranscriptLogEntry 
        { 
            speaker = TranscriptSpeakerType.Operator, 
            text = text, 
            isExtractable = false, 
            isActiveExtractableChunk = false 
        });
    }

    /// <summary>
    /// Convenience method to add a Caller log.
    /// </summary>
    public void AddCallerLog(string text, bool isExtractable = false, bool setAsActiveChunk = false)
    {
        AddLog(new TranscriptLogEntry 
        { 
            speaker = TranscriptSpeakerType.Caller, 
            text = text, 
            isExtractable = isExtractable, 
            isActiveExtractableChunk = setAsActiveChunk 
        });
    }

    /// <summary>
    /// Returns the entry currently marked as the active extractable chunk, if any.
    /// </summary>
    public TranscriptLogEntry GetCurrentActiveCallerEntry()
    {
        return entries.Find(e => e.isActiveExtractableChunk && e.speaker == TranscriptSpeakerType.Caller);
    }

    /// <summary>
    /// Sets the most recent extractable caller log as the active chunk.
    /// </summary>
    public void SetCurrentActiveCallerChunkToLastExtractable()
    {
        ClearPreviousActiveChunk();

        TranscriptLogEntry lastExtractable = entries.FindLast(e => e.isExtractable && e.speaker == TranscriptSpeakerType.Caller);
        if (lastExtractable != null)
        {
            lastExtractable.isActiveExtractableChunk = true;
            RebindAllItems();
            QueueScrollToBottom();
            
            if (enableDebugLogs) Debug.Log($"{nameof(TranscriptLogsController)}: Active caller chunk updated to: '{lastExtractable.text}'");
        }
    }

    private void ClearPreviousActiveChunk()
    {
        bool changed = false;
        foreach (var entry in entries)
        {
            if (entry.isActiveExtractableChunk)
            {
                entry.isActiveExtractableChunk = false;
                changed = true;
            }
        }

        if (changed)
        {
            RebindAllItems();
            QueueScrollToBottom();
        }
    }

    private void CreateAndBindLogItem(TranscriptLogEntry entry)
    {
        if (logContentRoot == null || logItemPrefab == null)
        {
            if (enableDebugLogs) Debug.LogWarning($"{nameof(TranscriptLogsController)}: Cannot create UI item. Root or Prefab is missing.");
            return;
        }

        TranscriptLogItem item = Instantiate(logItemPrefab, logContentRoot);
        item.Bind(entry);
        items.Add(item);
    }

    private void RebindAllItems()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (i < items.Count && items[i] != null)
            {
                items[i].Bind(entries[i]);
            }
        }
    }

    private void QueueScrollToBottom()
    {
        if (transcriptScrollRect == null || logContentRoot == null)
        {
            return;
        }

        if (scrollToBottomCoroutine != null)
        {
            StopCoroutine(scrollToBottomCoroutine);
        }

        scrollToBottomCoroutine = StartCoroutine(ScrollToBottomAtEndOfFrame());
    }

    private System.Collections.IEnumerator ScrollToBottomAtEndOfFrame()
    {
        for (int i = 0; i < ScrollToBottomRetryFrames; i++)
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            ApplyScrollToBottom();
        }

        scrollToBottomCoroutine = null;
    }

    private void ApplyScrollToBottom()
    {
        if (transcriptScrollRect == null || logContentRoot == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(logContentRoot);

        if (transcriptScrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(transcriptScrollRect.content);
        }

        transcriptScrollRect.StopMovement();
        transcriptScrollRect.normalizedPosition = Vector2.zero;
        transcriptScrollRect.verticalNormalizedPosition = 0f;
    }

    private System.Collections.IEnumerator PlayInitialPrototypeTranscriptRoutine()
    {
        if (scenarioData != null && scenarioData.initialTranscriptLines != null && scenarioData.initialTranscriptLines.Count > 0)
        {
            for (int i = 0; i < scenarioData.initialTranscriptLines.Count; i++)
            {
                AddScenarioLine(scenarioData.initialTranscriptLines[i]);

                if (i < scenarioData.initialTranscriptLines.Count - 1)
                {
                    yield return WaitForPrototypeLineDelay();
                }
            }

            openingTranscriptCoroutine = null;
            yield break;
        }

        AddOperatorLog(LanguageManager.Tr(
            "callphase.scenario.kitchen_fire_house_call.line.intro_operator_open",
            "911, where is your emergency?"));
        yield return WaitForPrototypeLineDelay();

        AddCallerLog(LanguageManager.Tr(
            "callphase.scenario.kitchen_fire_house_call.line.intro_caller_open",
            "My house is on fire! Please help!"));
        yield return WaitForPrototypeLineDelay();

        AddOperatorLog(LanguageManager.Tr(
            "callphase.scenario.kitchen_fire_house_call.line.intro_operator_fire_location",
            "Where is the fire right now?"));
        yield return WaitForPrototypeLineDelay();

        AddCallerLog(
            LanguageManager.Tr(
                "callphase.scenario.kitchen_fire_house_call.line.intro_caller_fire_location",
                "It's in the kitchen, there's smoke everywhere!"),
            isExtractable: true,
            setAsActiveChunk: true);
        openingTranscriptCoroutine = null;
    }

    private CustomYieldInstruction WaitForPrototypeLineDelay()
    {
        return new WaitForSecondsRealtime(CallPhaseResponseSpeedSettings.ApplyDelayPreference(prototypeLineDelaySeconds));
    }

    private void ResolveScenarioData()
    {
        if (scenarioData == null)
        {
            scenarioData = CallPhaseScenarioContext.ResolveFrom(this);
        }

        if (scenarioData == null)
        {
            scenarioData = Resources.Load<CallPhaseScenarioData>(CallPhaseScenarioData.DefaultScenarioResourcePath);
        }
    }

    private void AddScenarioLine(CallPhaseScenarioLineData lineData)
    {
        if (lineData == null)
        {
            return;
        }

        string localizedText = scenarioData != null
            ? scenarioData.GetLocalizedLineText(lineData)
            : lineData.text;
        if (string.IsNullOrWhiteSpace(localizedText))
        {
            return;
        }

        if (lineData.speaker == TranscriptSpeakerType.Operator)
        {
            AddOperatorLog(localizedText);
            return;
        }

        AddCallerLog(
            localizedText,
            lineData.isExtractable,
            lineData.startsAsActiveChunk);
    }
}
