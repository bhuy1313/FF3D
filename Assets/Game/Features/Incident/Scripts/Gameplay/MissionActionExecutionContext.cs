using UnityEngine;

public readonly struct MissionActionExecutionContext
{
    public MissionActionExecutionContext(
        IncidentMissionSystem missionSystem,
        MissionDefinition missionDefinition,
        MissionActionTrigger trigger)
    {
        MissionSystem = missionSystem;
        MissionDefinition = missionDefinition;
        Trigger = trigger;
    }

    public IncidentMissionSystem MissionSystem { get; }
    public MissionDefinition MissionDefinition { get; }
    public MissionActionTrigger Trigger { get; }

    public bool TryResolveSceneObject(string key, out GameObject targetObject)
    {
        targetObject = null;
        return MissionSystem != null && MissionSystem.TryResolveSceneObject(key, out targetObject);
    }
}
