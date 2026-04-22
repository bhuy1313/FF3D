using UnityEngine;

public partial class BotCommandAgent
{
    private sealed class AcquireFollowTargetTask : IBotPlanTask
    {
        public string Name => "Acquire Follow Target";

        public void OnStart(BotCommandAgent agent)
        {
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            if (agent.behaviorContext == null || !agent.behaviorContext.TryGetFollowOrder(out BotFollowOrder followOrder))
            {
                return BotPlanTaskStatus.Failure;
            }

            Transform seededTarget = followOrder.Target != null ? followOrder.Target : agent.CurrentFollowTarget;
            Transform target = agent.runtimeDecisionService.ResolveFollowTarget(seededTarget, followOrder.TargetTag, agent.PerceptionMemory);
            if (target == null)
            {
                agent.CurrentFollowTarget = null;
                agent.CurrentEscortSlotIndex = -1;
                agent.StopNavMeshMovement();

                if (agent.ShouldCancelFollowAfterTargetLoss())
                {
                    agent.TryCancelFollowCommand();
                    return BotPlanTaskStatus.Success;
                }

                return BotPlanTaskStatus.Running;
            }

            agent.ClearFollowTargetLossState();
            agent.CurrentFollowTarget = target;
            return BotPlanTaskStatus.Success;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
        }
    }

    private sealed class MaintainFollowTask : IBotPlanTask
    {
        public string Name => "Maintain Follow Formation";

        public void OnStart(BotCommandAgent agent)
        {
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            if (agent.behaviorContext == null || !agent.behaviorContext.HasFollowOrder)
            {
                return BotPlanTaskStatus.Success;
            }

            if (!agent.behaviorContext.TryGetFollowOrder(out BotFollowOrder followOrder))
            {
                return BotPlanTaskStatus.Failure;
            }

            Transform seededTarget = followOrder.Target != null ? followOrder.Target : agent.CurrentFollowTarget;
            Transform target = agent.runtimeDecisionService.ResolveFollowTarget(seededTarget, followOrder.TargetTag, agent.PerceptionMemory);
            if (target == null)
            {
                agent.CurrentFollowTarget = null;
                agent.CurrentEscortSlotIndex = -1;
                agent.StopNavMeshMovement();

                if (agent.ShouldCancelFollowAfterTargetLoss())
                {
                    agent.TryCancelFollowCommand();
                    return BotPlanTaskStatus.Success;
                }

                return BotPlanTaskStatus.Running;
            }

            agent.ClearFollowTargetLossState();
            agent.CurrentFollowTarget = target;
            if (agent.HasActiveTacticalMovementInterrupt())
            {
                return BotPlanTaskStatus.Running;
            }

            agent.ProcessFollowOrder();
            return agent.behaviorContext != null && agent.behaviorContext.HasFollowOrder
                ? BotPlanTaskStatus.Running
                : BotPlanTaskStatus.Success;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
        }
    }

    private BotPlan BuildFollowPlan()
    {
        return new BotPlan("Follow")
            .Add(new AcquireFollowTargetTask())
            .Add(new MaintainFollowTask());
    }
}
