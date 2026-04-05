using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to a parent object above SelectableSpan instances in the transcript UI.
/// Assign the extraction text fields and buttons in the Inspector.
/// SelectableSpan clicks will reach this component through OnSelectableSpanClicked(SelectableSpan span).
/// </summary>
public class TranscriptExtractionController : MonoBehaviour
{
    private const string DefaultEmptyValue = "-";
    private const string MatchTypeExact = "Exact";
    private const string MatchTypeExtra = "Extra";
    private const string MatchTypeInvalid = "Invalid";
    private const string PenaltyNone = "None";
    private const string PenaltyMinor = "Minor";
    private const string PenaltyMajor = "Major";

    [Header("UI References")]
    [SerializeField] private TMP_Text selectionValueText;
    [SerializeField] private TMP_Text infoTypeValueText;
    [SerializeField] private TMP_Text matchTypeValueText;
    [SerializeField] private TMP_Text penaltyValueText;
    [SerializeField] private TMP_Text targetFieldValueText;

    [Header("Buttons")]
    [SerializeField] private Button confirmExtractionButton;
    [SerializeField] private Button clearSelectionButton;
    [SerializeField] private Button exitExtractionButton;

    [Header("State")]
    [SerializeField] private TranscriptStateController stateController;
    [SerializeField] private IncidentReportController incidentReportController;

    [Header("Behavior")]
    [SerializeField] private bool clearSelectionOnConfirm = true;
    [SerializeField] private bool exitExtractModeOnConfirm = false;
    [SerializeField] private bool enableDebugLogs = false;

    private SelectableSpan currentSpan;

    private void Awake()
    {
        ResolveReferences();

        if (confirmExtractionButton != null)
        {
            confirmExtractionButton.onClick.AddListener(ConfirmExtraction);
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning($"{nameof(TranscriptExtractionController)}: Missing Confirm Extraction Button reference.", this);
        }

        if (clearSelectionButton != null)
        {
            clearSelectionButton.onClick.AddListener(ClearSelection);
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning($"{nameof(TranscriptExtractionController)}: Missing Clear Selection Button reference.", this);
        }

        if (exitExtractionButton != null)
        {
            exitExtractionButton.onClick.AddListener(ExitExtraction);
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning($"{nameof(TranscriptExtractionController)}: Missing Exit Extraction Button reference.", this);
        }
    }

    private void OnEnable()
    {
        ResolveReferences();
        currentSpan = null;
        ResetUiToDefault();
        SetSelectionButtons(confirmInteractable: false, clearInteractable: false);
    }

    private void Update()
    {
        HandleConfirmShortcut();
        TryAutoConfirmActiveConfirmationSpan();
    }

    private void OnDestroy()
    {
        if (confirmExtractionButton != null)
        {
            confirmExtractionButton.onClick.RemoveListener(ConfirmExtraction);
        }

        if (clearSelectionButton != null)
        {
            clearSelectionButton.onClick.RemoveListener(ClearSelection);
        }

        if (exitExtractionButton != null)
        {
            exitExtractionButton.onClick.RemoveListener(ExitExtraction);
        }
    }

    public void OnSelectableSpanClicked(SelectableSpan span)
    {
        if (span == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"{nameof(TranscriptExtractionController)}: Received null SelectableSpan.", this);
            }
            return;
        }

        if (currentSpan != null && currentSpan != span)
        {
            currentSpan.SetSelected(false);
        }

        currentSpan = span;
        currentSpan.SetSelected(true);

        SetText(selectionValueText, currentSpan.RawText);
        SetText(infoTypeValueText, currentSpan.InfoType);
        SetText(targetFieldValueText, currentSpan.TargetFieldId);

        bool isValidSelection = EvaluateSelection(currentSpan, out string matchType, out string penalty);
        SetText(matchTypeValueText, matchType);
        SetText(penaltyValueText, penalty);

        SetSelectionButtons(confirmInteractable: isValidSelection, clearInteractable: true);

        if (enableDebugLogs)
        {
            Debug.Log(
                $"{nameof(TranscriptExtractionController)}: Selected span '{currentSpan.RawText}' with match '{matchType}' and penalty '{penalty}'.",
                this);
        }
    }

    public void ClearSelection()
    {
        if (currentSpan != null)
        {
            currentSpan.SetSelected(false);
            currentSpan = null;
        }

        ResetUiToDefault();
        SetSelectionButtons(confirmInteractable: false, clearInteractable: false);

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(TranscriptExtractionController)}: Selection cleared.", this);
        }
    }

    public void ConfirmExtraction()
    {
        if (currentSpan == null)
        {
            return;
        }

        if (!EvaluateSelection(currentSpan, out _, out _))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"{nameof(TranscriptExtractionController)}: Confirm ignored because current selection is invalid.", this);
            }
            return;
        }

        SendMessageUpwards("OnExtractionConfirmed", currentSpan, SendMessageOptions.DontRequireReceiver);

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(TranscriptExtractionController)}: Extraction confirmed for '{currentSpan.RawText}'.", this);
        }

        if (clearSelectionOnConfirm)
        {
            ClearSelection();
        }

        if (exitExtractModeOnConfirm && stateController != null)
        {
            stateController.EnterNormalMode();
        }
    }

    public void ExitExtraction()
    {
        ClearSelection();

        if (stateController != null)
        {
            stateController.EnterNormalMode();
        }

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(TranscriptExtractionController)}: Exited ExtractMode.", this);
        }
    }

    public void ResetForScenarioRun()
    {
        ClearSelection();
    }

    private void HandleConfirmShortcut()
    {
        if (!WasConfirmShortcutPressed())
        {
            return;
        }

        if (FollowUpPopupController.AnyPopupOpen)
        {
            return;
        }

        if (stateController != null && stateController.CurrentState != TranscriptPanelState.ExtractMode)
        {
            return;
        }

        if (confirmExtractionButton != null && !confirmExtractionButton.interactable)
        {
            return;
        }

        ConfirmExtraction();
    }

    private void TryAutoConfirmActiveConfirmationSpan()
    {
        if (!CallPhaseAutoValidateSettings.GetSavedOrDefaultEnabled())
        {
            return;
        }

        if (incidentReportController == null || !incidentReportController.HasActiveConfirmationContext)
        {
            return;
        }

        if (stateController != null && stateController.CurrentState != TranscriptPanelState.ExtractMode)
        {
            return;
        }

        if (currentSpan != null)
        {
            return;
        }

        SelectableSpan[] spans = GetComponentsInChildren<SelectableSpan>(true);
        for (int i = 0; i < spans.Length; i++)
        {
            SelectableSpan span = spans[i];
            if (!IsAutoValidationCandidate(span))
            {
                continue;
            }

            OnSelectableSpanClicked(span);
            ConfirmExtraction();
            return;
        }
    }

    private void ResetUiToDefault()
    {
        SetText(selectionValueText, DefaultEmptyValue);
        SetText(infoTypeValueText, DefaultEmptyValue);
        SetText(matchTypeValueText, DefaultEmptyValue);
        SetText(penaltyValueText, DefaultEmptyValue);
        SetText(targetFieldValueText, DefaultEmptyValue);
    }

    private void SetSelectionButtons(bool confirmInteractable, bool clearInteractable)
    {
        if (confirmExtractionButton != null)
        {
            confirmExtractionButton.interactable = confirmInteractable;
        }

        if (clearSelectionButton != null)
        {
            clearSelectionButton.interactable = clearInteractable;
        }
    }

    private void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = string.IsNullOrEmpty(value) ? DefaultEmptyValue : value;
        }
    }

    private bool EvaluateSelection(SelectableSpan span, out string matchType, out string penalty)
    {
        matchType = MatchTypeInvalid;
        penalty = PenaltyMajor;

        if (span == null)
        {
            return false;
        }

        if (span.IsExactSelection && !span.HasExtraContext)
        {
            matchType = MatchTypeExact;
            penalty = PenaltyNone;
            return true;
        }

        if (span.HasExtraContext)
        {
            matchType = MatchTypeExtra;
            penalty = PenaltyMinor;
            return true;
        }

        return false;
    }

    private bool IsAutoValidationCandidate(SelectableSpan span)
    {
        if (span == null || !span.gameObject.activeInHierarchy || incidentReportController == null)
        {
            return false;
        }

        if (!string.Equals(span.TargetFieldId, incidentReportController.CurrentConfirmationFieldId, System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(span.NormalizedValue, incidentReportController.ExpectedConfirmedValue, System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return EvaluateSelection(span, out _, out _);
    }

    private static bool WasConfirmShortcutPressed()
    {
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    private void ResolveReferences()
    {
        if (stateController == null)
        {
            stateController = GetComponentInParent<TranscriptStateController>();
            if (stateController == null)
            {
                stateController = FindFirstObjectByType<TranscriptStateController>();
            }
        }

        if (incidentReportController == null)
        {
            incidentReportController = GetComponentInParent<IncidentReportController>();
            if (incidentReportController == null)
            {
                incidentReportController = FindFirstObjectByType<IncidentReportController>();
            }
        }
    }
}
