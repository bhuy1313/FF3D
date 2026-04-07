using UnityEngine;

public abstract class MissionActionDefinition : ScriptableObject
{
    [Header("Action")]
    [SerializeField] private string actionTitle;
    [SerializeField, TextArea] private string actionDescription;

    public string ActionTitle => actionTitle;
    public string ActionDescription => actionDescription;

    public void Execute(MissionActionExecutionContext context)
    {
        ExecuteAction(context);
    }

    protected abstract void ExecuteAction(MissionActionExecutionContext context);

    protected string ResolveTitle(string fallbackTitle)
    {
        return string.IsNullOrWhiteSpace(actionTitle) ? fallbackTitle : actionTitle;
    }
}
