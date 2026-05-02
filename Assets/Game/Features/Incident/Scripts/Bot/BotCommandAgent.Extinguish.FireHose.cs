using UnityEngine;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private void ProcessFireHoseExtinguishRoute(
        Vector3 targetSearchPoint,
        IFireGroupTarget fireGroup,
        Vector3 firePosition,
        Vector3 botPosition)
    {
        currentExtinguishTargetPosition = firePosition;
        hasCurrentExtinguishTargetPosition = true;
        UpdateCurrentExtinguishAimData(activeExtinguisher, firePosition);

        float horizontalDistanceToFire = GetHorizontalDistance(botPosition, firePosition);
        float requiredHorizontalDistance = GetRequiredHorizontalDistanceForAim(activeExtinguisher, firePosition);
        float desiredHorizontalDistance = Mathf.Max(activeExtinguisher.PreferredSprayDistance, requiredHorizontalDistance);
        bool shouldReposition =
            horizontalDistanceToFire > activeExtinguisher.MaxSprayDistance ||
            horizontalDistanceToFire < desiredHorizontalDistance - 0.35f;

        if (shouldReposition)
        {
            SetExtinguishSubtask(BotExtinguishSubtask.MoveToFire, "Moving to extinguish position.");
            Vector3 desiredPosition = ResolveExtinguishPosition(targetSearchPoint, firePosition, desiredHorizontalDistance);
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Movement,
                $"movefirehose:{desiredPosition}",
                $"Repositioning fire hose. horizontal={horizontalDistanceToFire:F2}, desired={desiredHorizontalDistance:F2}, max={activeExtinguisher.MaxSprayDistance:F2}, target={firePosition}, destination={desiredPosition}.");
            UpdateExtinguishDebugStage(ExtinguishDebugStage.MovingToFire, $"Moving to fire hose position {desiredPosition} for fire at horizontal distance {horizontalDistanceToFire:F2}m.");
            ClearHeadAimFocus();
            ClearHandAimFocus();
            ResetExtinguishCrouchState();
            StopExtinguisher();
            sprayReadyTime = -1f;
            if (ShouldIssueExtinguisherApproachMove(desiredPosition))
            {
                MoveTo(desiredPosition);
            }

            return;
        }

        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = true;
        Vector3 aimPoint = hasCurrentExtinguishAimPoint ? currentExtinguishAimPoint : GetAimPoint(activeExtinguisher, firePosition);
        currentExtinguishAimPoint = aimPoint;
        hasCurrentExtinguishAimPoint = true;

        if (hasCurrentExtinguishLaunchDirection)
        {
            activeExtinguisher.SetExternalAimDirection(currentExtinguishLaunchDirection, gameObject);
        }
        else
        {
            activeExtinguisher.ClearExternalAimDirection(gameObject);
        }

        SetExtinguishSubtask(BotExtinguishSubtask.AimAtFire, "Aiming at fire.");
        AimTowards(aimPoint);
        SetHandAimFocus(aimPoint);
        SetHeadAimFocus(firePosition);

        if (ShouldUseFireHoseCrouch(activeExtinguisher))
        {
            behaviorContext.SetCrouchAnimation(true);

            if (crouchReadyTime < 0f)
            {
                behaviorContext.SetExtinguishStance(Random.Range(0, 2));
                crouchReadyTime = Time.time + Mathf.Max(0f, fireHoseCrouchDelay);
                StopExtinguisher();
                return;
            }

            if (Time.time < crouchReadyTime)
            {
                StopExtinguisher();
                return;
            }
        }
        else
        {
            ResetExtinguishCrouchState();
        }

        if (!IsAimSettled(activeExtinguisher, firePosition))
        {
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Timing,
                $"aimwaithose:{GetToolName(activeExtinguisher)}:{firePosition}",
                $"Waiting for fire hose aim settle. fire={firePosition}.");
            StopExtinguisher();
            sprayReadyTime = -1f;
            return;
        }

        if (sprayReadyTime < 0f)
        {
            sprayReadyTime = Time.time + Mathf.Max(0f, sprayStartDelay);
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Timing,
                $"delayhose:{GetToolName(activeExtinguisher)}:{firePosition}",
                $"Starting fire hose spray delay until {sprayReadyTime:F2} for fire={firePosition}.");
            StopExtinguisher();
            return;
        }

        if (Time.time < sprayReadyTime)
        {
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Timing,
                $"delaywaithose:{GetToolName(activeExtinguisher)}:{firePosition}",
                $"Waiting for fire hose spray delay. now={Time.time:F2}, ready={sprayReadyTime:F2}.");
            StopExtinguisher();
            return;
        }

        LogVerboseExtinguish(
            VerboseExtinguishLogCategory.Distance,
            $"disthose:{horizontalDistanceToFire:F2}:{desiredHorizontalDistance:F2}",
            $"Fire hose distance to target. horizontal={horizontalDistanceToFire:F2}, desired={desiredHorizontalDistance:F2}, max={activeExtinguisher.MaxSprayDistance:F2}, vertical={Mathf.Abs(firePosition.y - botPosition.y):F2}.");

        if (!CanApplyWaterToFireGroup(activeExtinguisher, fireGroup, firePosition))
        {
            SetExtinguishSubtask(BotExtinguishSubtask.AimAtFire, "Waiting for a clear suppression line.");
            StopExtinguisher();
            sprayReadyTime = -1f;
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Spray,
                $"blockedhose:{GetToolName(activeExtinguisher)}:{firePosition}",
                $"Fire hose suppression blocked by range, aim, or line of sight. fire={firePosition}.");
            return;
        }

        SetExtinguishSubtask(BotExtinguishSubtask.Spray, "Spraying fire.");
        UpdateExtinguishDebugStage(ExtinguishDebugStage.Spraying, $"Spraying fire hose at {firePosition}.");
        activeExtinguisher.SetExternalSprayState(true, gameObject);
        TryApplyWaterToFireGroup(activeExtinguisher, fireGroup, firePosition);

        if (fireGroup == null || !fireGroup.HasActiveFires)
        {
            CompleteExtinguishOrder("FireGroup extinguished.");
        }
    }
}
