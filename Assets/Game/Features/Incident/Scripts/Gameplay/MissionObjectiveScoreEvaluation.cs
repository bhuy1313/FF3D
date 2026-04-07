public readonly struct MissionObjectiveScoreEvaluation
{
    public MissionObjectiveScoreEvaluation(int score, int maxScore, string summary)
    {
        Score = score < 0 ? 0 : score;
        MaxScore = maxScore < 0 ? 0 : maxScore;
        Summary = summary ?? string.Empty;
    }

    public int Score { get; }
    public int MaxScore { get; }
    public string Summary { get; }
}
