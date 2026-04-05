using UnityEngine;
using UnityEngine.AI;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private bool IsRouteFireClearingActive()
    {
        return currentRouteBlockingFire != null && currentRouteBlockingFire.IsBurning;
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
        currentRouteBlockingFire = null;
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

        if (!TryCalculatePreviewPath(destination, out _, out NavMeshPath previewPath) || previewPath == null)
        {
            ClearRouteFireRuntime();
            return false;
        }

        IFireTarget lockedBlockingFire = currentRouteBlockingFire != null &&
                                         currentRouteBlockingFire.IsBurning &&
                                         IsExtinguisherTargetLocked(currentRouteBlockingFire)
            ? currentRouteBlockingFire
            : null;

        if (!TryResolveBlockingFireOnPath(previewPath, out IFireTarget blockingFire))
        {
            if (lockedBlockingFire != null)
            {
                blockingFire = lockedBlockingFire;
            }
            else
            {
                if (currentRouteBlockingFire != null)
                {
                    LogPathClearingFlow(
                        $"route-fire-open:{GetDebugTargetName(currentRouteBlockingFire)}",
                        "Route is clear.");
                }

                ClearRouteFireRuntime();
                return false;
            }
        }

        if (blockingFire == null)
        {
            if (currentRouteBlockingFire != null)
            {
                LogPathClearingFlow(
                    $"route-fire-open:{GetDebugTargetName(currentRouteBlockingFire)}",
                    "Route is clear.");
            }

            ClearRouteFireRuntime();
            return false;
        }

        currentRouteBlockingFire = blockingFire;
        LogPathClearingFlow(
            $"route-fire-detected:{GetDebugTargetName(blockingFire)}",
            "Detected fire blocking the path.");

        Vector3 firePosition = blockingFire.GetWorldPosition();
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

        IBotExtinguisherItem equippedTool = activeExtinguisher ?? routeTool;
        if (equippedTool == null || UsesPreciseAim(equippedTool))
        {
            ClearHeadAimFocus();
            ClearHandAimFocus();
            return true;
        }

        PrimeExtinguisherTargetLock(equippedTool, blockingFire);

        float desiredStandOffDistance = GetDesiredExtinguisherStandOffDistanceLocked(equippedTool, blockingFire);
        float desiredHorizontalDistance = GetDesiredExtinguisherCenterDistance(equippedTool, blockingFire);
        float horizontalDistanceToFire = GetHorizontalDistance(transform.position, firePosition);
        float edgeDistanceToFire = GetFireEdgeDistance(transform.position, firePosition, blockingFire);
        float distanceToFire = GetDistanceToFireEdge(transform.position, firePosition, blockingFire);
        float allowedEdgeRange = GetAllowedExtinguisherEdgeRange(equippedTool);
        bool keepCurrentStandDistance = IsExtinguisherTargetLocked(blockingFire);
        bool canExtinguishFromCurrentPosition = CanExtinguishFromCurrentPosition(equippedTool, firePosition, blockingFire);
        bool isFartherThanPreferred = edgeDistanceToFire > desiredStandOffDistance + ExtinguisherRangeSlack;
        bool isCloserThanPreferred = edgeDistanceToFire + ExtinguisherRangeSlack < desiredStandOffDistance;
        bool shouldReposition =
            distanceToFire > allowedEdgeRange ||
            (!keepCurrentStandDistance && (isFartherThanPreferred || isCloserThanPreferred)) ||
            !canExtinguishFromCurrentPosition;

        if (shouldReposition)
        {
            Vector3 desiredPosition = ResolveExtinguisherApproachPosition(transform.position, firePosition, desiredHorizontalDistance);
            LogPathClearingFlow(
                $"route-fire-move:{GetDebugTargetName(blockingFire)}:{FormatFlowVectorKey(desiredPosition)}",
                "Moving.");
            ClearHeadAimFocus();
            ClearHandAimFocus();
            StopExtinguisher();
            sprayReadyTime = -1f;
            TrySetDestinationDirect(desiredPosition);
            return true;
        }

        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = true;
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
        TryApplyWaterToFireTarget(equippedTool, blockingFire, firePosition);

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

    private bool TryResolveBlockingFireOnPath(NavMeshPath previewPath, out IFireTarget blockingFire)
    {
        blockingFire = null;
        if (!enableRouteFireClearing || previewPath == null || previewPath.corners == null || previewPath.corners.Length == 0)
        {
            return false;
        }

        float bestPathDistance = float.PositiveInfinity;
        Vector3 segmentStart = transform.position;
        float accumulatedPathDistance = 0f;

        for (int i = 0; i < previewPath.corners.Length; i++)
        {
            Vector3 segmentEnd = previewPath.corners[i];
            float segmentLength = GetHorizontalDistance(segmentStart, segmentEnd);
            if (segmentLength <= 0.01f)
            {
                segmentStart = segmentEnd;
                continue;
            }

            foreach (IFireTarget candidate in BotRuntimeRegistry.ActiveFireTargets)
            {
                if (candidate == null || !candidate.IsBurning)
                {
                    continue;
                }

                Vector3 firePosition = candidate.GetWorldPosition();
                if (Mathf.Abs(firePosition.y - transform.position.y) > routeFireVerticalTolerance)
                {
                    continue;
                }

                float detectionRadius = Mathf.Max(0.05f, candidate.GetWorldRadius() + routeFireDetectionPadding);
                float distanceToSegment = DistanceToSegment2D(firePosition, segmentStart, segmentEnd, out float t);
                if (distanceToSegment > detectionRadius)
                {
                    continue;
                }

                float pathDistance = accumulatedPathDistance + segmentLength * Mathf.Clamp01(t);
                if (pathDistance < bestPathDistance)
                {
                    bestPathDistance = pathDistance;
                    blockingFire = candidate;
                }
            }

            accumulatedPathDistance += segmentLength;
            segmentStart = segmentEnd;
        }

        return blockingFire != null;
    }
}
