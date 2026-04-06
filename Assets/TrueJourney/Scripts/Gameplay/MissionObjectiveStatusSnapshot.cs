public readonly struct MissionObjectiveStatusSnapshot
{
    public MissionObjectiveStatusSnapshot(string title, string summary, bool isComplete, bool hasFailed, int score, int maxScore)
    {
        Title = title;
        Summary = summary;
        IsComplete = isComplete;
        HasFailed = hasFailed;
        Score = score;
        MaxScore = maxScore;
    }

    public string Title { get; }
    public string Summary { get; }
    public bool IsComplete { get; }
    public bool HasFailed { get; }
    public int Score { get; }
    public int MaxScore { get; }
}
