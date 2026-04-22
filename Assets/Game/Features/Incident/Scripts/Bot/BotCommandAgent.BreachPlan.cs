using UnityEngine;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private sealed class AcquireBreachTargetTask : IBotPlanTask
    {
        public string Name => "Acquire Breach Target";

        public void OnStart(BotCommandAgent agent)
        {
            agent.SetBreakSubtask(BotBreakSubtask.AcquireTarget, "Acquiring breach target.");
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            if (!agent.TryGetBreachIntent(out BotCommandIntentPayload intent))
            {
                return BotPlanTaskStatus.Failure;
            }

            agent.TryResolveIntentOrderPoint(intent, out Vector3 orderPoint);

            if (agent.currentBreachPryTarget != null &&
                agent.currentBreachPryTarget.IsBreached &&
                !agent.currentBreachPryTarget.IsPryInProgress)
            {
                agent.CompleteBreachOrder("Breach completed.");
                return BotPlanTaskStatus.Success;
            }

            if (agent.TryResolveBreachPryTarget(orderPoint, out IBotPryTarget pryTarget))
            {
                agent.SetCurrentBreachPryTarget(pryTarget);
                agent.SetCurrentBlockedBreakable(null);
                return BotPlanTaskStatus.Success;
            }

            agent.SetCurrentBreachPryTarget(null);
            if (agent.TryResolveBreachBreakableTarget(orderPoint, out IBotBreakableTarget breakableTarget))
            {
                agent.SetCurrentBlockedBreakable(breakableTarget);
                return BotPlanTaskStatus.Success;
            }

            agent.SetCurrentBlockedBreakable(null);
            if (agent.IsNearBreachPoint(orderPoint))
            {
                agent.CompleteBreachOrder("No breach target found near the assigned point.");
                return BotPlanTaskStatus.Success;
            }

            return agent.AdvanceOrderPointSearch(
                orderPoint,
                agent.IsNearBreachPoint,
                () => agent.CompleteBreachOrder("No breach target found near the assigned point."),
                agent.AbortBreachOrder,
                "Failed to path to breach point.");
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
        }
    }

    private sealed class AcquireBreachToolTask : IBotPlanTask
    {
        public string Name => "Acquire Breach Tool";

        public void OnStart(BotCommandAgent agent)
        {
            agent.SetBreakSubtask(BotBreakSubtask.AcquireTool, "Acquiring breach tool.");
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            if (agent.currentBreachPryTarget == null && agent.currentBlockedBreakable == null)
            {
                agent.RequestCommandPlanRebuild();
                return BotPlanTaskStatus.Success;
            }

            if (agent.currentBreachPryTarget != null)
            {
                IBotBreakTool pryTool = agent.ResolvePreferredPryTool();
                if (pryTool == null)
                {
                    agent.AbortBreachOrder("No usable crowbar available for breach.");
                    return BotPlanTaskStatus.Failure;
                }

                if (!agent.TryEnsureBreakToolEquipped(pryTool))
                {
                    return BotPlanTaskStatus.Running;
                }

                return BotPlanTaskStatus.Success;
            }

            IBotBreakTool breakTool = agent.ResolveCommittedBreakTool();
            if (breakTool == null)
            {
                agent.AbortBreachOrder("No usable breaching tool found.");
                return BotPlanTaskStatus.Failure;
            }

            if (!agent.TryEnsureBreakToolEquipped(breakTool))
            {
                return BotPlanTaskStatus.Running;
            }

            return BotPlanTaskStatus.Success;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
        }
    }

    private sealed class ExecuteBreachTask : IBotPlanTask
    {
        public string Name => "Execute Breach";

        public void OnStart(BotCommandAgent agent)
        {
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            IBotPryTarget pryTarget = agent.currentBreachPryTarget;
            if (pryTarget != null)
            {
                if (pryTarget.IsBreached && !pryTarget.IsPryInProgress)
                {
                    agent.CompleteBreachOrder("Breach completed.");
                    return BotPlanTaskStatus.Success;
                }

                Vector3 targetPosition = pryTarget.GetWorldPosition();
                float interactionDistance = Mathf.Max(0.5f, agent.breachInteractionDistance);
                MoveTaskDirective moveDirective =
                    agent.UpdateMoveIntoHorizontalRange(targetPosition, interactionDistance, () => agent.StopAndAimTowards(targetPosition));
                if (moveDirective.Status != BotPlanTaskStatus.Success)
                {
                    agent.SetBreakSubtask(BotBreakSubtask.MoveToObstacle, $"Moving to pry target '{BotCommandAgent.GetDebugTargetName(pryTarget)}'.");
                    if (!agent.TryMoveIntoHorizontalRangeOrFail(targetPosition, interactionDistance, agent.AbortBreachOrder, "Failed to path to breach target."))
                    {
                        return BotPlanTaskStatus.Failure;
                    }

                    return BotPlanTaskStatus.Running;
                }

                if (!pryTarget.IsPryInProgress && !pryTarget.CanBePriedOpen)
                {
                    agent.AbortBreachOrder("Assigned pry target can no longer be breached.");
                    return BotPlanTaskStatus.Failure;
                }

                if (pryTarget.IsPryInProgress)
                {
                    agent.SetBreakSubtask(BotBreakSubtask.Pry, $"Prying '{BotCommandAgent.GetDebugTargetName(pryTarget)}'.");
                    return BotPlanTaskStatus.Running;
                }

                if (!pryTarget.TryPryOpen(agent.gameObject))
                {
                    agent.AbortBreachOrder("Failed to start prying the assigned breach target.");
                    return BotPlanTaskStatus.Failure;
                }

                agent.SetBreakSubtask(BotBreakSubtask.Pry, $"Prying '{BotCommandAgent.GetDebugTargetName(pryTarget)}'.");
                return BotPlanTaskStatus.Running;
            }

            IBotBreakableTarget breakableTarget = agent.currentBlockedBreakable;
            if (breakableTarget == null)
            {
                agent.RequestCommandPlanRebuild();
                return BotPlanTaskStatus.Success;
            }

            if (breakableTarget.IsBroken)
            {
                agent.CompleteBreachOrder("Breach completed.");
                return BotPlanTaskStatus.Success;
            }

            if (!agent.HandleEquippedBreakToolAgainstTarget(agent.activeBreakTool, breakableTarget))
            {
                agent.AbortBreachOrder("Failed to breach the assigned obstacle.");
                return BotPlanTaskStatus.Failure;
            }

            return breakableTarget.IsBroken
                ? BotPlanTaskStatus.Success
                : BotPlanTaskStatus.Running;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
        }
    }

    private BotPlan BuildBreachPlan()
    {
        return new BotPlan("Breach")
            .Add(new AcquireBreachTargetTask())
            .Add(new AcquireBreachToolTask())
            .Add(new ExecuteBreachTask());
    }
}
