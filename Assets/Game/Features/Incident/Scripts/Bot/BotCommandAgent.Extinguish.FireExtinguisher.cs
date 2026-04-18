using UnityEngine;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
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
        currentExtinguishTargetPosition = firePosition;
        hasCurrentExtinguishTargetPosition = true;
        UpdateCurrentExtinguishAimData(activeExtinguisher, firePosition);
        PrimeExtinguisherTargetLock(activeExtinguisher, fireTarget);

        float horizontalDistanceToFire = GetHorizontalDistance(botPosition, firePosition);
        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = true;
        currentExtinguishAimPoint = firePosition;
        hasCurrentExtinguishAimPoint = true;
        activeExtinguisher.ClearExternalAimDirection(gameObject);

        SetExtinguishSubtask(BotExtinguishSubtask.AimAtFire, "Aiming at fire.");
        AimTowards(firePosition);
        SetHandAimFocus(firePosition);
        SetHeadAimFocus(firePosition);
        ResetExtinguishCrouchState();

        if (sprayReadyTime < 0f)
        {
            sprayReadyTime = Time.time + Mathf.Max(0f, sprayStartDelay);
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Timing,
                $"delayextinguisher:{GetToolName(activeExtinguisher)}:{firePosition}",
                $"Starting extinguisher spray delay until {sprayReadyTime:F2} for fire={firePosition}.");
            StopExtinguisher();
            return;
        }

        if (Time.time < sprayReadyTime)
        {
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Timing,
                $"delaywaitextinguisher:{GetToolName(activeExtinguisher)}:{firePosition}",
                $"Waiting for extinguisher spray delay. now={Time.time:F2}, ready={sprayReadyTime:F2}.");
            StopExtinguisher();
            return;
        }

        LogVerboseExtinguish(
            VerboseExtinguishLogCategory.Distance,
            $"distextinguisher:{GetDebugTargetName(fireTarget)}:{horizontalDistanceToFire:F2}:{detectionRadius:F2}",
            $"Extinguisher distance to target. horizontal={horizontalDistanceToFire:F2}, allowed={detectionRadius:F2}, vertical={Mathf.Abs(firePosition.y - botPosition.y):F2}.");

        SetExtinguishSubtask(BotExtinguishSubtask.Spray, "Spraying fire.");
        UpdateExtinguishDebugStage(ExtinguishDebugStage.Spraying, $"Spraying extinguisher at {firePosition}.");
        LockExtinguisherTarget(fireTarget);
        activeExtinguisher.SetExternalSprayState(true, gameObject);
        TryApplyWaterToFireTarget(activeExtinguisher, fireTarget, firePosition, detectionRadius);
        LogVerboseExtinguish(
            VerboseExtinguishLogCategory.Spray,
            $"sprayextinguisher:{GetDebugTargetName(fireTarget)}",
            $"Applying extinguisher to fireTarget={GetDebugTargetName(fireTarget)} at {firePosition}.");

        if (fireTarget == null || !fireTarget.IsBurning)
        {
            StopExtinguisher();
            sprayReadyTime = -1f;
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

        if (preferredFireTarget != null &&
            preferredFireTarget.IsBurning &&
            IsFireWithinRouteDetectionRadius(preferredFireTarget, transform.position, detectionRadius))
        {
            routeFireTarget = preferredFireTarget;
        }
        else if (TryResolveBurningFireWithinRadius(transform.position, detectionRadius, out IFireTarget detectedFire))
        {
            routeFireTarget = detectedFire;
        }
        else
        {
            IFireTarget lockedTarget = GetLockedExtinguisherFireTarget();
            if (lockedTarget != null &&
                IsFireWithinRouteDetectionRadius(lockedTarget, transform.position, detectionRadius))
            {
                routeFireTarget = lockedTarget;
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
