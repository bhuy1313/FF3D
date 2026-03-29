using UnityEngine;

[CreateAssetMenu(
    fileName = "DeactivateMissionObjectAction",
    menuName = "TrueJourney/Missions/Actions/Deactivate Mission Object")]
public class DeactivateMissionObjectAction : MissionSceneObjectActionDefinition
{
    protected override void ExecuteAction(MissionActionExecutionContext context)
    {
        if (!TryResolveTarget(context, out GameObject targetObject) || targetObject == null)
        {
            return;
        }

        targetObject.SetActive(false);
    }
}
