using UnityEngine;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private readonly struct MoveTaskDirective
    {
        public MoveTaskDirective(BotPlanTaskStatus status, bool shouldMove, Vector3 destination)
        {
            Status = status;
            ShouldMove = shouldMove;
            Destination = destination;
        }

        public BotPlanTaskStatus Status { get; }
        public bool ShouldMove { get; }
        public Vector3 Destination { get; }

        public static MoveTaskDirective Running(Vector3 destination) => new MoveTaskDirective(BotPlanTaskStatus.Running, true, destination);
        public static MoveTaskDirective Continue() => new MoveTaskDirective(BotPlanTaskStatus.Running, false, default);
        public static MoveTaskDirective Success() => new MoveTaskDirective(BotPlanTaskStatus.Success, false, default);
        public static MoveTaskDirective Failure() => new MoveTaskDirective(BotPlanTaskStatus.Failure, false, default);
    }

    private class MoveToPositionTask : IBotPlanTask
    {
        private readonly string name;
        private readonly System.Action<BotCommandAgent> onStart;
        private readonly System.Func<BotCommandAgent, MoveTaskDirective> update;
        private readonly System.Action<BotCommandAgent, bool> onEnd;
        private readonly System.Func<BotCommandAgent, Vector3, bool> moveAction;

        public MoveToPositionTask(
            string name,
            System.Func<BotCommandAgent, MoveTaskDirective> update,
            System.Action<BotCommandAgent> onStart = null,
            System.Action<BotCommandAgent, bool> onEnd = null,
            System.Func<BotCommandAgent, Vector3, bool> moveAction = null)
        {
            this.name = string.IsNullOrWhiteSpace(name) ? "Move To Position" : name;
            this.update = update;
            this.onStart = onStart;
            this.onEnd = onEnd;
            this.moveAction = moveAction;
        }

        public string Name => name;

        public void OnStart(BotCommandAgent agent)
        {
            onStart?.Invoke(agent);
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            if (update == null)
            {
                return BotPlanTaskStatus.Failure;
            }

            MoveTaskDirective directive = update(agent);
            System.Func<BotCommandAgent, Vector3, bool> executeMove = moveAction ?? ((owner, destination) => owner.MoveToCommand(destination));
            if (directive.ShouldMove && !executeMove(agent, directive.Destination))
            {
                return BotPlanTaskStatus.Failure;
            }

            return directive.Status;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
            onEnd?.Invoke(agent, interrupted);
        }
    }

    private sealed class HoldPositionTask : IBotPlanTask
    {
        public string Name => "Hold Position";

        public void OnStart(BotCommandAgent agent)
        {
            if (agent.navMeshAgent != null && agent.navMeshAgent.enabled && agent.navMeshAgent.isOnNavMesh)
            {
                agent.navMeshAgent.ResetPath();
                agent.navMeshAgent.isStopped = true;
            }
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            if (agent.behaviorContext == null ||
                !agent.behaviorContext.TryGetCommandIntentSnapshot(out BotCommandIntentPayload payload) ||
                payload.CommandType != BotCommandType.Hold)
            {
                return BotPlanTaskStatus.Success;
            }

            if (agent.navMeshAgent != null && agent.navMeshAgent.enabled && agent.navMeshAgent.isOnNavMesh)
            {
                agent.navMeshAgent.ResetPath();
                agent.navMeshAgent.isStopped = true;
            }

            return BotPlanTaskStatus.Running;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
        }
    }

    private sealed class MovePickupTask : IBotPlanTask
    {
        public string Name => "Acquire Requested Tool";

        public void OnStart(BotCommandAgent agent)
        {
            if (agent.CurrentMovePickupTarget is IBotExtinguisherItem extinguisherItem)
            {
                agent.SetExtinguishSubtask(BotExtinguishSubtask.MoveToTool, $"Moving to tool '{GetToolName(extinguisherItem)}'.");
            }
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            if (!agent.HasMovePickupTarget)
            {
                return BotPlanTaskStatus.Success;
            }

            if (agent.TryCompleteMovePickupTarget())
            {
                if (agent.navMeshAgent != null && agent.navMeshAgent.enabled && agent.navMeshAgent.isOnNavMesh)
                {
                    agent.navMeshAgent.ResetPath();
                }

                agent.ResetMoveActivityDebug();
                return BotPlanTaskStatus.Success;
            }

            return BotPlanTaskStatus.Running;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
        }
    }

    private void RunActiveCommandPlan()
    {
        if (!TryResolveActivePlan(out BotPlan plan, out string planKey))
        {
            ResetCommandPlanProcessor();
            return;
        }

        bool shouldStartPlan = !planProcessor.HasActivePlan ||
                               string.IsNullOrWhiteSpace(activeCommandPlanKey) ||
                               activeCommandPlanKey != planKey && IsActivePlanComplete();
        if (forceCommandPlanRebuild || shouldStartPlan)
        {
            forceCommandPlanRebuild = false;
            activeCommandPlanKey = planKey;
            planProcessor.SetPlan(plan, this);
        }

        planProcessor.Tick(this);
    }

    private bool IsActivePlanComplete()
    {
        return planProcessor == null || !planProcessor.HasActivePlan;
    }

    private bool TryResolveActivePlan(out BotPlan plan, out string planKey)
    {
        plan = null;
        planKey = string.Empty;

        if (behaviorContext != null)
        {
            if (behaviorContext.HasExtinguishOrder)
            {
                if (!TrySyncExtinguishPlanOrder())
                {
                    return false;
                }

                plan = BuildExtinguishPlan();
                planKey = $"Extinguish:{BuildExtinguishPlanKey()}";
                return true;
            }

            if (IsRouteFireClearingActive())
            {
                plan = BuildRouteFireInterruptPlan();
                planKey = $"RouteFire:{currentRouteFirePhase}";
                return true;
            }

            if (behaviorContext.HasRescueOrder)
            {
                plan = BuildRescuePlan();
                planKey = BuildCommandIntentKey("Rescue");
                return true;
            }

            if (behaviorContext.HasFollowOrder &&
                behaviorContext.TryGetCommandIntentSnapshot(out BotCommandIntentPayload followPayload))
            {
                if (followPayload.CommandType == BotCommandType.Follow)
                {
                    plan = BuildFollowPlan();
                    planKey = BuildCommandIntentKey("Follow");
                    return true;
                }

                if (followPayload.CommandType == BotCommandType.Assist)
                {
                    plan = BuildFollowPlan();
                    planKey = BuildCommandIntentKey("Assist");
                    return true;
                }

                if (followPayload.CommandType == BotCommandType.Regroup)
                {
                    plan = BuildFollowPlan();
                    planKey = BuildCommandIntentKey("Regroup");
                    return true;
                }
            }

            if (IsBreachCommandActive())
            {
                plan = BuildBreachPlan();
                planKey = BuildCommandIntentKey("Breach");
                return true;
            }

            if (IsHazardIsolationCommandActive())
            {
                plan = BuildHazardIsolationPlan();
                planKey = BuildCommandIntentKey("Isolate");
                return true;
            }

            if (behaviorContext.TryGetCommandIntentSnapshot(out BotCommandIntentPayload payload))
            {
                switch (payload.CommandType)
                {
                    case BotCommandType.Move:
                        plan = new BotPlan("Move").Add(BuildMovementDestinationTask(BotCommandType.Move));
                        planKey = BuildCommandIntentKey("Move");
                        return true;
                    case BotCommandType.Search:
                        plan = new BotPlan("Search").Add(BuildMovementDestinationTask(BotCommandType.Search));
                        planKey = BuildCommandIntentKey("Search");
                        return true;
                    case BotCommandType.Hold:
                        plan = new BotPlan("Hold").Add(new HoldPositionTask());
                        planKey = BuildCommandIntentKey("Hold");
                        return true;
                }
            }
        }

        if (HasMovePickupTarget && (behaviorContext == null || !behaviorContext.UseMoveOrdersAsBehaviorInput))
        {
            plan = new BotPlan("MovePickup").Add(new MovePickupTask());
            planKey = "MovePickup";
            return true;
        }

        return false;
    }

    private string BuildCommandIntentKey(string label)
    {
        if (behaviorContext == null || !behaviorContext.TryGetCommandIntentSnapshot(out BotCommandIntentPayload payload))
        {
            return label;
        }

        Vector3 point = payload.HasWorldPoint ? payload.WorldPoint : Vector3.zero;
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0}:{1}:{2:F2}:{3:F2}:{4:F2}",
            label,
            payload.CommandType,
            point.x,
            point.y,
            point.z);
    }

    private bool HasPlannerDrivenCommandIntent()
    {
        if (behaviorContext == null || !behaviorContext.TryGetCommandIntentSnapshot(out BotCommandIntentPayload payload))
        {
            return false;
        }

        switch (payload.CommandType)
        {
            case BotCommandType.Move:
            case BotCommandType.Extinguish:
            case BotCommandType.Follow:
            case BotCommandType.Rescue:
            case BotCommandType.Search:
            case BotCommandType.Hold:
            case BotCommandType.Breach:
            case BotCommandType.Isolate:
            case BotCommandType.Assist:
            case BotCommandType.Regroup:
                return true;
            default:
                return false;
        }
    }

    private bool IsMovementCommandStillActive(BotCommandType commandType)
    {
        if (behaviorContext == null || !behaviorContext.TryGetCommandIntentSnapshot(out BotCommandIntentPayload payload))
        {
            return false;
        }

        return payload.CommandType == commandType &&
               (behaviorContext.HasMoveOrder || HasActiveDirectNavigationIntent() || hasIssuedDestination);
    }

    private void CompleteMovementStyleCommand(BotCommandType commandType)
    {
        CompleteCurrentTask(commandType == BotCommandType.Search
            ? "Search destination reached."
            : "Destination reached.");

        if (behaviorContext != null)
        {
            if (behaviorContext.HasMoveOrder)
            {
                behaviorContext.ClearMoveOrder();
            }
            else if (behaviorContext.TryGetCommandIntentSnapshot(out BotCommandIntentPayload payload) &&
                     payload.CommandType == commandType)
            {
                behaviorContext.ClearCommandIntent();
            }
        }

        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
        }

        hasIssuedDestination = false;
        if (commandType == BotCommandType.Move)
        {
            TryResumeSuspendedFollow();
        }
    }

    private void FailMovementStyleCommand(BotCommandType commandType, string detail)
    {
        FailCurrentTask(detail, BotTaskStatus.Blocked);

        if (behaviorContext != null)
        {
            if (behaviorContext.HasMoveOrder)
            {
                behaviorContext.ClearMoveOrder();
            }
            else if (behaviorContext.TryGetCommandIntentSnapshot(out BotCommandIntentPayload payload) &&
                     payload.CommandType == commandType)
            {
                behaviorContext.ClearCommandIntent();
            }
        }

        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
        }

        hasIssuedDestination = false;
        if (commandType == BotCommandType.Move)
        {
            ClearSuspendedFollowResume();
        }
    }

    private MoveToPositionTask BuildMovementDestinationTask(BotCommandType commandType)
    {
        return new MoveToPositionTask(
            commandType == BotCommandType.Search ? "Search Area" : "Move To Destination",
            agent =>
            {
                if (!agent.IsMovementCommandStillActive(commandType))
                {
                    return MoveTaskDirective.Success();
                }

                Vector3 destination = agent.hasIssuedDestination ? agent.lastIssuedDestination : agent.transform.position;
                if (agent.IsWithinArrivalDistance(destination))
                {
                    agent.CompleteMovementStyleCommand(commandType);
                    return MoveTaskDirective.Success();
                }

                return MoveTaskDirective.Running(destination);
            },
            moveAction: (agent, destination) =>
            {
                if (agent.MoveToCommand(destination))
                {
                    return true;
                }

                agent.FailMovementStyleCommand(commandType, "Failed to path to assigned destination.");
                return false;
            });
    }

    private bool forceCommandPlanRebuild;

    private void RequestCommandPlanRebuild()
    {
        forceCommandPlanRebuild = true;
    }
}
