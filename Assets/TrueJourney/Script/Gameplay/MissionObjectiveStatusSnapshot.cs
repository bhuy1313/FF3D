public readonly struct MissionObjectiveStatusSnapshot
{
    public MissionObjectiveStatusSnapshot(string title, string summary, bool isComplete, bool hasFailed)
    {
        Title = title;
        Summary = summary;
        IsComplete = isComplete;
        HasFailed = hasFailed;
    }

    public string Title { get; }
    public string Summary { get; }
    public bool IsComplete { get; }
    public bool HasFailed { get; }
}
