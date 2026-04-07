using UnityEngine;

public abstract class MissionObjectiveDefinition : ScriptableObject
{
    [Header("Objective")]
    [SerializeField] private string objectiveTitleLocalizationKey;
    [SerializeField] private string objectiveTitle;
    [SerializeField] private string objectiveDescriptionLocalizationKey;
    [SerializeField, TextArea] private string objectiveDescription;

    [Header("Scoring")]
    [SerializeField, Min(0)] private int scoreWeight = 10;
    [SerializeField] private MissionObjectiveScoreMode scoreMode = MissionObjectiveScoreMode.Binary;

    public string ObjectiveTitle => ResolveTitle();
    public string ObjectiveDescription => ResolveDescription();
    public int ScoreWeight => Mathf.Max(0, scoreWeight);
    public MissionObjectiveScoreMode ScoreMode => scoreMode;

    public virtual void CollectTargets(MissionRuntimeSceneData sceneData)
    {
    }

    public virtual MissionObjectiveEvaluation Evaluate(MissionObjectiveContext context)
    {
        return Evaluate(context.Snapshot);
    }

    public virtual MissionObjectiveScoreEvaluation EvaluateScore(MissionObjectiveContext context, MissionObjectiveEvaluation evaluation)
    {
        if (ScoreWeight <= 0 || !evaluation.IsRelevant)
        {
            return new MissionObjectiveScoreEvaluation(0, 0, string.Empty);
        }

        switch (scoreMode)
        {
            case MissionObjectiveScoreMode.Progressive:
                return CreateProgressiveScoreEvaluation(evaluation.IsComplete ? 1f : 0f);
            default:
                return CreateBinaryScoreEvaluation(evaluation.IsComplete);
        }
    }

    public abstract MissionObjectiveEvaluation Evaluate(MissionProgressSnapshot snapshot);

    protected string ResolveTitle(string fallbackTitle = "")
    {
        string resolvedFallback = !string.IsNullOrWhiteSpace(objectiveTitle) ? objectiveTitle : fallbackTitle;
        return MissionLocalization.Get(objectiveTitleLocalizationKey, resolvedFallback);
    }

    protected string ResolveDescription(string fallbackDescription = "")
    {
        string resolvedFallback = !string.IsNullOrWhiteSpace(objectiveDescription) ? objectiveDescription : fallbackDescription;
        return MissionLocalization.Get(objectiveDescriptionLocalizationKey, resolvedFallback);
    }

    protected string ResolveText(string localizationKey, string fallbackText, string secondaryFallback = "")
    {
        string resolvedFallback = !string.IsNullOrWhiteSpace(fallbackText) ? fallbackText : secondaryFallback;
        return MissionLocalization.Get(localizationKey, resolvedFallback);
    }

    protected MissionObjectiveScoreEvaluation CreateBinaryScoreEvaluation(bool isComplete, string summary = null)
    {
        int maxScore = ScoreWeight;
        if (maxScore <= 0)
        {
            return new MissionObjectiveScoreEvaluation(0, 0, string.Empty);
        }

        int score = isComplete ? maxScore : 0;
        return new MissionObjectiveScoreEvaluation(score, maxScore, summary);
    }

    protected MissionObjectiveScoreEvaluation CreateProgressiveScoreEvaluation(float normalizedProgress, string summary = null)
    {
        int maxScore = ScoreWeight;
        if (maxScore <= 0)
        {
            return new MissionObjectiveScoreEvaluation(0, 0, string.Empty);
        }

        int score = Mathf.Clamp(Mathf.RoundToInt(maxScore * Mathf.Clamp01(normalizedProgress)), 0, maxScore);
        return new MissionObjectiveScoreEvaluation(score, maxScore, summary);
    }
}
