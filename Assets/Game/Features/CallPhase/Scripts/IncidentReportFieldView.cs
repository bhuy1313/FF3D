using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to a single Incident Report row.
/// Assign a unique fieldId, the row value TMP_Text, and the StateColor Image.
/// The controller can then update this row through SetValueAndState.
/// </summary>
public class IncidentReportFieldView : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string fieldId;

    [Header("UI")]
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Image stateColorImage;

    [Header("Colors")]
    [SerializeField] private Color emptyColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color reportedColor = new Color(0.95f, 0.75f, 0.20f, 1f);
    [SerializeField] private Color confirmedColor = new Color(0.20f, 0.85f, 0.45f, 1f);
    [SerializeField] private Color assessedColor = new Color(0.29f, 0.55f, 0.95f, 1f);

    [Header("Options")]
    [SerializeField] private string emptyDisplayText = "Empty";
    [SerializeField] private bool enableDebugLogs = false;

    private ReportFieldState currentState = ReportFieldState.Empty;
    private string currentValue = string.Empty;

    public string FieldId => fieldId;
    public ReportFieldState CurrentState => currentState;
    public string CurrentValue => currentValue;

    private void Awake()
    {
        if (valueText == null && enableDebugLogs)
        {
            Debug.LogWarning($"{nameof(IncidentReportFieldView)}: Missing Value Text reference.", this);
        }

        if (stateColorImage == null && enableDebugLogs)
        {
            Debug.LogWarning($"{nameof(IncidentReportFieldView)}: Missing State Color Image reference.", this);
        }

        if (string.IsNullOrEmpty(currentValue))
        {
            SetDisplayedValue(emptyDisplayText);
        }

        ApplyStateColor(ReportFieldState.Empty);
    }

    public void ResetField()
    {
        currentValue = string.Empty;
        SetDisplayedValue(emptyDisplayText);
        SetState(ReportFieldState.Empty);

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(IncidentReportFieldView)}: Reset field '{fieldId}'.", this);
        }
    }

    public void SetValue(string value)
    {
        currentValue = value ?? string.Empty;

        if (string.IsNullOrWhiteSpace(currentValue))
        {
            SetDisplayedValue(emptyDisplayText);
        }
        else
        {
            SetDisplayedValue(currentValue);
        }

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(IncidentReportFieldView)}: Set value for '{fieldId}' to '{currentValue}'.", this);
        }
    }

    public void SetState(ReportFieldState state)
    {
        currentState = state;
        ApplyStateColor(state);

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(IncidentReportFieldView)}: Set state for '{fieldId}' to {state}.", this);
        }
    }

    public void SetValueAndState(string value, ReportFieldState state)
    {
        SetValue(value);
        SetState(state);
    }

    private void SetDisplayedValue(string value)
    {
        if (valueText != null)
        {
            valueText.text = value;
        }
    }

    private void ApplyStateColor(ReportFieldState state)
    {
        if (stateColorImage == null)
        {
            return;
        }

        switch (state)
        {
            case ReportFieldState.Reported:
                stateColorImage.color = reportedColor;
                break;
            case ReportFieldState.Confirmed:
                stateColorImage.color = confirmedColor;
                break;
            case ReportFieldState.Assessed:
                stateColorImage.color = assessedColor;
                break;
            case ReportFieldState.Empty:
            default:
                stateColorImage.color = emptyColor;
                break;
        }
    }
}
