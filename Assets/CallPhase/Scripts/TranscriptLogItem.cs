using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Represents a single transcript row in the UI.
/// Handles visual state for the operator, caller, and active extractable chunks.
/// </summary>
public class TranscriptLogItem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private GameObject operatorRoot;
    [SerializeField] private GameObject callerRoot;
    [SerializeField] private GameObject activeHighlightRoot;

    [Header("Icons")]
    [SerializeField] private GameObject operatorIcon;
    [SerializeField] private GameObject callerIcon;

    [Header("Settings")]
    [SerializeField] private bool enableDebugLogs = false;

    private static readonly Color OperatorBubbleColor = new Color(0.14f, 0.20f, 0.28f, 0.96f);
    private static readonly Color CallerBubbleColor = new Color(0.18f, 0.24f, 0.33f, 0.98f);
    private static readonly Color OperatorTextColor = new Color(0.84f, 0.90f, 0.96f, 1f);
    private static readonly Color CallerTextColor = new Color(0.93f, 0.95f, 0.98f, 1f);
    private static readonly Color HighlightColor = new Color(0.85f, 0.67f, 0.20f, 0.20f);
    private const string InlineCandidateColorHex = "#EDF4FB";
    private const string InlineHoverColorHex = "#F7E3AA";
    private const string InlineSelectedColorHex = "#FFFFFF";
    private const string InlineExactMarkHex = "#D9AA3330";
    private const string InlineExtraMarkHex = "#C67F1C38";
    private const string InlineExtraCandidateColorHex = "#F2D6A6";

    private RectTransform rootRect;
    private RectTransform messageRect;
    private RectTransform operatorRect;
    private RectTransform callerRect;
    private RectTransform highlightRect;
    private RectTransform spanRect;

    private Image operatorImage;
    private Image callerImage;
    private Image highlightImage;
    private VerticalLayoutGroup rootLayoutGroup;
    private GameObject spanContainer;
    private SelectableSpan[] selectableSpans;
    private TranscriptLogEntry boundEntry;
    private TranscriptStateController stateController;
    private IncidentReportController incidentReportController;
    private CallPhaseScenarioData scenarioData;
    private TranscriptInlineSpanText inlineSpanText;
    private int hoveredInlineSpanIndex = -1;
    private string lastInlineRenderState = string.Empty;

    private struct InlineSpanBinding
    {
        public int displayOrder;
        public int startIndex;
        public int length;
        public SelectableSpan span;
    }

    private void Awake()
    {
        CacheRefs();
        CacheStateController();
        CacheIncidentReportController();
        CacheScenarioData();
        ApplySharedStyle();
    }

    private void OnEnable()
    {
        CacheStateController();
        CacheIncidentReportController();
        CacheScenarioData();
        SubscribeToStateController();
        RefreshBoundEntryVisuals();
    }

    private void OnDisable()
    {
        UnsubscribeFromStateController();
    }

    private void OnDestroy()
    {
        UnsubscribeFromStateController();
    }

    private void Update()
    {
        RefreshInlineMessageVisualsIfNeeded();
    }

    public void Bind(TranscriptLogEntry entry)
    {
        CacheRefs();
        CacheStateController();
        CacheIncidentReportController();
        CacheScenarioData();
        ApplySharedStyle();
        boundEntry = entry;
        hoveredInlineSpanIndex = -1;
        lastInlineRenderState = string.Empty;

        if (entry == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"{nameof(TranscriptLogItem)}: Bind called with null entry.", this);
            }
            return;
        }

        RefreshBoundEntryVisuals();

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(TranscriptLogItem)}: Bound entry '{entry.text}' as {entry.speaker}.", this);
        }
    }

    public void HandleInlineSpanClicked(int linkIndex)
    {
        SelectableSpan span = GetInlineSpanByLinkIndex(linkIndex);
        if (span == null)
        {
            return;
        }

        SendMessageUpwards("OnSelectableSpanClicked", span, SendMessageOptions.DontRequireReceiver);
        RefreshInlineMessageVisualsIfNeeded(force: true);
    }

    public void HandleInlineSpanHovered(int linkIndex)
    {
        if (hoveredInlineSpanIndex == linkIndex)
        {
            return;
        }

        hoveredInlineSpanIndex = linkIndex;
        RefreshInlineMessageVisualsIfNeeded(force: true);
    }

    private void RefreshBoundEntryVisuals()
    {
        if (boundEntry == null)
        {
            return;
        }

        bool isCaller = boundEntry.speaker == TranscriptSpeakerType.Caller;
        bool isActiveChunk = isCaller && boundEntry.isActiveExtractableChunk;
        bool showExtractUi = isActiveChunk && IsExtractModeActive();

        if (!showExtractUi)
        {
            hoveredInlineSpanIndex = -1;
        }

        if (operatorIcon != null) operatorIcon.SetActive(!isCaller);
        if (callerIcon != null) callerIcon.SetActive(isCaller);

        ConfigurePrototypeSpans(isActiveChunk);

        if (operatorRoot != null)
        {
            operatorRoot.SetActive(!isCaller);
        }

        if (callerRoot != null)
        {
            callerRoot.SetActive(isCaller);
        }

        if (spanContainer != null)
        {
            // Hidden runtime span views remain as backing data only.
            spanContainer.SetActive(false);
        }

        if (activeHighlightRoot != null)
        {
            activeHighlightRoot.SetActive(showExtractUi);
        }

        ApplyEntryStyle(isCaller, showExtractUi);
        ApplyInlineMessageVisuals(isCaller, showExtractUi);
    }

    private void CacheStateController()
    {
        if (stateController == null)
        {
            stateController = GetComponentInParent<TranscriptStateController>();
        }
    }

    private void CacheIncidentReportController()
    {
        if (incidentReportController == null)
        {
            incidentReportController = GetComponentInParent<IncidentReportController>();
        }
    }

    private void CacheScenarioData()
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

    private void SubscribeToStateController()
    {
        if (stateController == null)
        {
            return;
        }

        stateController.StateChanged -= HandleTranscriptStateChanged;
        stateController.StateChanged += HandleTranscriptStateChanged;
    }

    private void UnsubscribeFromStateController()
    {
        if (stateController != null)
        {
            stateController.StateChanged -= HandleTranscriptStateChanged;
        }
    }

    private void HandleTranscriptStateChanged(TranscriptPanelState state)
    {
        RefreshBoundEntryVisuals();
    }

    private bool IsExtractModeActive()
    {
        return stateController != null && stateController.CurrentState == TranscriptPanelState.ExtractMode;
    }

    private void ConfigurePrototypeSpans(bool isActiveChunk)
    {
        if (selectableSpans == null || selectableSpans.Length == 0)
        {
            return;
        }

        for (int i = 0; i < selectableSpans.Length; i++)
        {
            if (selectableSpans[i] == null)
            {
                continue;
            }

            selectableSpans[i].SetSelected(false);
            selectableSpans[i].gameObject.SetActive(false);
        }

        if (!isActiveChunk || boundEntry == null || boundEntry.speaker != TranscriptSpeakerType.Caller)
        {
            return;
        }

        string callerText = boundEntry.text ?? string.Empty;

        if (TryBindScenarioSpans(callerText))
        {
            return;
        }

        // Legacy fallback for confirmation replies if a scenario asset omits explicit spans.
        if (TryBindConfirmationSpan(callerText))
        {
            return;
        }

        if (callerText.IndexOf("gas cylinder", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            BindSpan(0, "gas cylinder near the kitchen", "Gas cylinder near kitchen", "Hazard", "hazard", exact: true, extra: false);
            return;
        }

        if (callerText.IndexOf("dining area", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            BindSpan(0, "spreading toward the dining area", "Spreading toward dining area", "SpreadStatus", "SpreadStatus", exact: true, extra: false);
            return;
        }

        if (callerText.IndexOf("outside the house", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            BindSpan(0, "outside the house", "Outside house", "CallerSafety", "CallerSafety", exact: true, extra: false);
            return;
        }

        if (callerText.IndexOf("kitchen", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            BindSpan(0, "kitchen", "Kitchen", "FireLocation", "fire_location", exact: true, extra: false);
            BindSpan(1, "smoke everywhere", "Smoke Everywhere", "Hazard", "hazard", exact: true, extra: true);
            return;
        }

        if (callerText.IndexOf("27 Maple Street", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            BindSpan(0, "27 Maple Street", "27 Maple Street", "Address", "Address", exact: true, extra: false);
            return;
        }

        if (callerText.IndexOf("child is upstairs", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            BindSpan(0, "child is upstairs", "Child trapped upstairs", "OccupantRisk", "OccupantRisk", exact: true, extra: false);
            return;
        }
    }

    private bool TryBindConfirmationSpan(string callerText)
    {
        if (incidentReportController == null || !incidentReportController.HasActiveConfirmationContext)
        {
            return false;
        }

        string trimmedText = callerText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedText))
        {
            return false;
        }

        bool looksLikeConfirmation = trimmedText.IndexOf("yes", System.StringComparison.OrdinalIgnoreCase) >= 0
            || trimmedText.IndexOf("correct", System.StringComparison.OrdinalIgnoreCase) >= 0
            || trimmedText.IndexOf("that's right", System.StringComparison.OrdinalIgnoreCase) >= 0
            || trimmedText.IndexOf("outside", System.StringComparison.OrdinalIgnoreCase) >= 0;

        if (!looksLikeConfirmation)
        {
            return false;
        }

        BindSpan(
            0,
            trimmedText,
            incidentReportController.ExpectedConfirmedValue,
            incidentReportController.CurrentConfirmationFieldId,
            incidentReportController.CurrentConfirmationFieldId,
            exact: true,
            extra: false);
        return true;
    }

    private bool TryBindScenarioSpans(string callerText)
    {
        if (scenarioData == null
            || selectableSpans == null
            || selectableSpans.Length == 0
            || !scenarioData.TryGetCallerLineDefinition(callerText, out CallPhaseScenarioLineData lineData)
            || lineData == null
            || lineData.extractableSpans == null
            || lineData.extractableSpans.Count == 0)
        {
            return false;
        }

        int spanCount = Mathf.Min(selectableSpans.Length, lineData.extractableSpans.Count);
        bool boundAny = false;

        for (int i = 0; i < spanCount; i++)
        {
            CallPhaseExtractableSpanData spanData = lineData.extractableSpans[i];
            if (string.IsNullOrWhiteSpace(spanData.displayText))
            {
                continue;
            }

            BindSpan(
                i,
                spanData.displayText,
                spanData.normalizedValue,
                spanData.infoType,
                spanData.targetFieldId,
                spanData.exactSelection,
                spanData.hasExtraContext);
            boundAny = true;
        }

        return boundAny;
    }

    private void BindSpan(int index, string displayText, string normalizedValue, string infoTypeValue, string targetFieldId, bool exact, bool extra)
    {
        if (selectableSpans == null || index < 0 || index >= selectableSpans.Length)
        {
            return;
        }

        SelectableSpan span = selectableSpans[index];
        if (span == null)
        {
            return;
        }

        span.Bind(displayText, normalizedValue, infoTypeValue, targetFieldId, exact, extra);
        span.SetSelected(false);
        span.gameObject.SetActive(true);
    }

    private void CacheRefs()
    {
        if (rootRect == null) rootRect = transform as RectTransform;
        if (messageRect == null && messageText != null) messageRect = messageText.rectTransform;
        if (operatorRect == null && operatorRoot != null) operatorRect = operatorRoot.transform as RectTransform;
        if (callerRect == null && callerRoot != null) callerRect = callerRoot.transform as RectTransform;
        if (highlightRect == null && activeHighlightRoot != null) highlightRect = activeHighlightRoot.transform as RectTransform;
        if (operatorImage == null && operatorRoot != null) operatorImage = operatorRoot.GetComponent<Image>();
        if (callerImage == null && callerRoot != null) callerImage = callerRoot.GetComponent<Image>();
        if (highlightImage == null && activeHighlightRoot != null) highlightImage = activeHighlightRoot.GetComponent<Image>();
        if (rootLayoutGroup == null) rootLayoutGroup = GetComponent<VerticalLayoutGroup>();

        if (spanContainer == null && callerRoot != null)
        {
            Transform span = callerRoot.transform.Find("SpanContainer");
            if (span != null)
            {
                spanContainer = span.gameObject;
                spanRect = span as RectTransform;
                selectableSpans = span.GetComponentsInChildren<SelectableSpan>(true);
            }
        }
        else if (spanRect == null && spanContainer != null)
        {
            spanRect = spanContainer.transform as RectTransform;
        }

        if ((selectableSpans == null || selectableSpans.Length == 0) && spanContainer != null)
        {
            selectableSpans = spanContainer.GetComponentsInChildren<SelectableSpan>(true);
        }

        if (inlineSpanText == null && messageText != null)
        {
            inlineSpanText = messageText.GetComponent<TranscriptInlineSpanText>();
            if (inlineSpanText == null)
            {
                inlineSpanText = messageText.gameObject.AddComponent<TranscriptInlineSpanText>();
            }
        }

        if (inlineSpanText != null && messageText != null)
        {
            inlineSpanText.Bind(this, messageText);
        }
    }

    private void ApplySharedStyle()
    {
        if (rootRect != null)
        {
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            // Size is now managed by ContentSizeFitter/LayoutGroup
        }

        if (messageText != null)
        {
            messageText.fontSize = 20f;
            messageText.textWrappingMode = TextWrappingModes.Normal;
            messageText.richText = true;
        }

        ApplyBubbleBase(operatorRect, operatorImage);
        ApplyBubbleBase(callerRect, callerImage);

        if (highlightImage != null)
        {
            highlightImage.color = HighlightColor;
            highlightImage.raycastTarget = false;
        }

        if (spanRect != null)
        {
            spanRect.anchorMin = new Vector2(0f, 0f);
            spanRect.anchorMax = new Vector2(1f, 0f);
            spanRect.pivot = new Vector2(0.5f, 0f);
            spanRect.anchoredPosition = new Vector2(0f, 12f);
            spanRect.sizeDelta = new Vector2(-24f, 32f);
        }
    }

    private void ApplyBubbleBase(RectTransform bubbleRect, Image bubbleImage)
    {
        if (bubbleRect != null)
        {
            // Reset anchors to let Layout Groups handle positioning inside the Root
            bubbleRect.anchorMin = new Vector2(0.5f, 0.5f);
            bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
            bubbleRect.pivot = new Vector2(0.5f, 0.5f);
        }

        if (bubbleImage != null)
        {
            bubbleImage.raycastTarget = false;
        }
    }

    private void ApplyEntryStyle(bool isCaller, bool showExtractUi)
    {
        if (rootLayoutGroup != null)
        {
            rootLayoutGroup.childAlignment = isCaller ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        }

        if (messageText != null)
        {
            messageText.alignment = isCaller ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            messageText.color = isCaller ? CallerTextColor : OperatorTextColor;
        }

        if (operatorImage != null)
        {
            operatorImage.color = OperatorBubbleColor;
        }

        if (callerImage != null)
        {
            callerImage.color = CallerBubbleColor;
        }
    }

    private void ApplyInlineMessageVisuals(bool isCaller, bool showExtractUi)
    {
        if (messageText == null)
        {
            return;
        }

        bool useInlineMessage = showExtractUi && isCaller && HasVisibleBoundSpans();
        messageText.raycastTarget = useInlineMessage;
        messageText.text = useInlineMessage
            ? BuildInlineMessageText(boundEntry != null ? boundEntry.text : string.Empty)
            : (boundEntry != null ? boundEntry.text : string.Empty);
        lastInlineRenderState = BuildInlineRenderStateSignature(showExtractUi, useInlineMessage);
    }

    private void RefreshInlineMessageVisualsIfNeeded(bool force = false)
    {
        if (boundEntry == null || messageText == null)
        {
            return;
        }

        bool isCaller = boundEntry.speaker == TranscriptSpeakerType.Caller;
        bool showExtractUi = isCaller && boundEntry.isActiveExtractableChunk && IsExtractModeActive();
        bool useInlineMessage = showExtractUi && HasVisibleBoundSpans();
        string stateSignature = BuildInlineRenderStateSignature(showExtractUi, useInlineMessage);
        if (!force && string.Equals(lastInlineRenderState, stateSignature, System.StringComparison.Ordinal))
        {
            return;
        }

        ApplyInlineMessageVisuals(isCaller, showExtractUi);
    }

    private string BuildInlineRenderStateSignature(bool showExtractUi, bool useInlineMessage)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(showExtractUi ? '1' : '0');
        builder.Append(useInlineMessage ? '1' : '0');
        builder.Append('|');
        builder.Append(hoveredInlineSpanIndex);

        if (selectableSpans != null)
        {
            for (int i = 0; i < selectableSpans.Length; i++)
            {
                SelectableSpan span = selectableSpans[i];
                if (span == null)
                {
                    continue;
                }

                builder.Append('|');
                builder.Append(i);
                builder.Append(':');
                builder.Append(span.gameObject.activeSelf ? '1' : '0');
                builder.Append(span.IsSelected ? '1' : '0');
                builder.Append(span.RawText);
            }
        }

        return builder.ToString();
    }

    private bool HasVisibleBoundSpans()
    {
        if (selectableSpans == null)
        {
            return false;
        }

        for (int i = 0; i < selectableSpans.Length; i++)
        {
            if (selectableSpans[i] != null && selectableSpans[i].gameObject.activeSelf)
            {
                return true;
            }
        }

        return false;
    }

    private string BuildInlineMessageText(string sourceText)
    {
        if (string.IsNullOrEmpty(sourceText) || selectableSpans == null)
        {
            return sourceText ?? string.Empty;
        }

        List<InlineSpanBinding> bindings = BuildInlineSpanBindings(sourceText);
        if (bindings.Count <= 0)
        {
            return sourceText;
        }

        StringBuilder builder = new StringBuilder();
        int cursor = 0;

        for (int i = 0; i < bindings.Count; i++)
        {
            InlineSpanBinding binding = bindings[i];
            if (binding.startIndex < cursor)
            {
                continue;
            }

            if (binding.startIndex > cursor)
            {
                builder.Append(EscapeRichText(sourceText.Substring(cursor, binding.startIndex - cursor)));
            }

            string displayText = sourceText.Substring(binding.startIndex, binding.length);
            builder.Append(BuildInlineSpanMarkup(binding.displayOrder, binding.span, displayText));
            cursor = binding.startIndex + binding.length;
        }

        if (cursor < sourceText.Length)
        {
            builder.Append(EscapeRichText(sourceText.Substring(cursor)));
        }

        return builder.ToString();
    }

    private List<InlineSpanBinding> BuildInlineSpanBindings(string sourceText)
    {
        List<InlineSpanBinding> bindings = new List<InlineSpanBinding>();
        if (string.IsNullOrEmpty(sourceText) || selectableSpans == null)
        {
            return bindings;
        }

        int searchStartIndex = 0;
        for (int i = 0; i < selectableSpans.Length; i++)
        {
            SelectableSpan span = selectableSpans[i];
            if (span == null || !span.gameObject.activeSelf || string.IsNullOrWhiteSpace(span.RawText))
            {
                continue;
            }

            int foundIndex = sourceText.IndexOf(span.RawText, searchStartIndex, System.StringComparison.OrdinalIgnoreCase);
            if (foundIndex < 0)
            {
                foundIndex = sourceText.IndexOf(span.RawText, System.StringComparison.OrdinalIgnoreCase);
            }

            if (foundIndex < 0)
            {
                continue;
            }

            int textLength = Mathf.Min(span.RawText.Length, Mathf.Max(0, sourceText.Length - foundIndex));
            if (textLength <= 0)
            {
                continue;
            }

            bindings.Add(new InlineSpanBinding
            {
                displayOrder = i,
                startIndex = foundIndex,
                length = textLength,
                span = span
            });

            searchStartIndex = foundIndex + textLength;
        }

        bindings.Sort((left, right) => left.startIndex.CompareTo(right.startIndex));
        return bindings;
    }

    private string BuildInlineSpanMarkup(int linkIndex, SelectableSpan span, string displayText)
    {
        string safeText = EscapeRichText(displayText);
        string textColor = InlineCandidateColorHex;
        string markColor = string.Empty;

        if (span != null)
        {
            if (span.IsSelected)
            {
                textColor = InlineSelectedColorHex;
                markColor = span.HasExtraContext ? InlineExtraMarkHex : InlineExactMarkHex;
            }
            else if (hoveredInlineSpanIndex == linkIndex)
            {
                textColor = InlineHoverColorHex;
            }
            else if (span.HasExtraContext)
            {
                textColor = InlineExtraCandidateColorHex;
            }
        }

        if (!string.IsNullOrEmpty(markColor))
        {
            return $"<link=\"{linkIndex}\"><mark={markColor}><u><color={textColor}>{safeText}</color></u></mark></link>";
        }

        return $"<link=\"{linkIndex}\"><u><color={textColor}>{safeText}</color></u></link>";
    }

    private SelectableSpan GetInlineSpanByLinkIndex(int linkIndex)
    {
        if (selectableSpans == null || linkIndex < 0 || linkIndex >= selectableSpans.Length)
        {
            return null;
        }

        SelectableSpan span = selectableSpans[linkIndex];
        return span != null && span.gameObject.activeSelf ? span : null;
    }

    private static string EscapeRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
