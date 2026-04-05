using UnityEngine;

[CreateAssetMenu(
    fileName = "ActivateExplosiveMissionAction",
    menuName = "TrueJourney/Missions/Actions/Activate Explosive")]
public class ActivateExplosiveMissionAction : MissionSceneObjectActionDefinition
{
    protected override void ExecuteAction(MissionActionExecutionContext context)
    {
        if (!TryResolveTarget(context, out GameObject targetObject) || targetObject == null)
        {
            return;
        }

        if (!targetObject.TryGetComponent(out Explosive explosive))
        {
            explosive = targetObject.GetComponentInParent<Explosive>();
        }

        explosive?.ExplodeNow();
    }
}
