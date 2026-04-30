using UnityEngine;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private sealed class AcquireRescueTargetTask : IBotPlanTask
    {
        public string Name => "Acquire Rescue Target";

        public void OnStart(BotCommandAgent agent)
        {
            agent.SetRescueSubtask(BotRescueSubtask.AcquireTarget, "Acquiring rescue target.");
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            if (agent.behaviorContext == null || !agent.behaviorContext.TryGetRescueOrder(out Vector3 orderPoint))
            {
                return BotPlanTaskStatus.Failure;
            }

            agent.LogRescueActivityMessage(
                $"rescue-order:{agent.FormatFlowVectorKeyForLog(orderPoint)}",
                $"Received Rescue order to {orderPoint}.");

            IRescuableTarget rescueTarget = agent.runtimeDecisionService.ResolveRescueTarget(
                orderPoint,
                agent.CurrentRescueTarget,
                agent.gameObject,
                agent.rescueSearchRadius);
            if (rescueTarget == null)
            {
                agent.LogRescueActivityMessage("rescue-notfound", "No rescue target found.");
                agent.FailActiveRescueOrder("No rescue target found.", BotTaskStatus.Blocked);
                return BotPlanTaskStatus.Failure;
            }

            agent.CurrentRescueTarget = rescueTarget;
            if (!rescueTarget.NeedsRescue)
            {
                agent.LogRescueActivityMessage("rescue-complete", "Rescue completed.");
                agent.CompleteActiveRescueOrder();
                return BotPlanTaskStatus.Success;
            }

            if (rescueTarget.IsCarried && rescueTarget.ActiveRescuer != agent.gameObject)
            {
                agent.LogRescueActivityMessage("rescue-reacquire", "Assigned casualty is already being carried by another rescuer.");
                agent.ReacquireRescueTarget("Recovering after losing assigned casualty.");
                agent.RequestCommandPlanRebuild();
                return BotPlanTaskStatus.Success;
            }

            return BotPlanTaskStatus.Success;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
        }
    }

    private sealed class AcquireRescueSafeZoneTask : IBotPlanTask
    {
        public string Name => "Acquire Safe Zone";

        public void OnStart(BotCommandAgent agent)
        {
            agent.SetRescueSubtask(BotRescueSubtask.AcquireSafeZone, "Acquiring safe zone.");
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            IRescuableTarget rescueTarget = agent.CurrentRescueTarget;
            if (rescueTarget == null)
            {
                agent.RequestCommandPlanRebuild();
                return BotPlanTaskStatus.Success;
            }

            agent.CurrentSafeZoneTarget = agent.runtimeDecisionService.ResolveNearestSafeZone(
                rescueTarget.GetWorldPosition(),
                agent.CurrentSafeZoneTarget);
            if (agent.CurrentSafeZoneTarget == null)
            {
                agent.LogRescueActivityMessage("rescue-no-safezone", "No safe zone with available slots found.");
                agent.FailActiveRescueOrder("No safe zone with available slots found for rescue.", BotTaskStatus.Blocked);
                return BotPlanTaskStatus.Failure;
            }

            if (!agent.CurrentSafeZoneTarget.TryClaimSlot(agent.gameObject, out Vector3 slotPosition))
            {
                agent.LogRescueActivityMessage("rescue-no-slot", "Safe zone has no available slots.");
                agent.FailActiveRescueOrder("Safe zone has no available slots.", BotTaskStatus.Blocked);
                return BotPlanTaskStatus.Failure;
            }

            agent.ClaimedSlotPosition = slotPosition;
            return BotPlanTaskStatus.Success;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
        }
    }

    private sealed class MoveToRescueTargetTask : MoveToPositionTask
    {
        public MoveToRescueTargetTask() : base(
            "Move To Casualty",
            agent => agent.UpdateRescueTargetMove(),
            onStart: agent => agent.SetRescueSubtask(BotRescueSubtask.MoveToTarget, "Moving to casualty."),
            moveAction: (agent, destination) =>
            {
                if (agent.MoveToCommand(destination))
                {
                    return true;
                }

                agent.FailActiveRescueOrder("Failed to path to casualty.", BotTaskStatus.Blocked);
                return false;
            })
        {
        }
    }

    private sealed class StabilizeOrCarryRescueTask : IBotPlanTask
    {
        private const float ReacquireTargetDistanceSlack = 0.35f;

        public string Name => "Stabilize Or Carry Casualty";

        public void OnStart(BotCommandAgent agent)
        {
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            IRescuableTarget rescueTarget = agent.CurrentRescueTarget;
            if (rescueTarget == null)
            {
                agent.RequestCommandPlanRebuild();
                return BotPlanTaskStatus.Success;
            }

            Vector3 targetPosition = rescueTarget.GetWorldPosition();
            agent.StopNavMeshMovement();
            agent.AimTowardsPoint(targetPosition);

            if (rescueTarget.RequiresStabilization)
            {
                agent.SetRescueSubtask(BotRescueSubtask.StabilizeTarget, "Stabilizing casualty.");
                if (rescueTarget.IsRescueInProgress && rescueTarget.ActiveRescuer == agent.gameObject)
                {
                    agent.LogRescueActivityMessage("rescue-stabilize", "Stabilizing casualty.");
                    return BotPlanTaskStatus.Running;
                }

                if (rescueTarget.IsRescueInProgress && rescueTarget.ActiveRescuer != agent.gameObject)
                {
                    agent.ReacquireRescueTarget("Assigned casualty is being stabilized by another rescuer.");
                    agent.RequestCommandPlanRebuild();
                    return BotPlanTaskStatus.Success;
                }

                if (rescueTarget.TryStabilize(agent.gameObject))
                {
                    agent.LogRescueActivityMessage("rescue-stabilize", "Started casualty stabilization.");
                    return BotPlanTaskStatus.Running;
                }

                if (BotCommandAgent.GetHorizontalDistance(agent.transform.position, targetPosition) <= agent.rescueInteractionDistance + ReacquireTargetDistanceSlack)
                {
                    agent.FailActiveRescueOrder("Failed to start casualty stabilization.", BotTaskStatus.Failed);
                    return BotPlanTaskStatus.Failure;
                }

                agent.ReacquireRescueTarget("Recovering after failed stabilization attempt.");
                agent.RequestCommandPlanRebuild();
                return BotPlanTaskStatus.Success;
            }

            agent.SetRescueSubtask(BotRescueSubtask.BeginCarry, "Beginning casualty carry.");
            if (rescueTarget.IsRescueInProgress && rescueTarget.ActiveRescuer == agent.gameObject)
            {
                agent.LogRescueActivityMessage("rescue-start", "Starting rescue.");
                return BotPlanTaskStatus.Running;
            }

            if (rescueTarget.IsRescueInProgress && rescueTarget.ActiveRescuer != agent.gameObject)
            {
                agent.ReacquireRescueTarget("Assigned casualty is being handled by another rescuer.");
                agent.RequestCommandPlanRebuild();
                return BotPlanTaskStatus.Success;
            }

            agent.PrepareCarryRescueCommand();

            if (rescueTarget.TryBeginCarry(agent.gameObject, agent.EnsureRescueCarryAnchor()))
            {
                agent.PrepareCarryRescueCommand();
                agent.LogRescueActivityMessage("rescue-pickup", "Picked up casualty.");
                return BotPlanTaskStatus.Success;
            }

            agent.FailActiveRescueOrder("Failed to begin carrying casualty.", BotTaskStatus.Failed);
            return BotPlanTaskStatus.Failure;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
        }
    }

    private sealed class CarryRescueTargetTask : MoveToPositionTask
    {
        public CarryRescueTargetTask() : base(
            "Carry Casualty To Safe Zone",
            agent => agent.UpdateCarryRescueMove(),
            onStart: agent => agent.SetRescueSubtask(BotRescueSubtask.CarryToSafeZone, "Carrying casualty to safe zone."),
            moveAction: (agent, destination) =>
            {
                if (agent.MoveToRescueCarrySafeZoneCommand(destination))
                {
                    return true;
                }

                agent.FailActiveRescueOrder("Failed to path to rescue safe zone.", BotTaskStatus.Blocked);
                return false;
            })
        {
        }
    }

    private sealed class CompleteRescueTask : IBotPlanTask
    {
        public string Name => "Complete Rescue";

        public void OnStart(BotCommandAgent agent)
        {
            agent.SetRescueSubtask(BotRescueSubtask.CompleteRescue, "Completing rescue at safe zone.");
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            IRescuableTarget rescueTarget = agent.CurrentRescueTarget;
            ISafeZoneTarget safeZone = agent.CurrentSafeZoneTarget;
            if (rescueTarget == null || safeZone == null)
            {
                agent.RequestCommandPlanRebuild();
                return BotPlanTaskStatus.Success;
            }

            Vector3 fallbackDropPosition = agent.transform.position + agent.transform.TransformDirection(agent.rescueDropOffset);
            Vector3 dropPosition = agent.ClaimedSlotPosition ?? safeZone.GetDropPoint(fallbackDropPosition);
            rescueTarget.CompleteRescueAt(dropPosition, safeZone.GetSlotRotation(dropPosition));
            safeZone.OccupySlotAt(dropPosition);
            agent.LogRescueActivityMessage("rescue-complete", "Rescue completed.");
            agent.CompleteActiveRescueOrder();
            return BotPlanTaskStatus.Success;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
        }
    }

    private BotPlan BuildRescuePlan()
    {
        return new BotPlan("Rescue")
            .Add(new AcquireRescueTargetTask())
            .Add(new AcquireRescueSafeZoneTask())
            .Add(new MoveToRescueTargetTask())
            .Add(new StabilizeOrCarryRescueTask())
            .Add(new CarryRescueTargetTask())
            .Add(new CompleteRescueTask());
    }

    private MoveTaskDirective UpdateRescueTargetMove()
    {
        IRescuableTarget rescueTarget = CurrentRescueTarget;
        if (rescueTarget == null)
        {
            RequestCommandPlanRebuild();
            return MoveTaskDirective.Success();
        }

        if (rescueTarget.IsCarried && rescueTarget.ActiveRescuer == gameObject)
        {
            return MoveTaskDirective.Success();
        }

        if (rescueTarget.IsCarried && rescueTarget.ActiveRescuer != gameObject)
        {
            ReacquireRescueTarget("Assigned casualty is already being carried by another rescuer.");
            RequestCommandPlanRebuild();
            return MoveTaskDirective.Success();
        }

        Vector3 targetPosition = rescueTarget.GetWorldPosition();
        LogRescueActivityMessage("rescue-move", "Moving to casualty.");
        return UpdateMoveIntoHorizontalRange(targetPosition, rescueInteractionDistance, () => StopAndAimTowards(targetPosition));
    }

    private MoveTaskDirective UpdateCarryRescueMove()
    {
        IRescuableTarget rescueTarget = CurrentRescueTarget;
        ISafeZoneTarget safeZone = CurrentSafeZoneTarget;
        if (rescueTarget == null || safeZone == null)
        {
            RequestCommandPlanRebuild();
            return MoveTaskDirective.Success();
        }

        if (!rescueTarget.IsCarried || rescueTarget.ActiveRescuer != gameObject)
        {
            RequestCommandPlanRebuild();
            return MoveTaskDirective.Success();
        }

        PrepareCarryRescueCommand();

        Vector3 safeZonePosition = claimedSlotPosition ?? safeZone.GetWorldPosition();
        float distanceToSafeZone = BotCommandAgent.GetHorizontalDistance(transform.position, safeZonePosition);
        bool hasReachedSafeZone =
            safeZone.ContainsPoint(transform.position) ||
            distanceToSafeZone <= rescueSafeZoneArrivalDistance;
        if (hasReachedSafeZone)
        {
            StopNavMeshMovement();
            return MoveTaskDirective.Success();
        }

        LogRescueActivityMessage("rescue-carry", "Carrying casualty to safe zone.");
        return MoveTaskDirective.Running(safeZonePosition);
    }

}
