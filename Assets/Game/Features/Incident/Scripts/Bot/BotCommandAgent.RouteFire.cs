using UnityEngine;
using UnityEngine.AI;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private bool IsRouteFireClearingActive()
    {
        return currentRouteFirePhase != RouteFirePhase.Idle &&
               currentRouteBlockingFire != null &&
               currentRouteBlockingFire.IsBurning;
    }

    private bool IsAcquiringRouteFireExtinguisher()
    {
        if (currentRouteFirePhase != RouteFirePhase.AcquireTool ||
            currentRouteBlockingFire == null ||
            !currentRouteBlockingFire.IsBurning ||
            committedExtinguishTool == null)
        {
            return false;
        }

        if (activeExtinguisher != null &&
            activeExtinguisher.IsHeld &&
            activeExtinguisher.ClaimOwner == gameObject &&
            !UsesPreciseAim(activeExtinguisher))
        {
            return false;
        }

        return committedExtinguishTool.IsAvailableTo(gameObject);
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

        if (currentRouteFirePhase == RouteFirePhase.Idle)
        {
            if (!TryResolveNearbyRouteFire(out IFireTarget detectedBlockingFire))
            {
                ClearRouteFireRuntime();
                return false;
            }

            currentRouteBlockingFire = detectedBlockingFire;
            SetRouteFirePhase(RouteFirePhase.AcquireTool);
            LogPathClearingFlow(
                $"route-fire-detected:{GetDebugTargetName(detectedBlockingFire)}",
                "Detected fire blocking the path.");
        }
        else if (currentRouteBlockingFire == null || !currentRouteBlockingFire.IsBurning)
        {
            ClearRouteFireRuntime();
            return false;
        }

        IFireTarget blockingFire = currentRouteBlockingFire;
        Vector3 firePosition = blockingFire.GetWorldPosition();

        if (currentRouteFirePhase == RouteFirePhase.AcquireTool)
        {
            if (IsAcquiringRouteFireExtinguisher())
            {
                if (!TryEnsureExtinguisherEquipped(committedExtinguishTool))
                {
                    ClearHeadAimFocus();
                    ClearHandAimFocus();
                    LogPathClearingFlow(
                        $"route-fire-search-tool:{GetToolName(committedExtinguishTool)}",
                        "Searching for Fire Extinguisher.");
                    return true;
                }
            }
            IBotExtinguisherItem routeTool = ResolveCommittedExtinguishTool(
                firePosition,
                firePosition,
                null,
                blockingFire,
                BotExtinguishCommandMode.PointFire);
            if (routeTool == null || UsesPreciseAim(routeTool))
            {
                ClearHeadAimFocus();
                ClearHandAimFocus();
                navMeshAgent.ResetPath();
                navMeshAgent.isStopped = true;
                LogPathClearingFlow(
                    $"route-fire-no-tool:{GetDebugTargetName(blockingFire)}",
                    "No usable tool available.");
                LogPathClearingFlow(
                    $"route-fire-stop:{GetDebugTargetName(blockingFire)}",
                    "Stop.");
                return true;
            }

            if (!TryEnsureExtinguisherEquipped(routeTool))
            {
                ClearHeadAimFocus();
                ClearHandAimFocus();
                LogPathClearingFlow(
                    $"route-fire-search-tool:{GetToolName(routeTool)}",
                    "Searching for Fire Extinguisher.");
                return true;
            }

            SetRouteFirePhase(RouteFirePhase.ReturnToFire);
        }

        IBotExtinguisherItem equippedTool = activeExtinguisher ?? committedExtinguishTool;
        if (equippedTool == null || UsesPreciseAim(equippedTool))
        {
            ClearHeadAimFocus();
            ClearHandAimFocus();
            return true;
        }

        if (currentRouteFirePhase == RouteFirePhase.ReturnToFire)
        {
            if (!IsFireWithinRouteDetectionRadius(blockingFire, transform.position, routeFireDetectionRadius))
            {
                ClearHeadAimFocus();
                ClearHandAimFocus();
                StopExtinguisher();
                sprayReadyTime = -1f;
                LogPathClearingFlow(
                    $"route-fire-return:{GetDebugTargetName(blockingFire)}:{FormatFlowVectorKey(firePosition)}",
                    "Returning to blocked fire.");
                TrySetDestinationDirect(firePosition);
                return true;
            }

            SetRouteFirePhase(RouteFirePhase.Extinguish);
        }

        currentExtinguishTargetPosition = firePosition;
        hasCurrentExtinguishTargetPosition = true;
        UpdateCurrentExtinguishAimData(equippedTool, firePosition);
        currentExtinguishAimPoint = firePosition;
        hasCurrentExtinguishAimPoint = true;
        currentExtinguishLaunchDirection = Vector3.zero;
        hasCurrentExtinguishLaunchDirection = false;
        equippedTool.ClearExternalAimDirection(gameObject);
        PrimeExtinguisherTargetLock(equippedTool, blockingFire);
        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = true;
        LogPathClearingFlow(
            $"route-fire-stop:{GetDebugTargetName(blockingFire)}",
            "Stop.");
        AimTowards(firePosition);
        SetHandAimFocus(firePosition);
        SetHeadAimFocus(firePosition);

        if (sprayReadyTime < 0f)
        {
            sprayReadyTime = Time.time + Mathf.Max(0f, sprayStartDelay);
            StopExtinguisher();
            return true;
        }

        if (Time.time < sprayReadyTime)
        {
            StopExtinguisher();
            return true;
        }

        LogPathClearingFlow(
            $"route-fire-spray:{GetDebugTargetName(blockingFire)}",
            "Clearing fire from route.");
        LockExtinguisherTarget(blockingFire);
        equippedTool.SetExternalSprayState(true, gameObject);
        TryApplyWaterToFireTarget(equippedTool, blockingFire, firePosition, routeFireDetectionRadius);

        if (!blockingFire.IsBurning)
        {
            StopExtinguisher();
            sprayReadyTime = -1f;
            LogPathClearingFlow(
                $"route-fire-open:{GetDebugTargetName(blockingFire)}",
                "Route is clear.");
            ClearRouteFireRuntime();
            return false;
        }

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
