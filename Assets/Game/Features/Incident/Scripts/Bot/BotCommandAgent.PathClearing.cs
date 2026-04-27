using UnityEngine;
using UnityEngine.AI;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private sealed class AcquireBlockedPathTargetTask : IBotPlanTask
    {
        private readonly Vector3 destination;

        public AcquireBlockedPathTargetTask(Vector3 destination)
        {
            this.destination = destination;
        }

        public string Name => "Acquire Blocked Path Target";

        public void OnStart(BotCommandAgent agent)
        {
            agent.UpdatePathClearingDebugStage(PathClearingDebugStage.SearchingBlocker, $"Checking route to {destination} for blocking obstacles.");
            agent.SetBreakSubtask(BotBreakSubtask.AcquireTarget, "Checking route for blocking obstacle.");
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            if (agent.currentBlockedPryTarget != null &&
                !agent.currentBlockedPryTarget.IsBreached &&
                (agent.currentBlockedPryTarget.CanBePriedOpen || agent.currentBlockedPryTarget.IsPryInProgress) &&
                agent.IsPryTargetStillRelevant(agent.currentBlockedPryTarget))
            {
                agent.UpdatePathClearingDebugStage(
                    PathClearingDebugStage.BlockedByBreakable,
                    $"Continuing to pry blocking target '{BotCommandAgent.GetDebugTargetName(agent.currentBlockedPryTarget)}'.");
                return BotPlanTaskStatus.Success;
            }

            if (agent.currentBlockedBreakable != null &&
                !agent.currentBlockedBreakable.IsBroken &&
                agent.currentBlockedBreakable.CanBeClearedByBot &&
                agent.IsBreakableStillRelevant(agent.currentBlockedBreakable))
            {
                agent.UpdatePathClearingDebugStage(
                    PathClearingDebugStage.BlockedByBreakable,
                    $"Continuing to clear blocking breakable '{BotCommandAgent.GetDebugTargetName(agent.currentBlockedBreakable)}'.");
                return BotPlanTaskStatus.Success;
            }

            if (agent.TryResolveBlockedPryTarget(destination, out IBotPryTarget pryTarget))
            {
                agent.SetCurrentBlockedPryTarget(pryTarget);
                agent.SetCurrentBlockedBreakable(null);
                agent.SetBreakSubtask(BotBreakSubtask.AcquireTool, $"Preparing crowbar for '{BotCommandAgent.GetDebugTargetName(pryTarget)}'.");
                agent.UpdatePathClearingDebugStage(
                    PathClearingDebugStage.BlockedByBreakable,
                    $"Detected blocking pry target '{BotCommandAgent.GetDebugTargetName(pryTarget)}' at {pryTarget.GetWorldPosition()}.");
                agent.RefreshPathClearingResumeGrace();
                return BotPlanTaskStatus.Success;
            }

            if (!agent.TryResolveBlockedBreakable(destination, out IBotBreakableTarget blockedTarget))
            {
                agent.UpdatePathClearingDebugStage(PathClearingDebugStage.Cleared, $"No blocking obstacle detected toward {destination}.");
                agent.ClearBlockedPathRuntime();
                return BotPlanTaskStatus.Success;
            }

            agent.SetCurrentBlockedPryTarget(null);
            agent.SetCurrentBlockedBreakable(blockedTarget);
            agent.SetBreakSubtask(BotBreakSubtask.AcquireTool, $"Preparing tool for '{BotCommandAgent.GetDebugTargetName(blockedTarget)}'.");
            agent.UpdatePathClearingDebugStage(
                PathClearingDebugStage.BlockedByBreakable,
                $"Detected blocking breakable '{BotCommandAgent.GetDebugTargetName(blockedTarget)}' at {blockedTarget.GetWorldPosition()}.");
            agent.RefreshPathClearingResumeGrace();
            return BotPlanTaskStatus.Success;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
        }
    }

    private sealed class AcquireBlockedPathToolTask : IBotPlanTask
    {
        public string Name => "Acquire Blocked Path Tool";

        public void OnStart(BotCommandAgent agent)
        {
            agent.SetBreakSubtask(BotBreakSubtask.AcquireTool, "Acquiring break tool.");
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            IBotPryTarget pryTarget = agent.currentBlockedPryTarget;
            if (pryTarget != null && !pryTarget.IsBreached && (pryTarget.CanBePriedOpen || pryTarget.IsPryInProgress))
            {
                if (agent.activeBreakTool != null &&
                    agent.activeBreakTool.ToolKind == BreakToolKind.Crowbar &&
                    agent.activeBreakTool.IsAvailableTo(agent.gameObject))
                {
                    return BotPlanTaskStatus.Success;
                }

                IBotBreakTool pryTool = agent.ResolvePreferredPryTool();
                if (pryTool == null)
                {
                    agent.StopBreakToolRoute();
                    agent.UpdatePathClearingDebugStage(
                        PathClearingDebugStage.NoBreakTool,
                        $"No usable crowbar found for '{BotCommandAgent.GetDebugTargetName(pryTarget)}'.");
                    agent.RefreshPathClearingResumeGrace();
                    return BotPlanTaskStatus.Running;
                }

                if (!agent.TryEnsureBreakToolEquipped(pryTool))
                {
                    return BotPlanTaskStatus.Running;
                }

                return BotPlanTaskStatus.Success;
            }

            IBotBreakableTarget blockedTarget = agent.currentBlockedBreakable;
            if (blockedTarget == null || blockedTarget.IsBroken || !blockedTarget.CanBeClearedByBot)
            {
                agent.ClearBlockedPathRuntime();
                return BotPlanTaskStatus.Success;
            }

            if (agent.activeBreakTool != null && agent.IsBreakToolStillUsable(agent.activeBreakTool))
            {
                return BotPlanTaskStatus.Success;
            }

            IBotBreakTool breakTool = agent.ResolveCommittedBreakTool();
            if (breakTool == null)
            {
                agent.StopBreakToolRoute();
                agent.LogPathClearingFlow(
                    $"no-break-tool:blocker:{BotCommandAgent.GetDebugTargetName(blockedTarget)}",
                    "No usable tool available.");
                agent.LogPathClearingFlow(
                    $"stop-breaktool:blocker:{BotCommandAgent.GetDebugTargetName(blockedTarget)}",
                    "Stopped.");
                agent.UpdatePathClearingDebugStage(
                    PathClearingDebugStage.NoBreakTool,
                    $"No usable break tool found for '{BotCommandAgent.GetDebugTargetName(blockedTarget)}'.");
                agent.RefreshPathClearingResumeGrace();
                return BotPlanTaskStatus.Running;
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

    private sealed class ExecuteBlockedPathTask : IBotPlanTask
    {
        public string Name => "Clear Blocked Path";

        public void OnStart(BotCommandAgent agent)
        {
        }

        public BotPlanTaskStatus OnUpdate(BotCommandAgent agent)
        {
            IBotPryTarget pryTarget = agent.currentBlockedPryTarget;
            if (pryTarget != null)
            {
                if (pryTarget.IsBreached)
                {
                    agent.ClearBlockedPathRuntime();
                    return BotPlanTaskStatus.Success;
                }

                if (agent.activeBreakTool == null ||
                    agent.activeBreakTool.ToolKind != BreakToolKind.Crowbar ||
                    !agent.activeBreakTool.IsAvailableTo(agent.gameObject))
                {
                    agent.planProcessor.InjectFront(
                        agent,
                        new AcquireBlockedPathToolTask(),
                        new ExecuteBlockedPathTask());
                    return BotPlanTaskStatus.Success;
                }

                agent.UpdatePathClearingDebugStage(
                    PathClearingDebugStage.BlockedByBreakable,
                    $"Continuing to pry blocking target '{BotCommandAgent.GetDebugTargetName(pryTarget)}'.");
                agent.RefreshPathClearingResumeGrace();
                if (!agent.HandleEquippedPryToolAgainstTarget(agent.activeBreakTool, pryTarget))
                {
                    agent.ClearBlockedPathRuntime();
                    return BotPlanTaskStatus.Success;
                }

                if (pryTarget.IsBreached)
                {
                    agent.ClearBlockedPathRuntime();
                    return BotPlanTaskStatus.Success;
                }

                return BotPlanTaskStatus.Running;
            }

            IBotBreakableTarget blockedTarget = agent.currentBlockedBreakable;
            if (blockedTarget == null)
            {
                agent.ClearBlockedPathRuntime();
                return BotPlanTaskStatus.Success;
            }

            if (blockedTarget.IsBroken || !blockedTarget.CanBeClearedByBot)
            {
                agent.ClearBlockedPathRuntime();
                return BotPlanTaskStatus.Success;
            }

            if (agent.activeBreakTool == null || !agent.IsBreakToolStillUsable(agent.activeBreakTool))
            {
                agent.planProcessor.InjectFront(
                    agent,
                    new AcquireBlockedPathToolTask(),
                    new ExecuteBlockedPathTask());
                return BotPlanTaskStatus.Success;
            }

            agent.UpdatePathClearingDebugStage(
                PathClearingDebugStage.BlockedByBreakable,
                $"Continuing to clear blocking breakable '{BotCommandAgent.GetDebugTargetName(blockedTarget)}'.");
            agent.RefreshPathClearingResumeGrace();
            if (!agent.HandleEquippedBreakToolAgainstTarget(agent.activeBreakTool, blockedTarget))
            {
                agent.ClearBlockedPathRuntime();
                return BotPlanTaskStatus.Success;
            }

            if (blockedTarget.IsBroken)
            {
                agent.ClearBlockedPathRuntime();
                return BotPlanTaskStatus.Success;
            }

            return BotPlanTaskStatus.Running;
        }

        public void OnEnd(BotCommandAgent agent, bool interrupted)
        {
        }
    }

    [Header("Break Task Flow")]
    [SerializeField] private BotBreakSubtask currentBreakSubtask;
    [SerializeField] private string breakTaskDetail = "Awaiting break assignment.";
    [SerializeField] private string lastBreakFailureReason;
    [SerializeField] private float breakSubtaskStartedAtTime;

    private bool TryHandleBlockedPath(Vector3 destination)
    {
        if (!enablePathClearing || navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            ClearBlockedPathRuntime();
            return false;
        }

        if (currentBlockedBreakable != null &&
            !currentBlockedBreakable.IsBroken &&
            currentBlockedBreakable.CanBeClearedByBot)
        {
            return true;
        }

        if (currentBlockedPryTarget != null && !currentBlockedPryTarget.IsBreached)
        {
            return true;
        }

        IBotPryTarget pryTarget = null;
        IBotBreakableTarget blockedTarget = null;
        bool hasPryTarget = TryResolveBlockedPryTarget(destination, out pryTarget);
        bool hasBreakableTarget = !hasPryTarget && TryResolveBlockedBreakable(destination, out blockedTarget);

        if (!hasPryTarget && !hasBreakableTarget)
        {
            ClearBlockedPathRuntime();
            return false;
        }

        if (planProcessor == null || !planProcessor.HasActivePlan)
        {
            ClearBlockedPathRuntime();
            return false;
        }

        if (hasPryTarget)
        {
            SetCurrentBlockedPryTarget(pryTarget);
            SetCurrentBlockedBreakable(null);
        }
        else
        {
            SetCurrentBlockedPryTarget(null);
            SetCurrentBlockedBreakable(blockedTarget);
        }

        planProcessor.InterruptWith(
            this,
            new AcquireBlockedPathTargetTask(destination),
            new AcquireBlockedPathToolTask(),
            new ExecuteBlockedPathTask());
        return true;
    }

    private bool HandleEquippedBreakToolAgainstTarget(IBotBreakTool equippedBreakTool, IBotBreakableTarget blockedTarget)
    {
        if (equippedBreakTool == null || blockedTarget == null || blockedTarget.IsBroken || !blockedTarget.CanBeClearedByBot)
        {
            return false;
        }

        Vector3 targetPosition = blockedTarget.GetWorldPosition();
        bool hasConfiguredStandPose = blockedTarget.TryGetBreakStandPose(transform.position, out Vector3 desiredPosition, out _);
        float distanceToDesiredPosition;

        if (hasConfiguredStandPose)
        {
            if (navMeshSampleDistance > 0f &&
                NavMesh.SamplePosition(desiredPosition, out NavMeshHit standNavMeshHit, navMeshSampleDistance, navMeshAgent.areaMask))
            {
                desiredPosition = standNavMeshHit.position;
            }

            distanceToDesiredPosition = GetHorizontalDistance(transform.position, desiredPosition);
        }
        else
        {
            float desiredDistance = Mathf.Clamp(equippedBreakTool.PreferredBreakDistance, 0.5f, equippedBreakTool.MaxBreakDistance);
            desiredPosition = ResolveStandPositionAroundPoint(transform.position, targetPosition, desiredDistance);
            float horizontalDistance = GetHorizontalDistance(transform.position, targetPosition);
            float standDistanceDelta = horizontalDistance - desiredDistance;
            distanceToDesiredPosition = horizontalDistance > equippedBreakTool.MaxBreakDistance
                ? horizontalDistance
                : Mathf.Max(0f, standDistanceDelta);
        }

        if (distanceToDesiredPosition > breakStandDistanceTolerance)
        {
            SetBreakSubtask(BotBreakSubtask.MoveToObstacle, $"Moving into range of '{GetDebugTargetName(blockedTarget)}'.");
            UpdatePathClearingDebugStage(PathClearingDebugStage.MovingToBreakable, $"Moving into break range of '{GetDebugTargetName(blockedTarget)}'.");
            LogVerbosePathClearing(
                VerbosePathClearingLogCategory.Movement,
                $"movebreak:{GetDebugTargetName(blockedTarget)}:{desiredPosition}",
                $"Moving to breakable '{GetDebugTargetName(blockedTarget)}'. target={targetPosition}, destination={desiredPosition}, distanceToDestination={distanceToDesiredPosition:F2}, usingConfiguredStandPoint={hasConfiguredStandPose}.");
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(desiredPosition);
            return true;
        }

        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = true;
        AimTowards(targetPosition);

        if (blockedTarget.IsBreakInProgress)
        {
            return true;
        }

        if (Time.time >= nextBreakUseTime)
        {
            bool startedBreak = equippedBreakTool.UseOnTarget(gameObject, blockedTarget);
            if (startedBreak)
            {
                SetBreakSubtask(BotBreakSubtask.Break, $"Breaking '{GetDebugTargetName(blockedTarget)}'.");
                UpdatePathClearingDebugStage(PathClearingDebugStage.Breaking, $"Breaking '{GetDebugTargetName(blockedTarget)}' with '{GetBreakToolName(equippedBreakTool)}'.");
                LogPathClearingFlow(
                    $"break-breakable:{GetDebugTargetName(blockedTarget)}",
                    "Breaking Breakable.");
            }

            nextBreakUseTime = Time.time + equippedBreakTool.UseCooldown;
        }

        return true;
    }

    private bool HandleEquippedPryToolAgainstTarget(IBotBreakTool equippedBreakTool, IBotPryTarget pryTarget)
    {
        if (equippedBreakTool == null || equippedBreakTool.ToolKind != BreakToolKind.Crowbar || pryTarget == null || pryTarget.IsBreached)
        {
            return false;
        }

        Vector3 targetPosition = pryTarget.GetWorldPosition();
        float interactionDistance = Mathf.Max(0.5f, breachInteractionDistance);
        float horizontalDistance = GetHorizontalDistance(transform.position, targetPosition);
        if (horizontalDistance > interactionDistance)
        {
            SetBreakSubtask(BotBreakSubtask.MoveToObstacle, $"Moving into range of '{GetDebugTargetName(pryTarget)}'.");
            UpdatePathClearingDebugStage(PathClearingDebugStage.MovingToBreakable, $"Moving into pry range of '{GetDebugTargetName(pryTarget)}'.");
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(targetPosition);
            return true;
        }

        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = true;
        AimTowards(targetPosition);

        if (pryTarget.IsPryInProgress)
        {
            SetBreakSubtask(BotBreakSubtask.Pry, $"Prying '{GetDebugTargetName(pryTarget)}'.");
            return true;
        }

        if (!pryTarget.CanBePriedOpen)
        {
            return false;
        }

        if (Time.time >= nextBreakUseTime)
        {
            bool startedPry = equippedBreakTool.UseOnTarget(gameObject, pryTarget);
            if (startedPry)
            {
                SetBreakSubtask(BotBreakSubtask.Pry, $"Prying '{GetDebugTargetName(pryTarget)}'.");
                UpdatePathClearingDebugStage(PathClearingDebugStage.Breaking, $"Prying '{GetDebugTargetName(pryTarget)}' with '{GetBreakToolName(equippedBreakTool)}'.");
                LogPathClearingFlow(
                    $"pry-target:{GetDebugTargetName(pryTarget)}",
                    "Prying target.");
            }

            nextBreakUseTime = Time.time + equippedBreakTool.UseCooldown;
        }

        return true;
    }

    private bool TryResolveBlockedBreakable(Vector3 destination, out IBotBreakableTarget blockedTarget)
    {
        blockedTarget = null;

        if (currentBlockedBreakable != null &&
            !currentBlockedBreakable.IsBroken &&
            currentBlockedBreakable.CanBeClearedByBot &&
            IsBreakableStillRelevant(currentBlockedBreakable))
        {
            LogVerbosePathClearing(
                VerbosePathClearingLogCategory.Detection,
                $"reusebreakable:{GetDebugTargetName(currentBlockedBreakable)}",
                $"Continuing with current breakable '{GetDebugTargetName(currentBlockedBreakable)}'.");
            blockedTarget = currentBlockedBreakable;
            return true;
        }

        if (interactionSensor != null && interactionSensor.TryFindBreakableAhead(out IBotBreakableTarget sensedBreakable))
        {
            LogPathClearingFlow(
                $"sensor-blocker-ahead:{GetDebugTargetName(sensedBreakable)}",
                $"Blocker detected: '{GetDebugTargetName(sensedBreakable)}'.");
            LogVerbosePathClearing(
                VerbosePathClearingLogCategory.Detection,
                $"sensorbreakable:{GetDebugTargetName(sensedBreakable)}",
                $"Sensor detected breakable '{GetDebugTargetName(sensedBreakable)}' ahead.");
            blockedTarget = sensedBreakable;
            return true;
        }

        if (TryFindBreakableInFront(destination, out IBotBreakableTarget fallbackBreakable))
        {
            LogVerbosePathClearing(
                VerbosePathClearingLogCategory.Detection,
                $"fallbackbreakable:{GetDebugTargetName(fallbackBreakable)}",
                $"Fallback probe detected breakable '{GetDebugTargetName(fallbackBreakable)}' toward {destination}.");
            blockedTarget = fallbackBreakable;
            return true;
        }

        return false;
    }

    private bool TryResolveBlockedPryTarget(Vector3 destination, out IBotPryTarget pryTarget)
    {
        pryTarget = null;

        if (currentBlockedPryTarget != null &&
            !currentBlockedPryTarget.IsBreached &&
            (currentBlockedPryTarget.CanBePriedOpen || currentBlockedPryTarget.IsPryInProgress) &&
            IsPryTargetStillRelevant(currentBlockedPryTarget))
        {
            LogVerbosePathClearing(
                VerbosePathClearingLogCategory.Detection,
                $"reuseprytarget:{GetDebugTargetName(currentBlockedPryTarget)}",
                $"Continuing with current pry target '{GetDebugTargetName(currentBlockedPryTarget)}'.");
            pryTarget = currentBlockedPryTarget;
            return true;
        }

        if (interactionSensor != null && interactionSensor.TryFindPryTargetAhead(out IBotPryTarget sensedPryTarget))
        {
            LogPathClearingFlow(
                $"sensor-pry-ahead:{GetDebugTargetName(sensedPryTarget)}",
                $"Pry target detected: '{GetDebugTargetName(sensedPryTarget)}'.");
            pryTarget = sensedPryTarget;
            return true;
        }

        if (TryFindPryTargetInFront(destination, out IBotPryTarget fallbackPryTarget))
        {
            LogVerbosePathClearing(
                VerbosePathClearingLogCategory.Detection,
                $"fallbackprytarget:{GetDebugTargetName(fallbackPryTarget)}",
                $"Fallback probe detected pry target '{GetDebugTargetName(fallbackPryTarget)}' toward {destination}.");
            pryTarget = fallbackPryTarget;
            return true;
        }

        return false;
    }

    private bool IsBreakableStillRelevant(IBotBreakableTarget candidate)
    {
        if (candidate == null || candidate.IsBroken || !candidate.CanBeClearedByBot)
        {
            return false;
        }

        if (interactionSensor != null && interactionSensor.TryFindBreakableAhead(out IBotBreakableTarget sensedBreakable))
        {
            return ReferenceEquals(candidate, sensedBreakable);
        }

        if (TryFindBreakableInFront(lastIssuedDestination, out IBotBreakableTarget fallbackBreakable))
        {
            return ReferenceEquals(candidate, fallbackBreakable);
        }

        return false;
    }

    private bool IsPryTargetStillRelevant(IBotPryTarget candidate)
    {
        if (candidate == null || candidate.IsBreached || (!candidate.CanBePriedOpen && !candidate.IsPryInProgress))
        {
            return false;
        }

        if (interactionSensor != null && interactionSensor.TryFindPryTargetAhead(out IBotPryTarget sensedPryTarget))
        {
            return ReferenceEquals(candidate, sensedPryTarget);
        }

        if (TryFindPryTargetInFront(lastIssuedDestination, out IBotPryTarget fallbackPryTarget))
        {
            return ReferenceEquals(candidate, fallbackPryTarget);
        }

        return false;
    }

    private bool TryFindBreakableInFront(Vector3 destination, out IBotBreakableTarget breakableTarget)
    {
        breakableTarget = null;
        Vector3 forwardDirection = GetPathClearingProbeDirection(destination);
        if (forwardDirection.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        float bestDistance = float.PositiveInfinity;
        Vector3 origin = transform.position;

        foreach (IBotBreakableTarget candidate in BotRuntimeRegistry.ActiveBreakableTargets)
        {
            if (candidate == null || candidate.IsBroken || !candidate.CanBeClearedByBot)
            {
                continue;
            }

            if (BotRuntimeRegistry.Reservations.IsReservedByOther(candidate, gameObject))
            {
                continue;
            }

            Vector3 candidatePosition = candidate.GetWorldPosition();
            Vector3 toCandidate = candidatePosition - origin;
            float forwardDistance = Vector3.Dot(forwardDirection, toCandidate);
            if (forwardDistance < 0f || forwardDistance > breakableLookAheadDistance)
            {
                continue;
            }

            Vector3 projectedPoint = origin + forwardDirection * forwardDistance;
            float lateralDistance = GetHorizontalDistance(projectedPoint, candidatePosition);
            float maxLateralDistance = breakableCorridorWidth + GetBreakableRouteRadius(candidate);
            if (lateralDistance > maxLateralDistance)
            {
                continue;
            }

            if (forwardDistance < bestDistance)
            {
                bestDistance = forwardDistance;
                breakableTarget = candidate;
            }
        }

        if (breakableTarget == null &&
            perceptionMemory != null &&
            perceptionMemory.TryGetNearestRecentBreakable(origin, breakableLookAheadDistance, out IBotBreakableTarget rememberedBreakable) &&
            !BotRuntimeRegistry.Reservations.IsReservedByOther(rememberedBreakable, gameObject))
        {
            breakableTarget = rememberedBreakable;
        }

        if (breakableTarget == null &&
            BotRuntimeRegistry.SharedIncidentBlackboard.TryGetNearestRecentBreakable(origin, breakableLookAheadDistance, gameObject, out IBotBreakableTarget sharedBreakable))
        {
            breakableTarget = sharedBreakable;
        }

        return breakableTarget != null;
    }

    private bool TryFindPryTargetInFront(Vector3 destination, out IBotPryTarget pryTarget)
    {
        pryTarget = null;
        Vector3 forwardDirection = GetPathClearingProbeDirection(destination);
        if (forwardDirection.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        float bestDistance = float.PositiveInfinity;
        Vector3 origin = transform.position;

        foreach (IBotPryTarget candidate in BotRuntimeRegistry.ActivePryTargets)
        {
            if (candidate == null || candidate.IsBreached || (!candidate.CanBePriedOpen && !candidate.IsPryInProgress))
            {
                continue;
            }

            if (BotRuntimeRegistry.Reservations.IsReservedByOther(candidate, gameObject))
            {
                continue;
            }

            Vector3 candidatePosition = candidate.GetWorldPosition();
            Vector3 toCandidate = candidatePosition - origin;
            float forwardDistance = Vector3.Dot(forwardDirection, toCandidate);
            if (forwardDistance < 0f || forwardDistance > breakableLookAheadDistance)
            {
                continue;
            }

            Vector3 projectedPoint = origin + forwardDirection * forwardDistance;
            float lateralDistance = GetHorizontalDistance(projectedPoint, candidatePosition);
            float maxLateralDistance = breakableCorridorWidth + GetPryTargetRouteRadius(candidate);
            if (lateralDistance > maxLateralDistance)
            {
                continue;
            }

            if (forwardDistance < bestDistance)
            {
                bestDistance = forwardDistance;
                pryTarget = candidate;
            }
        }

        return pryTarget != null;
    }

    private Vector3 GetPathClearingProbeDirection(Vector3 destination)
    {
        Vector3 direction = navMeshAgent != null ? navMeshAgent.velocity : Vector3.zero;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f && navMeshAgent != null)
        {
            direction = navMeshAgent.desiredVelocity;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.01f && navMeshAgent != null && navMeshAgent.hasPath)
        {
            direction = navMeshAgent.steeringTarget - transform.position;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.01f)
        {
            direction = destination - transform.position;
            direction.y = 0f;
        }

        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero;
    }

    private float GetBreakableRouteRadius(IBotBreakableTarget candidate)
    {
        if (candidate is Component component && TryGetWorldBounds(component, out Bounds bounds))
        {
            return Mathf.Max(bounds.extents.x, bounds.extents.z);
        }

        return 0.5f;
    }

    private float GetPryTargetRouteRadius(IBotPryTarget candidate)
    {
        if (candidate is Component component && TryGetWorldBounds(component, out Bounds bounds))
        {
            return Mathf.Max(bounds.extents.x, bounds.extents.z);
        }

        return 0.5f;
    }

    private static bool TryGetWorldBounds(Component component, out Bounds bounds)
    {
        bounds = default;
        if (component == null)
        {
            return false;
        }

        Collider[] colliders = component.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            if (bounds.size == Vector3.zero)
            {
                bounds = collider.bounds;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        if (bounds.size != Vector3.zero)
        {
            return true;
        }

        Renderer[] renderers = component.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (bounds.size == Vector3.zero)
            {
                bounds = renderer.bounds;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds.size != Vector3.zero;
    }

    private IBotBreakTool ResolveCommittedBreakTool()
    {
        if (IsBreakToolStillUsable(committedBreakTool))
        {
            LogVerbosePathClearing(
                VerbosePathClearingLogCategory.Tooling,
                $"reusebreaktool:{GetBreakToolName(committedBreakTool)}",
                $"Reusing committed break tool '{GetBreakToolName(committedBreakTool)}'.");
            return committedBreakTool;
        }

        ReleaseCommittedBreakTool();
        IBotBreakTool bestTool = null;
        float bestScore = float.PositiveInfinity;
        System.Collections.Generic.List<IBotBreakTool> inventoryTools = new System.Collections.Generic.List<IBotBreakTool>();
        inventorySystem.CollectItems(inventoryTools);

        for (int i = 0; i < inventoryTools.Count; i++)
        {
            IBotBreakTool candidate = inventoryTools[i];
            if (candidate == null ||
                !candidate.IsAvailableTo(gameObject) ||
                (currentBlockedBreakable != null && !currentBlockedBreakable.SupportsBreakTool(candidate.ToolKind)) ||
                IsBreakToolBlockedByCurrentBreakable(candidate) ||
                IsBreakToolTemporarilyRejected(candidate))
            {
                continue;
            }

            if (0f < bestScore)
            {
                bestScore = 0f;
                bestTool = candidate;
            }
        }

        float searchRadiusSq = toolSearchRadius * toolSearchRadius;
        foreach (IBotBreakTool candidate in BotRuntimeRegistry.ActiveBreakTools)
        {
            Component candidateComponent = candidate as Component;
            if (candidateComponent == null ||
                candidate.IsHeld ||
                !candidate.IsAvailableTo(gameObject) ||
                (currentBlockedBreakable != null && !currentBlockedBreakable.SupportsBreakTool(candidate.ToolKind)) ||
                IsBreakToolBlockedByCurrentBreakable(candidate) ||
                IsBreakToolTemporarilyRejected(candidate))
            {
                continue;
            }

            float distanceSq = (candidateComponent.transform.position - transform.position).sqrMagnitude;
            if (distanceSq > searchRadiusSq)
            {
                continue;
            }

            float score = Mathf.Sqrt(distanceSq);
            if (score < bestScore)
            {
                bestScore = score;
                bestTool = candidate;
            }
        }

        if (bestTool == null &&
            perceptionMemory != null &&
            perceptionMemory.TryGetNearestRecentBreakTool(transform.position, toolSearchRadius, gameObject, out IBotBreakTool rememberedTool) &&
            (currentBlockedBreakable == null || currentBlockedBreakable.SupportsBreakTool(rememberedTool.ToolKind)) &&
            !IsBreakToolBlockedByCurrentBreakable(rememberedTool) &&
            !IsBreakToolTemporarilyRejected(rememberedTool))
        {
            bestTool = rememberedTool;
        }

        if (bestTool == null &&
            BotRuntimeRegistry.SharedIncidentBlackboard.TryGetNearestRecentBreakTool(transform.position, toolSearchRadius, gameObject, out IBotBreakTool sharedTool) &&
            (currentBlockedBreakable == null || currentBlockedBreakable.SupportsBreakTool(sharedTool.ToolKind)) &&
            !IsBreakToolBlockedByCurrentBreakable(sharedTool) &&
            !IsBreakToolTemporarilyRejected(sharedTool))
        {
            bestTool = sharedTool;
        }

        if (bestTool == null || !bestTool.TryClaim(gameObject))
        {
            return null;
        }

        committedBreakTool = bestTool;
        perceptionMemory?.RememberBreakTool(committedBreakTool);
        BotRuntimeRegistry.SharedIncidentBlackboard.RememberBreakTool(committedBreakTool);
        LogVerbosePathClearing(
            VerbosePathClearingLogCategory.Tooling,
            $"claimbreaktool:{GetBreakToolName(committedBreakTool)}",
            $"Committed break tool '{GetBreakToolName(committedBreakTool)}'.");
        return committedBreakTool;
    }

    private bool TryEnsureBreakToolEquipped(IBotBreakTool desiredTool)
    {
        BotToolAcquisitionOptions<IBotBreakTool> options = new BotToolAcquisitionOptions<IBotBreakTool>
        {
            BotTransform = transform,
            InventorySystem = inventorySystem,
            PickupDistance = pickupDistance,
            IsAvailableToBot = tool => tool.IsAvailableTo(gameObject),
            IsHeldByBot = tool => tool.IsHeldBy(gameObject),
            SetActiveTool = tool => activeBreakTool = tool,
            OnUnavailable = ReleaseCommittedBreakTool,
            ReportSearching = toolName =>
            {
                SetBreakSubtask(BotBreakSubtask.AcquireTool, $"Acquiring break tool '{toolName}'.");
                UpdatePathClearingDebugStage(PathClearingDebugStage.SearchingBreakTool, $"Acquiring break tool '{toolName}'.");
            },
            ReportPickingUp = toolName =>
            {
                LogPathClearingFlow($"pickup-breaktool:{toolName}", $"Picked up {toolName}.");
                SetBreakSubtask(BotBreakSubtask.AcquireTool, $"Picking up break tool '{toolName}'.");
                UpdatePathClearingDebugStage(PathClearingDebugStage.PickingUpBreakTool, $"Picking up break tool '{toolName}'.");
            },
            ReportMovingToTool = (toolName, toolPosition) =>
            {
                SetBreakSubtask(BotBreakSubtask.MoveToTool, $"Moving to break tool '{toolName}'.");
                UpdatePathClearingDebugStage(PathClearingDebugStage.MovingToBreakTool, $"Moving to break tool '{toolName}' at {toolPosition}.");
            },
            LogHeld = toolName => LogVerbosePathClearing(
                VerbosePathClearingLogCategory.Tooling,
                $"heldbreaktool:{toolName}",
                $"Using already-held break tool '{toolName}'."),
            LogEquipped = toolName => LogVerbosePathClearing(
                VerbosePathClearingLogCategory.Tooling,
                $"equipbreaktool:{toolName}",
                $"Equipped break tool '{toolName}' from inventory."),
            LogPickedUp = toolName => LogVerbosePathClearing(
                VerbosePathClearingLogCategory.Tooling,
                $"pickupbreaktool:{toolName}",
                $"Picked up and equipped break tool '{toolName}'."),
            SetPickupWindow = SetPickupWindow,
            MoveToTool = toolPosition =>
            {
                if (IsBreakToolBlockedByCurrentBreakable(desiredTool))
                {
                    if (currentBlockedBreakable != null)
                    {
                        LogPathClearingFlow(
                            $"discard-breaktool-move:{GetBreakToolName(desiredTool)}:{GetDebugTargetName(currentBlockedBreakable)}",
                            $"Discarding {GetBreakToolName(desiredTool)}.");
                        RejectBreakToolForCurrentBlocker(desiredTool, currentBlockedBreakable);
                    }

                    StopBreakToolRoute();
                    SetPickupWindow(false, null);
                    ReleaseCommittedBreakTool();
                    UpdatePathClearingDebugStage(
                        PathClearingDebugStage.SearchingBreakTool,
                        $"Break tool route is blocked. Re-evaluating tool.");
                    return;
                }

                LogVerbosePathClearing(
                    VerbosePathClearingLogCategory.Movement,
                    $"movetobreaktool:{toolPosition}",
                    $"Moving to break tool at {toolPosition}.");
                LogPathClearingFlow(
                    $"move-breaktool:{FormatFlowVectorKey(toolPosition)}",
                    "Moving.");
                MoveToIgnoringBlockedPathInterrupt(toolPosition);
            }
        };

        return BotToolAcquisitionUtility.TryEnsureToolEquipped(desiredTool, options);
    }

    private bool IsBreakToolStillUsable(IBotBreakTool tool)
    {
        return tool != null &&
               tool.IsAvailableTo(gameObject) &&
               (currentBlockedBreakable == null || currentBlockedBreakable.SupportsBreakTool(tool.ToolKind)) &&
               !IsBreakToolBlockedByCurrentBreakable(tool) &&
               !IsBreakToolTemporarilyRejected(tool);
    }

    private bool IsBreakToolBlockedByCurrentBreakable(IBotBreakTool tool)
    {
        if (tool == null ||
            tool.IsHeldBy(gameObject) ||
            currentBlockedBreakable == null ||
            currentBlockedBreakable.IsBroken ||
            !currentBlockedBreakable.CanBeClearedByBot)
        {
            return false;
        }

        if (!(tool is Component toolComponent))
        {
            return false;
        }

        Vector3 toolPosition = toolComponent.transform.position;
        string toolName = GetBreakToolName(tool);
        LogPathClearingFlow(
            $"candidate-breaktool:{toolName}:{FormatFlowVectorKey(toolPosition)}",
            $"Searching for {toolName}.");
        LogPathClearingFlow(
            $"create-path:{toolName}:{FormatFlowVectorKey(toolPosition)}",
            $"Creating path to {toolName}.");

        if (interactionSensor != null &&
            TryFindBreakableOnPreviewPath(toolName, toolPosition, out IBotBreakableTarget sensedBreakable))
        {
            if (sensedBreakable != null && !sensedBreakable.IsBroken && sensedBreakable.CanBeClearedByBot)
            {
                LogPathClearingFlow(
                    $"retry-breaktool:{toolName}:{GetDebugTargetName(sensedBreakable)}",
                    "Searching for another breaching tool.");
                LogPathClearingFlow(
                    $"discard-breaktool:{toolName}:{GetDebugTargetName(sensedBreakable)}",
                    $"Discarding {toolName}.");
                LogVerbosePathClearing(
                    VerbosePathClearingLogCategory.Tooling,
                    $"blockedbreaktool:{toolName}:{GetDebugTargetName(sensedBreakable)}",
                    $"Break tool '{toolName}' is blocked by '{GetDebugTargetName(sensedBreakable)}' on the previewed route to the tool.");
                return true;
            }
        }

        if (currentBlockedBreakable.IsOnSameSide(transform.position, toolPosition))
        {
            return false;
        }

        LogPathClearingFlow(
            $"retry-breaktool:{toolName}:{GetDebugTargetName(currentBlockedBreakable)}",
            "Searching for another breaching tool.");
        LogPathClearingFlow(
            $"discard-breaktool:{toolName}:{GetDebugTargetName(currentBlockedBreakable)}",
            $"Discarding {toolName}.");
        LogVerbosePathClearing(
            VerbosePathClearingLogCategory.Tooling,
            $"blockedbreaktool:{toolName}:{GetDebugTargetName(currentBlockedBreakable)}",
            $"Break tool '{toolName}' is on the opposite side of '{GetDebugTargetName(currentBlockedBreakable)}'.");
        return true;
    }

    private bool TryFindBreakableOnPreviewPath(string toolName, Vector3 toolPosition, out IBotBreakableTarget breakableTarget)
    {
        breakableTarget = null;
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        Vector3 sampledToolPosition = toolPosition;
        if (navMeshSampleDistance > 0f &&
            NavMesh.SamplePosition(toolPosition, out NavMeshHit toolNavMeshHit, navMeshSampleDistance, navMeshAgent.areaMask))
        {
            sampledToolPosition = toolNavMeshHit.position;
        }

        NavMeshPath path = new NavMeshPath();
        if (!navMeshAgent.CalculatePath(sampledToolPosition, path) || path.corners == null || path.corners.Length < 2)
        {
            if (interactionSensor != null &&
                interactionSensor.TryFindBreakableTowards(sampledToolPosition, out breakableTarget))
            {
                LogPathClearingFlow(
                    $"sensor-blocker-tool:{toolName}:{GetDebugTargetName(breakableTarget)}",
                    $"Blocker detected: '{GetDebugTargetName(breakableTarget)}'.");
                return true;
            }

            return false;
        }

        for (int i = 1; i < path.corners.Length; i++)
        {
            if (interactionSensor != null &&
                interactionSensor.TryFindBreakableBetween(path.corners[i - 1], path.corners[i], out breakableTarget))
            {
                LogPathClearingFlow(
                    $"sensor-blocker-path:{toolName}:{GetDebugTargetName(breakableTarget)}:{i}",
                    $"Blocker detected: '{GetDebugTargetName(breakableTarget)}'.");
                return true;
            }
        }

        return false;
    }

    private void ReleaseCommittedBreakTool()
    {
        if (committedBreakTool != null)
        {
            committedBreakTool.ReleaseClaim(gameObject);
            committedBreakTool = null;
        }
    }

    private void StopBreakToolRoute()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled)
        {
            return;
        }

        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = true;
    }

    private void ClearBlockedPathRuntime()
    {
        SetPickupWindow(false);
        activeBreakTool = null;
        SetCurrentBlockedBreakable(null);
        SetCurrentBlockedPryTarget(null);
        temporarilyRejectedBreakTool = null;
        temporarilyRejectedBreakable = null;
        nextBreakUseTime = 0f;
        nextPathClearingRefreshTime = 0f;
        RefreshPathClearingResumeGrace();
        temporarilyRejectedBreakToolUntilTime = 0f;
        currentBreakSubtask = BotBreakSubtask.None;
        breakTaskDetail = "Awaiting break assignment.";
        lastBreakFailureReason = string.Empty;
        breakSubtaskStartedAtTime = 0f;
        activityDebug?.ResetPathClearing();
        ReleaseCommittedBreakTool();
    }

    internal void SetBreakSubtask(BotBreakSubtask subtask, string detail)
    {
        if (currentBreakSubtask != subtask)
        {
            breakSubtaskStartedAtTime = Application.isPlaying ? Time.time : 0f;
        }

        currentBreakSubtask = subtask;
        breakTaskDetail = string.IsNullOrWhiteSpace(detail) ? "Executing break task." : detail;
    }

    internal void SetBreakFailureReason(string detail)
    {
        lastBreakFailureReason = string.IsNullOrWhiteSpace(detail) ? string.Empty : detail;
    }

    internal string GetActiveBreakTaskDetail()
    {
        if (!string.IsNullOrWhiteSpace(lastBreakFailureReason))
        {
            return lastBreakFailureReason;
        }

        return string.IsNullOrWhiteSpace(breakTaskDetail)
            ? "Executing break task."
            : breakTaskDetail;
    }

    public void ResetMoveActivityDebug()
    {
        activityDebug?.ResetMovePathFlow();
        movePickupController?.Reset();
    }

    private bool IsBreakToolTemporarilyRejected(IBotBreakTool tool)
    {
        if (tool == null || temporarilyRejectedBreakTool == null || currentBlockedBreakable == null)
        {
            return false;
        }

        if (Time.time >= temporarilyRejectedBreakToolUntilTime)
        {
            return false;
        }

        return ReferenceEquals(tool, temporarilyRejectedBreakTool) &&
               ReferenceEquals(currentBlockedBreakable, temporarilyRejectedBreakable);
    }

    private void RejectBreakToolForCurrentBlocker(IBotBreakTool tool, IBotBreakableTarget blocker)
    {
        if (tool == null || blocker == null)
        {
            return;
        }

        temporarilyRejectedBreakTool = tool;
        temporarilyRejectedBreakable = blocker;
        temporarilyRejectedBreakToolUntilTime = Time.time + Mathf.Max(0.1f, blockedBreakToolRetryDelay);
        LogVerbosePathClearing(
            VerbosePathClearingLogCategory.Tooling,
            $"rejectbreaktool:{GetBreakToolName(tool)}:{GetDebugTargetName(blocker)}",
            $"Temporarily rejecting break tool '{GetBreakToolName(tool)}' because route is blocked by '{GetDebugTargetName(blocker)}'.");
    }

    private Vector3 ResolveStandPositionAroundPoint(Vector3 referencePoint, Vector3 targetPosition, float preferredDistance)
    {
        if (TryResolveExtinguisherStandPosition(referencePoint, targetPosition, preferredDistance, out Vector3 desiredPosition))
        {
            return desiredPosition;
        }

        return transform.position;
    }
}
