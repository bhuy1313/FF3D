using UnityEngine;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    [Header("Tasking")]
    [SerializeField] private float taskReservationDuration = 1.5f;
    [SerializeField] private BotTask currentTask = new BotTask();

    private IRescuableTarget reservedRescueTarget;
    private IFireTarget reservedFireTarget;
    private IFireGroupTarget reservedFireGroupTarget;
    private IBotBreakableTarget reservedBreakableTarget;
    private IBotPryTarget reservedPryTarget;
    private IBotHazardIsolationTarget reservedHazardIsolationTarget;

    public BotTask CurrentTask => currentTask;

    private void RefreshTaskState()
    {
        if (behaviorContext == null)
        {
            ClearCurrentTask();
            ReleaseAllTaskReservations();
            return;
        }

        if (IsRouteFireClearingActive())
        {
            RefreshReservation(currentRouteBlockingFireGroupTarget);
            RefreshReservation(currentRouteBlockingFire);
            Component routeFireComponent = (currentRouteBlockingFireGroupTarget as Component) ?? (currentRouteBlockingFire as Component);
            Vector3? routeFirePosition = currentRouteBlockingFire != null
                ? (Vector3?)currentRouteBlockingFire.GetWorldPosition()
                : currentRouteBlockingFireGroupTarget != null
                    ? (Vector3?)currentRouteBlockingFireGroupTarget.GetClosestActiveFirePosition(transform.position)
                    : hasIssuedDestination ? (Vector3?)lastIssuedDestination : (Vector3?)null;

            switch (currentRouteFirePhase)
            {
                case RouteFirePhase.AcquireTool:
                    BeginOrRefreshTask(
                        BotTaskType.AcquireTool,
                        HasMovePickupTarget
                            ? "Retrieving suppression tool for route fire."
                            : "Acquiring suppression tool for route fire.",
                        routeFireComponent,
                        routeFirePosition);
                    return;
                case RouteFirePhase.ReturnToFire:
                    BeginOrRefreshTask(
                        BotTaskType.Move,
                        "Returning to fire blocking the route.",
                        routeFireComponent,
                        routeFirePosition);
                    return;
                case RouteFirePhase.Extinguish:
                    BeginOrRefreshTask(
                        BotTaskType.Extinguish,
                        "Clearing fire blocking the route.",
                        routeFireComponent,
                        routeFirePosition);
                    return;
            }
        }

        if (currentBlockedBreakable != null &&
            !currentBlockedBreakable.IsBroken &&
            currentBlockedBreakable.CanBeClearedByBot)
        {
            if (behaviorContext.TryGetCommandIntentSnapshot(out BotCommandIntentPayload activeIntent) &&
                activeIntent.CommandType == BotCommandType.Breach)
            {
                RefreshReservation(currentBlockedBreakable);
                BeginOrRefreshTask(BotTaskType.Breach, GetActiveBreakTaskDetail(), currentBlockedBreakable as Component);
                return;
            }

            RefreshReservation(currentBlockedBreakable);
            BeginOrRefreshTask(BotTaskType.PathClear, GetActiveBreakTaskDetail(), currentBlockedBreakable as Component);
            return;
        }

        if (currentBlockedPryTarget != null &&
            !currentBlockedPryTarget.IsBreached &&
            (currentBlockedPryTarget.CanBePriedOpen || currentBlockedPryTarget.IsPryInProgress))
        {
            RefreshReservation(currentBlockedPryTarget);
            BeginOrRefreshTask(
                BotTaskType.PathClear,
                GetActiveBreakTaskDetail(),
                currentBlockedPryTarget as Component,
                currentBlockedPryTarget.GetWorldPosition());
            return;
        }

        if (IsExtinguishV2Active)
        {
            RefreshReservation(extinguishV2State.FireGroup);
            RefreshReservation(extinguishV2State.FireTarget);
            BeginOrRefreshTask(
                BotTaskType.Extinguish,
                GetExtinguishV2TaskDetail(),
                GetExtinguishV2TaskTargetComponent(),
                GetExtinguishV2TaskPosition());
            return;
        }

        if (behaviorContext.HasExtinguishOrder)
        {
            RefreshReservation(currentFireGroupTarget);
            RefreshReservation(commandedFireGroupTarget);
            RefreshReservation(currentFireTarget);
            BeginOrRefreshTask(
                BotTaskType.Extinguish,
                GetActiveExtinguishTaskDetail(),
                (currentFireGroupTarget as Component) ?? (currentFireTarget as Component),
                currentFireTarget != null
                    ? (Vector3?)currentFireTarget.GetWorldPosition()
                    : currentFireGroupTarget != null
                        ? (Vector3?)currentFireGroupTarget.GetClosestActiveFirePosition(transform.position)
                    : hasIssuedDestination ? lastIssuedDestination : (Vector3?)null);
            return;
        }

        if (behaviorContext.HasRescueOrder)
        {
            RefreshReservation(currentRescueTarget);
            BeginOrRefreshTask(
                BotTaskType.Rescue,
                GetActiveRescueTaskDetail(),
                currentRescueTarget as Component,
                currentRescueTarget != null
                    ? (Vector3?)currentRescueTarget.GetWorldPosition()
                    : currentSafeZoneTarget != null
                        ? (Vector3?)currentSafeZoneTarget.GetWorldPosition()
                        : hasIssuedDestination ? lastIssuedDestination : (Vector3?)null);
            return;
        }

        if (behaviorContext.TryGetCommandIntentSnapshot(out BotCommandIntentPayload commandIntent))
        {
            switch (commandIntent.CommandType)
            {
                case BotCommandType.Hold:
                    BeginOrRefreshTask(BotTaskType.Hold, "Holding assigned position.", null, commandIntent.HasWorldPoint ? commandIntent.WorldPoint : (Vector3?)transform.position);
                    return;
                case BotCommandType.Regroup:
                    if (behaviorContext.HasFollowOrder)
                    {
                        BeginOrRefreshTask(BotTaskType.Regroup, "Regrouping with squad.", followTarget);
                        return;
                    }

                    break;
                case BotCommandType.Assist:
                    if (behaviorContext.HasFollowOrder)
                    {
                        BeginOrRefreshTask(BotTaskType.Assist, "Assisting assigned target.", followTarget);
                        return;
                    }

                    break;
                case BotCommandType.Search:
                    if (behaviorContext.HasMoveOrder || HasActiveDirectNavigationIntent())
                    {
                        BeginOrRefreshTask(BotTaskType.Search, "Searching assigned area.", null, hasIssuedDestination ? lastIssuedDestination : (Vector3?)null);
                        return;
                    }

                    break;
                case BotCommandType.Breach:
                    if (behaviorContext.HasMoveOrder || HasActiveDirectNavigationIntent())
                    {
                        RefreshReservation(currentBreachPryTarget);
                        BeginOrRefreshTask(
                            BotTaskType.Breach,
                            GetActiveBreakTaskDetail(),
                            currentBreachPryTarget as Component,
                            currentBreachPryTarget != null
                                ? (Vector3?)currentBreachPryTarget.GetWorldPosition()
                                : hasIssuedDestination ? lastIssuedDestination : (Vector3?)null);
                        return;
                    }

                    break;
                case BotCommandType.Isolate:
                    if (behaviorContext.HasMoveOrder || HasActiveDirectNavigationIntent())
                    {
                        RefreshReservation(currentHazardIsolationTarget);
                        BeginOrRefreshTask(
                            BotTaskType.Isolate,
                            currentHazardIsolationTarget != null ? "Isolating assigned hazard device." : "Moving to isolate hazard.",
                            currentHazardIsolationTarget as Component,
                            currentHazardIsolationTarget != null
                                ? (Vector3?)currentHazardIsolationTarget.GetWorldPosition()
                                : hasIssuedDestination ? lastIssuedDestination : (Vector3?)null);
                        return;
                    }

                    break;
                case BotCommandType.Move:
                    if (behaviorContext.HasMoveOrder || HasActiveDirectNavigationIntent())
                    {
                        BeginOrRefreshTask(BotTaskType.Move, "Moving to assigned destination.", null, hasIssuedDestination ? lastIssuedDestination : (Vector3?)null);
                        return;
                    }

                    break;
            }
        }

        if (behaviorContext.HasFollowOrder)
        {
            BeginOrRefreshTask(BotTaskType.Follow, "Following assigned target.", followTarget);
            return;
        }

        if (HasMovePickupTarget)
        {
            BeginOrRefreshTask(BotTaskType.AcquireTool, "Acquiring requested tool.");
            return;
        }

        if (behaviorContext.HasMoveOrder)
        {
            BeginOrRefreshTask(BotTaskType.Move, "Moving to assigned destination.", null, hasIssuedDestination ? lastIssuedDestination : (Vector3?)null);
            return;
        }

        ClearCurrentTask();
    }

    private bool HasActiveDirectNavigationIntent()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh || navMeshAgent.isStopped)
        {
            return false;
        }

        return navMeshAgent.hasPath || navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance + 0.05f;
    }

    private void BeginOrRefreshTask(BotTaskType taskType, string detail, Component targetComponent = null, Vector3? explicitPosition = null)
    {
        string targetName = targetComponent != null ? targetComponent.name : string.Empty;
        Vector3? targetPosition = explicitPosition;
        if (!targetPosition.HasValue && targetComponent != null)
        {
            targetPosition = targetComponent.transform.position;
        }

        currentTask.Begin(taskType, detail, targetName, targetPosition);
    }

    private void CompleteCurrentTask(string detail)
    {
        currentTask.Mark(BotTaskStatus.Completed, detail);
    }

    private void FailCurrentTask(string detail, BotTaskStatus failureStatus = BotTaskStatus.Failed)
    {
        currentTask.Mark(failureStatus, detail);
    }

    private void ClearCurrentTask()
    {
        currentTask.Clear();
    }

    private void SetCurrentRescueTarget(IRescuableTarget target)
    {
        if (ReferenceEquals(currentRescueTarget, target))
        {
            RefreshReservation(target);
            return;
        }

        ReleaseReservation(currentRescueTarget);
        currentRescueTarget = target;
        ReserveTarget(target, BotTaskType.Rescue);
    }

    private void SetCurrentFireTarget(IFireTarget target)
    {
        SetCurrentFireGroupTarget(commandedFireGroupTarget != null && commandedFireGroupTarget.HasActiveFires
            ? commandedFireGroupTarget
            : null);

        if (ReferenceEquals(currentFireTarget, target))
        {
            if (commandedFireGroupTarget != null && commandedFireGroupTarget.HasActiveFires)
            {
                RefreshReservation(commandedFireGroupTarget);
            }

            RefreshReservation(target);
            return;
        }

        ReleaseReservation(currentFireTarget);
        currentFireTarget = target;
        if (commandedFireGroupTarget != null && commandedFireGroupTarget.HasActiveFires)
        {
            ReserveTarget(commandedFireGroupTarget, BotTaskType.Extinguish);
        }

        ReserveTarget(target, BotTaskType.Extinguish);
    }

    private void SetCurrentFireGroupTarget(IFireGroupTarget target)
    {
        if (ReferenceEquals(currentFireGroupTarget, target))
        {
            RefreshReservation(target);
            return;
        }

        ReleaseReservation(currentFireGroupTarget);
        currentFireGroupTarget = target;
        ReserveTarget(target, BotTaskType.Extinguish);
    }

    private void SetCurrentBlockedBreakable(IBotBreakableTarget target)
    {
        if (ReferenceEquals(currentBlockedBreakable, target))
        {
            RefreshReservation(target);
            return;
        }

        ReleaseReservation(currentBlockedBreakable);
        currentBlockedBreakable = target;
        ReserveTarget(target, IsBreachCommandActive() ? BotTaskType.Breach : BotTaskType.PathClear);
    }

    private void SetCurrentBlockedPryTarget(IBotPryTarget target)
    {
        if (ReferenceEquals(currentBlockedPryTarget, target))
        {
            RefreshReservation(target);
            return;
        }

        ReleaseReservation(currentBlockedPryTarget);
        currentBlockedPryTarget = target;
        ReserveTarget(target, BotTaskType.PathClear);
    }

    private void ReserveTarget(object target, BotTaskType taskType)
    {
        if (target == null)
        {
            return;
        }

        BotRuntimeRegistry.Reservations.TryReserve(target, gameObject, taskType, taskReservationDuration);

        switch (target)
        {
            case IRescuableTarget rescuable:
                reservedRescueTarget = rescuable;
                break;
            case IFireTarget fireTarget:
                reservedFireTarget = fireTarget;
                break;
            case IFireGroupTarget fireGroupTarget:
                reservedFireGroupTarget = fireGroupTarget;
                break;
            case IBotBreakableTarget breakableTarget:
                reservedBreakableTarget = breakableTarget;
                break;
            case IBotPryTarget pryTarget:
                reservedPryTarget = pryTarget;
                break;
            case IBotHazardIsolationTarget hazardIsolationTarget:
                reservedHazardIsolationTarget = hazardIsolationTarget;
                break;
        }
    }

    private void RefreshReservation(object target)
    {
        if (target == null)
        {
            return;
        }

        BotRuntimeRegistry.Reservations.RefreshReservation(target, gameObject, taskReservationDuration);
    }

    private void ReleaseReservation(object target)
    {
        if (target == null)
        {
            return;
        }

        BotRuntimeRegistry.Reservations.Release(target, gameObject);

        if (ReferenceEquals(target, reservedRescueTarget))
        {
            reservedRescueTarget = null;
        }
        else if (ReferenceEquals(target, reservedFireTarget))
        {
            reservedFireTarget = null;
        }
        else if (ReferenceEquals(target, reservedFireGroupTarget))
        {
            reservedFireGroupTarget = null;
        }
        else if (ReferenceEquals(target, reservedBreakableTarget))
        {
            reservedBreakableTarget = null;
        }
        else if (ReferenceEquals(target, reservedPryTarget))
        {
            reservedPryTarget = null;
        }
        else if (ReferenceEquals(target, reservedHazardIsolationTarget))
        {
            reservedHazardIsolationTarget = null;
        }
    }

    private void ReleaseAllTaskReservations()
    {
        ReleaseReservation(reservedRescueTarget);
        ReleaseReservation(reservedFireTarget);
        ReleaseReservation(reservedFireGroupTarget);
        ReleaseReservation(reservedBreakableTarget);
        ReleaseReservation(reservedPryTarget);
        ReleaseReservation(reservedHazardIsolationTarget);
        BotRuntimeRegistry.Reservations.ReleaseAllOwnedBy(gameObject);
        reservedRescueTarget = null;
        reservedFireTarget = null;
        reservedFireGroupTarget = null;
        reservedBreakableTarget = null;
        reservedPryTarget = null;
        reservedHazardIsolationTarget = null;
    }

    private void SetCurrentHazardIsolationTarget(IBotHazardIsolationTarget target)
    {
        if (ReferenceEquals(currentHazardIsolationTarget, target))
        {
            RefreshReservation(target);
            return;
        }

        ReleaseReservation(currentHazardIsolationTarget);
        currentHazardIsolationTarget = target;
        ReserveTarget(target, BotTaskType.Isolate);
    }

    private void SetCurrentBreachPryTarget(IBotPryTarget target)
    {
        if (ReferenceEquals(currentBreachPryTarget, target))
        {
            RefreshReservation(target);
            return;
        }

        ReleaseReservation(currentBreachPryTarget);
        currentBreachPryTarget = target;
        ReserveTarget(target, BotTaskType.Breach);
    }
}
