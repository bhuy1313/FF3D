using UnityEngine;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private enum SuppressionToolAcquisitionKind
    {
        ExtinguishPlan = 0,
        RouteFireInterrupt = 1
    }

    private sealed class BotExtinguishPlanState
    {
        public Vector3 OrderPoint;
        public Vector3 ScanOrigin;
        public Vector3 TargetSearchPoint;
        public Vector3 FirePosition;
        public BotExtinguishCommandMode Mode;
        public IFireTarget FireTarget;
        public IFireGroupTarget FireGroup;
        public IBotExtinguisherItem PlannedTool;
        public bool UsesPreciseAim;

        public void Reset()
        {
            OrderPoint = default;
            ScanOrigin = default;
            TargetSearchPoint = default;
            FirePosition = default;
            Mode = BotExtinguishCommandMode.Auto;
            FireTarget = null;
            FireGroup = null;
            PlannedTool = null;
            UsesPreciseAim = false;
        }
    }

    private sealed class AcquireExtinguishTargetTask : IBotPlanTask
    {
        public string Name => "Locate Fire Target";

        public void OnStart(BotCommandAgent agent)
        {
            agent.SetExtinguishSubtask(BotExtinguishSubtask.AcquireTarget, "Acquiring fire target.");
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            if (!agent.TrySyncExtinguishPlanOrder())
            {
                return BotPlanTaskStatus.Failure;
            }

            Vector3 targetSearchPoint = agent.extinguishPlanState.TargetSearchPoint;
            IFireGroupTarget fireGroup = agent.extinguishPlanState.Mode == BotExtinguishCommandMode.PointFire
                ? null
                : agent.ResolveIssuedFireGroupTarget(targetSearchPoint);
            IFireTarget fireTarget = agent.extinguishPlanState.Mode == BotExtinguishCommandMode.PointFire
                ? agent.ResolveIssuedPointFireTarget(targetSearchPoint)
                : agent.ResolveRepresentativeFireTarget(fireGroup, agent.transform.position);

            if ((fireGroup == null || !fireGroup.HasActiveFires) && (fireTarget == null || !fireTarget.IsBurning))
            {
                agent.CompleteExtinguishOrder("No active fire target remained near the assigned point.");
                return BotPlanTaskStatus.Success;
            }

            Vector3 botPosition = agent.transform.position;
            Vector3 firePosition = fireTarget != null && fireTarget.IsBurning
                ? fireTarget.GetWorldPosition()
                : fireGroup.GetClosestActiveFirePosition(botPosition);
            agent.extinguishPlanState.FireGroup = fireGroup;
            agent.extinguishPlanState.FireTarget = fireTarget;
            agent.extinguishPlanState.FirePosition = firePosition;
            agent.UpdateExtinguishDebugStage(ExtinguishDebugStage.SearchingFireGroup, $"Resolved fire target near {agent.extinguishPlanState.OrderPoint}.");
            return BotPlanTaskStatus.Success;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
            if (interrupted)
            {
                agent.StopExtinguisher();
                agent.sprayReadyTime = -1f;
            }
        }
    }

    private sealed class AcquireSuppressionToolTask : IBotPlanTask
    {
        private readonly SuppressionToolAcquisitionKind kind;
        private bool movingToPickup;

        public AcquireSuppressionToolTask(SuppressionToolAcquisitionKind kind)
        {
            this.kind = kind;
        }

        public string Name => "Acquire Suppression Tool";

        public void OnStart(BotCommandAgent agent)
        {
            if (kind == SuppressionToolAcquisitionKind.RouteFireInterrupt)
            {
                IFireTarget blockingFire = agent.ResolveCurrentRouteBlockingFireTarget();
                if (blockingFire != null &&
                    agent.TryRestoreHeldSuppressionTool(BotExtinguishCommandMode.PointFire, blockingFire, out IBotExtinguisherItem heldTool) &&
                    !BotCommandAgent.UsesPreciseAim(heldTool))
                {
                    agent.SetRouteFirePhase(RouteFirePhase.ReturnToFire);
                    return;
                }

                agent.SetRouteFirePhase(RouteFirePhase.AcquireTool);
                return;
            }

            BotExtinguishPlanState state = agent.extinguishPlanState;
            if (agent.TryRestoreHeldSuppressionTool(state.Mode, state.FireTarget, out _))
            {
                return;
            }

            agent.SetExtinguishSubtask(BotExtinguishSubtask.AcquireTool, "Acquiring suppression tool.");
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            if (movingToPickup)
            {
                if (!agent.HasMovePickupTarget)
                {
                    movingToPickup = false;
                }
                else if (agent.TryCompleteMovePickupTarget())
                {
                    movingToPickup = false;
                    return BotPlanTaskStatus.Success;
                }
                else
                {
                    return BotPlanTaskStatus.Running;
                }
            }

            BotPlanTaskStatus status = kind == SuppressionToolAcquisitionKind.RouteFireInterrupt
                ? agent.TryUpdateRouteFireSuppressionToolAcquisition(out movingToPickup)
                : agent.TryUpdateExtinguishSuppressionToolAcquisition(out movingToPickup);
            return movingToPickup ? BotPlanTaskStatus.Running : status;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
            movingToPickup = false;
        }
    }

    private sealed class MoveToExtinguishPositionTask : MoveToPositionTask
    {
        public MoveToExtinguishPositionTask() : base(
            "Move To Extinguish Position",
            agent => agent.UpdateExtinguishPositionMove(),
            onStart: agent => agent.SetExtinguishSubtask(BotExtinguishSubtask.MoveToFire, "Moving to extinguish position."))
        {
        }
    }

    private sealed class SuppressFireTask : IBotPlanTask
    {
        public string Name => "Suppress Fire";

        public void OnStart(BotCommandAgent agent)
        {
            agent.SetExtinguishSubtask(BotExtinguishSubtask.AimAtFire, "Preparing to suppress fire.");
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            if (!agent.TrySyncExtinguishPlanOrder())
            {
                return BotPlanTaskStatus.Failure;
            }

            BotExtinguishPlanState state = agent.extinguishPlanState;
            if (agent.activeExtinguisher == null &&
                !agent.TryRestoreHeldSuppressionTool(state.Mode, state.FireTarget, out _))
            {
                agent.planProcessor?.InjectFront(
                    agent,
                    new AcquireSuppressionToolTask(SuppressionToolAcquisitionKind.ExtinguishPlan),
                    new MovePickupTask(),
                    new MoveToExtinguishPositionTask(),
                    new SuppressFireTask());
                return BotPlanTaskStatus.Success;
            }

            if (!agent.activeExtinguisher.HasUsableCharge)
            {
                agent.HandleExtinguishChargeDepleted(state);
                return agent.behaviorContext != null && agent.behaviorContext.HasExtinguishOrder
                    ? BotPlanTaskStatus.Success
                    : BotPlanTaskStatus.Failure;
            }

            if (BotCommandAgent.IsUnsafeSuppressionToolForFire(agent.activeExtinguisher, state.FireTarget))
            {
                agent.HandleUnsafeSuppressionTool(state, state.FireTarget);
                return agent.behaviorContext != null && agent.behaviorContext.HasExtinguishOrder
                    ? BotPlanTaskStatus.Success
                    : BotPlanTaskStatus.Failure;
            }

            if (state.UsesPreciseAim)
            {
                IFireGroupTarget fireGroup = state.FireGroup != null && state.FireGroup.HasActiveFires
                    ? state.FireGroup
                    : agent.ResolveIssuedFireGroupTarget(state.TargetSearchPoint);
                IFireTarget fireTarget = agent.ResolveStickyFireGroupRepresentative(state.FireTarget, fireGroup, agent.transform.position);
                if ((fireGroup == null || !fireGroup.HasActiveFires) && (fireTarget == null || !fireTarget.IsBurning))
                {
                    agent.CompleteExtinguishOrder("FireGroup extinguished.");
                    return BotPlanTaskStatus.Success;
                }

                state.FireGroup = fireGroup;
                state.FireTarget = fireTarget;
                state.FirePosition = fireTarget != null && fireTarget.IsBurning
                    ? fireTarget.GetWorldPosition()
                    : fireGroup.GetWorldCenter();
                agent.ProcessFireHoseExtinguishRoute(state.TargetSearchPoint, fireGroup, state.FirePosition, agent.transform.position);
            }
            else
            {
                IFireTarget fireTarget = agent.ResolveExtinguisherRouteTarget(state.TargetSearchPoint);
                if (fireTarget != null && fireTarget.IsBurning)
                {
                    state.FireTarget = fireTarget;
                    state.FirePosition = fireTarget.GetWorldPosition();
                }

                if (BotCommandAgent.IsUnsafeSuppressionToolForFire(agent.activeExtinguisher, state.FireTarget))
                {
                    agent.HandleUnsafeSuppressionTool(state, state.FireTarget);
                    return agent.behaviorContext != null && agent.behaviorContext.HasExtinguishOrder
                        ? BotPlanTaskStatus.Success
                        : BotPlanTaskStatus.Failure;
                }

                agent.ProcessFireExtinguisherExtinguishRoute(
                    state.OrderPoint,
                    state.TargetSearchPoint,
                    state.FireTarget,
                    state.FirePosition,
                    agent.transform.position);
            }

            if (agent.behaviorContext == null || !agent.behaviorContext.HasExtinguishOrder)
            {
                return BotPlanTaskStatus.Success;
            }

            if (agent.activeExtinguisher == null)
            {
                return agent.behaviorContext != null && agent.behaviorContext.HasExtinguishOrder
                    ? BotPlanTaskStatus.Success
                    : BotPlanTaskStatus.Failure;
            }

            if (!agent.activeExtinguisher.HasUsableCharge)
            {
                agent.HandleExtinguishChargeDepleted(state);
                return BotPlanTaskStatus.Success;
            }

            return BotPlanTaskStatus.Running;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
        }
    }

    private BotPlanProcessor planProcessor;
    private readonly BotExtinguishPlanState extinguishPlanState = new BotExtinguishPlanState();
    private string activeCommandPlanKey = string.Empty;

    private BotPlan BuildExtinguishPlan()
    {
        return new BotPlan("Extinguish")
            .Add(new AcquireExtinguishTargetTask())
            .Add(new AcquireSuppressionToolTask(SuppressionToolAcquisitionKind.ExtinguishPlan))
            .Add(new MoveToExtinguishPositionTask())
            .Add(new SuppressFireTask());
    }

    private bool TrySyncExtinguishPlanOrder()
    {
        if (behaviorContext == null ||
            !behaviorContext.TryGetExtinguishOrder(out Vector3 orderPoint, out Vector3 scanOrigin, out BotExtinguishCommandMode orderMode))
        {
            return false;
        }

        extinguishPlanState.OrderPoint = orderPoint;
        extinguishPlanState.ScanOrigin = scanOrigin;
        extinguishPlanState.Mode = orderMode;
        extinguishPlanState.TargetSearchPoint = orderMode == BotExtinguishCommandMode.PointFire ? scanOrigin : orderPoint;
        return true;
    }

    private string BuildExtinguishPlanKey()
    {
        Vector3 orderPoint = extinguishPlanState.OrderPoint;
        Vector3 scanOrigin = extinguishPlanState.ScanOrigin;
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0}:{1:F2}:{2:F2}:{3:F2}:{4:F2}:{5:F2}:{6:F2}",
            extinguishPlanState.Mode,
            orderPoint.x,
            orderPoint.y,
            orderPoint.z,
            scanOrigin.x,
            scanOrigin.y,
            scanOrigin.z);
    }

    private void ResetCommandPlanProcessor()
    {
        planProcessor?.Clear(this);
        activeCommandPlanKey = string.Empty;
        forceCommandPlanRebuild = false;
        extinguishPlanState.Reset();
    }

    private void InvalidateActiveCommandPlan()
    {
        activeCommandPlanKey = string.Empty;
    }

    private void HandleExtinguishChargeDepleted(BotExtinguishPlanState state)
    {
        bool hasRemainingFire =
            (state.FireTarget != null && state.FireTarget.IsBurning) ||
            (state.FireGroup != null && state.FireGroup.HasActiveFires);
        if (!hasRemainingFire)
        {
            CompleteExtinguishOrder("All nearby fires extinguished.");
            return;
        }

        StopExtinguisher();
        TemporarilyRejectExtinguishTool(activeExtinguisher);
        sprayReadyTime = -1f;
        ClearHeadAimFocus();
        ClearHandAimFocus();
        ResetExtinguishCrouchState();
        preferredExtinguishTool = null;
        planProcessor.InjectFront(
            this,
            new AcquireSuppressionToolTask(SuppressionToolAcquisitionKind.ExtinguishPlan),
            new MoveToExtinguishPositionTask(),
            new SuppressFireTask());
        SetExtinguishSubtask(BotExtinguishSubtask.Recover, "Suppression tool depleted. Acquiring replacement tool.");
    }

    private void HandleUnsafeSuppressionTool(BotExtinguishPlanState state, IFireTarget fireTarget)
    {
        if (state == null)
        {
            return;
        }

        if (fireTarget == null || !fireTarget.IsBurning)
        {
            FailActiveExtinguishOrder("Suppression tool is unsafe for the current fire target.", BotTaskStatus.Blocked);
            return;
        }

        StopExtinguisher();
        TemporarilyRejectExtinguishTool(activeExtinguisher);
        sprayReadyTime = -1f;
        ClearHeadAimFocus();
        ClearHandAimFocus();
        ResetExtinguishCrouchState();
        preferredExtinguishTool = null;

        if (!TryResolveSuppressionTool(
            state.OrderPoint,
            state.FirePosition,
            state.FireGroup,
            fireTarget,
            state.Mode,
            false,
            out IBotExtinguisherItem replacementTool))
        {
            FailActiveExtinguishOrder("No safe suppression tool available for the current fire target.", BotTaskStatus.Blocked);
            return;
        }

        state.PlannedTool = replacementTool;
        planProcessor.InjectFront(
            this,
            new AcquireSuppressionToolTask(SuppressionToolAcquisitionKind.ExtinguishPlan),
            new MoveToExtinguishPositionTask(),
            new SuppressFireTask());
        SetExtinguishSubtask(BotExtinguishSubtask.Recover, "Current suppression tool is unsafe. Acquiring a safer replacement.");
    }

    private BotPlanTaskStatus TryUpdateExtinguishSuppressionToolAcquisition(out bool movingToPickup)
    {
        movingToPickup = false;
        if (!TrySyncExtinguishPlanOrder())
        {
            return BotPlanTaskStatus.Failure;
        }

        BotExtinguishPlanState state = extinguishPlanState;
        if (!TryResolveSuppressionTool(
            state.OrderPoint,
            state.FirePosition,
            state.FireGroup,
            state.FireTarget,
            state.Mode,
            false,
            out state.PlannedTool))
        {
            if (state.Mode == BotExtinguishCommandMode.FireGroup &&
                TryFallbackFireGroupOrderToPointFire(state.FireTarget, out Vector3 fallbackDestination))
            {
                SetExtinguishSubtask(BotExtinguishSubtask.Recover, $"Replanning extinguish route through point fire at {fallbackDestination}.");
                InvalidateActiveCommandPlan();
                return BotPlanTaskStatus.Success;
            }

            FailActiveExtinguishOrder("No available suppression tool can reach the assigned fire.", BotTaskStatus.Blocked);
            return BotPlanTaskStatus.Failure;
        }

        state.UsesPreciseAim = BotCommandAgent.UsesPreciseAim(state.PlannedTool);
        if (!state.UsesPreciseAim && TryReplanExtinguishOrderForPointFireTool(state))
        {
            SetExtinguishSubtask(BotExtinguishSubtask.Recover, "Replanning handheld extinguisher against a point fire.");
            InvalidateActiveCommandPlan();
            return BotPlanTaskStatus.Success;
        }

        if (!TryAdvanceSuppressionToolAcquisition(state.PlannedTool, true))
        {
            if (TryPrepareSuppressionToolMovePickup(state.PlannedTool))
            {
                movingToPickup = true;
                return BotPlanTaskStatus.Running;
            }

            return BotPlanTaskStatus.Running;
        }

        if (extinguishStartupPending)
        {
            SetExtinguishSubtask(BotExtinguishSubtask.Recover, "Recovering extinguish order.");
            ClearHeadAimFocus();
            ClearHandAimFocus();
            extinguishStartupPending = false;
            return BotPlanTaskStatus.Running;
        }

        if (activeExtinguisher == null || !activeExtinguisher.HasUsableCharge)
        {
            FailActiveExtinguishOrder("Active suppression tool is out of charge.", BotTaskStatus.Failed);
            return BotPlanTaskStatus.Failure;
        }

        return BotPlanTaskStatus.Success;
    }

    private bool TryReplanExtinguishOrderForPointFireTool(BotExtinguishPlanState state)
    {
        if (state == null ||
            state.Mode == BotExtinguishCommandMode.PointFire ||
            state.PlannedTool == null ||
            BotCommandAgent.UsesPreciseAim(state.PlannedTool) ||
            state.FireTarget == null ||
            !state.FireTarget.IsBurning ||
            behaviorContext == null ||
            navMeshAgent == null ||
            !navMeshAgent.enabled ||
            !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        Vector3 scanOrigin = state.FireTarget.GetWorldPosition();
        Vector3 destination = scanOrigin;
        if (TryResolvePointFireApproachPosition(scanOrigin, out Vector3 approachDestination))
        {
            destination = approachDestination;
        }
        else if (navMeshSampleDistance > 0f &&
                 UnityEngine.AI.NavMesh.SamplePosition(scanOrigin, out UnityEngine.AI.NavMeshHit navMeshHit, navMeshSampleDistance, navMeshAgent.areaMask))
        {
            destination = navMeshHit.position;
        }

        CacheIssuedExtinguishTargets(BotExtinguishCommandMode.PointFire, state.FireTarget, null);
        behaviorContext.SetExtinguishOrder(destination, scanOrigin, BotExtinguishCommandMode.PointFire);
        extinguishStartupPending = true;
        lastIssuedDestination = destination;
        hasIssuedDestination = true;
        return true;
    }

    private MoveTaskDirective UpdateExtinguishPositionMove()
    {
        if (!TrySyncExtinguishPlanOrder())
        {
            return MoveTaskDirective.Failure();
        }

        BotExtinguishPlanState state = extinguishPlanState;
        if (activeExtinguisher == null &&
            !TryRestoreHeldSuppressionTool(state.Mode, state.FireTarget, out _))
        {
            return MoveTaskDirective.Failure();
        }

        Vector3 botPosition = transform.position;

        if (!state.UsesPreciseAim)
        {
            IFireTarget routeFireTarget = ResolveExtinguisherRouteTarget(state.TargetSearchPoint);
            if (routeFireTarget != null && routeFireTarget.IsBurning)
            {
                state.FireTarget = routeFireTarget;
                state.FirePosition = routeFireTarget.GetWorldPosition();
                return MoveTaskDirective.Success();
            }

            if (!IsWithinArrivalDistance(state.OrderPoint))
            {
                return ShouldIssueExtinguisherApproachMove(state.OrderPoint)
                    ? MoveTaskDirective.Running(state.OrderPoint)
                    : MoveTaskDirective.Continue();
            }

            return MoveTaskDirective.Success();
        }

        IFireGroupTarget fireGroup = state.FireGroup != null && state.FireGroup.HasActiveFires
            ? state.FireGroup
            : ResolveIssuedFireGroupTarget(state.TargetSearchPoint);
        IFireTarget fireTarget = state.FireTarget != null && state.FireTarget.IsBurning
            ? state.FireTarget
            : ResolveRepresentativeFireTarget(fireGroup, botPosition);
        if ((fireGroup == null || !fireGroup.HasActiveFires) && (fireTarget == null || !fireTarget.IsBurning))
        {
            CompleteExtinguishOrder("No active fire target remained near the assigned point.");
            return MoveTaskDirective.Success();
        }

        state.FireGroup = fireGroup;
        state.FireTarget = fireTarget;
        state.FirePosition = fireTarget != null && fireTarget.IsBurning
            ? fireTarget.GetWorldPosition()
            : fireGroup.GetWorldCenter();

        float horizontalDistanceToFire = BotCommandAgent.GetHorizontalDistance(botPosition, state.FirePosition);
        float requiredHorizontalDistance = GetRequiredHorizontalDistanceForAim(activeExtinguisher, state.FirePosition);
        float desiredHorizontalDistance = Mathf.Max(activeExtinguisher.PreferredSprayDistance, requiredHorizontalDistance);
        bool shouldReposition =
            horizontalDistanceToFire > activeExtinguisher.MaxSprayDistance ||
            horizontalDistanceToFire < desiredHorizontalDistance - 0.35f;
        if (!shouldReposition)
        {
            return MoveTaskDirective.Success();
        }

        Vector3 desiredPosition = ResolveExtinguishPosition(state.TargetSearchPoint, state.FirePosition, desiredHorizontalDistance);
        return ShouldIssueExtinguisherApproachMove(desiredPosition)
            ? MoveTaskDirective.Running(desiredPosition)
            : MoveTaskDirective.Continue();
    }
}
