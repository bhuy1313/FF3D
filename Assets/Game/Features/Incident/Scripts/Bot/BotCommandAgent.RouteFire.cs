using UnityEngine;
using UnityEngine.AI;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private bool IsRouteFireClearingActive()
    {
        return currentRouteBlockingFire != null && currentRouteBlockingFire.IsBurning;
    }

    private void ClearRouteFireRuntime(bool releaseCommittedTool = true)
    {
        ClearHeadAimFocus();
        ClearHandAimFocus();
        ResetExtinguishCrouchState();
        StopExtinguisher();
        ClearExtinguisherTargetLock();
        SetPickupWindow(false);
        if (releaseCommittedTool)
        {
            ReleaseCommittedTool();
        }

        sprayReadyTime = -1f;
        currentRouteBlockingFire = null;
    }

    private void ResetExtinguishCrouchState()
    {
        if (behaviorContext != null)
        {
            behaviorContext.SetCrouchAnimation(false);
        }
    }

    private bool TryHandleRouteBlockingFire(Vector3 destination)
    {
        return TryHandleSharedFireRoute(destination, false);
    }

    private bool TryHandleProactiveExtinguishRoute(Vector3 destination)
    {
        return TryHandleSharedFireRoute(destination, true);
    }

    private bool TryHandleSharedFireRoute(Vector3 destination, bool acquireToolImmediately)
    {
        if (!enableRouteFireClearing ||
            navMeshAgent == null ||
            !navMeshAgent.enabled ||
            !navMeshAgent.isOnNavMesh ||
            behaviorContext == null ||
            behaviorContext.HasFollowOrder)
        {
            ClearRouteFireRuntime(!acquireToolImmediately);
            return false;
        }

        bool hasEquippedRouteTool = IsRouteFireExtinguisherUsable(activeExtinguisher);
        IFireTarget blockingFire = hasEquippedRouteTool &&
                                   currentRouteBlockingFire != null &&
                                   currentRouteBlockingFire.IsBurning &&
                                   IsWithinRouteFireDetection(currentRouteBlockingFire)
            ? currentRouteBlockingFire
            : FindNearbyRouteFire();

        if (blockingFire == null)
        {
            if (currentRouteBlockingFire != null)
            {
                LogPathClearingFlow(
                    $"route-fire-open:{GetDebugTargetName(currentRouteBlockingFire)}",
                    "Route is clear.");
            }

            ClearRouteFireRuntime(!acquireToolImmediately);
            SetCurrentFireTarget(null);

            if (!acquireToolImmediately)
            {
                return false;
            }

            if (IsWithinArrivalDistance(destination))
            {
                navMeshAgent.ResetPath();
                navMeshAgent.isStopped = true;
                return false;
            }

            SetExtinguishSubtask(BotExtinguishSubtask.MoveToFire, "Moving to extinguish destination.");
            UpdateExtinguishDebugStage(ExtinguishDebugStage.MovingToFire, $"Moving toward extinguish destination {destination}.");
            TrySetDestinationDirect(destination);
            return true;
        }

        bool targetChanged = !ReferenceEquals(currentRouteBlockingFire, blockingFire);
        if (targetChanged)
        {
            sprayReadyTime = -1f;
            StopExtinguisher();
            ClearExtinguisherTargetLock();
        }

        currentRouteBlockingFire = blockingFire;
        SetCurrentFireTarget(blockingFire);
        LogPathClearingFlow(
            $"route-fire-detected:{GetDebugTargetName(blockingFire)}",
            "Detected fire nearby, stopping to clear.");

        Vector3 firePosition = blockingFire.GetWorldPosition();
        IBotExtinguisherItem routeTool = IsRouteFireExtinguisherUsable(activeExtinguisher)
            ? activeExtinguisher
            : ResolveCommittedExtinguishTool(
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

        SetExtinguishSubtask(BotExtinguishSubtask.AcquireTool, "Acquiring Fire Extinguisher.");
        if (!TryEnsureExtinguisherEquipped(routeTool))
        {
            if (!acquireToolImmediately)
            {
                if (routeTool is IPickupable pickupable && pickupable.Rigidbody != null && !routeTool.IsHeld)
                {
                    SetMovePickupTarget(pickupable);
                }

                currentRouteBlockingFire = null;
                SetCurrentFireTarget(null);
                sprayReadyTime = -1f;
                ClearExtinguisherTargetLock();
            }

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

        if (!equippedTool.HasUsableCharge)
        {
            StopExtinguisher();
            ClearRouteFireRuntime(!acquireToolImmediately);
            return false;
        }

        PrimeExtinguisherTargetLock(equippedTool, blockingFire);
        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = true;
        AimTowards(firePosition);
        SetHandAimFocus(firePosition);
        SetHeadAimFocus(firePosition);
        SetExtinguishSubtask(BotExtinguishSubtask.AimAtFire, "Aiming at nearby fire.");

        if (!IsAimSettled(equippedTool, firePosition))
        {
            StopExtinguisher();
            sprayReadyTime = -1f;
            return true;
        }

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

        SetExtinguishSubtask(BotExtinguishSubtask.Spray, "Spraying nearby fire.");
        LogPathClearingFlow(
            $"route-fire-spray:{GetDebugTargetName(blockingFire)}",
            "Detected nearby fire. Stop and extinguish.");
        LockExtinguisherTarget(blockingFire);
        equippedTool.SetExternalSprayState(true, gameObject);
        ApplyRouteFireSuppression(equippedTool, blockingFire);

        if (!blockingFire.IsBurning)
        {
            StopExtinguisher();
            sprayReadyTime = -1f;
            LogPathClearingFlow(
                $"route-fire-open:{GetDebugTargetName(blockingFire)}",
                "Route is clear.");
            ClearRouteFireRuntime(!acquireToolImmediately);
            SetCurrentFireTarget(null);
            return acquireToolImmediately;
        }

        return true;
    }

    private bool IsWithinRouteFireDetection(IFireTarget fireTarget)
    {
        if (fireTarget == null || !fireTarget.IsBurning)
        {
            return false;
        }

        Vector3 firePosition = fireTarget.GetWorldPosition();
        Vector3 botPosition = transform.position;
        if (Mathf.Abs(firePosition.y - botPosition.y) > routeFireVerticalTolerance)
        {
            return false;
        }

        float detectionRadiusSq = Mathf.Max(0.1f, routeFireDetectionRadius);
        detectionRadiusSq *= detectionRadiusSq;
        return (firePosition - botPosition).sqrMagnitude <= detectionRadiusSq;
    }

    private IFireTarget FindNearbyRouteFire()
    {
        float nearestDistanceSq = Mathf.Max(0.1f, routeFireDetectionRadius);
        nearestDistanceSq *= nearestDistanceSq;
        IFireTarget nearestFire = null;
        Vector3 botPosition = transform.position;

        foreach (IFireTarget candidate in BotRuntimeRegistry.ActiveFireTargets)
        {
            if (candidate == null || !candidate.IsBurning)
            {
                continue;
            }

            Vector3 firePosition = candidate.GetWorldPosition();
            if (Mathf.Abs(firePosition.y - botPosition.y) > routeFireVerticalTolerance)
            {
                continue;
            }

            float distanceSq = (firePosition - botPosition).sqrMagnitude;
            if (distanceSq > nearestDistanceSq)
            {
                continue;
            }

            nearestDistanceSq = distanceSq;
            nearestFire = candidate;
        }

        return nearestFire;
    }

    private static void ApplyRouteFireSuppression(IBotExtinguisherItem tool, IFireTarget fireTarget)
    {
        if (tool == null || fireTarget == null || !fireTarget.IsBurning)
        {
            return;
        }

        float suppressionAmount = Mathf.Max(0f, tool.ApplyWaterPerSecond) * Time.deltaTime;
        if (suppressionAmount <= 0f)
        {
            return;
        }

        fireTarget.ApplySuppression(suppressionAmount, tool.SuppressionAgent);
    }
}
