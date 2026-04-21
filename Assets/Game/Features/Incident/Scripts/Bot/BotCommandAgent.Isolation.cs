using UnityEngine;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private bool IsHazardIsolationCommandActive()
    {
        return behaviorContext != null &&
               behaviorContext.TryGetCommandIntentSnapshot(out BotCommandIntentPayload payload) &&
               payload.CommandType == BotCommandType.Isolate;
    }

    private bool HandleUnavailableHazardIsolationTarget()
    {
        if (hazardIsolationUnavailableSinceTime < 0f)
        {
            hazardIsolationUnavailableSinceTime = Time.time;
        }

        if (Time.time - hazardIsolationUnavailableSinceTime < Mathf.Max(0f, hazardIsolationUnavailableTimeout))
        {
            return false;
        }

        if (hazardIsolationUnavailableRetryCount < Mathf.Max(0, hazardIsolationMaxUnavailableRetries))
        {
            hazardIsolationUnavailableRetryCount++;
            hazardIsolationUnavailableSinceTime = -1f;
            SetCurrentHazardIsolationTarget(null);

            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.ResetPath();
                navMeshAgent.isStopped = true;
            }

            return false;
        }

        AbortHazardIsolationOrder("Hazard isolation target remained unavailable for interaction.");
        return true;
    }

    private void ResetHazardIsolationUnavailableState()
    {
        hazardIsolationUnavailableSinceTime = -1f;
        hazardIsolationUnavailableRetryCount = 0;
    }

    private bool TryResolveHazardIsolationTarget(Vector3 orderPoint, out IBotHazardIsolationTarget target)
    {
        target = null;
        if (CanTrackHazardIsolationTarget(currentHazardIsolationTarget))
        {
            target = currentHazardIsolationTarget;
            return true;
        }

        if (runtimeDecisionService == null)
        {
            return false;
        }

        target = runtimeDecisionService.ResolveNearestHazardIsolationTarget(
            orderPoint,
            gameObject,
            Mathf.Max(1f, hazardIsolationSearchRadius));
        return IsUsableHazardIsolationTarget(target);
    }

    private bool TryGetHazardIsolationIntent(out BotCommandIntentPayload intent)
    {
        intent = default;
        if (behaviorContext == null || !behaviorContext.TryGetCommandIntentSnapshot(out BotCommandIntentPayload payload))
        {
            return false;
        }

        if (payload.CommandType != BotCommandType.Isolate)
        {
            return false;
        }

        intent = payload;
        return true;
    }

    private bool IsUsableHazardIsolationTarget(IBotHazardIsolationTarget target)
    {
        return target != null &&
               target.IsHazardActive &&
               target.IsInteractionAvailable &&
               !BotRuntimeRegistry.Reservations.IsReservedByOther(target, gameObject);
    }

    private bool CanTrackHazardIsolationTarget(IBotHazardIsolationTarget target)
    {
        return target != null &&
               target.IsHazardActive &&
               !BotRuntimeRegistry.Reservations.IsReservedByOther(target, gameObject);
    }

    private bool TryGetHazardIsolationComponent(IBotHazardIsolationTarget target, out Component component)
    {
        component = target as Component;
        return component != null;
    }

    private static bool TryGetHazardIsolationInteractable(Component targetComponent, out IInteractable interactable)
    {
        interactable = null;
        if (targetComponent == null)
        {
            return false;
        }

        interactable = targetComponent.GetComponent(typeof(IInteractable)) as IInteractable;
        return interactable != null;
    }

    private bool IsNearHazardIsolationPoint(Vector3 point)
    {
        float threshold = Mathf.Max(
            hazardIsolationInteractionDistance,
            behaviorContext != null ? behaviorContext.ArrivalDistance : 0.35f);
        return (point - transform.position).sqrMagnitude <= threshold * threshold;
    }

    private void CompleteHazardIsolationOrder(string detail)
    {
        CompleteCurrentTask(detail);
        if (behaviorContext != null)
        {
            if (behaviorContext.HasMoveOrder)
            {
                behaviorContext.ClearMoveOrder();
            }
            else
            {
                behaviorContext.ClearCommandIntent();
            }
        }

        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
        }

        hasIssuedDestination = false;
        ClearHazardIsolationRuntimeState();
    }

    private void AbortHazardIsolationOrder(string detail)
    {
        FailCurrentTask(detail, BotTaskStatus.Blocked);
        if (behaviorContext != null)
        {
            if (behaviorContext.HasMoveOrder)
            {
                behaviorContext.ClearMoveOrder();
            }
            else
            {
                behaviorContext.ClearCommandIntent();
            }
        }

        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
        }

        hasIssuedDestination = false;
        ClearHazardIsolationRuntimeState();
    }

    private void ClearHazardIsolationRuntimeState()
    {
        SetCurrentHazardIsolationTarget(null);
        ResetHazardIsolationUnavailableState();

        if (cachedHazardIsolationStoppingDistance >= 0f &&
            navMeshAgent != null &&
            navMeshAgent.enabled)
        {
            navMeshAgent.stoppingDistance = cachedHazardIsolationStoppingDistance;
        }

        cachedHazardIsolationStoppingDistance = -1f;
    }
}
