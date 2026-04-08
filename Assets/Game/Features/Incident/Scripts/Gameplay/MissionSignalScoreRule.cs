using System;
using UnityEngine;

[Serializable]
public class MissionSignalScoreRule
{
    [SerializeField] private string signalKey = "signal";
    [SerializeField] private string summaryLabel;
    [SerializeField] private int scoreDelta = 5;
    [SerializeField] private bool includeInMaximumScore = true;

    public string SignalKey => signalKey;
    public string SummaryLabel => summaryLabel;
    public int ScoreDelta => scoreDelta;
    public bool IncludeInMaximumScore => includeInMaximumScore;

    public bool Matches(string candidateSignalKey)
    {
        if (string.IsNullOrWhiteSpace(signalKey) || string.IsNullOrWhiteSpace(candidateSignalKey))
        {
            return false;
        }

        return string.Equals(signalKey.Trim(), candidateSignalKey.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
