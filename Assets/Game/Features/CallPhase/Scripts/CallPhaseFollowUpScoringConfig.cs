using UnityEngine;

[CreateAssetMenu(menuName = "Call Phase/Follow-Up Scoring Config", fileName = "CallPhaseFollowUpScoringConfig")]
public class CallPhaseFollowUpScoringConfig : ScriptableObject
{
    public const string DefaultResourcePath = "CallPhaseFollowUpScoringConfig";

    [Header("Result Review Scoring")]
    [Min(0)] public int optimalQuestionScore = 2;
    [Min(0)] public int acceptableQuestionScore = 1;
    [Min(0)] public int poorQuestionPenalty = 1;
    [Min(0)] public int followUpMaxScore = 8;
    [Min(0)] public int followUpGoodThreshold = 4;
    [Min(0)] public int followUpMixedThreshold = 1;

    [Header("Question Priority Weighting")]
    public int currentLinkedWeight = 100;
    public int unresolvedLinkedWeight = 50;
    public int missingRelatedFieldWeight = 20;
    public int suggestedWhenMissingWeight = 20;
    public int nonDistractorWeight = 8;

    [Header("Recent Repeat Penalty")]
    [Min(1)] public int rememberedQuestionCount = 8;
    [Min(0)] public int repeatPenaltyMinimum = 10;
    [Min(0)] public int repeatPenaltyMaximum = 40;
    [Min(1)] public int repeatPenaltyDistanceStep = 5;
}

