using System;
using UnityEngine;

/// <summary>
/// Manages the current visual state of the Transcript Logs panel.
/// Shows or hides UI elements based on Normal vs ExtractMode.
/// </summary>
public class TranscriptStateController : MonoBehaviour
{
    public event Action<TranscriptPanelState> StateChanged;

    [Header("UI References")]
    [SerializeField] private GameObject extractionControlRoot;
    [SerializeField] private GameObject extractionResultRoot;
    [SerializeField] private GameObject normalModeHintRoot;

    [Header("Settings")]
    [SerializeField] private bool enableDebugLogs = false;

    private TranscriptPanelState currentState = TranscriptPanelState.Normal;

    /// <summary>
    /// Gets the current state of the panel.
    /// </summary>
    public TranscriptPanelState CurrentState => currentState;

    private void Start()
    {
        // Initialize to normal mode by default
        SetState(TranscriptPanelState.Normal);
    }

    /// <summary>
    /// Switches the UI to Normal mode.
    /// </summary>
    public void EnterNormalMode()
    {
        SetState(TranscriptPanelState.Normal);
    }

    /// <summary>
    /// Switches the UI to ExtractMode.
    /// </summary>
    public void EnterExtractMode()
    {
        SetState(TranscriptPanelState.ExtractMode);
    }

    public void ResetForScenarioRun()
    {
        SetState(TranscriptPanelState.Normal);
    }

    /// <summary>
    /// Sets the UI state directly to the provided state.
    /// </summary>
    public void SetState(TranscriptPanelState state)
    {
        if (currentState == state)
        {
            ApplyState(state);
            return;
        }

        currentState = state;
        ApplyState(state);
        StateChanged?.Invoke(currentState);

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(TranscriptStateController)}: State changed to {state}");
        }
    }

    private void ApplyState(TranscriptPanelState state)
    {
        bool isExtractMode = (state == TranscriptPanelState.ExtractMode);

        if (extractionControlRoot != null)
        {
            extractionControlRoot.SetActive(isExtractMode);
        }

        if (extractionResultRoot != null)
        {
            extractionResultRoot.SetActive(isExtractMode);
        }

        if (normalModeHintRoot != null)
        {
            normalModeHintRoot.SetActive(!isExtractMode);
        }
    }
}
