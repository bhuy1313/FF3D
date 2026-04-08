using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MissionScoreConfig
{
    [Header("General")]
    [SerializeField] private bool enableScoring = true;
    [SerializeField, Min(0)] private int completionBonus = 10;
    [SerializeField, Min(0)] private int failurePenalty = 0;

    [Header("Victim Outcome")]
    [SerializeField, Min(0)] private int noVictimDeathsBonus = 5;
    [SerializeField, Min(0)] private int perVictimDeathPenalty = 5;

    [Header("Time Bonus")]
    [SerializeField, Min(0)] private int timeBonusMaxScore = 10;
    [SerializeField, Min(0f)] private float targetTimeSeconds = 120f;
    [SerializeField, Min(0f)] private float acceptableTimeSeconds = 240f;

    [Header("Signal Score Rules")]
    [SerializeField] private List<MissionSignalScoreRule> signalScoreRules = new List<MissionSignalScoreRule>();

    [Header("Rank Thresholds")]
    [SerializeField, Range(0f, 1f)] private float sRankThreshold = 0.9f;
    [SerializeField, Range(0f, 1f)] private float aRankThreshold = 0.75f;
    [SerializeField, Range(0f, 1f)] private float bRankThreshold = 0.5f;
    [SerializeField] private string sRankLabel = "S";
    [SerializeField] private string aRankLabel = "A";
    [SerializeField] private string bRankLabel = "B";
    [SerializeField] private string cRankLabel = "C";

    public bool EnableScoring => enableScoring;
    public int CompletionBonus => Mathf.Max(0, completionBonus);
    public int FailurePenalty => Mathf.Max(0, failurePenalty);
    public int NoVictimDeathsBonus => Mathf.Max(0, noVictimDeathsBonus);
    public int PerVictimDeathPenalty => Mathf.Max(0, perVictimDeathPenalty);
    public int TimeBonusMaxScore => Mathf.Max(0, timeBonusMaxScore);
    public float TargetTimeSeconds => Mathf.Max(0f, targetTimeSeconds);
    public float AcceptableTimeSeconds => Mathf.Max(TargetTimeSeconds, acceptableTimeSeconds);
    public IReadOnlyList<MissionSignalScoreRule> SignalScoreRules => signalScoreRules;

    public int GetMaximumBonusScore()
    {
        return CompletionBonus + NoVictimDeathsBonus + TimeBonusMaxScore + GetMaximumSignalRuleScore();
    }

    public int EvaluateTimeBonus(float elapsedTimeSeconds)
    {
        int maximumScore = TimeBonusMaxScore;
        float targetSeconds = TargetTimeSeconds;
        float acceptableSeconds = AcceptableTimeSeconds;
        if (maximumScore <= 0 || targetSeconds <= 0f)
        {
            return 0;
        }

        if (elapsedTimeSeconds <= targetSeconds)
        {
            return maximumScore;
        }

        if (elapsedTimeSeconds >= acceptableSeconds)
        {
            return 0;
        }

        float remainingWindow = acceptableSeconds - targetSeconds;
        if (remainingWindow <= 0f)
        {
            return 0;
        }

        float ratio = (acceptableSeconds - elapsedTimeSeconds) / remainingWindow;
        return Mathf.Clamp(Mathf.RoundToInt(maximumScore * ratio), 0, maximumScore);
    }

    public string EvaluateRank(int score, int maxScore)
    {
        if (maxScore <= 0)
        {
            return string.Empty;
        }

        float ratio = Mathf.Clamp01((float)Mathf.Max(0, score) / maxScore);
        if (ratio >= Mathf.Clamp01(sRankThreshold))
        {
            return string.IsNullOrWhiteSpace(sRankLabel) ? "S" : sRankLabel;
        }

        if (ratio >= Mathf.Clamp01(aRankThreshold))
        {
            return string.IsNullOrWhiteSpace(aRankLabel) ? "A" : aRankLabel;
        }

        if (ratio >= Mathf.Clamp01(bRankThreshold))
        {
            return string.IsNullOrWhiteSpace(bRankLabel) ? "B" : bRankLabel;
        }

        return string.IsNullOrWhiteSpace(cRankLabel) ? "C" : cRankLabel;
    }

    public int GetMaximumSignalRuleScore()
    {
        if (signalScoreRules == null || signalScoreRules.Count == 0)
        {
            return 0;
        }

        int score = 0;
        for (int i = 0; i < signalScoreRules.Count; i++)
        {
            MissionSignalScoreRule rule = signalScoreRules[i];
            if (rule == null || !rule.IncludeInMaximumScore)
            {
                continue;
            }

            score += Mathf.Max(0, rule.ScoreDelta);
        }

        return score;
    }
}
