using UnityEngine;

public readonly struct MissionActionExecutionContext
{
    public MissionActionExecutionContext(
        IncidentMissionSystem missionSystem,
        MissionDefinition missionDefinition,
        MissionStageDefinition stageDefinition,
        int stageIndex,
        string stageId,
        MissionActionTrigger trigger)
    {
        MissionSystem = missionSystem;
        MissionDefinition = missionDefinition;
        StageDefinition = stageDefinition;
        StageIndex = stageIndex;
        StageId = stageId;
        Trigger = trigger;
    }

    public IncidentMissionSystem MissionSystem { get; }
    public MissionDefinition MissionDefinition { get; }
    public MissionStageDefinition StageDefinition { get; }
    public int StageIndex { get; }
    public string StageId { get; }
    public MissionActionTrigger Trigger { get; }

    public bool TryResolveSceneObject(string key, out GameObject targetObject)
    {
        targetObject = null;
        return MissionSystem != null && MissionSystem.TryResolveSceneObject(key, out targetObject);
    }
}
