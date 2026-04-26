using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class AssessRiskPopupEntryController
{
    private string BuildSubmittedReportSummaryText()
    {
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < SubmitSummaryFieldIds.Length; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append(GetFieldDisplayName(SubmitSummaryFieldIds[i]));
            builder.Append(": ");
            builder.Append(GetDisplayValue(GetPopupSummaryValue(SubmitSummaryFieldIds[i])));
        }

        return builder.ToString();
    }

    private string BuildResultPopupReviewText()
    {
        int keyValuesScore = CalculateKeyValuesScore(out List<string> keyValueIssues);
        int confirmedFactsCount = CountConfirmedFacts();
        int confirmedFactsScore = CalculateConfirmedFactsScore();
        string severityChosen = GetFieldValueOrFallback(SeverityFieldId);
        string severityChosenDisplay = GetDisplayValue(severityChosen, SeverityFieldId);
        string expectedSeverityDisplay = GetDisplayValue(GetExpectedSeverityValue(), SeverityFieldId);
        int severityScore = CalculateSeverityScore(severityChosen);
        int readinessScore = CalculateSubmissionReadinessScore();
        int followUpTotalCount = GetTotalSelectedFollowUpQuestions();
        int followUpScore = CalculateFollowUpQualityScore(followUpTotalCount);
        string followUpQualityLabel = GetFollowUpQualityLabel(followUpScore, followUpTotalCount);
        int callTimeSeconds = GetCallDurationSeconds();
        bool callTimeQualified = HasQualifiedForCallTimeScore(readinessScore);
        int callTimeScore = CalculateCallTimeScore(callTimeQualified, callTimeSeconds);
        string callTimeEfficiencyLabel = GetCallTimeEfficiencyLabel(callTimeQualified, callTimeScore);
        int maximumScore = GetMaximumResultScore(callTimeQualified);
        int finalScore = keyValuesScore + confirmedFactsScore + severityScore + readinessScore + followUpScore + callTimeScore;
        finalScore = Mathf.Clamp(finalScore, 0, maximumScore);

        List<string> feedbackLines = BuildReviewFeedbackLines(
            keyValuesScore,
            keyValueIssues,
            confirmedFactsCount,
            confirmedFactsScore,
            severityChosen,
            severityScore,
            readinessScore,
            followUpScore,
            followUpTotalCount,
            callTimeQualified,
            callTimeScore);

        StringBuilder builder = new StringBuilder();
        builder.Append(CallPhaseUiChromeText.Tr("callphase.result.final_score", "Final Score"));
        builder.Append(": ");
        builder.Append(finalScore);
        builder.Append(" / ");
        builder.Append(maximumScore);
        builder.Append("\n\n");
        builder.Append(CallPhaseUiChromeText.Tr("callphase.result.severity_chosen", "Severity Chosen"));
        builder.Append(": ");
        builder.Append(severityChosenDisplay);
        builder.Append("\n");
        builder.Append(CallPhaseUiChromeText.Tr("callphase.result.expected_severity", "Expected Severity"));
        builder.Append(": ");
        builder.Append(expectedSeverityDisplay);
        builder.Append("\n\n");
        builder.Append(CallPhaseUiChromeText.Tr("callphase.result.confirmed_facts", "Confirmed Facts"));
        builder.Append(": ");
        builder.Append(confirmedFactsCount);
        builder.Append("\n\n");
        builder.Append(CallPhaseUiChromeText.Tr("callphase.result.followup_quality", "Follow-up Quality"));
        builder.Append(": ");
        builder.Append(followUpQualityLabel);
        builder.Append("\n");
        builder.Append(CallPhaseUiChromeText.Tr("callphase.result.optimal_questions", "Optimal Questions Chosen"));
        builder.Append(": ");
        builder.Append(GetOptimalFollowUpCount());
        builder.Append("\n");
        builder.Append(CallPhaseUiChromeText.Tr("callphase.result.acceptable_questions", "Acceptable Questions Chosen"));
        builder.Append(": ");
        builder.Append(GetAcceptableFollowUpCount());
        builder.Append("\n");
        builder.Append(CallPhaseUiChromeText.Tr("callphase.result.poor_questions", "Poor Questions Chosen"));
        builder.Append(": ");
        builder.Append(GetPoorFollowUpCount());
        builder.Append("\n\n");
        builder.Append(CallPhaseUiChromeText.Tr("callphase.result.call_time", "Call Time"));
        builder.Append(": ");
        builder.Append(FormatDuration(callTimeSeconds));
        builder.Append("\n");
        builder.Append(CallPhaseUiChromeText.Tr("callphase.result.target_time", "Target Time"));
        builder.Append(": ");
        builder.Append(FormatDuration(GetTargetCallTimeSeconds()));
        builder.Append("\n");
        builder.Append(CallPhaseUiChromeText.Tr("callphase.result.efficiency", "Efficiency"));
        builder.Append(": ");
        builder.Append(callTimeEfficiencyLabel);
        builder.Append("\n\n");
        builder.Append(CallPhaseUiChromeText.Tr("callphase.result.feedback", "Feedback"));
        builder.Append(":");

        for (int i = 0; i < feedbackLines.Count; i++)
        {
            builder.Append("\n- ");
            builder.Append(feedbackLines[i]);
        }

        return builder.ToString();
    }

    private int CalculateKeyValuesScore(out List<string> issues)
    {
        List<string> criticalIssues = new List<string>();
        List<string> nonCriticalIssues = new List<string>();
        int score = 0;

        List<CallPhaseScenarioScoredFieldData> correctnessFields = GetReviewCorrectnessFields();
        for (int i = 0; i < correctnessFields.Count; i++)
        {
            CallPhaseScenarioScoredFieldData fieldConfig = correctnessFields[i];
            if (fieldConfig == null || string.IsNullOrWhiteSpace(fieldConfig.fieldId))
            {
                continue;
            }

            if (FieldMatchesExpectation(fieldConfig.fieldId, GetFieldValueOrFallback(fieldConfig.fieldId)))
            {
                score += Mathf.Max(0, fieldConfig.scoreWeight);
                continue;
            }

            string issueText = !string.IsNullOrWhiteSpace(fieldConfig.issueText)
                ? fieldConfig.issueText.Trim()
                : BuildDefaultFieldIssueText(fieldConfig.fieldId, fieldConfig.displayName);

            if (StringMatches(fieldConfig.fieldId, "hazard"))
            {
                issueText = BuildHazardIssueText(GetFieldValueOrFallback(fieldConfig.fieldId));
            }

            if (IsCriticalReviewField(fieldConfig.fieldId))
            {
                criticalIssues.Add(issueText);
            }
            else
            {
                nonCriticalIssues.Add(issueText);
            }
        }

        issues = new List<string>(criticalIssues.Count + nonCriticalIssues.Count);
        issues.AddRange(criticalIssues);
        issues.AddRange(nonCriticalIssues);

        return score;
    }

    private int CalculateConfirmedFactsScore()
    {
        int score = 0;
        List<CallPhaseScenarioScoredFieldData> expectedConfirmedFields = GetReviewExpectedConfirmedFields();
        for (int i = 0; i < expectedConfirmedFields.Count; i++)
        {
            CallPhaseScenarioScoredFieldData fieldConfig = expectedConfirmedFields[i];
            if (fieldConfig == null || string.IsNullOrWhiteSpace(fieldConfig.fieldId))
            {
                continue;
            }

            if (IsFieldConfirmed(fieldConfig.fieldId))
            {
                score += Mathf.Max(0, fieldConfig.scoreWeight);
            }
        }

        return score;
    }

    private int CalculateSeverityScore(string severityChosen)
    {
        string expectedSeverity = GetExpectedSeverityValue();
        if (string.Equals(severityChosen, expectedSeverity, StringComparison.OrdinalIgnoreCase))
        {
            return GetSeverityCorrectScore();
        }

        if (string.Equals(severityChosen, GetSeverityPartialValue(), StringComparison.OrdinalIgnoreCase))
        {
            return GetSeverityPartialScore();
        }

        return 0;
    }

    private int CalculateSubmissionReadinessScore()
    {
        int score = 0;
        List<CallPhaseScenarioScoredFieldData> readinessFields = GetReviewReadinessFields();
        for (int i = 0; i < readinessFields.Count; i++)
        {
            CallPhaseScenarioScoredFieldData fieldConfig = readinessFields[i];
            if (fieldConfig == null || string.IsNullOrWhiteSpace(fieldConfig.fieldId))
            {
                continue;
            }

            if (HasUsableFieldValue(fieldConfig.fieldId))
            {
                score += Mathf.Max(0, fieldConfig.scoreWeight);
            }
        }

        return score;
    }

    private List<string> BuildReviewFeedbackLines(
        int keyValuesScore,
        List<string> keyValueIssues,
        int confirmedFactsCount,
        int confirmedFactsScore,
        string severityChosen,
        int severityScore,
        int readinessScore,
        int followUpScore,
        int followUpTotalCount,
        bool callTimeQualified,
        int callTimeScore)
    {
        List<string> feedbackLines = new List<string>();
        CallPhaseScenarioResultFeedbackData feedbackConfig = GetResultReviewFeedbackConfig();

        if (severityScore >= GetSeverityCorrectScore())
        {
            feedbackLines.Add(GetConfiguredFeedbackLine(
                feedbackConfig.positiveSeverity,
                "callphase.result.feedback.positive_severity",
                "Good risk assessment."));
        }
        else if (IsMissingValue(severityChosen))
        {
            feedbackLines.Add(GetConfiguredFeedbackLine(
                feedbackConfig.missingSeverity,
                "callphase.result.feedback.missing_severity",
                "Severity assessment was missing."));
        }
        else
        {
            feedbackLines.Add(GetConfiguredFeedbackLine(
                feedbackConfig.incorrectSeverity,
                "callphase.result.feedback.incorrect_severity",
                "Severity was lower than expected."));
        }

        int maximumConfirmedFactsScore = GetMaximumScore(GetReviewExpectedConfirmedFields());
        if (maximumConfirmedFactsScore > 0 && confirmedFactsScore >= maximumConfirmedFactsScore)
        {
            feedbackLines.Add(GetConfiguredFeedbackLine(
                feedbackConfig.allConfirmed,
                "callphase.result.feedback.all_confirmed",
                BuildAllConfirmedFeedbackFallback()));
        }
        else if (confirmedFactsCount <= 0)
        {
            feedbackLines.Add(GetConfiguredFeedbackLine(
                feedbackConfig.noneConfirmed,
                "callphase.result.feedback.none_confirmed",
                "No critical facts were confirmed."));
        }
        else
        {
            feedbackLines.Add(GetConfiguredFeedbackLine(
                feedbackConfig.partialConfirmed,
                "callphase.result.feedback.partial_confirmed",
                "More read-back confirmation was needed for critical facts."));
        }

        if (keyValuesScore >= GetStrongCorrectnessThreshold())
        {
            feedbackLines.Add(GetConfiguredFeedbackLine(
                feedbackConfig.strongCorrectness,
                "callphase.result.feedback.strong_correctness",
                "Key incident details matched the scenario well."));
        }
        else if (keyValueIssues.Count > 0)
        {
            feedbackLines.Add(keyValueIssues[0]);
        }

        if (readinessScore >= GetReadySubmissionThreshold())
        {
            feedbackLines.Add(GetConfiguredFeedbackLine(
                feedbackConfig.readyToSubmit,
                "callphase.result.feedback.ready_to_submit",
                "Report was ready to submit."));
        }
        else
        {
            feedbackLines.Add(GetConfiguredFeedbackLine(
                feedbackConfig.incompleteSubmission,
                "callphase.result.feedback.incomplete_submission",
                "Some report details were missing at submission."));
        }

        if (followUpTotalCount > 0)
        {
            if (followUpScore >= GetFollowUpGoodThreshold())
            {
                feedbackLines.Add(GetConfiguredFeedbackLine(
                    feedbackConfig.strongFollowUp,
                    "callphase.result.feedback.strong_followup",
                    "You generally prioritized relevant follow-up questions."));
            }
            else if (followUpScore >= GetFollowUpMixedThreshold())
            {
                feedbackLines.Add(GetConfiguredFeedbackLine(
                    feedbackConfig.mixedFollowUp,
                    "callphase.result.feedback.mixed_followup",
                    "Some follow-up questions were useful, but prioritization was uneven."));
            }
            else
            {
                feedbackLines.Add(GetConfiguredFeedbackLine(
                    feedbackConfig.poorFollowUp,
                    "callphase.result.feedback.poor_followup",
                    "Some follow-up questions were lower priority than the situation required."));
            }
        }

        if (callTimeQualified)
        {
            if (callTimeScore >= GetMaximumCallTimeScore())
            {
                feedbackLines.Add(CallPhaseUiChromeText.Tr(
                    "callphase.result.feedback.call_time_good",
                    "Call handling was efficient for this scenario."));
            }
            else if (callTimeScore > 0)
            {
                feedbackLines.Add(CallPhaseUiChromeText.Tr(
                    "callphase.result.feedback.call_time_acceptable",
                    "Call handling pace was acceptable, but could be faster."));
            }
            else
            {
                feedbackLines.Add(CallPhaseUiChromeText.Tr(
                    "callphase.result.feedback.call_time_slow",
                    "Call handling took longer than the scenario target."));
            }
        }
        else
        {
            feedbackLines.Add(CallPhaseUiChromeText.Tr(
                "callphase.result.feedback.call_time_not_scored",
                "Call Time efficiency was not scored because the submitted report was not complete enough."));
        }

        return feedbackLines;
    }

    private int GetMaximumResultScore(bool callTimeQualified)
    {
        return GetMaximumScore(GetReviewCorrectnessFields())
            + GetMaximumScore(GetReviewExpectedConfirmedFields())
            + Mathf.Max(0, GetSeverityCorrectScore())
            + GetMaximumScore(GetReviewReadinessFields())
            + (GetTotalSelectedFollowUpQuestions() > 0 ? GetMaximumFollowUpScore() : 0)
            + (callTimeQualified ? GetMaximumCallTimeScore() : 0);
    }

    private int GetMaximumScore(List<CallPhaseScenarioScoredFieldData> fieldConfigs)
    {
        int score = 0;
        if (fieldConfigs == null)
        {
            return score;
        }

        for (int i = 0; i < fieldConfigs.Count; i++)
        {
            CallPhaseScenarioScoredFieldData fieldConfig = fieldConfigs[i];
            if (fieldConfig == null)
            {
                continue;
            }

            score += Mathf.Max(0, fieldConfig.scoreWeight);
        }

        return score;
    }

    private List<CallPhaseScenarioScoredFieldData> GetReviewCorrectnessFields()
    {
        if (scenarioData != null
            && scenarioData.resultReview != null
            && scenarioData.resultReview.correctnessFields != null
            && scenarioData.resultReview.correctnessFields.Count > 0)
        {
            return scenarioData.resultReview.correctnessFields;
        }

        return new List<CallPhaseScenarioScoredFieldData>
        {
            new CallPhaseScenarioScoredFieldData { fieldId = "Address", displayName = "Address", scoreWeight = 8 },
            new CallPhaseScenarioScoredFieldData { fieldId = FireLocationFieldId, displayName = "Fire Location", scoreWeight = 6 },
            new CallPhaseScenarioScoredFieldData { fieldId = "OccupantRisk", displayName = "Occupant Risk", scoreWeight = 8 },
            new CallPhaseScenarioScoredFieldData { fieldId = "hazard", displayName = "Hazard", scoreWeight = 6 },
            new CallPhaseScenarioScoredFieldData { fieldId = "SpreadStatus", displayName = "Spread Status", scoreWeight = 6 },
            new CallPhaseScenarioScoredFieldData { fieldId = "CallerSafety", displayName = "Caller Safety", scoreWeight = 6 }
        };
    }

    private List<CallPhaseScenarioScoredFieldData> GetReviewExpectedConfirmedFields()
    {
        if (scenarioData != null
            && scenarioData.resultReview != null
            && scenarioData.resultReview.expectedConfirmedFields != null
            && scenarioData.resultReview.expectedConfirmedFields.Count > 0)
        {
            return scenarioData.resultReview.expectedConfirmedFields;
        }

        return new List<CallPhaseScenarioScoredFieldData>
        {
            new CallPhaseScenarioScoredFieldData { fieldId = "Address", displayName = "Address", scoreWeight = 8 },
            new CallPhaseScenarioScoredFieldData { fieldId = "OccupantRisk", displayName = "Occupant Risk", scoreWeight = 6 },
            new CallPhaseScenarioScoredFieldData { fieldId = "CallerSafety", displayName = "Caller Safety", scoreWeight = 6 }
        };
    }

    private List<CallPhaseScenarioScoredFieldData> GetReviewReadinessFields()
    {
        if (scenarioData != null
            && scenarioData.resultReview != null
            && scenarioData.resultReview.readinessFields != null
            && scenarioData.resultReview.readinessFields.Count > 0)
        {
            return scenarioData.resultReview.readinessFields;
        }

        return new List<CallPhaseScenarioScoredFieldData>
        {
            new CallPhaseScenarioScoredFieldData { fieldId = "Address", displayName = "Address", scoreWeight = 4 },
            new CallPhaseScenarioScoredFieldData { fieldId = FireLocationFieldId, displayName = "Fire Location", scoreWeight = 3 },
            new CallPhaseScenarioScoredFieldData { fieldId = "OccupantRisk", displayName = "Occupant Risk", scoreWeight = 3 },
            new CallPhaseScenarioScoredFieldData { fieldId = "hazard", displayName = "Hazard", scoreWeight = 3 },
            new CallPhaseScenarioScoredFieldData { fieldId = "SpreadStatus", displayName = "Spread Status", scoreWeight = 3 },
            new CallPhaseScenarioScoredFieldData { fieldId = "CallerSafety", displayName = "Caller Safety", scoreWeight = 2 },
            new CallPhaseScenarioScoredFieldData { fieldId = SeverityFieldId, displayName = "Severity", scoreWeight = 2 }
        };
    }

    private int CalculateFollowUpQualityScore(int totalSelectedQuestions)
    {
        if (totalSelectedQuestions <= 0)
        {
            return 0;
        }

        int score = (GetOptimalFollowUpCount() * Mathf.Max(0, GetFollowUpOptimalScore()))
            + (GetAcceptableFollowUpCount() * Mathf.Max(0, GetFollowUpAcceptableScore()))
            - (GetPoorFollowUpCount() * Mathf.Max(0, GetFollowUpPoorPenalty()));

        return Mathf.Clamp(score, 0, GetMaximumFollowUpScore());
    }

    private string GetFollowUpQualityLabel(int followUpScore, int totalSelectedQuestions)
    {
        if (totalSelectedQuestions <= 0)
        {
            return CallPhaseUiChromeText.Tr("callphase.result.followup_quality.not_used", "Not used");
        }

        if (followUpScore >= GetFollowUpGoodThreshold())
        {
            return CallPhaseUiChromeText.Tr("callphase.result.followup_quality.good", "Good");
        }

        if (followUpScore >= GetFollowUpMixedThreshold())
        {
            return CallPhaseUiChromeText.Tr("callphase.result.followup_quality.mixed", "Mixed");
        }

        return CallPhaseUiChromeText.Tr("callphase.result.followup_quality.poor", "Poor");
    }

    private int GetOptimalFollowUpCount()
    {
        return followUpController != null ? Mathf.Max(0, followUpController.OptimalFollowUpCount) : 0;
    }

    private int GetAcceptableFollowUpCount()
    {
        return followUpController != null ? Mathf.Max(0, followUpController.AcceptableFollowUpCount) : 0;
    }

    private int GetPoorFollowUpCount()
    {
        return followUpController != null ? Mathf.Max(0, followUpController.PoorFollowUpCount) : 0;
    }

    private int GetTotalSelectedFollowUpQuestions()
    {
        return followUpController != null ? Mathf.Max(0, followUpController.GetTotalSelectedFollowUpQuestions()) : 0;
    }

    private int GetFollowUpOptimalScore()
    {
        CallPhaseFollowUpScoringConfig scoringConfig = ResolveFollowUpScoringConfig();
        if (scoringConfig != null)
        {
            return Mathf.Max(0, scoringConfig.optimalQuestionScore);
        }

        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.followUpOptimalScore);
        }

        return 2;
    }

    private int GetFollowUpAcceptableScore()
    {
        CallPhaseFollowUpScoringConfig scoringConfig = ResolveFollowUpScoringConfig();
        if (scoringConfig != null)
        {
            return Mathf.Max(0, scoringConfig.acceptableQuestionScore);
        }

        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.followUpAcceptableScore);
        }

        return 1;
    }

    private int GetFollowUpPoorPenalty()
    {
        CallPhaseFollowUpScoringConfig scoringConfig = ResolveFollowUpScoringConfig();
        if (scoringConfig != null)
        {
            return Mathf.Max(0, scoringConfig.poorQuestionPenalty);
        }

        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.followUpPoorPenalty);
        }

        return 1;
    }

    private int GetMaximumFollowUpScore()
    {
        CallPhaseFollowUpScoringConfig scoringConfig = ResolveFollowUpScoringConfig();
        if (scoringConfig != null)
        {
            return Mathf.Max(0, scoringConfig.followUpMaxScore);
        }

        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.followUpMaxScore);
        }

        return 8;
    }

    private int GetFollowUpGoodThreshold()
    {
        CallPhaseFollowUpScoringConfig scoringConfig = ResolveFollowUpScoringConfig();
        if (scoringConfig != null)
        {
            return Mathf.Max(0, scoringConfig.followUpGoodThreshold);
        }

        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.followUpGoodThreshold);
        }

        return 4;
    }

    private int GetFollowUpMixedThreshold()
    {
        CallPhaseFollowUpScoringConfig scoringConfig = ResolveFollowUpScoringConfig();
        if (scoringConfig != null)
        {
            return Mathf.Clamp(scoringConfig.followUpMixedThreshold, 0, GetFollowUpGoodThreshold());
        }

        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.followUpMixedThreshold);
        }

        return 1;
    }

    private CallPhaseFollowUpScoringConfig ResolveFollowUpScoringConfig()
    {
        if (followUpController != null && followUpController.ActiveFollowUpScoringConfig != null)
        {
            return followUpController.ActiveFollowUpScoringConfig;
        }

        if (scenarioData != null && scenarioData.followUpScoringConfig != null)
        {
            return scenarioData.followUpScoringConfig;
        }

        return Resources.Load<CallPhaseFollowUpScoringConfig>(CallPhaseFollowUpScoringConfig.DefaultResourcePath);
    }

    private int GetCallDurationSeconds()
    {
        return scenarioContext != null ? Mathf.Max(0, scenarioContext.CurrentCallDurationSeconds) : 0;
    }

    private bool HasQualifiedForCallTimeScore(int readinessScore)
    {
        return readinessScore >= GetReadySubmissionThreshold();
    }

    private int CalculateCallTimeScore(bool isQualified, int callTimeSeconds)
    {
        if (!isQualified)
        {
            return 0;
        }

        int targetSeconds = GetTargetCallTimeSeconds();
        int acceptableSeconds = GetAcceptableCallTimeSeconds();
        int maximumScore = GetMaximumCallTimeScore();
        if (maximumScore <= 0 || targetSeconds <= 0)
        {
            return 0;
        }

        if (callTimeSeconds <= targetSeconds)
        {
            return maximumScore;
        }

        if (acceptableSeconds <= targetSeconds || callTimeSeconds > acceptableSeconds)
        {
            return 0;
        }

        float remainingWindow = acceptableSeconds - targetSeconds;
        float remainingScoreRatio = (acceptableSeconds - callTimeSeconds) / remainingWindow;
        return Mathf.Clamp(Mathf.RoundToInt(maximumScore * remainingScoreRatio), 0, maximumScore);
    }

    private string GetCallTimeEfficiencyLabel(bool isQualified, int callTimeScore)
    {
        if (!isQualified)
        {
            return CallPhaseUiChromeText.Tr("callphase.result.call_time_efficiency.not_counted", "Not counted");
        }

        if (callTimeScore >= GetMaximumCallTimeScore())
        {
            return CallPhaseUiChromeText.Tr("callphase.result.call_time_efficiency.good", "Good");
        }

        if (callTimeScore > 0)
        {
            return CallPhaseUiChromeText.Tr("callphase.result.call_time_efficiency.acceptable", "Acceptable");
        }

        return CallPhaseUiChromeText.Tr("callphase.result.call_time_efficiency.slow", "Slow");
    }

    private int GetTargetCallTimeSeconds()
    {
        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.targetCallTimeSeconds);
        }

        return 90;
    }

    private int GetAcceptableCallTimeSeconds()
    {
        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(GetTargetCallTimeSeconds(), scenarioData.resultReview.acceptableCallTimeSeconds);
        }

        return 150;
    }

    private int GetMaximumCallTimeScore()
    {
        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.callTimeMaxScore);
        }

        return 4;
    }

    private string FormatDuration(int totalSeconds)
    {
        int safeSeconds = Mathf.Max(0, totalSeconds);
        int minutes = safeSeconds / 60;
        int seconds = safeSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    private CallPhaseScenarioResultFeedbackData GetResultReviewFeedbackConfig()
    {
        if (scenarioData != null
            && scenarioData.resultReview != null
            && scenarioData.resultReview.feedback != null)
        {
            return scenarioData.resultReview.feedback;
        }

        return new CallPhaseScenarioResultFeedbackData();
    }

    private bool IsCriticalReviewField(string fieldId)
    {
        if (string.IsNullOrWhiteSpace(fieldId))
        {
            return false;
        }

        if (scenarioData != null
            && scenarioData.resultReview != null
            && scenarioData.resultReview.criticalFieldIds != null
            && scenarioData.resultReview.criticalFieldIds.Count > 0)
        {
            for (int i = 0; i < scenarioData.resultReview.criticalFieldIds.Count; i++)
            {
                if (StringMatches(fieldId, scenarioData.resultReview.criticalFieldIds[i]))
                {
                    return true;
                }
            }
        }

        return StringMatches(fieldId, "Address")
            || StringMatches(fieldId, FireLocationFieldId)
            || StringMatches(fieldId, "OccupantRisk");
    }

    private int GetSeverityCorrectScore()
    {
        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.severityCorrectScore);
        }

        return 20;
    }

    private int GetSeverityPartialScore()
    {
        if (scenarioData != null && scenarioData.resultReview != null)
        {
            return Mathf.Max(0, scenarioData.resultReview.severityPartialScore);
        }

        return 10;
    }

    private string GetSeverityPartialValue()
    {
        if (scenarioData != null
            && scenarioData.resultReview != null
            && !string.IsNullOrWhiteSpace(scenarioData.resultReview.severityPartialValue))
        {
            return scenarioData.resultReview.severityPartialValue.Trim();
        }

        return SeverityMedium;
    }

    private int GetStrongCorrectnessThreshold()
    {
        if (scenarioData != null && scenarioData.resultReview != null && scenarioData.resultReview.strongCorrectnessThreshold > 0)
        {
            return scenarioData.resultReview.strongCorrectnessThreshold;
        }

        return 32;
    }

    private int GetReadySubmissionThreshold()
    {
        if (scenarioData != null && scenarioData.resultReview != null && scenarioData.resultReview.readySubmissionThreshold > 0)
        {
            return scenarioData.resultReview.readySubmissionThreshold;
        }

        return 18;
    }

    private string GetConfiguredFeedbackLine(string configuredValue, string localizationKey, string fallbackValue)
    {
        string localizedFallback = CallPhaseUiChromeText.Tr(localizationKey, fallbackValue);
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return localizedFallback;
        }

        string trimmedValue = configuredValue.Trim();
        return string.Equals(trimmedValue, fallbackValue, StringComparison.Ordinal)
            ? localizedFallback
            : trimmedValue;
    }

    private string BuildAllConfirmedFeedbackFallback()
    {
        List<CallPhaseScenarioScoredFieldData> expectedConfirmedFields = GetReviewExpectedConfirmedFields();
        List<string> names = new List<string>();
        for (int i = 0; i < expectedConfirmedFields.Count; i++)
        {
            CallPhaseScenarioScoredFieldData fieldConfig = expectedConfirmedFields[i];
            if (fieldConfig == null)
            {
                continue;
            }

            string displayName = GetFieldDisplayName(fieldConfig.fieldId);
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                names.Add(displayName);
            }
        }

        return CallPhaseUiChromeText.Tr("callphase.result.feedback.all_confirmed", "Critical facts were confirmed.");
    }

    private string BuildDefaultFieldIssueText(string fieldId, string displayName)
    {
        string resolvedDisplayName = !string.IsNullOrWhiteSpace(displayName)
            ? displayName.Trim()
            : GetFieldDisplayName(fieldId);

        if (string.IsNullOrWhiteSpace(resolvedDisplayName))
        {
            resolvedDisplayName = CallPhaseUiChromeText.Tr("callphase.result.issue.field", "Field");
        }

        string format = CallPhaseUiChromeText.Tr(
            "callphase.result.issue.incorrect_or_missing",
            "{0} was incorrect or missing.");
        return string.Format(format, resolvedDisplayName);
    }

    private string GetFieldDisplayName(string fieldId)
    {
        return CallPhaseUiChromeText.GetFieldDisplayName(fieldId);
    }

    private string GetDisplayValue(string value, string fieldId = null)
    {
        if (IsMissingValue(value))
        {
            return CallPhaseUiChromeText.Tr("callphase.value.not_provided", "Not provided");
        }

        if (!string.IsNullOrWhiteSpace(fieldId))
        {
            return LocalizeFieldValueIfNeeded(fieldId, value);
        }

        return value ?? string.Empty;
    }

    private string LocalizeFieldValueIfNeeded(string fieldId, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? string.Empty;
        }

        return string.Equals(fieldId, SeverityFieldId, StringComparison.OrdinalIgnoreCase)
            ? CallPhaseUiChromeText.GetSeverityDisplayName(value)
            : value;
    }

    private bool IsMissingValue(string value)
    {
        return string.Equals(value, MissingValueToken, StringComparison.Ordinal);
    }
}


