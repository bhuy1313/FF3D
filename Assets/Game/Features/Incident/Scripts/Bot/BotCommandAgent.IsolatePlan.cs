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

                if (!agent.TryMoveToOrFail(orderPoint, agent.AbortHazardIsolationOrder, "Failed to path to isolate point."))
                {
                    return BotPlanTaskStatus.Failure;
                }

                return BotPlanTaskStatus.Running;
            }

            agent.SetCurrentHazardIsolationTarget(target);
            return BotPlanTaskStatus.Success;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
        }
    }

    private sealed class MoveToHazardIsolationTargetTask : IBotPlanTask
    {
        public string Name => "Move To Hazard Device";

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

            if (!agent.TryGetHazardIsolationComponent(target, out Component _))
            {
                agent.AbortHazardIsolationOrder("Hazard isolation target is missing its runtime component.");
                return BotPlanTaskStatus.Failure;
            }

            Vector3 targetPosition = target.GetWorldPosition();
            float interactionDistance = Mathf.Max(0.5f, agent.hazardIsolationInteractionDistance);
            if ((targetPosition - agent.transform.position).sqrMagnitude <= interactionDistance * interactionDistance)
            {
                agent.StopNavMeshMovement();

                return BotPlanTaskStatus.Success;
            }

            if (agent.navMeshAgent == null || !agent.navMeshAgent.enabled || !agent.navMeshAgent.isOnNavMesh)
            {
                agent.AbortHazardIsolationOrder("Bot is not on a valid NavMesh to reach the hazard device.");
                return BotPlanTaskStatus.Failure;
            }

            if (agent.cachedHazardIsolationStoppingDistance < 0f)
            {
                agent.cachedHazardIsolationStoppingDistance = agent.navMeshAgent.stoppingDistance;
            }

            agent.navMeshAgent.stoppingDistance = Mathf.Max(agent.navMeshAgent.stoppingDistance, interactionDistance * 0.85f);
            if (!agent.TryMoveToOrFail(targetPosition, agent.AbortHazardIsolationOrder, "Failed to path to hazard device."))
            {
                return BotPlanTaskStatus.Failure;
            }

            return BotPlanTaskStatus.Running;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
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
            agent.StopNavMeshMovement();

            agent.AimTowards(targetPosition);
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
}
