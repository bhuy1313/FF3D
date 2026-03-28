using UnityEngine;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    internal void AbortActiveRescueOrder()
    {
        if (behaviorContext != null)
        {
            behaviorContext.ClearRescueOrder();
        }

        ClearRescueRuntimeState();
        ClearRouteFireRuntime();
        if (navMeshAgent != null)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
        }
    }

    internal void CompleteActiveRescueOrder()
    {
        if (behaviorContext != null)
        {
            behaviorContext.ClearRescueOrder();
        }

        ClearRescueRuntimeState();
        ClearRouteFireRuntime();
        if (navMeshAgent != null)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
        }
    }

    private void ProcessRescueOrder()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh || behaviorContext == null || !behaviorContext.TryGetRescueOrder(out Vector3 orderPoint))
        {
            return;
        }

        LogRescueActivity($"rescue-order:{FormatFlowVectorKey(orderPoint)}", $"Received Rescue order to {orderPoint}.");

        IRescuableTarget rescueTarget = GetCommittedRescueTarget();
        if (rescueTarget == null)
        {
            rescueTarget = ResolveRescueTarget(orderPoint);
        }

        if (rescueTarget == null)
        {
            LogRescueActivity("rescue-notfound", "No rescue target found.");
            behaviorContext.ClearRescueOrder();
            currentRescueTarget = null;
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
            return;
        }

        currentRescueTarget = rescueTarget;
        currentSafeZoneTarget = ResolveNearestSafeZone(rescueTarget.GetWorldPosition());

        if (!rescueTarget.NeedsRescue)
        {
            LogRescueActivity("rescue-complete", "Rescue completed.");
            behaviorContext.ClearRescueOrder();
            currentRescueTarget = null;
            currentSafeZoneTarget = null;
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
            return;
        }

        if (currentSafeZoneTarget == null)
        {
            LogRescueActivity("rescue-no-safezone", "No safe zone found.");
            behaviorContext.ClearRescueOrder();
            currentRescueTarget = null;
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
            return;
        }

        if (rescueTarget.IsCarried && rescueTarget.ActiveRescuer == gameObject)
        {
            Vector3 safeZonePosition = currentSafeZoneTarget.GetWorldPosition();
            float distanceToSafeZone = GetHorizontalDistance(transform.position, safeZonePosition);
            bool hasReachedSafeZone =
                currentSafeZoneTarget.ContainsPoint(transform.position) ||
                distanceToSafeZone <= rescueSafeZoneArrivalDistance;

            if (!hasReachedSafeZone)
            {
                LogRescueActivity("rescue-carry", "Carrying victim to safe zone.");
                MoveTo(safeZonePosition);
                return;
            }

            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;
            Vector3 fallbackDropPosition = transform.position + transform.TransformDirection(rescueDropOffset);
            Vector3 dropPosition = currentSafeZoneTarget.GetDropPoint(fallbackDropPosition);
            rescueTarget.CompleteRescueAt(dropPosition);
            LogRescueActivity("rescue-complete", "Rescue completed.");
            behaviorContext.ClearRescueOrder();
            currentRescueTarget = null;
            currentSafeZoneTarget = null;
            return;
        }

        Vector3 targetPosition = rescueTarget.GetWorldPosition();
        float horizontalDistance = GetHorizontalDistance(transform.position, targetPosition);
        if (horizontalDistance > rescueInteractionDistance)
        {
            LogRescueActivity("rescue-move", "Moving to victim.");
            MoveTo(targetPosition);
            return;
        }

        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = true;
        AimTowards(targetPosition);

        if (rescueTarget.IsRescueInProgress && rescueTarget.ActiveRescuer == gameObject)
        {
            LogRescueActivity("rescue-start", "Started rescue.");
            return;
        }

        if (rescueTarget.TryBeginCarry(gameObject, GetRescueCarryAnchor()))
        {
            LogRescueActivity("rescue-pickup", "Picked up victim.");
            return;
        }

        currentRescueTarget = null;
    }

    private IRescuableTarget ResolveRescueTarget(Vector3 orderPoint)
    {
        return runtimeDecisionService != null
            ? runtimeDecisionService.ResolveRescueTarget(orderPoint, currentRescueTarget, gameObject, rescueSearchRadius)
            : null;
    }

    private IRescuableTarget GetCommittedRescueTarget()
    {
        if (currentRescueTarget == null || !currentRescueTarget.NeedsRescue)
        {
            return null;
        }

        if (currentRescueTarget.ActiveRescuer != gameObject)
        {
            return null;
        }

        if (!currentRescueTarget.IsCarried && !currentRescueTarget.IsRescueInProgress)
        {
            return null;
        }

        return currentRescueTarget;
    }

    private ISafeZoneTarget ResolveNearestSafeZone(Vector3 fromPosition)
    {
        return runtimeDecisionService != null
            ? runtimeDecisionService.ResolveNearestSafeZone(fromPosition, currentSafeZoneTarget)
            : null;
    }

    private Transform GetRescueCarryAnchor()
    {
        if (rescueCarryAnchor != null)
        {
            return rescueCarryAnchor;
        }

        if (runtimeRescueCarryAnchor == null)
        {
            GameObject anchorObject = new GameObject("RescueCarryAnchor");
            runtimeRescueCarryAnchor = anchorObject.transform;
            runtimeRescueCarryAnchor.SetParent(transform, false);
            runtimeRescueCarryAnchor.localPosition = rescueCarryLocalPosition;
            runtimeRescueCarryAnchor.localRotation = Quaternion.identity;
        }

        return runtimeRescueCarryAnchor;
    }

    private void ClearRescueRuntimeState()
    {
        currentRescueTarget = null;
        currentSafeZoneTarget = null;
        activityDebug?.ResetRescue();
    }
}
