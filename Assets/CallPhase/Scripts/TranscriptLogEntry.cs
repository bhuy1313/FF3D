using System;
using UnityEngine;

/// <summary>
/// Serializable data model representing a single transcript log entry.
/// </summary>
[Serializable]
public class TranscriptLogEntry
{
    [Tooltip("The speaker of this log line.")]
    public TranscriptSpeakerType speaker;
    
    [TextArea(2, 5)]
    [Tooltip("The text content of the message.")]
    public string text;
    
    [Tooltip("Whether this caller message contains extractable information.")]
    public bool isExtractable;
    
    [Tooltip("Whether this caller message is currently the active chunk for extraction.")]
    public bool isActiveExtractableChunk;
}
