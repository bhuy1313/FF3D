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

    private void ProcessHazardIsolationCommand()
    {
        if (behaviorContext == null || !TryGetHazardIsolationIntent(out BotCommandIntentPayload intent))
        {
            ClearHazardIsolationRuntimeState();
            return;
        }

        Vector3 orderPoint = intent.HasWorldPoint
            ? intent.WorldPoint
            : hasIssuedDestination ? lastIssuedDestination : transform.position;

        if (currentHazardIsolationTarget != null && !currentHazardIsolationTarget.IsHazardActive)
        {
            CompleteHazardIsolationOrder("Hazard isolated.");
            return;
        }

        if (!TryResolveHazardIsolationTarget(orderPoint, out IBotHazardIsolationTarget target))
        {
            SetCurrentHazardIsolationTarget(null);
            if (hazardIsolationUnavailableRetryCount > 0 && IsNearHazardIsolationPoint(orderPoint))
            {
                AbortHazardIsolationOrder("No interactable hazard device is currently available near the isolate point.");
                return;
            }

            if (IsNearHazardIsolationPoint(orderPoint))
            {
                CompleteHazardIsolationOrder("No active hazard device found near the isolate point.");
            }

            return;
        }

        SetCurrentHazardIsolationTarget(target);
        if (!TryGetHazardIsolationComponent(target, out Component targetComponent))
        {
            AbortHazardIsolationOrder("Hazard isolation target is missing its runtime component.");
            return;
        }

        Vector3 targetPosition = target.GetWorldPosition();
        float interactionDistance = Mathf.Max(0.5f, hazardIsolationInteractionDistance);
        if ((targetPosition - transform.position).sqrMagnitude <= interactionDistance * interactionDistance)
        {
            if (!target.IsHazardActive)
            {
                CompleteHazardIsolationOrder("Hazard was already isolated.");
                return;
            }

            if (!target.IsInteractionAvailable)
            {
                if (HandleUnavailableHazardIsolationTarget())
                {
                    return;
                }

                return;
            }

            ResetHazardIsolationUnavailableState();

            if (!TryGetHazardIsolationInteractable(targetComponent, out IInteractable interactable))
            {
                AbortHazardIsolationOrder("Hazard isolation target cannot be interacted with.");
                return;
            }

            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.ResetPath();
                navMeshAgent.isStopped = true;
            }

            AimTowards(targetPosition);
            interactable.Interact(gameObject);

            if (!target.IsHazardActive)
            {
                CompleteHazardIsolationOrder("Hazard isolated.");
            }

            return;
        }

        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            AbortHazardIsolationOrder("Bot is not on a valid NavMesh to reach the hazard device.");
            return;
        }

        if (cachedHazardIsolationStoppingDistance < 0f)
        {
            cachedHazardIsolationStoppingDistance = navMeshAgent.stoppingDistance;
        }

        navMeshAgent.stoppingDistance = Mathf.Max(navMeshAgent.stoppingDistance, interactionDistance * 0.85f);
        TryNavigateTo(targetPosition);
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
