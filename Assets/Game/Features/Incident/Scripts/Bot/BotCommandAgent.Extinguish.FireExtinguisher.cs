using UnityEngine;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private float PreparePointFireExtinguisherSuppression(
        IFireTarget fireTarget,
        Vector3 firePosition,
        Vector3 botPosition)
    {
        currentExtinguishTargetPosition = firePosition;
        hasCurrentExtinguishTargetPosition = true;
        UpdateCurrentExtinguishAimData(activeExtinguisher, firePosition);
        PrimeExtinguisherTargetLock(activeExtinguisher, fireTarget);

        float horizontalDistanceToFire = GetHorizontalDistance(botPosition, firePosition);
        StopNavMeshMovement();
        currentExtinguishAimPoint = firePosition;
        hasCurrentExtinguishAimPoint = true;
        activeExtinguisher.ClearExternalAimDirection(gameObject);
        AimTowards(firePosition);
        SetHandAimFocus(firePosition);
        SetHeadAimFocus(firePosition);
        ResetExtinguishCrouchState();
        return horizontalDistanceToFire;
    }

    private bool IsPointFireExtinguisherSprayReady(Vector3 firePosition, bool emitVerboseLogs)
    {
        if (sprayReadyTime < 0f)
        {
            sprayReadyTime = Time.time + Mathf.Max(0f, sprayStartDelay);
            if (emitVerboseLogs)
            {
                LogVerboseExtinguish(
                    VerboseExtinguishLogCategory.Timing,
                    $"delayextinguisher:{GetToolName(activeExtinguisher)}:{firePosition}",
                    $"Starting extinguisher spray delay until {sprayReadyTime:F2} for fire={firePosition}.");
            }

            StopExtinguisher();
            return false;
        }

        if (Time.time < sprayReadyTime)
        {
            if (emitVerboseLogs)
            {
                LogVerboseExtinguish(
                    VerboseExtinguishLogCategory.Timing,
                    $"delaywaitextinguisher:{GetToolName(activeExtinguisher)}:{firePosition}",
                    $"Waiting for extinguisher spray delay. now={Time.time:F2}, ready={sprayReadyTime:F2}.");
            }

            StopExtinguisher();
            return false;
        }

        return true;
    }

    private void SprayPointFireExtinguisher(
        IFireTarget fireTarget,
        Vector3 firePosition,
        float detectionRadius,
        bool emitExtinguishDebug,
        bool emitVerboseLogs,
        string flowDetail)
    {
        if (emitExtinguishDebug)
        {
            SetExtinguishSubtask(BotExtinguishSubtask.Spray, "Spraying fire.");
            UpdateExtinguishDebugStage(ExtinguishDebugStage.Spraying, $"Spraying extinguisher at {firePosition}.");
        }

        if (!string.IsNullOrWhiteSpace(flowDetail))
        {
            LogPathClearingFlow(
                $"route-fire-spray:{GetDebugTargetName(fireTarget)}",
                flowDetail);
        }

        LockExtinguisherTarget(fireTarget);
        activeExtinguisher.SetExternalSprayState(true, gameObject);
        ApplyWaterToFireTarget(activeExtinguisher, fireTarget);

        if (emitVerboseLogs)
        {
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Spray,
                $"sprayextinguisher:{GetDebugTargetName(fireTarget)}",
                $"Applying extinguisher to fireTarget={GetDebugTargetName(fireTarget)} at {firePosition}.");
        }
    }

    private void ProcessFireExtinguisherExtinguishRoute(
        Vector3 orderPoint,
        Vector3 targetSearchPoint,
        IFireTarget fireTarget,
        Vector3 firePosition,
        Vector3 botPosition)
    {
        float detectionRadius = Mathf.Max(0.05f, routeFireDetectionRadius);
        if (!TryResolveExtinguisherRouteFireTarget(fireTarget, out IFireTarget routeFireTarget))
        {
            currentExtinguishTargetPosition = targetSearchPoint;
            hasCurrentExtinguishTargetPosition = true;
            ClearHeadAimFocus();
            ClearHandAimFocus();
            ResetExtinguishCrouchState();
            StopExtinguisher();
            sprayReadyTime = -1f;
            ClearExtinguisherTargetLock();
            SetCurrentFireTarget(null);

            if (!IsWithinArrivalDistance(orderPoint))
            {
                SetExtinguishSubtask(BotExtinguishSubtask.MoveToFire, "Scanning for nearby fire.");
                if (ShouldIssueExtinguisherApproachMove(orderPoint))
                {
                    MoveTo(orderPoint);
                }

                return;
            }

            CompleteExtinguishOrder("No nearby fire detected within extinguish radius.");
            return;
        }

        fireTarget = routeFireTarget;
        firePosition = fireTarget.GetWorldPosition();
        SetExtinguishSubtask(BotExtinguishSubtask.AimAtFire, "Aiming at fire.");
        float horizontalDistanceToFire = PreparePointFireExtinguisherSuppression(fireTarget, firePosition, botPosition);
        if (!IsPointFireExtinguisherSprayReady(firePosition, true))
        {
            return;
        }

        LogVerboseExtinguish(
            VerboseExtinguishLogCategory.Distance,
            $"distextinguisher:{GetDebugTargetName(fireTarget)}:{horizontalDistanceToFire:F2}:{detectionRadius:F2}",
            $"Extinguisher distance to target. horizontal={horizontalDistanceToFire:F2}, allowed={detectionRadius:F2}, vertical={Mathf.Abs(firePosition.y - botPosition.y):F2}.");

        SprayPointFireExtinguisher(fireTarget, firePosition, detectionRadius, true, true, null);

        if (fireTarget == null || !fireTarget.IsBurning)
        {
            StopExtinguisher();
            sprayReadyTime = -1f;
            ClearExtinguisherTargetLock();
            SetCurrentFireTarget(ResolveExtinguisherRouteTarget(targetSearchPoint));
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Targeting,
                $"nextextinguisherfire:{GetDebugTargetName(currentFireTarget)}",
                $"Current fire extinguished. Next fire={GetDebugTargetName(currentFireTarget)}.");
            if (currentFireTarget == null || !currentFireTarget.IsBurning)
            {
                CompleteExtinguishOrder("All nearby fires extinguished.");
            }
        }
    }

    private bool TryResolveExtinguisherRouteFireTarget(IFireTarget preferredFireTarget, out IFireTarget routeFireTarget)
    {
        routeFireTarget = null;
        float detectionRadius = Mathf.Max(0.05f, routeFireDetectionRadius);
        float stickyDetectionRadius = detectionRadius + ExtinguisherTargetStickinessRadiusSlack;

        IFireTarget lockedTarget = GetLockedExtinguisherFireTarget();
        if (lockedTarget != null &&
            IsFireWithinRouteDetectionRadius(lockedTarget, transform.position, stickyDetectionRadius))
        {
            routeFireTarget = lockedTarget;
        }
        else if (preferredFireTarget != null &&
                 preferredFireTarget.IsBurning &&
                 IsFireWithinRouteDetectionRadius(preferredFireTarget, transform.position, stickyDetectionRadius))
        {
            routeFireTarget = preferredFireTarget;
        }
        else if (currentFireTarget != null &&
                 currentFireTarget.IsBurning &&
                 IsFireWithinRouteDetectionRadius(currentFireTarget, transform.position, stickyDetectionRadius))
        {
            routeFireTarget = currentFireTarget;
        }
        else
        {
            if (TryResolveBurningFireWithinRadius(transform.position, detectionRadius, out IFireTarget detectedFire))
            {
                routeFireTarget = detectedFire;
            }
        }

        if (routeFireTarget == null)
        {
            return false;
        }

        SetCurrentFireTarget(routeFireTarget);
        return true;
    }
}
