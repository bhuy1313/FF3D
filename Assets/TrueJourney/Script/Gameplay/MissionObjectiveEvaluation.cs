using UnityEngine;

public readonly struct MissionObjectiveEvaluation
{
    public MissionObjectiveEvaluation(string title, string summary, bool isComplete, bool hasFailed, bool isRelevant)
    {
        Title = title;
        Summary = summary;
        IsComplete = isComplete;
        HasFailed = hasFailed;
        IsRelevant = isRelevant;
    }

    public string Title { get; }
    public string Summary { get; }
    public bool IsComplete { get; }
    public bool HasFailed { get; }
    public bool IsRelevant { get; }
}
