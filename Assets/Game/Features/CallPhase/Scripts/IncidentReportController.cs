using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to MainCallPhaseRoot or another parent above SelectableSpan objects.
/// Assign all IncidentReportFieldView rows in the Inspector.
/// This receives OnExtractionConfirmed(SelectableSpan span) through SendMessageUpwards.
/// </summary>
public class IncidentReportController : MonoBehaviour
{
    private const string HazardFieldId = "hazard";
    private const string SeverityFieldId = "Severity";

    public event Action ReportStateChanged;

    [Header("Field Views")]
    [SerializeField] private List<IncidentReportFieldView> fieldViews = new List<IncidentReportFieldView>();

    [Header("Behavior")]
    [SerializeField] private bool overwriteExistingReportedValues = true;
    [SerializeField] private bool enableDebugLogs = false;

    private readonly Dictionary<string, IncidentReportFieldView> fieldLookup = new Dictionary<string, IncidentReportFieldView>();
    private string currentConfirmationFieldId = string.Empty;
    private string expectedConfirmedValue = string.Empty;
    private bool isSubmitted;

    public bool HasActiveConfirmationContext => !string.IsNullOrWhiteSpace(currentConfirmationFieldId) && !string.IsNullOrWhiteSpace(expectedConfirmedValue);
    public string CurrentConfirmationFieldId => currentConfirmationFieldId;
    public string ExpectedConfirmedValue => expectedConfirmedValue;
    public bool IsSubmitted => isSubmitted;

    private void Awake()
    {
        fieldLookup.Clear();

        for (int i = 0; i < fieldViews.Count; i++)
        {
            IncidentReportFieldView fieldView = fieldViews[i];
            if (fieldView == null)
            {
                continue;
            }

            string key = fieldView.FieldId;
            if (string.IsNullOrWhiteSpace(key))
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"{nameof(IncidentReportController)}: Skipping field view with empty field id.", fieldView);
                }
                continue;
            }

            if (fieldLookup.ContainsKey(key))
            {
                Debug.LogWarning($"{nameof(IncidentReportController)}: Duplicate field id '{key}' found. Keeping the first entry.", fieldView);
                continue;
            }

            fieldLookup.Add(key, fieldView);
        }
    }

    public void ResetReport()
    {
        isSubmitted = false;

        for (int i = 0; i < fieldViews.Count; i++)
        {
            if (fieldViews[i] != null)
            {
                fieldViews[i].ResetField();
            }
        }

        ClearConfirmationContext();
        NotifyReportStateChanged();

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(IncidentReportController)}: Report reset.", this);
        }
    }

    public IncidentReportFieldView GetFieldView(string fieldId)
    {
        if (string.IsNullOrWhiteSpace(fieldId))
        {
            return null;
        }

        fieldLookup.TryGetValue(fieldId, out IncidentReportFieldView fieldView);
        return fieldView;
    }

    public bool HasCollectedValue(string fieldId)
    {
        IncidentReportFieldView fieldView = GetFieldView(fieldId);
        if (fieldView == null)
        {
            return false;
        }

        return fieldView.CurrentState != ReportFieldState.Empty
            && !string.IsNullOrWhiteSpace(fieldView.CurrentValue);
    }

    public void MarkSubmitted()
    {
        if (isSubmitted)
        {
            return;
        }

        isSubmitted = true;
        NotifyReportStateChanged();

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(IncidentReportController)}: Report marked as submitted.", this);
        }
    }

    public void BeginConfirmationContext(string fieldId, string expectedValue)
    {
        currentConfirmationFieldId = fieldId ?? string.Empty;
        expectedConfirmedValue = expectedValue ?? string.Empty;
        NotifyReportStateChanged();

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(IncidentReportController)}: Began confirmation context for '{currentConfirmationFieldId}' with expected value '{expectedConfirmedValue}'.", this);
        }
    }

    public void ClearConfirmationContext()
    {
        bool changed = !string.IsNullOrWhiteSpace(currentConfirmationFieldId)
            || !string.IsNullOrWhiteSpace(expectedConfirmedValue);

        currentConfirmationFieldId = string.Empty;
        expectedConfirmedValue = string.Empty;

        if (changed)
        {
            NotifyReportStateChanged();
        }

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(IncidentReportController)}: Cleared confirmation context.", this);
        }
    }

    public void ApplyExtraction(string targetFieldId, string value, bool confirmed = false)
    {
        if (string.IsNullOrWhiteSpace(targetFieldId))
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"{nameof(IncidentReportController)}: Missing target field id.", this);
            }
            return;
        }

        IncidentReportFieldView fieldView = GetFieldView(targetFieldId);
        if (fieldView == null)
        {
            Debug.LogWarning($"{nameof(IncidentReportController)}: No IncidentReportFieldView found for field id '{targetFieldId}'.", this);
            return;
        }

        string nextValue = GetResolvedFieldValue(fieldView, targetFieldId, value);

        if (!overwriteExistingReportedValues && fieldView.CurrentState != ReportFieldState.Empty && !confirmed)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"{nameof(IncidentReportController)}: Skipped overwrite for field '{targetFieldId}'.", this);
            }
            return;
        }

        ReportFieldState nextState = confirmed ? ReportFieldState.Confirmed : ReportFieldState.Reported;
        fieldView.SetValueAndState(nextValue, nextState);
        NotifyReportStateChanged();

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(IncidentReportController)}: Applied '{nextValue}' to field '{targetFieldId}' as {nextState}.", this);
        }
    }

    public void ApplySeverityAssessment(string severityValue)
    {
        IncidentReportFieldView fieldView = GetFieldView(SeverityFieldId);
        if (fieldView == null)
        {
            Debug.LogWarning($"{nameof(IncidentReportController)}: No IncidentReportFieldView found for field id '{SeverityFieldId}'.", this);
            return;
        }

        string nextValue = severityValue?.Trim() ?? string.Empty;
        fieldView.SetValueAndState(nextValue, ReportFieldState.Assessed);
        NotifyReportStateChanged();

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(IncidentReportController)}: Assessed severity as '{nextValue}'.", this);
        }
    }

    public void OnExtractionConfirmed(SelectableSpan span)
    {
        if (span == null)
        {
            return;
        }

        if (TryApplyConfirmation(span))
        {
            return;
        }

        bool isValidSelection = false;
        if (span.IsExactSelection && !span.HasExtraContext)
        {
            isValidSelection = true;
        }
        else if (span.HasExtraContext)
        {
            isValidSelection = true;
        }

        if (!isValidSelection)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"{nameof(IncidentReportController)}: Ignored invalid extraction for '{span.RawText}'.", this);
            }
            return;
        }

        ApplyExtraction(span.TargetFieldId, span.NormalizedValue, false);
    }

    private bool TryApplyConfirmation(SelectableSpan span)
    {
        if (!HasActiveConfirmationContext)
        {
            return false;
        }

        if (!StringMatches(span.TargetFieldId, currentConfirmationFieldId) || !StringMatches(span.NormalizedValue, expectedConfirmedValue))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"{nameof(IncidentReportController)}: Confirmation span did not match current context.", this);
            }
            return false;
        }

        IncidentReportFieldView fieldView = GetFieldView(currentConfirmationFieldId);
        if (fieldView == null)
        {
            ClearConfirmationContext();
            return false;
        }

        if (fieldView.CurrentState == ReportFieldState.Empty)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"{nameof(IncidentReportController)}: Cannot confirm empty field '{currentConfirmationFieldId}'.", this);
            }
            ClearConfirmationContext();
            return false;
        }

        if (fieldView.CurrentState == ReportFieldState.Confirmed)
        {
            ClearConfirmationContext();
            return true;
        }

        if (!DoesStoredFieldValueMatchConfirmationExpectation(fieldView.FieldId, fieldView.CurrentValue, expectedConfirmedValue))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"{nameof(IncidentReportController)}: Stored value mismatch for confirmation field '{currentConfirmationFieldId}'.", this);
            }
            ClearConfirmationContext();
            return false;
        }

        fieldView.SetValueAndState(fieldView.CurrentValue, ReportFieldState.Confirmed);
        NotifyReportStateChanged();
        ClearConfirmationContext();

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(IncidentReportController)}: Confirmed field '{fieldView.FieldId}'.", this);
        }

        return true;
    }

    private string GetResolvedFieldValue(IncidentReportFieldView fieldView, string targetFieldId, string incomingValue)
    {
        string sanitizedIncomingValue = incomingValue?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sanitizedIncomingValue))
        {
            return sanitizedIncomingValue;
        }

        if (!string.Equals(targetFieldId, HazardFieldId, StringComparison.OrdinalIgnoreCase))
        {
            return sanitizedIncomingValue;
        }

        return HazardValueUtility.MergeValues(fieldView.CurrentValue, sanitizedIncomingValue);
    }

    private bool DoesStoredFieldValueMatchConfirmationExpectation(string fieldId, string storedValue, string expectedValue)
    {
        if (string.Equals(fieldId, HazardFieldId, StringComparison.OrdinalIgnoreCase))
        {
            return HazardValueUtility.ContainsValue(storedValue, expectedValue);
        }

        return StringMatches(storedValue, expectedValue);
    }

    private void NotifyReportStateChanged()
    {
        ReportStateChanged?.Invoke();
    }

    private bool StringMatches(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
