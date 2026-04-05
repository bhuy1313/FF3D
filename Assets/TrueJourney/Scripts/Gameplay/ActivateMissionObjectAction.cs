using UnityEngine;

[CreateAssetMenu(
    fileName = "ActivateMissionObjectAction",
    menuName = "TrueJourney/Missions/Actions/Activate Mission Object")]
public class ActivateMissionObjectAction : MissionSceneObjectActionDefinition
{
    protected override void ExecuteAction(MissionActionExecutionContext context)
    {
        if (!TryResolveTarget(context, out GameObject targetObject) || targetObject == null)
        {
            return;
        }

        targetObject.SetActive(true);
    }
}
