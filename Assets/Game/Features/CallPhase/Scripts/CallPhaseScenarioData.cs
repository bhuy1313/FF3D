using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inspector-friendly scenario data for the Call Phase prototype.
/// This defines the current scenario content without forcing a full runtime rewrite yet.
/// </summary>
[CreateAssetMenu(menuName = "Call Phase/Scenario Data", fileName = "NewCallPhaseScenario")]
public class CallPhaseScenarioData : ScriptableObject
{
    public const string DefaultScenarioResourcePath = "CallPhaseKitchenFireScenario";

    [Header("Metadata")]
    public string scenarioId = "scenario_id";
    public string displayName = "Call Phase Scenario";
    public string displayNameLocalizationKey;
    public string category = "Prototype";
    public string categoryLocalizationKey;
    [TextArea(2, 5)] public string description;
    public string descriptionLocalizationKey;

    [Header("Transcript")]
    public List<CallPhaseScenarioLineData> initialTranscriptLines = new List<CallPhaseScenarioLineData>();

    [Header("Follow-Up Flow")]
    public List<CallPhaseScenarioStepData> followUpSteps = new List<CallPhaseScenarioStepData>();
    public List<CallPhaseFollowUpQuestionOptionData> followUpQuestionPool = new List<CallPhaseFollowUpQuestionOptionData>();
    public CallPhaseFollowUpScoringConfig followUpScoringConfig;

    [Header("Incident Seed")]
    public CallPhaseScenarioIncidentSeedData incidentSeed = new CallPhaseScenarioIncidentSeedData();

    [Header("Victims")]
    [Min(0)] public int actualVictimCount;
    public bool dispatchEstimatedVictimCountKnown;
    [Min(0)] public int dispatchEstimatedVictimCountMin;
    [Min(0)] public int dispatchEstimatedVictimCountMax;

    [Header("Scenario Expectations")]
    public CallPhaseScenarioExpectedReportData expectedReport = new CallPhaseScenarioExpectedReportData();
    public CallPhaseScenarioAssessRiskData assessRisk = new CallPhaseScenarioAssessRiskData();
    public CallPhaseScenarioResultReviewData resultReview = new CallPhaseScenarioResultReviewData();

    public bool TryGetCallerLineDefinition(string lineText, out CallPhaseScenarioLineData lineData)
    {
        if (TryGetMatchingLine(initialTranscriptLines, lineText, out lineData))
        {
            return true;
        }

        for (int i = 0; i < followUpSteps.Count; i++)
        {
            CallPhaseScenarioStepData step = followUpSteps[i];
            if (step == null || step.callerLine == null || step.callerLine.speaker != TranscriptSpeakerType.Caller)
            {
                continue;
            }

            if (TextMatches(step.callerLine.text, lineText) || TextMatches(GetLocalizedLineText(step.callerLine), lineText))
            {
                lineData = step.callerLine;
                return true;
            }
        }

        lineData = null;
        return false;
    }

    public string GetExpectedFieldValue(string fieldId)
    {
        if (string.IsNullOrWhiteSpace(fieldId))
        {
            return string.Empty;
        }

        switch (fieldId)
        {
            case "Address":
                return expectedReport.address;
            case "fire_location":
                return expectedReport.fireLocation;
            case "OccupantRisk":
                return expectedReport.occupantRisk;
            case "hazard":
                return expectedReport.hazard;
            case "SpreadStatus":
                return expectedReport.spreadStatus;
            case "CallerSafety":
                return expectedReport.callerSafety;
            case "Severity":
                return !string.IsNullOrWhiteSpace(expectedReport.severity)
                    ? expectedReport.severity
                    : assessRisk.expectedSeverity;
            default:
                return string.Empty;
        }
    }

    public string GetLocalizedDisplayName()
    {
        return ResolveLocalizedMetadata(displayNameLocalizationKey, "name", displayName);
    }

    public string GetLocalizedCategory()
    {
        return ResolveLocalizedMetadata(categoryLocalizationKey, "category", category);
    }

    public string GetLocalizedDescription()
    {
        return ResolveLocalizedMetadata(descriptionLocalizationKey, "description", description);
    }

    public string GetLocalizedLineText(CallPhaseScenarioLineData lineData)
    {
        if (lineData == null)
        {
            return string.Empty;
        }

        string fallback = !string.IsNullOrWhiteSpace(lineData.text)
            ? lineData.text
            : string.Empty;
        string key = !string.IsNullOrWhiteSpace(lineData.textLocalizationKey)
            ? lineData.textLocalizationKey.Trim()
            : GetDefaultLineLocalizationKey(lineData.lineId);
        return !string.IsNullOrWhiteSpace(key)
            ? LanguageManager.Tr(key, fallback)
            : fallback;
    }

    public string GetLocalizedQuestionText(CallPhaseFollowUpQuestionOptionData questionOption)
    {
        if (questionOption == null)
        {
            return string.Empty;
        }

        string fallback = !string.IsNullOrWhiteSpace(questionOption.questionText)
            ? questionOption.questionText
            : string.Empty;
        string key = !string.IsNullOrWhiteSpace(questionOption.questionLocalizationKey)
            ? questionOption.questionLocalizationKey.Trim()
            : GetDefaultQuestionLocalizationKey(questionOption.questionId);
        return !string.IsNullOrWhiteSpace(key)
            ? LanguageManager.Tr(key, fallback)
            : fallback;
    }

    public string GetLocalizedDistractorCallerReply(CallPhaseFollowUpQuestionOptionData questionOption)
    {
        if (questionOption == null)
        {
            return string.Empty;
        }

        string fallback = !string.IsNullOrWhiteSpace(questionOption.distractorCallerReplyText)
            ? questionOption.distractorCallerReplyText
            : string.Empty;
        string key = !string.IsNullOrWhiteSpace(questionOption.distractorCallerReplyLocalizationKey)
            ? questionOption.distractorCallerReplyLocalizationKey.Trim()
            : GetDefaultQuestionReplyLocalizationKey(questionOption.questionId);
        return !string.IsNullOrWhiteSpace(key)
            ? LanguageManager.Tr(key, fallback)
            : fallback;
    }

    public string GetLocalizedSpanDisplayText(CallPhaseScenarioLineData lineData, CallPhaseExtractableSpanData spanData, int spanIndex)
    {
        if (spanData == null)
        {
            return string.Empty;
        }

        string fallback = !string.IsNullOrWhiteSpace(spanData.displayText)
            ? spanData.displayText
            : string.Empty;
        string key = !string.IsNullOrWhiteSpace(spanData.displayTextLocalizationKey)
            ? spanData.displayTextLocalizationKey.Trim()
            : GetDefaultSpanLocalizationKey(lineData != null ? lineData.lineId : string.Empty, spanIndex);
        return !string.IsNullOrWhiteSpace(key)
            ? LanguageManager.Tr(key, fallback)
            : fallback;
    }

    private bool TryGetMatchingLine(List<CallPhaseScenarioLineData> source, string lineText, out CallPhaseScenarioLineData lineData)
    {
        if (source != null)
        {
            for (int i = 0; i < source.Count; i++)
            {
                CallPhaseScenarioLineData candidate = source[i];
                if (candidate == null || candidate.speaker != TranscriptSpeakerType.Caller)
                {
                    continue;
                }

                if (TextMatches(candidate.text, lineText) || TextMatches(GetLocalizedLineText(candidate), lineText))
                {
                    lineData = candidate;
                    return true;
                }
            }
        }

        lineData = null;
        return false;
    }

    private bool TextMatches(string left, string right)
    {
        return string.Equals(
            left != null ? left.Trim() : string.Empty,
            right != null ? right.Trim() : string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveLocalizedMetadata(string explicitKey, string suffix, string fallback)
    {
        string key = !string.IsNullOrWhiteSpace(explicitKey)
            ? explicitKey.Trim()
            : GetDefaultMetadataLocalizationKey(suffix);
        return !string.IsNullOrWhiteSpace(key)
            ? LanguageManager.Tr(key, fallback)
            : fallback;
    }

    private string GetDefaultMetadataLocalizationKey(string suffix)
    {
        if (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(suffix))
        {
            return string.Empty;
        }

        return $"callphase.scenario.{NormalizeLocalizationToken(scenarioId)}.{suffix}";
    }

    private string GetDefaultLineLocalizationKey(string lineId)
    {
        if (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(lineId))
        {
            return string.Empty;
        }

        return $"callphase.scenario.{NormalizeLocalizationToken(scenarioId)}.line.{NormalizeLocalizationToken(lineId)}";
    }

    private string GetDefaultQuestionLocalizationKey(string questionId)
    {
        if (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(questionId))
        {
            return string.Empty;
        }

        return $"callphase.scenario.{NormalizeLocalizationToken(scenarioId)}.question.{NormalizeLocalizationToken(questionId)}";
    }

    private string GetDefaultQuestionReplyLocalizationKey(string questionId)
    {
        if (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(questionId))
        {
            return string.Empty;
        }

        return $"callphase.scenario.{NormalizeLocalizationToken(scenarioId)}.question_reply.{NormalizeLocalizationToken(questionId)}";
    }

    private string GetDefaultSpanLocalizationKey(string lineId, int spanIndex)
    {
        if (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(lineId) || spanIndex < 0)
        {
            return string.Empty;
        }

        return $"callphase.scenario.{NormalizeLocalizationToken(scenarioId)}.span.{NormalizeLocalizationToken(lineId)}_{spanIndex}";
    }

    private static string NormalizeLocalizationToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        char[] buffer = value.Trim().ToLowerInvariant().ToCharArray();
        for (int i = 0; i < buffer.Length; i++)
        {
            char character = buffer[i];
            if ((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9'))
            {
                continue;
            }

            buffer[i] = '_';
        }

        return new string(buffer);
    }
}

[Serializable]
public class CallPhaseScenarioLineData
{
    public string lineId;
    public TranscriptSpeakerType speaker = TranscriptSpeakerType.Caller;
    [TextArea(2, 5)] public string text;
    public string textLocalizationKey;
    public bool isExtractable;
    public bool startsAsActiveChunk;
    public bool isConfirmationLine;
    public List<CallPhaseExtractableSpanData> extractableSpans = new List<CallPhaseExtractableSpanData>();
}

[Serializable]
public class CallPhaseExtractableSpanData
{
    public string displayText;
    public string displayTextLocalizationKey;
    public string normalizedValue;
    public string targetFieldId;
    public string infoType;
    public bool exactSelection = true;
    public bool hasExtraContext;
}

[Serializable]
public class CallPhaseScenarioStepData
{
    public string stepId;
    public CallPhaseScenarioStepType stepType;
    public string triggerFieldId;
    public string triggerValue;
    public string confirmationFieldId;
    public string expectedConfirmedValue;
    public CallPhaseScenarioLineData operatorLine = new CallPhaseScenarioLineData
    {
        speaker = TranscriptSpeakerType.Operator
    };
    public CallPhaseScenarioLineData callerLine = new CallPhaseScenarioLineData
    {
        speaker = TranscriptSpeakerType.Caller,
        isExtractable = true,
        startsAsActiveChunk = true
    };
}

public enum CallPhaseScenarioStepType
{
    InformationCollection,
    ConfirmationReadBack
}

[Serializable]
public class CallPhaseFollowUpQuestionOptionData
{
    public string questionId;
    [TextArea(1, 4)] public string questionText;
    public string questionLocalizationKey;
    public CallPhaseFollowUpQuestionQuality quality;
    public string relatedFieldId;
    public bool suggestedWhenFieldMissing = true;
    public bool hideIfFieldAlreadyKnown = true;
    public string linkedStepId;
    [TextArea(1, 3)] public string distractorCallerReplyText;
    public string distractorCallerReplyLocalizationKey;
    public bool isDistractorQuestion;
    [TextArea(1, 3)] public string authorNote;
}

public enum CallPhaseFollowUpQuestionQuality
{
    Optimal,
    Acceptable,
    Poor
}

[Serializable]
public class CallPhaseScenarioExpectedReportData
{
    public string address;
    public string fireLocation;
    public string occupantRisk;
    public string hazard;
    public string spreadStatus;
    public string callerSafety;
    public string severity;
}

[Serializable]
public class CallPhaseScenarioIncidentSeedData
{
    public string fireOrigin = "Unknown";
    public string hazardType = "OrdinaryCombustibles";
    public bool requiresIsolation;
    [Range(0f, 1f)] public float initialFireIntensity = 0.65f;
    public int initialFireCount = 1;
    public string fireSpreadPreset = "Moderate";
    [Range(0f, 1f)] public float startSmokeDensity = 0.2f;
    public float smokeAccumulationMultiplier = 1f;
    public string ventilationPreset = "Neutral";
}

[Serializable]
public class CallPhaseScenarioAssessRiskData
{
    public List<string> requiredForAssessRisk = new List<string>();
    public List<string> recommendedForAssessRisk = new List<string>();
    public int minimumRecommendedCount = 1;
    public string expectedSeverity = "High";
}

[Serializable]
public class CallPhaseScenarioResultReviewData
{
    public List<CallPhaseScenarioScoredFieldData> correctnessFields = new List<CallPhaseScenarioScoredFieldData>();
    public List<string> criticalFieldIds = new List<string>();
    public List<CallPhaseScenarioScoredFieldData> expectedConfirmedFields = new List<CallPhaseScenarioScoredFieldData>();
    public List<CallPhaseScenarioScoredFieldData> readinessFields = new List<CallPhaseScenarioScoredFieldData>();
    public int severityCorrectScore = 20;
    public int severityPartialScore = 10;
    public string severityPartialValue = "Medium";
    public int followUpOptimalScore = 2;
    public int followUpAcceptableScore = 1;
    public int followUpPoorPenalty = 1;
    public int followUpMaxScore = 8;
    public int followUpGoodThreshold = 4;
    public int followUpMixedThreshold = 1;
    public int targetCallTimeSeconds = 90;
    public int acceptableCallTimeSeconds = 150;
    public int callTimeMaxScore = 4;
    public int strongCorrectnessThreshold = 32;
    public int readySubmissionThreshold = 18;
    public CallPhaseScenarioResultFeedbackData feedback = new CallPhaseScenarioResultFeedbackData();
}

[Serializable]
public class CallPhaseScenarioScoredFieldData
{
    public string fieldId;
    public string displayName;
    public int scoreWeight = 1;
    [TextArea(1, 3)] public string issueText;
}

[Serializable]
public class CallPhaseScenarioResultFeedbackData
{
    [TextArea(1, 3)] public string positiveSeverity = "Good risk assessment.";
    [TextArea(1, 3)] public string missingSeverity = "Severity assessment was missing.";
    [TextArea(1, 3)] public string incorrectSeverity = "Severity was lower than expected.";
    [TextArea(1, 3)] public string strongFollowUp = "You generally prioritized relevant follow-up questions.";
    [TextArea(1, 3)] public string mixedFollowUp = "Some follow-up questions were useful, but prioritization was uneven.";
    [TextArea(1, 3)] public string poorFollowUp = "Some follow-up questions were lower priority than the situation required.";
    [TextArea(1, 3)] public string allConfirmed = "Critical facts were confirmed.";
    [TextArea(1, 3)] public string noneConfirmed = "No critical facts were confirmed.";
    [TextArea(1, 3)] public string partialConfirmed = "More read-back confirmation was needed for critical facts.";
    [TextArea(1, 3)] public string strongCorrectness = "Key incident details matched the scenario well.";
    [TextArea(1, 3)] public string readyToSubmit = "Report was ready to submit.";
    [TextArea(1, 3)] public string incompleteSubmission = "Some report details were missing at submission.";
}
