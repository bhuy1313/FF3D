using UnityEngine;
using UnityEngine.AI;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private void ProcessExtinguishOrder()
    {
        if (inventorySystem == null ||
            !navMeshAgent.enabled ||
            !navMeshAgent.isOnNavMesh ||
            !behaviorContext.TryGetExtinguishOrder(out Vector3 orderPoint, out Vector3 scanOrigin, out BotExtinguishCommandMode orderMode))
        {
            return;
        }

        Vector3 targetSearchPoint = orderMode == BotExtinguishCommandMode.PointFire ? scanOrigin : orderPoint;
        IFireGroupTarget fireGroup = orderMode == BotExtinguishCommandMode.PointFire ? null : ResolveIssuedFireGroupTarget(targetSearchPoint);
        IFireTarget fireTarget = orderMode == BotExtinguishCommandMode.PointFire
            ? ResolveIssuedPointFireTarget(targetSearchPoint)
            : ResolveActiveFireTarget(targetSearchPoint);
        LogVerboseExtinguish(
            VerboseExtinguishLogCategory.Targeting,
            $"target:{GetDebugTargetName(fireTarget)}:{GetDebugTargetName(fireGroup)}",
            $"Order={targetSearchPoint}, fireTarget={GetDebugTargetName(fireTarget)}, fireGroup={GetDebugTargetName(fireGroup)}, mode={orderMode}.");
        if ((fireGroup == null || !fireGroup.HasActiveFires) && (fireTarget == null || !fireTarget.IsBurning))
        {
            UpdateExtinguishDebugStage(ExtinguishDebugStage.NoFireGroupFound, $"No active FireGroup found near {orderPoint}. Clearing order.");
            ClearExtinguishRuntimeState();
            behaviorContext.ClearExtinguishOrder();
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
            return;
        }

        UpdateExtinguishDebugStage(ExtinguishDebugStage.SearchingFireGroup, $"Resolved FireGroup near {orderPoint}.");

        Vector3 botPosition = transform.position;
        Vector3 firePosition = fireTarget != null && fireTarget.IsBurning
            ? fireTarget.GetWorldPosition()
            : fireGroup.GetClosestActiveFirePosition(botPosition);
        preferredExtinguishTool = ResolveCommittedExtinguishTool(orderPoint, firePosition, fireGroup, fireTarget, orderMode);
        LogVerboseExtinguish(
            VerboseExtinguishLogCategory.Tooling,
            $"tool:{GetToolName(preferredExtinguishTool)}:{firePosition}",
            $"Selected tool={GetToolName(preferredExtinguishTool)} for fire={firePosition}.");
        if (preferredExtinguishTool == null)
        {
            if (orderMode == BotExtinguishCommandMode.FireGroup &&
                TryFallbackFireGroupOrderToPointFire(fireTarget, out Vector3 fallbackDestination))
            {
                LogVerboseExtinguish(
                    VerboseExtinguishLogCategory.Tooling,
                    $"fallback-pointfire:{GetDebugTargetName(fireTarget)}:{fallbackDestination}",
                    $"No suitable FireGroup tool found. Falling back to PointFire at {fallbackDestination}.");
                return;
            }

            UpdateExtinguishDebugStage(ExtinguishDebugStage.NoReachableTool, $"No available suppression tool can reach fire near {firePosition}.");
            ClearExtinguishRuntimeState();
            behaviorContext.ClearExtinguishOrder();
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
            return;
        }

        if (!TryEnsureExtinguisherEquipped(preferredExtinguishTool))
        {
            ClearHeadAimFocus();
            ClearHandAimFocus();
            ResetExtinguishCrouchState();
            return;
        }

        if (extinguishStartupPending)
        {
            ClearHeadAimFocus();
            ClearHandAimFocus();
            extinguishStartupPending = false;
            return;
        }

        LogVerboseExtinguish(
            VerboseExtinguishLogCategory.Tooling,
            $"equipped:{GetToolName(activeExtinguisher)}",
            $"Equipped tool={GetToolName(activeExtinguisher)}.");

        if (UsesPreciseAim(activeExtinguisher) && fireGroup != null)
        {
            firePosition = fireGroup.GetWorldCenter();
        }

        if (!UsesPreciseAim(activeExtinguisher))
        {
            fireTarget = orderMode == BotExtinguishCommandMode.PointFire
                ? ResolveIssuedPointFireTarget(targetSearchPoint)
                : ResolveExtinguisherRouteTarget(targetSearchPoint);
            if (fireTarget != null && fireTarget.IsBurning)
            {
                firePosition = fireTarget.GetWorldPosition();
                LogVerboseExtinguish(
                    VerboseExtinguishLogCategory.Targeting,
                    $"routetarget:{GetDebugTargetName(fireTarget)}",
                    $"Using route fire target={GetDebugTargetName(fireTarget)} at {firePosition}.");
            }

            if (orderMode == BotExtinguishCommandMode.FireGroup &&
                fireTarget != null &&
                fireTarget.IsBurning &&
                TryFallbackFireGroupOrderToPointFire(fireTarget, out Vector3 extinguisherFallbackDestination))
            {
                LogVerboseExtinguish(
                    VerboseExtinguishLogCategory.Tooling,
                    $"fallback-extinguisher-pointfire:{GetDebugTargetName(fireTarget)}:{extinguisherFallbackDestination}",
                    $"Fire Extinguisher selected for FireGroup. Falling back to PointFire at {extinguisherFallbackDestination}.");
                return;
            }
        }

        if (!activeExtinguisher.HasUsableCharge)
        {
            ResetExtinguishCrouchState();
            UpdateExtinguishDebugStage(ExtinguishDebugStage.OutOfCharge, "Extinguisher is out of charge.");
            ClearExtinguishRuntimeState();
            behaviorContext.ClearExtinguishOrder();
            return;
        }

        currentExtinguishTargetPosition = firePosition;
        hasCurrentExtinguishTargetPosition = true;
        UpdateCurrentExtinguishAimData(activeExtinguisher, firePosition);
        if (!UsesPreciseAim(activeExtinguisher))
        {
            PrimeExtinguisherTargetLock(activeExtinguisher, fireTarget);
        }

        float horizontalDistanceToFire = GetHorizontalDistance(botPosition, firePosition);
        bool shouldReposition;
        Vector3 desiredPosition = transform.position;
        float desiredHorizontalDistance;

        if (UsesPreciseAim(activeExtinguisher))
        {
            float requiredHorizontalDistance = GetRequiredHorizontalDistanceForAim(activeExtinguisher, firePosition);
            desiredHorizontalDistance = Mathf.Max(activeExtinguisher.PreferredSprayDistance, requiredHorizontalDistance);
            shouldReposition =
                horizontalDistanceToFire > activeExtinguisher.MaxSprayDistance ||
                horizontalDistanceToFire < desiredHorizontalDistance - 0.35f;
        }
        else
        {
            float edgeDistanceToFire = GetFireEdgeDistance(botPosition, firePosition, fireTarget);
            float distanceToFire = GetDistanceToFireEdge(botPosition, firePosition, fireTarget);
            float desiredStandOffDistance = GetDesiredExtinguisherStandOffDistanceLocked(activeExtinguisher, fireTarget);
            float allowedEdgeRange = GetAllowedExtinguisherEdgeRange(activeExtinguisher);
            desiredHorizontalDistance = GetDesiredExtinguisherCenterDistance(activeExtinguisher, fireTarget);
            bool keepCurrentStandDistance = IsExtinguisherTargetLocked(fireTarget);
            bool canExtinguishFromCurrentPosition = CanExtinguishFromCurrentPosition(activeExtinguisher, firePosition, fireTarget);
            bool isFartherThanPreferred = edgeDistanceToFire > desiredStandOffDistance + ExtinguisherRangeSlack;
            bool isCloserThanPreferred = edgeDistanceToFire + ExtinguisherRangeSlack < desiredStandOffDistance;
            shouldReposition =
                distanceToFire > allowedEdgeRange ||
                (!keepCurrentStandDistance && (isFartherThanPreferred || isCloserThanPreferred)) ||
                !canExtinguishFromCurrentPosition;
        }

        if (shouldReposition)
        {
            if (orderMode == BotExtinguishCommandMode.PointFire &&
                !CanExtinguishFromCurrentPosition(activeExtinguisher, firePosition, fireTarget) &&
                !IsNearOrderPoint(orderPoint))
            {
                UpdateExtinguishDebugStage(ExtinguishDebugStage.MovingToFire, $"Moving to point-fire approach position {orderPoint}.");
                ClearHeadAimFocus();
                ClearHandAimFocus();
                ResetExtinguishCrouchState();
                StopExtinguisher();
                sprayReadyTime = -1f;
                MoveTo(orderPoint);
                return;
            }

            if (!UsesPreciseAim(activeExtinguisher) && orderMode == BotExtinguishCommandMode.PointFire)
            {
                desiredPosition = orderPoint;
            }
            else
            {
                desiredPosition = UsesPreciseAim(activeExtinguisher)
                    ? ResolveExtinguishPosition(targetSearchPoint, firePosition, desiredHorizontalDistance)
                    : ResolveExtinguisherApproachPosition(orderPoint, firePosition, desiredHorizontalDistance);
            }
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Movement,
                $"movefire:{desiredPosition}",
                $"Repositioning. horizontal={horizontalDistanceToFire:F2}, desired={desiredHorizontalDistance:F2}, max={activeExtinguisher.MaxSprayDistance:F2}, preciseAim={UsesPreciseAim(activeExtinguisher)}, target={firePosition}, destination={desiredPosition}.");
            UpdateExtinguishDebugStage(ExtinguishDebugStage.MovingToFire, $"Moving to extinguish position {desiredPosition} for fire at horizontal distance {horizontalDistanceToFire:F2}m.");
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

        AimTowards(aimPoint);
        SetHandAimFocus(aimPoint);
        SetHeadAimFocus(firePosition);

        if (ShouldUseFireHoseCrouch(activeExtinguisher))
        {
            behaviorContext.SetCrouchAnimation(true);

            if (crouchReadyTime < 0f)
            {
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

        if (UsesPreciseAim(activeExtinguisher) && !IsAimSettled(activeExtinguisher, firePosition))
        {
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Timing,
                $"aimwait:{GetToolName(activeExtinguisher)}:{firePosition}",
                $"Waiting for precise aim settle. fire={firePosition}.");
            StopExtinguisher();
            sprayReadyTime = -1f;
            return;
        }

        if (sprayReadyTime < 0f)
        {
            sprayReadyTime = Time.time + Mathf.Max(0f, sprayStartDelay);
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Timing,
                $"delay:{GetToolName(activeExtinguisher)}:{firePosition}",
                $"Starting spray delay until {sprayReadyTime:F2} for fire={firePosition}.");
            StopExtinguisher();
            return;
        }

        if (Time.time < sprayReadyTime)
        {
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Timing,
                $"delaywait:{GetToolName(activeExtinguisher)}:{firePosition}",
                $"Waiting for spray delay. now={Time.time:F2}, ready={sprayReadyTime:F2}.");
            StopExtinguisher();
            return;
        }

        LogVerboseExtinguish(
            VerboseExtinguishLogCategory.Distance,
            $"dist:{GetDebugTargetName(fireTarget)}:{horizontalDistanceToFire:F2}:{desiredHorizontalDistance:F2}",
            $"Distance to target. horizontal={horizontalDistanceToFire:F2}, desired={desiredHorizontalDistance:F2}, max={activeExtinguisher.MaxSprayDistance:F2}, vertical={Mathf.Abs(firePosition.y - botPosition.y):F2}.");

        UpdateExtinguishDebugStage(ExtinguishDebugStage.Spraying, $"Spraying fire at {firePosition}.");
        if (!UsesPreciseAim(activeExtinguisher))
        {
            LockExtinguisherTarget(fireTarget);
        }

        activeExtinguisher.SetExternalSprayState(true, gameObject);
        if (UsesPreciseAim(activeExtinguisher))
        {
            TryApplyWaterToFireGroup(activeExtinguisher, fireGroup, firePosition);

            if (fireGroup == null || !fireGroup.HasActiveFires)
            {
                CompleteExtinguishOrder("FireGroup extinguished.");
            }
        }
        else
        {
            TryApplyWaterToFireTarget(activeExtinguisher, fireTarget, firePosition);
            LogVerboseExtinguish(
                VerboseExtinguishLogCategory.Spray,
                $"sprayfire:{GetDebugTargetName(fireTarget)}",
                $"Applying extinguisher to fireTarget={GetDebugTargetName(fireTarget)} at {firePosition}.");

            if (fireTarget == null || !fireTarget.IsBurning)
            {
                StopExtinguisher();
                sprayReadyTime = -1f;
                currentFireTarget = ResolveExtinguisherRouteTarget(orderPoint);
                LogVerboseExtinguish(
                    VerboseExtinguishLogCategory.Targeting,
                    $"nextfire:{GetDebugTargetName(currentFireTarget)}",
                    $"Current fire extinguished. Next fire={GetDebugTargetName(currentFireTarget)}.");
                if (currentFireTarget == null || !currentFireTarget.IsBurning)
                {
                    CompleteExtinguishOrder("All nearby fires extinguished.");
                }
            }
        }
    }

    private bool TryEnsureExtinguisherEquipped(IBotExtinguisherItem desiredTool)
    {
        BotToolAcquisitionOptions<IBotExtinguisherItem> options = new BotToolAcquisitionOptions<IBotExtinguisherItem>
        {
            BotTransform = transform,
            InventorySystem = inventorySystem,
            PickupDistance = pickupDistance,
            IsAvailableToBot = tool => tool.IsAvailableTo(gameObject),
            IsHeldByBot = tool => tool.IsHeld && tool.ClaimOwner == gameObject,
            SetActiveTool = tool => activeExtinguisher = tool,
            OnUnavailable = () => ReleaseCommittedToolIfMatches(desiredTool),
            OnBeforeAcquire = StopExtinguisher,
            ReportSearching = toolName => UpdateExtinguishDebugStage(ExtinguishDebugStage.SearchingExtinguisher, $"Acquiring tool '{toolName}'."),
            ReportPickingUp = toolName => UpdateExtinguishDebugStage(ExtinguishDebugStage.PickingUpExtinguisher, $"Picking up extinguisher '{toolName}'."),
            ReportMovingToTool = (toolName, toolPosition) => UpdateExtinguishDebugStage(ExtinguishDebugStage.MovingToExtinguisher, $"Moving to tool '{toolName}' at {toolPosition}."),
            SetPickupWindow = SetPickupWindow,
            MoveToTool = toolPosition => TrySetDestinationDirect(toolPosition)
        };

        return BotToolAcquisitionUtility.TryEnsureToolEquipped(desiredTool, options);
    }

    private IBotExtinguisherItem SelectPreferredExtinguishTool(Vector3 orderPoint, Vector3 firePosition, IFireGroupTarget fireGroup, IFireTarget fireTarget, BotExtinguishCommandMode orderMode)
    {
        IBotExtinguisherItem bestTool = null;
        float bestScore = float.PositiveInfinity;
        System.Collections.Generic.List<IBotExtinguisherItem> inventoryTools = new System.Collections.Generic.List<IBotExtinguisherItem>();
        inventorySystem.CollectItems(inventoryTools);
        for (int i = 0; i < inventoryTools.Count; i++)
        {
            IBotExtinguisherItem candidate = inventoryTools[i];
            if (candidate == null || !candidate.HasUsableCharge || !candidate.IsAvailableTo(gameObject))
            {
                continue;
            }

            if (!DoesToolMatchExtinguishMode(candidate, orderMode) ||
                !CanToolReachFire(candidate, orderMode, orderPoint, firePosition, fireGroup, fireTarget))
            {
                continue;
            }

            float score = ScoreSuppressionTool(candidate, orderPoint, firePosition, transform.position, fireTarget);
            if (score < bestScore)
            {
                bestScore = score;
                bestTool = candidate;
            }
        }

        float searchRadiusSq = toolSearchRadius * toolSearchRadius;

        foreach (IBotExtinguisherItem extinguisher in BotRuntimeRegistry.ActiveExtinguisherItems)
        {
            EvaluateWorldToolCandidate(extinguisher, orderPoint, firePosition, fireGroup, fireTarget, orderMode, searchRadiusSq, ref bestTool, ref bestScore);
        }

        return bestTool;
    }

    private void EvaluateWorldToolCandidate(
        IBotExtinguisherItem candidate,
        Vector3 orderPoint,
        Vector3 firePosition,
        IFireGroupTarget fireGroup,
        IFireTarget fireTarget,
        BotExtinguishCommandMode orderMode,
        float searchRadiusSq,
        ref IBotExtinguisherItem bestTool,
        ref float bestScore)
    {
        Component candidateComponent = candidate as Component;
        if (candidateComponent == null || candidate.IsHeld || candidate.Rigidbody == null || !candidate.HasUsableCharge || !candidate.IsAvailableTo(gameObject))
        {
            return;
        }

        if (!DoesToolMatchExtinguishMode(candidate, orderMode) ||
            !CanToolReachFire(candidate, orderMode, orderPoint, firePosition, fireGroup, fireTarget))
        {
            return;
        }

        float distanceSq = (candidateComponent.transform.position - transform.position).sqrMagnitude;
        if (distanceSq > searchRadiusSq)
        {
            return;
        }

        float score = ScoreSuppressionTool(candidate, orderPoint, firePosition, candidateComponent.transform.position, fireTarget) + Mathf.Sqrt(distanceSq);
        if (score < bestScore)
        {
            bestScore = score;
            bestTool = candidate;
        }
    }

    private IBotExtinguisherItem ResolveCommittedExtinguishTool(Vector3 orderPoint, Vector3 firePosition, IFireGroupTarget fireGroup, IFireTarget fireTarget, BotExtinguishCommandMode orderMode)
    {
        if (activeExtinguisher != null &&
            activeExtinguisher.IsHeld &&
            activeExtinguisher.ClaimOwner == gameObject &&
            activeExtinguisher.HasUsableCharge &&
            DoesToolMatchExtinguishMode(activeExtinguisher, orderMode))
        {
            committedExtinguishTool = activeExtinguisher;
            return activeExtinguisher;
        }

        if (IsToolStillUsable(committedExtinguishTool, orderMode, orderPoint, firePosition, fireGroup, fireTarget))
        {
            return committedExtinguishTool;
        }

        ReleaseCommittedTool();
        IBotExtinguisherItem selectedTool = SelectPreferredExtinguishTool(orderPoint, firePosition, fireGroup, fireTarget, orderMode);
        if (selectedTool == null)
        {
            return null;
        }

        if (!selectedTool.TryClaim(gameObject))
        {
            return null;
        }

        committedExtinguishTool = selectedTool;
        return committedExtinguishTool;
    }

    private bool TryFallbackFireGroupOrderToPointFire(IFireTarget fireTarget, out Vector3 fallbackDestination)
    {
        fallbackDestination = default;
        if (behaviorContext == null ||
            navMeshAgent == null ||
            !navMeshAgent.enabled ||
            !navMeshAgent.isOnNavMesh ||
            fireTarget == null ||
            !fireTarget.IsBurning)
        {
            return false;
        }

        Vector3 scanOrigin = fireTarget.GetWorldPosition();
        fallbackDestination = scanOrigin;
        if (TryResolvePointFireApproachPosition(scanOrigin, out Vector3 approachDestination))
        {
            fallbackDestination = approachDestination;
        }
        else if (navMeshSampleDistance > 0f &&
                 NavMesh.SamplePosition(scanOrigin, out NavMeshHit navMeshHit, navMeshSampleDistance, navMeshAgent.areaMask))
        {
            fallbackDestination = navMeshHit.position;
        }

        ClearExtinguishRuntimeState();
        CacheIssuedExtinguishTargets(BotExtinguishCommandMode.PointFire, fireTarget, null);
        behaviorContext.SetExtinguishOrder(fallbackDestination, scanOrigin, BotExtinguishCommandMode.PointFire);
        extinguishStartupPending = true;
        lastIssuedDestination = fallbackDestination;
        hasIssuedDestination = true;
        return true;
    }

    private void CacheIssuedExtinguishTargets(
        BotExtinguishCommandMode mode,
        IFireTarget pointFireTarget,
        IFireGroupTarget fireGroupTarget)
    {
        commandedPointFireTarget = mode == BotExtinguishCommandMode.PointFire && pointFireTarget != null && pointFireTarget.IsBurning
            ? pointFireTarget
            : null;
        commandedFireGroupTarget = mode == BotExtinguishCommandMode.FireGroup && fireGroupTarget != null && fireGroupTarget.HasActiveFires
            ? fireGroupTarget
            : null;
    }

    private IFireTarget ResolveIssuedPointFireTarget(Vector3 scanOrigin)
    {
        IFireTarget localTarget = ResolvePointFireTarget(scanOrigin);
        if (localTarget != null && localTarget.IsBurning)
        {
            if (commandedPointFireTarget != null && !commandedPointFireTarget.IsBurning)
            {
                commandedPointFireTarget = null;
            }

            currentFireTarget = localTarget;
            return currentFireTarget;
        }

        if (commandedPointFireTarget != null && commandedPointFireTarget.IsBurning)
        {
            currentFireTarget = commandedPointFireTarget;
            return currentFireTarget;
        }

        currentFireTarget = null;
        commandedPointFireTarget = null;
        return currentFireTarget;
    }

    private IFireGroupTarget ResolveIssuedFireGroupTarget(Vector3 orderPoint)
    {
        if (commandedFireGroupTarget != null && commandedFireGroupTarget.HasActiveFires)
        {
            return commandedFireGroupTarget;
        }

        commandedFireGroupTarget = FindClosestActiveFireGroup(orderPoint);
        return commandedFireGroupTarget;
    }

    private bool IsToolStillUsable(IBotExtinguisherItem tool, BotExtinguishCommandMode orderMode, Vector3 orderPoint, Vector3 firePosition, IFireGroupTarget fireGroup, IFireTarget fireTarget)
    {
        if (tool == null)
        {
            return false;
        }

        if (!tool.HasUsableCharge || !tool.IsAvailableTo(gameObject) || !DoesToolMatchExtinguishMode(tool, orderMode))
        {
            return false;
        }

        return CanToolReachFire(tool, orderMode, orderPoint, firePosition, fireGroup, fireTarget);
    }

    private void ReleaseCommittedToolIfMatches(IBotExtinguisherItem tool)
    {
        if (ReferenceEquals(committedExtinguishTool, tool))
        {
            ReleaseCommittedTool();
        }
    }

    private void ReleaseCommittedTool()
    {
        if (committedExtinguishTool != null)
        {
            committedExtinguishTool.ReleaseClaim(gameObject);
            committedExtinguishTool = null;
        }
    }

    private IFireGroupTarget FindClosestActiveFireGroup(Vector3 orderPoint)
    {
        IFireGroupTarget bestGroup = null;
        IFireGroupTarget nearestGroup = null;
        float bestDistanceSq = float.PositiveInfinity;
        float nearestDistanceSq = float.PositiveInfinity;
        float searchRadiusSq = fireSearchRadius * fireSearchRadius;

        foreach (IFireGroupTarget candidate in BotRuntimeRegistry.ActiveFireGroups)
        {
            if (candidate == null || !candidate.HasActiveFires)
            {
                continue;
            }

            float distanceSq = (candidate.GetWorldCenter() - orderPoint).sqrMagnitude;
            if (distanceSq < nearestDistanceSq)
            {
                nearestDistanceSq = distanceSq;
                nearestGroup = candidate;
            }

            if (distanceSq > searchRadiusSq || distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            bestGroup = candidate;
        }

        return bestGroup != null ? bestGroup : nearestGroup;
    }

    private Vector3 ResolveExtinguishPosition(Vector3 requestedPoint, Vector3 firePosition, float preferredDistance)
    {
        if (TryResolvePreciseStandPosition(requestedPoint, firePosition, preferredDistance, out Vector3 desiredPosition))
        {
            return desiredPosition;
        }

        return transform.position;
    }

    private Vector3 ResolveExtinguisherApproachPosition(Vector3 orderPoint, Vector3 firePosition, float preferredDistance)
    {
        if (TryResolveExtinguisherStandPosition(orderPoint, firePosition, preferredDistance, out Vector3 desiredPosition))
        {
            return desiredPosition;
        }

        if (TryResolvePointFireApproachPosition(orderPoint, out desiredPosition))
        {
            return desiredPosition;
        }

        if (TryResolveReachableReferencePosition(orderPoint, Mathf.Max(navMeshSampleDistance, pointFireApproachSampleStep, 2f), out desiredPosition))
        {
            return desiredPosition;
        }

        return transform.position;
    }

    private bool ShouldIssueExtinguisherApproachMove(Vector3 destination)
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return true;
        }

        if (navMeshAgent.isStopped || !navMeshAgent.hasPath || navMeshAgent.pathPending || navMeshAgent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            return true;
        }

        return GetHorizontalDistance(navMeshAgent.destination, destination) > Mathf.Max(0.1f, extinguisherApproachRetargetDistance);
    }
}
