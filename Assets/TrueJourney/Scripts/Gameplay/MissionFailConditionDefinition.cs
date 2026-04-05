using UnityEngine;

public abstract class MissionFailConditionDefinition : ScriptableObject
{
    [Header("Fail Condition")]
    [SerializeField] private string failConditionTitle;
    [SerializeField, TextArea] private string failConditionDescription;

    public string FailConditionTitle => failConditionTitle;
    public string FailConditionDescription => failConditionDescription;

    public virtual void CollectTargets(MissionRuntimeSceneData sceneData)
    {
    }

    public abstract MissionFailConditionEvaluation Evaluate(MissionFailConditionContext context);

    public virtual bool TryGetTimeLimitSeconds(out float timeLimitSeconds)
    {
        timeLimitSeconds = 0f;
        return false;
    }

    protected string ResolveTitle(string fallbackTitle)
    {
        return string.IsNullOrWhiteSpace(failConditionTitle) ? fallbackTitle : failConditionTitle;
    }
}
