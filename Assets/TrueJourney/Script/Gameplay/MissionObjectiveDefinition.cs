using UnityEngine;

public abstract class MissionObjectiveDefinition : ScriptableObject
{
    [Header("Objective")]
    [SerializeField] private string objectiveTitle;
    [SerializeField, TextArea] private string objectiveDescription;

    public string ObjectiveTitle => objectiveTitle;
    public string ObjectiveDescription => objectiveDescription;

    public virtual void CollectTargets(MissionRuntimeSceneData sceneData)
    {
    }

    public virtual MissionObjectiveEvaluation Evaluate(MissionObjectiveContext context)
    {
        return Evaluate(context.Snapshot);
    }

    public abstract MissionObjectiveEvaluation Evaluate(MissionProgressSnapshot snapshot);

    protected string ResolveTitle(string fallbackTitle)
    {
        return string.IsNullOrWhiteSpace(objectiveTitle) ? fallbackTitle : objectiveTitle;
    }
}
