using UnityEngine;

public abstract class MissionFailConditionDefinition : ScriptableObject
{
    [Header("Fail Condition")]
    [SerializeField] private string failConditionTitleLocalizationKey;
    [SerializeField] private string failConditionTitle;
    [SerializeField] private string failConditionDescriptionLocalizationKey;
    [SerializeField, TextArea] private string failConditionDescription;

    public string FailConditionTitle => ResolveTitle();
    public string FailConditionDescription => ResolveDescription();

    public virtual void CollectTargets(MissionRuntimeSceneData sceneData)
    {
    }

    public abstract MissionFailConditionEvaluation Evaluate(MissionFailConditionContext context);

    public virtual bool TryGetTimeLimitSeconds(out float timeLimitSeconds)
    {
        timeLimitSeconds = 0f;
        return false;
    }

    protected string ResolveTitle(string fallbackTitle = "")
    {
        string resolvedFallback = !string.IsNullOrWhiteSpace(failConditionTitle) ? failConditionTitle : fallbackTitle;
        return MissionLocalization.Get(failConditionTitleLocalizationKey, resolvedFallback);
    }

    protected string ResolveDescription(string fallbackDescription = "")
    {
        string resolvedFallback = !string.IsNullOrWhiteSpace(failConditionDescription) ? failConditionDescription : fallbackDescription;
        return MissionLocalization.Get(failConditionDescriptionLocalizationKey, resolvedFallback);
    }

    protected string ResolveText(string localizationKey, string fallbackText, string secondaryFallback = "")
    {
        string resolvedFallback = !string.IsNullOrWhiteSpace(fallbackText) ? fallbackText : secondaryFallback;
        return MissionLocalization.Get(localizationKey, resolvedFallback);
    }
}
