using UnityEngine;
using UnityEngine.AI;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private sealed class AcquireRouteFireToolTask : IBotPlanTask
    {
        public string Name => "Acquire Route Fire Tool";

        public void OnStart(BotCommandAgent agent)
        {
            agent.SetRouteFirePhase(RouteFirePhase.AcquireTool);
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            if (agent.currentRouteBlockingFire == null || !agent.currentRouteBlockingFire.IsBurning)
            {
                agent.ClearRouteFireRuntime();
                return BotPlanTaskStatus.Success;
            }

            IBotExtinguisherItem equippedTool = agent.activeExtinguisher ?? agent.committedExtinguishTool;
            if (equippedTool != null &&
                equippedTool.HasUsableCharge &&
                !BotCommandAgent.UsesPreciseAim(equippedTool))
            {
                agent.SetRouteFirePhase(RouteFirePhase.ReturnToFire);
                return BotPlanTaskStatus.Success;
            }

            Vector3 firePosition = agent.currentRouteBlockingFire.GetWorldPosition();
            IBotExtinguisherItem routeTool = agent.ResolveCommittedExtinguishTool(
                firePosition,
                firePosition,
                null,
                agent.currentRouteBlockingFire,
                BotExtinguishCommandMode.PointFire);
            if (routeTool == null || BotCommandAgent.UsesPreciseAim(routeTool))
            {
                agent.ClearHeadAimFocus();
                agent.ClearHandAimFocus();
                agent.StopExtinguisher();
                agent.StopNavMeshMovement();
                agent.LogPathClearingFlow(
                    $"route-fire-no-tool:{BotCommandAgent.GetDebugTargetName(agent.currentRouteBlockingFire)}",
                    "No usable tool available.");
                return BotPlanTaskStatus.Running;
            }

            if (!agent.TryEnsureExtinguisherEquipped(routeTool))
            {
                agent.ClearHeadAimFocus();
                agent.ClearHandAimFocus();
                return BotPlanTaskStatus.Running;
            }

            agent.SetRouteFirePhase(RouteFirePhase.ReturnToFire);
            return BotPlanTaskStatus.Success;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
        }
    }

    private sealed class MoveToRouteFireTask : IBotPlanTask
    {
        public string Name => "Return To Route Fire";

        public void OnStart(BotCommandAgent agent)
        {
            agent.SetRouteFirePhase(RouteFirePhase.ReturnToFire);
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            if (agent.currentRouteBlockingFire == null || !agent.currentRouteBlockingFire.IsBurning)
            {
                agent.ClearRouteFireRuntime();
                return BotPlanTaskStatus.Success;
            }

            IBotExtinguisherItem equippedTool = agent.activeExtinguisher ?? agent.committedExtinguishTool;
            if (equippedTool == null || !equippedTool.HasUsableCharge || BotCommandAgent.UsesPreciseAim(equippedTool))
            {
                agent.planProcessor.InjectFront(
                    agent,
                    new AcquireRouteFireToolTask(),
                    new MoveToRouteFireTask(),
                    new SuppressRouteFireTask());
                return BotPlanTaskStatus.Success;
            }

            Vector3 firePosition = agent.currentRouteBlockingFire.GetWorldPosition();
            if (!agent.IsFireWithinRouteDetectionRadius(agent.currentRouteBlockingFire, agent.transform.position, agent.routeFireDetectionRadius))
            {
                agent.LogPathClearingFlow(
                    $"route-fire-return:{BotCommandAgent.GetDebugTargetName(agent.currentRouteBlockingFire)}:{BotCommandAgent.FormatFlowVectorKey(firePosition)}",
                    "Returning to blocked fire.");
                agent.TrySetDestinationDirect(firePosition);
                return BotPlanTaskStatus.Running;
            }

            agent.SetRouteFirePhase(RouteFirePhase.Extinguish);
            return BotPlanTaskStatus.Success;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
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
            IFireTarget blockingFire = agent.currentRouteBlockingFire;
            if (blockingFire == null || !blockingFire.IsBurning)
            {
                agent.ClearRouteFireRuntime();
                return BotPlanTaskStatus.Success;
            }

            IBotExtinguisherItem equippedTool = agent.activeExtinguisher ?? agent.committedExtinguishTool;
            if (equippedTool == null || BotCommandAgent.UsesPreciseAim(equippedTool))
            {
                agent.planProcessor.InjectFront(
                    agent,
                    new AcquireRouteFireToolTask(),
                    new MoveToRouteFireTask(),
                    new SuppressRouteFireTask());
                return BotPlanTaskStatus.Success;
            }

            if (!equippedTool.HasUsableCharge)
            {
                agent.StopExtinguisher();
                agent.sprayReadyTime = -1f;
                agent.ReleaseCommittedToolIfMatches(agent.activeExtinguisher);
                agent.activeExtinguisher = null;
                agent.preferredExtinguishTool = null;
                agent.planProcessor.InjectFront(
                    agent,
                    new AcquireRouteFireToolTask(),
                    new MoveToRouteFireTask(),
                    new SuppressRouteFireTask());
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
               currentRouteBlockingFire != null &&
               currentRouteBlockingFire.IsBurning;
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
        sprayReadyTime = -1f;
        currentExtinguishTargetPosition = default;
        currentExtinguishAimPoint = default;
        currentExtinguishLaunchDirection = default;
        hasCurrentExtinguishTargetPosition = false;
        hasCurrentExtinguishAimPoint = false;
        hasCurrentExtinguishLaunchDirection = false;
        currentRouteFirePhase = RouteFirePhase.Idle;
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
            !navMeshAgent.isOnNavMesh ||
            behaviorContext == null ||
            behaviorContext.HasExtinguishOrder ||
            behaviorContext.HasFollowOrder)
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

        currentRouteBlockingFire = detectedBlockingFire;
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
            new AcquireRouteFireToolTask(),
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
        int hitCount = Physics.OverlapSphereNonAlloc(origin, effectiveRadius, routeFireDetectionHits, ~0, QueryTriggerInteraction.Collide);
        if (hitCount <= 0)
        {
            return false;
        }

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
                blockingFire = candidate;
            }
        }

        return blockingFire != null;
    }
}
