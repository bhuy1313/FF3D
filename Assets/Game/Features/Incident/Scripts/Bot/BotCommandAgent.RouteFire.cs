using UnityEngine;
using UnityEngine.AI;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private sealed class MoveToRouteFireTask : MoveToPositionTask
    {
        public MoveToRouteFireTask() : base(
            "Return To Route Fire",
            agent => agent.UpdateRouteFireReturnMove(),
            onStart: agent => agent.SetRouteFirePhase(RouteFirePhase.ReturnToFire),
            moveAction: (agent, destination) => agent.MoveToIgnoringRouteFireInterrupt(destination))
        {
        }
    }

    private sealed class SuppressRouteFireTask : IBotPlanTask
    {
        public string Name => "Suppress Route Fire";

        public void OnStart(BotCommandAgent agent)
        {
            agent.SetRouteFirePhase(RouteFirePhase.Extinguish);
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            IFireTarget blockingFire = agent.ResolveCurrentRouteBlockingFireTarget();
            if (blockingFire == null || !blockingFire.IsBurning)
            {
                agent.ClearRouteFireRuntime();
                return BotPlanTaskStatus.Success;
            }

            IBotExtinguisherItem equippedTool = agent.ResolveHeldSuppressionTool();
            if (equippedTool == null || BotCommandAgent.UsesPreciseAim(equippedTool))
            {
                agent.QueueRouteFireRecovery();
                return BotPlanTaskStatus.Success;
            }

            if (BotCommandAgent.IsUnsafeSuppressionToolForFire(equippedTool, blockingFire))
            {
                agent.StopExtinguisher();
                agent.sprayReadyTime = -1f;
                agent.preferredExtinguishTool = null;
                agent.QueueRouteFireRecovery();
                return BotPlanTaskStatus.Success;
            }

            if (!equippedTool.HasUsableCharge)
            {
                agent.StopExtinguisher();
                agent.sprayReadyTime = -1f;
                agent.preferredExtinguishTool = null;
                agent.QueueRouteFireRecovery();
                return BotPlanTaskStatus.Success;
            }

            Vector3 firePosition = blockingFire.GetWorldPosition();
            agent.activeExtinguisher = equippedTool;
            agent.PreparePointFireExtinguisherSuppression(blockingFire, firePosition, agent.transform.position);
            agent.currentExtinguishLaunchDirection = Vector3.zero;
            agent.hasCurrentExtinguishLaunchDirection = false;
            agent.LogPathClearingFlow(
                $"route-fire-stop:{BotCommandAgent.GetDebugTargetName(blockingFire)}",
                "Stop.");
            if (!agent.IsPointFireExtinguisherSprayReady(firePosition, false))
            {
                return BotPlanTaskStatus.Running;
            }

            agent.SprayPointFireExtinguisher(
                blockingFire,
                firePosition,
                agent.routeFireDetectionRadius,
                false,
                false,
                "Clearing fire from route.");

            if (blockingFire.IsBurning)
            {
                return BotPlanTaskStatus.Running;
            }

            agent.StopExtinguisher();
            agent.sprayReadyTime = -1f;
            agent.LogPathClearingFlow(
                $"route-fire-open:{BotCommandAgent.GetDebugTargetName(blockingFire)}",
                "Route is clear.");
            agent.ClearRouteFireRuntime();
            return BotPlanTaskStatus.Success;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
            if (!interrupted)
            {
                return;
            }

            agent.StopExtinguisher();
            agent.sprayReadyTime = -1f;
        }
    }

    private bool IsRouteFireClearingActive()
    {
        return currentRouteFirePhase != RouteFirePhase.Idle &&
               ((currentRouteBlockingFire != null && currentRouteBlockingFire.IsBurning) ||
                (currentRouteBlockingFireGroupTarget != null && currentRouteBlockingFireGroupTarget.HasActiveFires));
    }

    private BotPlan BuildRouteFireInterruptPlan()
    {
        BotPlan plan = new BotPlan("RouteFire");
        switch (currentRouteFirePhase)
        {
            case RouteFirePhase.AcquireTool:
                plan.Add(new AcquireSuppressionToolTask(SuppressionToolAcquisitionKind.RouteFireInterrupt))
                    .Add(new MoveToRouteFireTask())
                    .Add(new SuppressRouteFireTask());
                break;
            case RouteFirePhase.ReturnToFire:
                plan.Add(new MoveToRouteFireTask())
                    .Add(new SuppressRouteFireTask());
                break;
            case RouteFirePhase.Extinguish:
                plan.Add(new SuppressRouteFireTask());
                break;
        }

        return plan;
    }

    private void QueueRouteFireRecovery()
    {
        planProcessor?.InjectFront(
            this,
            new AcquireSuppressionToolTask(SuppressionToolAcquisitionKind.RouteFireInterrupt),
            new MoveToRouteFireTask(),
            new SuppressRouteFireTask());
    }

    private IFireTarget ResolveCurrentRouteBlockingFireTarget()
    {
        if (currentRouteBlockingFire != null && currentRouteBlockingFire.IsBurning)
        {
            return currentRouteBlockingFire;
        }

        if (currentRouteBlockingFireGroupTarget != null && currentRouteBlockingFireGroupTarget.HasActiveFires)
        {
            currentRouteBlockingFire = ResolveRepresentativeFireTarget(currentRouteBlockingFireGroupTarget, transform.position);
        }
        else
        {
            currentRouteBlockingFire = null;
        }

        return currentRouteBlockingFire != null && currentRouteBlockingFire.IsBurning
            ? currentRouteBlockingFire
            : null;
    }

    private void ClearRouteFireRuntime()
    {
        ClearHeadAimFocus();
        ClearHandAimFocus();
        ResetExtinguishCrouchState();
        StopExtinguisher();
        ClearExtinguisherTargetLock();
        SetPickupWindow(false);
        ReleaseCommittedTool();
        activeExtinguisher = null;
        preferredExtinguishTool = null;
        sprayReadyTime = -1f;
        currentExtinguishTargetPosition = default;
        currentExtinguishAimPoint = default;
        currentExtinguishLaunchDirection = default;
        hasCurrentExtinguishTargetPosition = false;
        hasCurrentExtinguishAimPoint = false;
        hasCurrentExtinguishLaunchDirection = false;
        currentRouteFirePhase = RouteFirePhase.Idle;
        ReleaseReservation(currentRouteBlockingFireGroupTarget);
        currentRouteBlockingFireGroupTarget = null;
        currentRouteBlockingFire = null;
    }

    private void SetRouteFirePhase(RouteFirePhase phase)
    {
        currentRouteFirePhase = phase;
    }

    private bool ShouldUseFireHoseCrouch(IBotExtinguisherItem tool)
    {
        return crouchBeforeFireHoseSpray && tool != null && UsesPreciseAim(tool);
    }

    private void ResetExtinguishCrouchState()
    {
        crouchReadyTime = -1f;
        if (behaviorContext != null)
        {
            behaviorContext.SetCrouchAnimation(false);
            behaviorContext.SetExtinguishStance(-1f);
        }
    }

    private bool TryHandleRouteBlockingFire(Vector3 destination)
    {
        if (!enableRouteFireClearing ||
            navMeshAgent == null ||
            !navMeshAgent.enabled ||
            !navMeshAgent.isOnNavMesh)
        {
            ClearRouteFireRuntime();
            return false;
        }

        if (IsRouteFireClearingActive())
        {
            return true;
        }

        if (!TryResolveNearbyRouteFire(out IFireTarget detectedBlockingFire))
        {
            ClearRouteFireRuntime();
            return false;
        }

        currentRouteBlockingFireGroupTarget = FindClosestActiveFireGroup(detectedBlockingFire.GetWorldPosition());
        currentRouteBlockingFire = detectedBlockingFire;
        TrySuspendFollowIntoMove(destination);
        SetRouteFirePhase(RouteFirePhase.AcquireTool);
        LogPathClearingFlow(
            $"route-fire-detected:{BotCommandAgent.GetDebugTargetName(detectedBlockingFire)}",
            "Detected fire blocking the path.");

        if (planProcessor == null || !planProcessor.HasActivePlan)
        {
            ClearRouteFireRuntime();
            return false;
        }

        planProcessor.InterruptWith(
            this,
            new AcquireSuppressionToolTask(SuppressionToolAcquisitionKind.RouteFireInterrupt),
            new MovePickupTask(),
            new MoveToRouteFireTask(),
            new SuppressRouteFireTask());
        return true;
    }

    private bool TryResolveNearbyRouteFire(out IFireTarget blockingFire)
    {
        blockingFire = null;
        if (!enableRouteFireClearing)
        {
            return false;
        }

        if (lockedExtinguisherFireTarget != null &&
            lockedExtinguisherFireTarget.IsBurning &&
            IsFireWithinRouteDetectionRadius(lockedExtinguisherFireTarget, transform.position, routeFireDetectionRadius))
        {
            blockingFire = lockedExtinguisherFireTarget;
            return true;
        }

        IFireGroupTarget nearbyGroup = FindClosestActiveFireGroup(transform.position);
        IFireTarget representativeTarget = ResolveRepresentativeFireTarget(nearbyGroup, transform.position);
        if (representativeTarget != null &&
            representativeTarget.IsBurning &&
            IsFireWithinRouteDetectionRadius(representativeTarget, transform.position, routeFireDetectionRadius))
        {
            blockingFire = representativeTarget;
            return true;
        }

        return TryResolveBurningFireWithinRadius(transform.position, routeFireDetectionRadius, out blockingFire);
    }

    private bool IsFireWithinRouteDetectionRadius(IFireTarget fireTarget, Vector3 origin, float detectionRadius)
    {
        if (fireTarget == null || !fireTarget.IsBurning)
        {
            return false;
        }

        if (Mathf.Abs(fireTarget.GetWorldPosition().y - origin.y) > routeFireVerticalTolerance)
        {
            return false;
        }

        return GetDistanceToFireEdge(origin, fireTarget.GetWorldPosition(), fireTarget) <= Mathf.Max(0.05f, detectionRadius);
    }

    private bool TryResolveBurningFireWithinRadius(Vector3 origin, float detectionRadius, out IFireTarget blockingFire)
    {
        blockingFire = null;
        float effectiveRadius = Mathf.Max(0.05f, detectionRadius);
        IFireGroupTarget nearbyGroup = FindClosestActiveFireGroup(origin);
        IFireTarget representativeTarget = ResolveRepresentativeFireTarget(nearbyGroup, origin);
        if (representativeTarget != null &&
            representativeTarget.IsBurning &&
            !BotRuntimeRegistry.Reservations.IsReservedByOther(representativeTarget, gameObject) &&
            Mathf.Abs(representativeTarget.GetWorldPosition().y - origin.y) <= routeFireVerticalTolerance)
        {
            float representativeEdgeDistance = GetDistanceToFireEdge(origin, representativeTarget.GetWorldPosition(), representativeTarget);
            if (representativeEdgeDistance <= effectiveRadius)
            {
                blockingFire = representativeTarget;
                return true;
            }
        }

        int hitCount = Physics.OverlapSphereNonAlloc(origin, effectiveRadius, routeFireDetectionHits, ~0, QueryTriggerInteraction.Collide);
        if (hitCount <= 0)
        {
            return false;
        }

        blockingFire = FindClosestRouteFireFallbackTarget(origin, effectiveRadius, hitCount);
        return blockingFire != null;
    }

    private IFireTarget FindClosestRouteFireFallbackTarget(Vector3 origin, float effectiveRadius, int hitCount)
    {
        IFireTarget bestTarget = null;
        float bestDistance = float.PositiveInfinity;

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider hitCollider = routeFireDetectionHits[hitIndex];
            if (hitCollider == null)
            {
                continue;
            }

            foreach (IFireTarget candidate in BotRuntimeRegistry.ActiveFireTargets)
            {
                if (candidate == null || !candidate.IsBurning)
                {
                    continue;
                }

                if (BotRuntimeRegistry.Reservations.IsReservedByOther(candidate, gameObject))
                {
                    continue;
                }

                if (Mathf.Abs(candidate.GetWorldPosition().y - origin.y) > routeFireVerticalTolerance)
                {
                    continue;
                }

                if (!IsColliderPartOfFireTarget(hitCollider, candidate))
                {
                    continue;
                }

                float edgeDistance = GetDistanceToFireEdge(origin, candidate.GetWorldPosition(), candidate);
                if (edgeDistance > effectiveRadius || edgeDistance >= bestDistance)
                {
                    continue;
                }

                bestDistance = edgeDistance;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    private BotPlanTaskStatus TryUpdateRouteFireSuppressionToolAcquisition(out bool movingToPickup)
    {
        movingToPickup = false;
        IFireTarget blockingFire = ResolveCurrentRouteBlockingFireTarget();
        if (blockingFire == null || !blockingFire.IsBurning)
        {
            ClearRouteFireRuntime();
            return BotPlanTaskStatus.Success;
        }

        IBotExtinguisherItem equippedTool = ResolveHeldSuppressionTool();
        if (equippedTool != null &&
            equippedTool.HasUsableCharge &&
            !BotCommandAgent.IsUnsafeSuppressionToolForFire(equippedTool, blockingFire) &&
            !BotCommandAgent.UsesPreciseAim(equippedTool))
        {
            SetRouteFirePhase(RouteFirePhase.ReturnToFire);
            return BotPlanTaskStatus.Success;
        }

        if (HasMovePickupTarget)
        {
            movingToPickup = true;
            return TryCompleteMovePickupTarget()
                ? BotPlanTaskStatus.Success
                : BotPlanTaskStatus.Running;
        }

        Vector3 firePosition = blockingFire.GetWorldPosition();
        if (!TryResolveSuppressionTool(
            firePosition,
            firePosition,
            null,
            blockingFire,
            BotExtinguishCommandMode.PointFire,
            BotExtinguishEngagementMode.DirectBestTool,
            true,
            out IBotExtinguisherItem routeTool))
        {
            ClearHeadAimFocus();
            ClearHandAimFocus();
            StopExtinguisher();
            StopNavMeshMovement();
            LogPathClearingFlow(
                $"route-fire-no-tool:{BotCommandAgent.GetDebugTargetName(blockingFire)}",
                "No usable tool available.");
            return BotPlanTaskStatus.Running;
        }

        if (!TryAdvanceSuppressionToolAcquisition(routeTool, false))
        {
            if (TryPrepareSuppressionToolMovePickup(routeTool))
            {
                movingToPickup = true;
                return BotPlanTaskStatus.Running;
            }

            return BotPlanTaskStatus.Running;
        }

        SetRouteFirePhase(RouteFirePhase.ReturnToFire);
        return BotPlanTaskStatus.Success;
    }

    private MoveTaskDirective UpdateRouteFireReturnMove()
    {
        IFireTarget blockingFire = ResolveCurrentRouteBlockingFireTarget();
        if (blockingFire == null || !blockingFire.IsBurning)
        {
            ClearRouteFireRuntime();
            return MoveTaskDirective.Success();
        }

        IBotExtinguisherItem equippedTool = ResolveHeldSuppressionTool();
        if (equippedTool == null || !equippedTool.HasUsableCharge || BotCommandAgent.UsesPreciseAim(equippedTool))
        {
            QueueRouteFireRecovery();
            return MoveTaskDirective.Success();
        }

        Vector3 firePosition = blockingFire.GetWorldPosition();
        if (!IsFireWithinRouteDetectionRadius(blockingFire, transform.position, routeFireDetectionRadius))
        {
            LogPathClearingFlow(
                $"route-fire-return:{BotCommandAgent.GetDebugTargetName(blockingFire)}:{BotCommandAgent.FormatFlowVectorKey(firePosition)}",
                "Returning to blocked fire.");
            return MoveTaskDirective.Running(firePosition);
        }

        SetRouteFirePhase(RouteFirePhase.Extinguish);
        return MoveTaskDirective.Success();
    }
}
