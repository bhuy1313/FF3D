public readonly struct MissionFailConditionEvaluation
{
    public MissionFailConditionEvaluation(string title, string summary, bool hasFailed, bool isRelevant)
    {
        Title = title;
        Summary = summary;
        HasFailed = hasFailed;
        IsRelevant = isRelevant;
    }

    public string Title { get; }
    public string Summary { get; }
    public bool HasFailed { get; }
    public bool IsRelevant { get; }
}
