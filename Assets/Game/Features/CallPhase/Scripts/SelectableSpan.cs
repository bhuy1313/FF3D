using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to a clickable span chip inside a caller transcript item.
/// Assign LabelText, SelectedVisual, and Button in the Inspector.
/// A parent controller can receive clicks by implementing OnSelectableSpanClicked(SelectableSpan span).
/// </summary>
public class SelectableSpan : MonoBehaviour
{
    [Header("Span Data")]
    [SerializeField] private string rawText;
    [SerializeField] private string normalizedValue;
    [SerializeField] private string infoType;
    [SerializeField] private string targetFieldId;
    [SerializeField] private bool isExactSelection = true;
    [SerializeField] private bool hasExtraContext = false;
    [SerializeField] private bool enableDebugLogs = false;

    [Header("UI References")]
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private GameObject selectedVisual;
    [SerializeField] private Button button;

    public string RawText => rawText;
    public string NormalizedValue => normalizedValue;
    public string InfoType => infoType;
    public string TargetFieldId => targetFieldId;
    public bool IsExactSelection => isExactSelection;
    public bool HasExtraContext => hasExtraContext;
    public bool IsSelected { get; private set; }

    private void Awake()
    {
        ResolveReferences();

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.AddListener(HandleClicked);
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning($"{nameof(SelectableSpan)}: Missing Button reference.", this);
        }

        RefreshVisuals();
        RefreshLabel();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RefreshVisuals();
        RefreshLabel();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
        }
    }

    public void Bind(
        string displayText,
        string normalized,
        string infoTypeValue,
        string fieldId,
        bool exact = true,
        bool extra = false)
    {
        rawText = displayText ?? string.Empty;
        normalizedValue = string.IsNullOrWhiteSpace(normalized) ? rawText : normalized;
        infoType = infoTypeValue ?? string.Empty;
        targetFieldId = fieldId ?? string.Empty;
        isExactSelection = exact;
        hasExtraContext = extra;

        RefreshLabel();

        if (enableDebugLogs)
        {
            Debug.Log(
                $"{nameof(SelectableSpan)}: Bound span '{rawText}' -> normalized '{normalizedValue}', infoType '{infoType}', targetField '{targetFieldId}'.",
                this);
        }
    }

    public void SetSelected(bool value)
    {
        IsSelected = value;
        RefreshVisuals();

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(SelectableSpan)}: Selection changed to {value} for '{rawText}'.", this);
        }
    }

    private void HandleClicked()
    {
        SetSelected(true);

        // Future extraction controllers can listen from a parent object without a direct hard reference.
        SendMessageUpwards("OnSelectableSpanClicked", this, SendMessageOptions.DontRequireReceiver);

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(SelectableSpan)}: Clicked '{rawText}'.", this);
        }
    }

    private void RefreshLabel()
    {
        if (labelText != null)
        {
            labelText.text = rawText;
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning($"{nameof(SelectableSpan)}: Missing LabelText reference.", this);
        }
    }

    private void RefreshVisuals()
    {
        if (selectedVisual != null)
        {
            selectedVisual.SetActive(IsSelected);
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning($"{nameof(SelectableSpan)}: Missing SelectedVisual reference.", this);
        }
    }

    private void ResolveReferences()
    {
        if (labelText == null)
        {
            Transform label = transform.Find("LabelText");
            if (label != null)
            {
                labelText = label.GetComponent<TMP_Text>();
            }
        }

        if (selectedVisual == null)
        {
            Transform selected = transform.Find("SelectedVisual");
            if (selected != null)
            {
                selectedVisual = selected.gameObject;
            }
        }

        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }
}
