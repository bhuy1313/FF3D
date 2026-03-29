using UnityEngine;

[CreateAssetMenu(
    fileName = "ToggleMissionObjectAction",
    menuName = "TrueJourney/Missions/Actions/Toggle Mission Object")]
public class ToggleMissionObjectAction : MissionSceneObjectActionDefinition
{
    protected override void ExecuteAction(MissionActionExecutionContext context)
    {
        if (!TryResolveTarget(context, out GameObject targetObject) || targetObject == null)
        {
            return;
        }

        targetObject.SetActive(!targetObject.activeSelf);
    }
}
