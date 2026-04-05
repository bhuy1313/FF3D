using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "MissionStageDefinition",
    menuName = "TrueJourney/Missions/Mission Stage")]
public class MissionStageDefinition : ScriptableObject
{
    [Header("Stage")]
    [SerializeField] private string stageId = "stage";
    [SerializeField] private string stageTitle = "Mission Stage";
    [SerializeField, TextArea] private string stageDescription;
    [SerializeField, Min(0f)] private float nextStageDelaySeconds;

    [Header("Objectives")]
    [SerializeField] private List<MissionObjectiveDefinition> objectives = new List<MissionObjectiveDefinition>();

    [Header("Actions")]
    [SerializeField] private List<MissionActionDefinition> onStageStartedActions = new List<MissionActionDefinition>();
    [SerializeField] private List<MissionActionDefinition> onStageCompletedActions = new List<MissionActionDefinition>();

    public string StageId => stageId;
    public string StageTitle => stageTitle;
    public string StageDescription => stageDescription;
    public float NextStageDelaySeconds => Mathf.Max(0f, nextStageDelaySeconds);

    public void CollectObjectives(List<MissionObjectiveDefinition> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        if (objectives == null)
        {
            return;
        }

        for (int i = 0; i < objectives.Count; i++)
        {
            MissionObjectiveDefinition objective = objectives[i];
            if (objective != null)
            {
                results.Add(objective);
            }
        }
    }

    public void ExecuteActions(MissionActionExecutionContext context)
    {
        List<MissionActionDefinition> actions = context.Trigger == MissionActionTrigger.StageStarted
            ? onStageStartedActions
            : onStageCompletedActions;

        if (actions == null)
        {
            return;
        }

        for (int i = 0; i < actions.Count; i++)
        {
            MissionActionDefinition action = actions[i];
            if (action != null)
            {
                action.Execute(context);
            }
        }
    }
}
