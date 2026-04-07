using UnityEngine;

/// <summary>
/// A utility helper to quickly add an extractable caller log and automatically
/// transition the panel into ExtractMode. Requires TranscriptLogsController and TranscriptStateController.
/// </summary>
public class TranscriptAutoExtractEntry : MonoBehaviour
{
    [Header("Controllers")]
    [SerializeField] private TranscriptLogsController logsController;
    [SerializeField] private TranscriptStateController stateController;
    [SerializeField] private TranscriptExtractionController extractionController;

    [Header("Settings")]
    [SerializeField] private bool enableDebugLogs = false;

    /// <summary>
    /// Adds a caller log, marks it as the active extractable chunk, 
    /// and forces the panel into ExtractMode.
    /// </summary>
    /// <param name="message">The text string the caller is saying.</param>
    public void AddCallerLogAndEnterExtractMode(string message)
    {
        ResolveReferences();

        if (logsController == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"{nameof(TranscriptAutoExtractEntry)}: Missing TranscriptLogsController reference. Cannot proceed.");
            }
            return;
        }

        // Add the log and mark it as active
        logsController.AddCallerLog(message, isExtractable: true, setAsActiveChunk: true);

        if (extractionController != null && extractionController.TryAutoConfirmPendingConfirmationSpan())
        {
            if (enableDebugLogs)
            {
                Debug.Log($"{nameof(TranscriptAutoExtractEntry)}: Added extractable log and auto-confirmed without entering ExtractMode. Message: '{message}'");
            }
            return;
        }

        if (stateController == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"{nameof(TranscriptAutoExtractEntry)}: Missing TranscriptStateController reference. Cannot enter ExtractMode.");
            }
            return;
        }

        // Switch the UI state to extraction mode
        stateController.EnterExtractMode();

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(TranscriptAutoExtractEntry)}: Added extractable log and entered ExtractMode. Message: '{message}'");
        }
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (logsController == null)
        {
            logsController = GetComponentInParent<TranscriptLogsController>();
            if (logsController == null)
            {
                logsController = FindAnyObjectByType<TranscriptLogsController>();
            }
        }

        if (stateController == null)
        {
            stateController = GetComponentInParent<TranscriptStateController>();
            if (stateController == null)
            {
                stateController = FindAnyObjectByType<TranscriptStateController>();
            }
        }

        if (extractionController == null)
        {
            extractionController = GetComponentInParent<TranscriptExtractionController>();
            if (extractionController == null)
            {
                extractionController = FindAnyObjectByType<TranscriptExtractionController>();
            }
        }
    }
}
