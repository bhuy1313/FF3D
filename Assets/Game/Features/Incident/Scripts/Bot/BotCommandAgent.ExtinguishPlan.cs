using UnityEngine;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
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
                : agent.ResolveActiveFireTarget(targetSearchPoint);

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
        }
    }

    private sealed class AcquireExtinguishToolTask : IBotPlanTask
    {
        public string Name => "Acquire Suppression Tool";

        public void OnStart(BotCommandAgent agent)
        {
            agent.SetExtinguishSubtask(BotExtinguishSubtask.AcquireTool, "Acquiring suppression tool.");
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            if (!agent.TrySyncExtinguishPlanOrder())
            {
                return BotPlanTaskStatus.Failure;
            }

            BotExtinguishPlanState state = agent.extinguishPlanState;
            state.PlannedTool = agent.ResolveCommittedExtinguishTool(
                state.OrderPoint,
                state.FirePosition,
                state.FireGroup,
                state.FireTarget,
                state.Mode);

            if (state.PlannedTool == null)
            {
                if (state.Mode == BotExtinguishCommandMode.FireGroup &&
                    agent.TryFallbackFireGroupOrderToPointFire(state.FireTarget, out Vector3 fallbackDestination))
                {
                    agent.SetExtinguishSubtask(BotExtinguishSubtask.Recover, $"Replanning extinguish route through point fire at {fallbackDestination}.");
                    agent.InvalidateActiveCommandPlan();
                    return BotPlanTaskStatus.Success;
                }

                agent.FailActiveExtinguishOrder("No available suppression tool can reach the assigned fire.", BotTaskStatus.Blocked);
                return BotPlanTaskStatus.Failure;
            }

            state.UsesPreciseAim = BotCommandAgent.UsesPreciseAim(state.PlannedTool);
            if (!agent.TryEnsureExtinguisherEquipped(state.PlannedTool))
            {
                agent.ClearHeadAimFocus();
                agent.ClearHandAimFocus();
                agent.ResetExtinguishCrouchState();
                return BotPlanTaskStatus.Running;
            }

            if (agent.extinguishStartupPending)
            {
                agent.SetExtinguishSubtask(BotExtinguishSubtask.Recover, "Recovering extinguish order.");
                agent.ClearHeadAimFocus();
                agent.ClearHandAimFocus();
                agent.extinguishStartupPending = false;
                return BotPlanTaskStatus.Running;
            }

            if (agent.activeExtinguisher == null || !agent.activeExtinguisher.HasUsableCharge)
            {
                agent.FailActiveExtinguishOrder("Active suppression tool is out of charge.", BotTaskStatus.Failed);
                return BotPlanTaskStatus.Failure;
            }

            return BotPlanTaskStatus.Success;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
        }
    }

    private sealed class MoveToExtinguishPositionTask : IBotPlanTask
    {
        public string Name => "Move To Extinguish Position";

        public void OnStart(BotCommandAgent agent)
        {
            agent.SetExtinguishSubtask(BotExtinguishSubtask.MoveToFire, "Moving to extinguish position.");
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            if (!agent.TrySyncExtinguishPlanOrder() || agent.activeExtinguisher == null)
            {
                return BotPlanTaskStatus.Failure;
            }

            BotExtinguishPlanState state = agent.extinguishPlanState;
            Vector3 botPosition = agent.transform.position;

            if (!state.UsesPreciseAim)
            {
                IFireTarget routeFireTarget = agent.ResolveExtinguisherRouteTarget(state.TargetSearchPoint);
                if (routeFireTarget != null && routeFireTarget.IsBurning)
                {
                    state.FireTarget = routeFireTarget;
                    state.FirePosition = routeFireTarget.GetWorldPosition();
                    return BotPlanTaskStatus.Success;
                }

                if (!agent.IsWithinArrivalDistance(state.OrderPoint))
                {
                    if (agent.ShouldIssueExtinguisherApproachMove(state.OrderPoint))
                    {
                        agent.MoveTo(state.OrderPoint);
                    }

                    return BotPlanTaskStatus.Running;
                }

                return BotPlanTaskStatus.Success;
            }

            IFireGroupTarget fireGroup = state.FireGroup != null && state.FireGroup.HasActiveFires
                ? state.FireGroup
                : agent.ResolveIssuedFireGroupTarget(state.TargetSearchPoint);
            IFireTarget fireTarget = state.FireTarget != null && state.FireTarget.IsBurning
                ? state.FireTarget
                : agent.ResolveActiveFireTarget(state.TargetSearchPoint);
            if ((fireGroup == null || !fireGroup.HasActiveFires) && (fireTarget == null || !fireTarget.IsBurning))
            {
                agent.CompleteExtinguishOrder("No active fire target remained near the assigned point.");
                return BotPlanTaskStatus.Success;
            }

            state.FireGroup = fireGroup;
            state.FireTarget = fireTarget;
            state.FirePosition = fireTarget != null && fireTarget.IsBurning
                ? fireTarget.GetWorldPosition()
                : fireGroup.GetWorldCenter();

            float horizontalDistanceToFire = BotCommandAgent.GetHorizontalDistance(botPosition, state.FirePosition);
            float requiredHorizontalDistance = agent.GetRequiredHorizontalDistanceForAim(agent.activeExtinguisher, state.FirePosition);
            float desiredHorizontalDistance = Mathf.Max(agent.activeExtinguisher.PreferredSprayDistance, requiredHorizontalDistance);
            bool shouldReposition =
                horizontalDistanceToFire > agent.activeExtinguisher.MaxSprayDistance ||
                horizontalDistanceToFire < desiredHorizontalDistance - 0.35f;
            if (!shouldReposition)
            {
                return BotPlanTaskStatus.Success;
            }

            Vector3 desiredPosition = agent.ResolveExtinguishPosition(state.TargetSearchPoint, state.FirePosition, desiredHorizontalDistance);
            if (agent.ShouldIssueExtinguisherApproachMove(desiredPosition))
            {
                agent.MoveTo(desiredPosition);
            }

            return BotPlanTaskStatus.Running;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
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

            if (agent.activeExtinguisher == null)
            {
                agent.FailActiveExtinguishOrder("No active suppression tool equipped.", BotTaskStatus.Blocked);
                return BotPlanTaskStatus.Failure;
            }

            BotExtinguishPlanState state = agent.extinguishPlanState;
            if (!agent.activeExtinguisher.HasUsableCharge)
            {
                agent.HandleExtinguishChargeDepleted(state);
                return agent.behaviorContext != null && agent.behaviorContext.HasExtinguishOrder
                    ? BotPlanTaskStatus.Success
                    : BotPlanTaskStatus.Failure;
            }

            if (state.UsesPreciseAim)
            {
                IFireGroupTarget fireGroup = state.FireGroup != null && state.FireGroup.HasActiveFires
                    ? state.FireGroup
                    : agent.ResolveIssuedFireGroupTarget(state.TargetSearchPoint);
                IFireTarget fireTarget = state.FireTarget != null && state.FireTarget.IsBurning
                    ? state.FireTarget
                    : agent.ResolveActiveFireTarget(state.TargetSearchPoint);
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

            if (!agent.activeExtinguisher.HasUsableCharge)
            {
                agent.HandleExtinguishChargeDepleted(state);
                return BotPlanTaskStatus.Success;
            }

            return BotPlanTaskStatus.Running;
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

    private BotPlanProcessor planProcessor;
    private readonly BotExtinguishPlanState extinguishPlanState = new BotExtinguishPlanState();
    private string activeCommandPlanKey = string.Empty;

    private BotPlan BuildExtinguishPlan()
    {
        return new BotPlan("Extinguish")
            .Add(new AcquireExtinguishTargetTask())
            .Add(new AcquireExtinguishToolTask())
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
        sprayReadyTime = -1f;
        ClearHeadAimFocus();
        ClearHandAimFocus();
        ResetExtinguishCrouchState();
        ReleaseCommittedToolIfMatches(activeExtinguisher);
        activeExtinguisher = null;
        preferredExtinguishTool = null;
        planProcessor.InjectFront(
            this,
            new AcquireExtinguishToolTask(),
            new MoveToExtinguishPositionTask(),
            new SuppressFireTask());
        SetExtinguishSubtask(BotExtinguishSubtask.Recover, "Suppression tool depleted. Acquiring replacement tool.");
    }
}
