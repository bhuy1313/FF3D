using UnityEngine;
using UnityEngine.AI;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private bool TryHandleBlockedPath(Vector3 destination)
    {
        if (!enablePathClearing || navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            ClearBlockedPathRuntime();
            return false;
        }

        if (HasPendingCommittedBreakTool())
        {
            if (interactionSensor != null &&
                interactionSensor.TryFindBreakableAhead(out IBotBreakableTarget routeBlockedBreakable) &&
                routeBlockedBreakable != null &&
                routeBlockedBreakable.CanBeClearedByBot &&
                !routeBlockedBreakable.IsBroken)
            {
                LogPathClearingFlow(
                    $"sensor-blocker-reroute:{GetDebugTargetName(routeBlockedBreakable)}:{GetBreakToolName(committedBreakTool)}",
                    $"Blocker detected: '{GetDebugTargetName(routeBlockedBreakable)}'.");
                LogPathClearingFlow(
                    $"retry-breaktool-route:{GetBreakToolName(committedBreakTool)}:{GetDebugTargetName(routeBlockedBreakable)}",
                    "Searching for another breaching tool.");
                LogPathClearingFlow(
                    $"discard-breaktool-route:{GetBreakToolName(committedBreakTool)}:{GetDebugTargetName(routeBlockedBreakable)}",
                    $"Discarding {GetBreakToolName(committedBreakTool)}.");
                RejectBreakToolForCurrentBlocker(committedBreakTool, routeBlockedBreakable);
                StopBreakToolRoute();
                ReleaseCommittedBreakTool();
                UpdatePathClearingDebugStage(PathClearingDebugStage.SearchingBreakTool, $"Break tool route is blocked by '{GetDebugTargetName(routeBlockedBreakable)}'. Re-evaluating tool.");
                return true;
            }

            if (!IsBreakToolStillUsable(committedBreakTool))
            {
                StopBreakToolRoute();
                LogPathClearingFlow(
                    $"no-break-tool:committed:{GetBreakToolName(committedBreakTool)}",
                    "No usable tool available.");
                LogPathClearingFlow(
                    $"stop-breaktool:committed:{GetBreakToolName(committedBreakTool)}",
                    "Stopped.");
                UpdatePathClearingDebugStage(PathClearingDebugStage.NoBreakTool, $"Committed break tool '{GetBreakToolName(committedBreakTool)}' is no longer usable.");
                ReleaseCommittedBreakTool();
                RefreshPathClearingResumeGrace();
                return true;
            }

            UpdatePathClearingDebugStage(PathClearingDebugStage.SearchingBreakTool, $"Continuing acquisition of break tool '{GetBreakToolName(committedBreakTool)}'.");
            RefreshPathClearingResumeGrace();
            return !TryEnsureBreakToolEquipped(committedBreakTool) || activeBreakTool != null;
        }

        if (activeBreakTool != null &&
            currentBlockedBreakable != null &&
            !currentBlockedBreakable.IsBroken &&
            currentBlockedBreakable.CanBeClearedByBot)
        {
            IBotBreakableTarget lockedBlockedTarget = currentBlockedBreakable;
            UpdatePathClearingDebugStage(PathClearingDebugStage.BlockedByBreakable, $"Continuing to clear blocking breakable '{GetDebugTargetName(lockedBlockedTarget)}'.");
            RefreshPathClearingResumeGrace();
            return HandleEquippedBreakToolAgainstTarget(activeBreakTool, lockedBlockedTarget);
        }

        UpdatePathClearingDebugStage(PathClearingDebugStage.SearchingBlocker, $"Checking route to {destination} for blocking breakables.");
        if (!TryResolveBlockedBreakable(destination, out IBotBreakableTarget blockedTarget))
        {
            UpdatePathClearingDebugStage(PathClearingDebugStage.Cleared, $"No blocking breakable detected toward {destination}.");
            ClearBlockedPathRuntime();
            return false;
        }

        SetCurrentBlockedBreakable(blockedTarget);
        UpdatePathClearingDebugStage(PathClearingDebugStage.BlockedByBreakable, $"Detected blocking breakable '{GetDebugTargetName(blockedTarget)}' at {blockedTarget.GetWorldPosition()}.");
        RefreshPathClearingResumeGrace();
        IBotBreakTool breakTool = ResolveCommittedBreakTool();
        if (breakTool == null)
        {
            StopBreakToolRoute();
            LogPathClearingFlow(
                $"no-break-tool:blocker:{GetDebugTargetName(blockedTarget)}",
                "No usable tool available.");
            LogPathClearingFlow(
                $"stop-breaktool:blocker:{GetDebugTargetName(blockedTarget)}",
                "Stopped.");
            UpdatePathClearingDebugStage(PathClearingDebugStage.NoBreakTool, $"No usable break tool found for '{GetDebugTargetName(blockedTarget)}'.");
            RefreshPathClearingResumeGrace();
            return true;
        }

        if (!TryEnsureBreakToolEquipped(breakTool))
        {
            return true;
        }

        IBotBreakTool equippedBreakTool = activeBreakTool ?? breakTool;
        if (equippedBreakTool == null)
        {
            return false;
        }

        return HandleEquippedBreakToolAgainstTarget(equippedBreakTool, blockedTarget);
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
                UpdatePathClearingDebugStage(PathClearingDebugStage.Breaking, $"Breaking '{GetDebugTargetName(blockedTarget)}' with '{GetBreakToolName(equippedBreakTool)}'.");
                LogPathClearingFlow(
                    $"break-breakable:{GetDebugTargetName(blockedTarget)}",
                    "Breaking Breakable.");
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
            ReportSearching = toolName => UpdatePathClearingDebugStage(PathClearingDebugStage.SearchingBreakTool, $"Acquiring break tool '{toolName}'."),
            ReportPickingUp = toolName =>
            {
                LogPathClearingFlow($"pickup-breaktool:{toolName}", $"Picked up {toolName}.");
                UpdatePathClearingDebugStage(PathClearingDebugStage.PickingUpBreakTool, $"Picking up break tool '{toolName}'.");
            },
            ReportMovingToTool = (toolName, toolPosition) => UpdatePathClearingDebugStage(PathClearingDebugStage.MovingToBreakTool, $"Moving to break tool '{toolName}' at {toolPosition}."),
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
                TrySetDestinationDirect(toolPosition);
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
        temporarilyRejectedBreakTool = null;
        temporarilyRejectedBreakable = null;
        nextBreakUseTime = 0f;
        nextPathClearingRefreshTime = 0f;
        RefreshPathClearingResumeGrace();
        temporarilyRejectedBreakToolUntilTime = 0f;
        activityDebug?.ResetPathClearing();
        ReleaseCommittedBreakTool();
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
