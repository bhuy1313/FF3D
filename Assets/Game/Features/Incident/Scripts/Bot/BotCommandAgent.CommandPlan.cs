using UnityEngine;

public partial class BotCommandAgent
{
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

    private sealed class MoveToIssuedDestinationTask : IBotPlanTask
    {
        private readonly BotCommandType commandType;

        public MoveToIssuedDestinationTask(BotCommandType commandType)
        {
            this.commandType = commandType;
        }

        public string Name => commandType == BotCommandType.Search ? "Search Area" : "Move To Destination";

        public void OnStart(BotCommandAgent agent)
        {
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            if (!agent.IsMovementCommandStillActive(commandType))
            {
                return BotPlanTaskStatus.Success;
            }

            Vector3 destination = agent.hasIssuedDestination ? agent.lastIssuedDestination : agent.transform.position;
            if (agent.IsWithinArrivalDistance(destination))
            {
                agent.CompleteMovementStyleCommand(commandType);
                return BotPlanTaskStatus.Success;
            }

            if (!agent.MoveToCommand(destination))
            {
                agent.FailMovementStyleCommand(commandType, "Failed to path to assigned destination.");
                return BotPlanTaskStatus.Failure;
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

        if (forceCommandPlanRebuild || !planProcessor.HasActivePlan || activeCommandPlanKey != planKey)
        {
            forceCommandPlanRebuild = false;
            activeCommandPlanKey = planKey;
            planProcessor.SetPlan(plan, this);
        }

        planProcessor.Tick(this);
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
                        plan = new BotPlan("Move").Add(new MoveToIssuedDestinationTask(BotCommandType.Move));
                        planKey = BuildCommandIntentKey("Move");
                        return true;
                    case BotCommandType.Search:
                        plan = new BotPlan("Search").Add(new MoveToIssuedDestinationTask(BotCommandType.Search));
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
    }

    private bool forceCommandPlanRebuild;

    private void RequestCommandPlanRebuild()
    {
        forceCommandPlanRebuild = true;
    }
}
