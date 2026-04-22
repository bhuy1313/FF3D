using UnityEngine;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private sealed class AcquireHazardIsolationTargetTask : IBotPlanTask
    {
        public string Name => "Acquire Hazard Target";

        public void OnStart(BotCommandAgent agent)
        {
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            if (!agent.TryGetHazardIsolationIntent(out BotCommandIntentPayload intent))
            {
                return BotPlanTaskStatus.Failure;
            }

            agent.TryResolveIntentOrderPoint(intent, out Vector3 orderPoint);

            if (agent.currentHazardIsolationTarget != null && !agent.currentHazardIsolationTarget.IsHazardActive)
            {
                agent.CompleteHazardIsolationOrder("Hazard isolated.");
                return BotPlanTaskStatus.Success;
            }

            if (!agent.TryResolveHazardIsolationTarget(orderPoint, out IBotHazardIsolationTarget target))
            {
                agent.SetCurrentHazardIsolationTarget(null);
                if (agent.hazardIsolationUnavailableRetryCount > 0 && agent.IsNearHazardIsolationPoint(orderPoint))
                {
                    agent.AbortHazardIsolationOrder("No interactable hazard device is currently available near the isolate point.");
                    return BotPlanTaskStatus.Failure;
                }

                if (agent.IsNearHazardIsolationPoint(orderPoint))
                {
                    agent.CompleteHazardIsolationOrder("No active hazard device found near the isolate point.");
                    return BotPlanTaskStatus.Success;
                }

                return agent.AdvanceOrderPointSearch(
                    orderPoint,
                    agent.IsNearHazardIsolationPoint,
                    () => agent.CompleteHazardIsolationOrder("No active hazard device found near the isolate point."),
                    agent.AbortHazardIsolationOrder,
                    "Failed to path to isolate point.");
            }

            agent.SetCurrentHazardIsolationTarget(target);
            return BotPlanTaskStatus.Success;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
        }
    }

    private sealed class MoveToHazardIsolationTargetTask : MoveToPositionTask
    {
        public MoveToHazardIsolationTargetTask() : base(
            "Move To Hazard Device",
            agent => agent.UpdateHazardIsolationTargetMove(),
            moveAction: (agent, destination) => agent.TryMoveToHazardIsolationTarget(destination),
            onEnd: (agent, interrupted) => agent.RestoreHazardIsolationStoppingDistance())
        {
        }
    }

    private sealed class InteractHazardIsolationTargetTask : IBotPlanTask
    {
        public string Name => "Interact With Hazard Device";

        public void OnStart(BotCommandAgent agent)
        {
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            IBotHazardIsolationTarget target = agent.currentHazardIsolationTarget;
            if (target == null)
            {
                agent.RequestCommandPlanRebuild();
                return BotPlanTaskStatus.Success;
            }

            if (!target.IsHazardActive)
            {
                agent.CompleteHazardIsolationOrder("Hazard was already isolated.");
                return BotPlanTaskStatus.Success;
            }

            if (!target.IsInteractionAvailable)
            {
                if (agent.HandleUnavailableHazardIsolationTarget())
                {
                    return BotPlanTaskStatus.Failure;
                }

                return BotPlanTaskStatus.Running;
            }

            agent.ResetHazardIsolationUnavailableState();

            if (!agent.TryGetHazardIsolationComponent(target, out Component targetComponent))
            {
                agent.AbortHazardIsolationOrder("Hazard isolation target is missing its runtime component.");
                return BotPlanTaskStatus.Failure;
            }

            if (!TryGetHazardIsolationInteractable(targetComponent, out IInteractable interactable))
            {
                agent.AbortHazardIsolationOrder("Hazard isolation target cannot be interacted with.");
                return BotPlanTaskStatus.Failure;
            }

            Vector3 targetPosition = target.GetWorldPosition();
            agent.StopAndAimTowards(targetPosition);
            interactable.Interact(agent.gameObject);

            if (!target.IsHazardActive)
            {
                agent.CompleteHazardIsolationOrder("Hazard isolated.");
                return BotPlanTaskStatus.Success;
            }

            return BotPlanTaskStatus.Running;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
        }
    }

    private BotPlan BuildHazardIsolationPlan()
    {
        return new BotPlan("Isolate")
            .Add(new AcquireHazardIsolationTargetTask())
            .Add(new MoveToHazardIsolationTargetTask())
            .Add(new InteractHazardIsolationTargetTask());
    }

    private MoveTaskDirective UpdateHazardIsolationTargetMove()
    {
        IBotHazardIsolationTarget target = currentHazardIsolationTarget;
        if (target == null)
        {
            RequestCommandPlanRebuild();
            return MoveTaskDirective.Success();
        }

        if (!TryGetHazardIsolationComponent(target, out Component _))
        {
            AbortHazardIsolationOrder("Hazard isolation target is missing its runtime component.");
            return MoveTaskDirective.Failure();
        }

        Vector3 targetPosition = target.GetWorldPosition();
        float interactionDistance = Mathf.Max(0.5f, hazardIsolationInteractionDistance);
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            AbortHazardIsolationOrder("Bot is not on a valid NavMesh to reach the hazard device.");
            return MoveTaskDirective.Failure();
        }

        return UpdateMoveIntoHorizontalRange(targetPosition, interactionDistance);
    }

    private bool TryMoveToHazardIsolationTarget(Vector3 destination)
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            AbortHazardIsolationOrder("Bot is not on a valid NavMesh to reach the hazard device.");
            return false;
        }

        if (cachedHazardIsolationStoppingDistance < 0f)
        {
            cachedHazardIsolationStoppingDistance = navMeshAgent.stoppingDistance;
        }

        float interactionDistance = Mathf.Max(0.5f, hazardIsolationInteractionDistance);
        if (TryMoveIntoHorizontalRangeOrFail(destination, interactionDistance, AbortHazardIsolationOrder, "Failed to path to hazard device.", 0.85f))
        {
            return true;
        }

        return false;
    }

    private void RestoreHazardIsolationStoppingDistance()
    {
        if (cachedHazardIsolationStoppingDistance >= 0f &&
            navMeshAgent != null &&
            navMeshAgent.enabled)
        {
            navMeshAgent.stoppingDistance = cachedHazardIsolationStoppingDistance;
        }

        cachedHazardIsolationStoppingDistance = -1f;
    }
}
