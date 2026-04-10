using TrueJourney.BotBehavior;
using UnityEngine;
using UnityEngine.AI;

public sealed class BotRescueController
{
    private readonly BotRuntimeDecisionService decisionService;
    private const float ReacquireTargetDistanceSlack = 0.35f;

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

        owner.SetRescueSubtask(BotRescueSubtask.AcquireTarget, "Acquiring rescue target.");
        IRescuableTarget rescueTarget = decisionService.ResolveRescueTarget(orderPoint, owner.CurrentRescueTarget, owner.gameObject, rescueSearchRadius);
        if (rescueTarget == null)
        {
            owner.LogRescueActivityMessage("rescue-notfound", "No rescue target found.");
            owner.FailActiveRescueOrder("No rescue target found.", BotTaskStatus.Blocked);
            return;
        }

        owner.CurrentRescueTarget = rescueTarget;
        owner.SetRescueSubtask(BotRescueSubtask.AcquireSafeZone, "Acquiring safe zone.");
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
            owner.FailActiveRescueOrder("No safe zone found for rescue.", BotTaskStatus.Blocked);
            return;
        }

        if (rescueTarget.IsCarried && rescueTarget.ActiveRescuer == owner.gameObject)
        {
            owner.SetRescueSubtask(BotRescueSubtask.CarryToSafeZone, "Carrying casualty to safe zone.");
            owner.PrepareCarryRescueCommand();

            Vector3 safeZonePosition = owner.CurrentSafeZoneTarget.GetWorldPosition();
            float distanceToSafeZone = GetHorizontalDistance(owner.transform.position, safeZonePosition);
            bool hasReachedSafeZone =
                owner.CurrentSafeZoneTarget.ContainsPoint(owner.transform.position) ||
                distanceToSafeZone <= rescueSafeZoneArrivalDistance;

            if (!hasReachedSafeZone)
            {
                owner.LogRescueActivityMessage("rescue-carry", "Carrying casualty to safe zone.");
                if (!owner.MoveToRescueCarrySafeZoneCommand(safeZonePosition))
                {
                    owner.FailActiveRescueOrder("Failed to path to rescue safe zone.", BotTaskStatus.Blocked);
                }
                return;
            }

            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;
            Vector3 fallbackDropPosition = owner.transform.position + owner.transform.TransformDirection(rescueDropOffset);
            Vector3 dropPosition = owner.CurrentSafeZoneTarget.GetDropPoint(fallbackDropPosition);
            owner.SetRescueSubtask(BotRescueSubtask.CompleteRescue, "Completing rescue at safe zone.");
            rescueTarget.CompleteRescueAt(dropPosition);
            owner.LogRescueActivityMessage("rescue-complete", "Rescue completed.");
            owner.CompleteActiveRescueOrder();
            return;
        }

        if (rescueTarget.IsCarried && rescueTarget.ActiveRescuer != owner.gameObject)
        {
            owner.LogRescueActivityMessage("rescue-reacquire", "Assigned casualty is already being carried by another rescuer.");
            owner.ReacquireRescueTarget("Recovering after losing assigned casualty.");
            return;
        }

        Vector3 targetPosition = rescueTarget.GetWorldPosition();
        float horizontalDistance = GetHorizontalDistance(owner.transform.position, targetPosition);
        if (horizontalDistance > rescueInteractionDistance)
        {
            owner.SetRescueSubtask(BotRescueSubtask.MoveToTarget, "Moving to casualty.");
            owner.LogRescueActivityMessage("rescue-move", "Moving to casualty.");
            if (!owner.MoveToCommand(targetPosition))
            {
                owner.FailActiveRescueOrder("Failed to path to casualty.", BotTaskStatus.Blocked);
            }
            return;
        }

        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = true;
        owner.AimTowardsPoint(targetPosition);

        if (rescueTarget.RequiresStabilization)
        {
            owner.SetRescueSubtask(BotRescueSubtask.StabilizeTarget, "Stabilizing casualty.");
            if (rescueTarget.IsRescueInProgress && rescueTarget.ActiveRescuer == owner.gameObject)
            {
                owner.LogRescueActivityMessage("rescue-stabilize", "Stabilizing casualty.");
                return;
            }

            if (rescueTarget.IsRescueInProgress && rescueTarget.ActiveRescuer != owner.gameObject)
            {
                owner.ReacquireRescueTarget("Assigned casualty is being stabilized by another rescuer.");
                return;
            }

            if (rescueTarget.TryStabilize(owner.gameObject))
            {
                owner.LogRescueActivityMessage("rescue-stabilize", "Started casualty stabilization.");
                return;
            }

            if (GetHorizontalDistance(owner.transform.position, targetPosition) <= rescueInteractionDistance + ReacquireTargetDistanceSlack)
            {
                owner.FailActiveRescueOrder("Failed to start casualty stabilization.", BotTaskStatus.Failed);
                return;
            }

            owner.ReacquireRescueTarget("Recovering after failed stabilization attempt.");
            return;
        }

        owner.SetRescueSubtask(BotRescueSubtask.BeginCarry, "Beginning casualty carry.");
        if (rescueTarget.IsRescueInProgress && rescueTarget.ActiveRescuer == owner.gameObject)
        {
            owner.LogRescueActivityMessage("rescue-start", "Starting rescue.");
            return;
        }

        if (rescueTarget.IsRescueInProgress && rescueTarget.ActiveRescuer != owner.gameObject)
        {
            owner.ReacquireRescueTarget("Assigned casualty is being handled by another rescuer.");
            return;
        }

        owner.PrepareCarryRescueCommand();

        if (rescueTarget.TryBeginCarry(owner.gameObject, owner.EnsureRescueCarryAnchor()))
        {
            owner.PrepareCarryRescueCommand();
            owner.LogRescueActivityMessage("rescue-pickup", "Picked up casualty.");
            return;
        }

        owner.FailActiveRescueOrder("Failed to begin carrying casualty.", BotTaskStatus.Failed);
    }

    private static float GetHorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
