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

    [Header("Settings")]
    [SerializeField] private bool enableDebugLogs = false;

    /// <summary>
    /// Adds a caller log, marks it as the active extractable chunk, 
    /// and forces the panel into ExtractMode.
    /// </summary>
    /// <param name="message">The text string the caller is saying.</param>
    public void AddCallerLogAndEnterExtractMode(string message)
    {
        if (logsController == null || stateController == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"{nameof(TranscriptAutoExtractEntry)}: Missing controller references. Cannot proceed.");
            }
            return;
        }

        // Add the log and mark it as active
        logsController.AddCallerLog(message, isExtractable: true, setAsActiveChunk: true);

        // Switch the UI state to extraction mode
        stateController.EnterExtractMode();

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(TranscriptAutoExtractEntry)}: Added extractable log and entered ExtractMode. Message: '{message}'");
        }
    }
}
