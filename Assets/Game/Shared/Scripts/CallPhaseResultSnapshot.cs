using System;
using System.Collections.Generic;

[Serializable]
public class CallPhaseResultSnapshot
{
    public string caseId = string.Empty;
    public string scenarioId = string.Empty;
    public int finalScore;
    public int maximumScore;
    public int keyValuesScore;
    public int confirmedFactsCount;
    public int confirmedFactsScore;
    public string severityChosen = string.Empty;
    public string severityChosenDisplay = string.Empty;
    public string expectedSeverity = string.Empty;
    public string expectedSeverityDisplay = string.Empty;
    public int severityScore;
    public int readinessScore;
    public int followUpTotalCount;
    public int followUpOptimalCount;
    public int followUpAcceptableCount;
    public int followUpPoorCount;
    public int followUpScore;
    public string followUpQualityLabel = string.Empty;
    public int callDurationSeconds;
    public int targetCallTimeSeconds;
    public int acceptableCallTimeSeconds;
    public int callTimeScore;
    public bool callTimeQualified;
    public string callTimeEfficiencyLabel = string.Empty;
    public string resultReviewText = string.Empty;
    public List<string> feedbackLines = new List<string>();
    public List<string> keyValueIssues = new List<string>();
}
