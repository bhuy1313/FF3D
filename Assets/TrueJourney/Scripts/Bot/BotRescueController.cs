using TrueJourney.BotBehavior;
using UnityEngine;
using UnityEngine.AI;

public sealed class BotRescueController
{
    private readonly BotRuntimeDecisionService decisionService;

    public BotRescueController(BotRuntimeDecisionService decisionService)
    {
        this.decisionService = decisionService;
    }

    public void Tick(
        BotCommandAgent owner,
        NavMeshAgent navMeshAgent,
        BotBehaviorContext behaviorContext,
        float rescueSearchRadius,
        float rescueInteractionDistance,
        float rescueSafeZoneArrivalDistance,
        Vector3 rescueDropOffset)
    {
        if (navMeshAgent == null ||
            !navMeshAgent.enabled ||
            !navMeshAgent.isOnNavMesh ||
            behaviorContext == null ||
            !behaviorContext.TryGetRescueOrder(out Vector3 orderPoint))
        {
            return;
        }

        owner.LogRescueActivityMessage(
            $"rescue-order:{owner.FormatFlowVectorKeyForLog(orderPoint)}",
            $"Received Rescue order to {orderPoint}.");

        IRescuableTarget rescueTarget = decisionService.ResolveRescueTarget(orderPoint, owner.CurrentRescueTarget, owner.gameObject, rescueSearchRadius);
        if (rescueTarget == null)
        {
            owner.LogRescueActivityMessage("rescue-notfound", "No rescue target found.");
            owner.AbortActiveRescueOrder();
            return;
        }

        owner.CurrentRescueTarget = rescueTarget;
        owner.CurrentSafeZoneTarget = decisionService.ResolveNearestSafeZone(rescueTarget.GetWorldPosition(), owner.CurrentSafeZoneTarget);

        if (!rescueTarget.NeedsRescue)
        {
            owner.LogRescueActivityMessage("rescue-complete", "Rescue completed.");
            owner.CompleteActiveRescueOrder();
            return;
        }

        if (owner.CurrentSafeZoneTarget == null)
        {
            owner.LogRescueActivityMessage("rescue-no-safezone", "No safe zone found.");
            owner.AbortActiveRescueOrder();
            return;
        }

        if (rescueTarget.IsCarried && rescueTarget.ActiveRescuer == owner.gameObject)
        {
            owner.PrepareCarryRescueCommand();

            Vector3 safeZonePosition = owner.CurrentSafeZoneTarget.GetWorldPosition();
            float distanceToSafeZone = GetHorizontalDistance(owner.transform.position, safeZonePosition);
            bool hasReachedSafeZone =
                owner.CurrentSafeZoneTarget.ContainsPoint(owner.transform.position) ||
                distanceToSafeZone <= rescueSafeZoneArrivalDistance;

            if (!hasReachedSafeZone)
            {
                owner.LogRescueActivityMessage("rescue-carry", "Carrying casualty to safe zone.");
                owner.MoveToRescueCarrySafeZoneCommand(safeZonePosition);
                return;
            }

            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;
            Vector3 fallbackDropPosition = owner.transform.position + owner.transform.TransformDirection(rescueDropOffset);
            Vector3 dropPosition = owner.CurrentSafeZoneTarget.GetDropPoint(fallbackDropPosition);
            rescueTarget.CompleteRescueAt(dropPosition);
            owner.LogRescueActivityMessage("rescue-complete", "Rescue completed.");
            owner.CompleteActiveRescueOrder();
            return;
        }

        Vector3 targetPosition = rescueTarget.GetWorldPosition();
        float horizontalDistance = GetHorizontalDistance(owner.transform.position, targetPosition);
        if (horizontalDistance > rescueInteractionDistance)
        {
            owner.LogRescueActivityMessage("rescue-move", "Moving to casualty.");
            owner.MoveToCommand(targetPosition);
            return;
        }

        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = true;
        owner.AimTowardsPoint(targetPosition);

        if (rescueTarget.RequiresStabilization)
        {
            if (rescueTarget.IsRescueInProgress && rescueTarget.ActiveRescuer == owner.gameObject)
            {
                owner.LogRescueActivityMessage("rescue-stabilize", "Stabilizing casualty.");
                return;
            }

            if (rescueTarget.TryStabilize(owner.gameObject))
            {
                owner.LogRescueActivityMessage("rescue-stabilize", "Started casualty stabilization.");
                return;
            }

            owner.CurrentRescueTarget = null;
            return;
        }

        if (rescueTarget.IsRescueInProgress && rescueTarget.ActiveRescuer == owner.gameObject)
        {
            owner.LogRescueActivityMessage("rescue-start", "Starting rescue.");
            return;
        }

        owner.PrepareCarryRescueCommand();

        if (rescueTarget.TryBeginCarry(owner.gameObject, owner.EnsureRescueCarryAnchor()))
        {
            owner.PrepareCarryRescueCommand();
            owner.LogRescueActivityMessage("rescue-pickup", "Picked up casualty.");
            return;
        }

        owner.CurrentRescueTarget = null;
    }

    private static float GetHorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
