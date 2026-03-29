using UnityEngine;

public abstract class MissionSceneObjectActionDefinition : MissionActionDefinition
{
    [Header("Target")]
    [SerializeField] private string targetKey;

    protected string TargetKey => targetKey;

    protected bool TryResolveTarget(MissionActionExecutionContext context, out GameObject targetObject)
    {
        targetObject = null;
        if (string.IsNullOrWhiteSpace(targetKey))
        {
            return false;
        }

        return context.TryResolveSceneObject(targetKey, out targetObject);
    }
}
