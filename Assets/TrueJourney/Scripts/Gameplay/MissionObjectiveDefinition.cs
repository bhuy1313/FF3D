using UnityEngine;

public abstract class MissionObjectiveDefinition : ScriptableObject
{
    [Header("Objective")]
    [SerializeField] private string objectiveTitle;
    [SerializeField, TextArea] private string objectiveDescription;

    [Header("Scoring")]
    [SerializeField, Min(0)] private int scoreWeight = 10;
    [SerializeField] private MissionObjectiveScoreMode scoreMode = MissionObjectiveScoreMode.Binary;

    public string ObjectiveTitle => objectiveTitle;
    public string ObjectiveDescription => objectiveDescription;
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

    protected string ResolveTitle(string fallbackTitle)
    {
        return string.IsNullOrWhiteSpace(objectiveTitle) ? fallbackTitle : objectiveTitle;
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
