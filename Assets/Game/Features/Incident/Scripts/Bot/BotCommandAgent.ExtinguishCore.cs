using UnityEngine;
using UnityEngine.AI;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private bool TryResolveSuppressionTool(
        Vector3 orderPoint,
        Vector3 firePosition,
        IFireGroupTarget fireGroup,
        IFireTarget fireTarget,
        BotExtinguishCommandMode orderMode,
        bool requireNonPreciseTool,
        out IBotExtinguisherItem plannedTool)
    {
        plannedTool = ResolveCommittedExtinguishTool(orderPoint, firePosition, fireGroup, fireTarget, orderMode);
        return plannedTool != null && (!requireNonPreciseTool || !BotCommandAgent.UsesPreciseAim(plannedTool));
    }

    private bool TryAdvanceSuppressionToolAcquisition(IBotExtinguisherItem plannedTool, bool resetCrouchState)
    {
        if (plannedTool == null)
        {
            return false;
        }

        if (TryEnsureExtinguisherEquipped(plannedTool, false, false))
        {
            return true;
        }

        ClearHeadAimFocus();
        ClearHandAimFocus();
        if (resetCrouchState)
        {
            ResetExtinguishCrouchState();
        }

        return false;
    }

    private bool TryPrepareSuppressionToolMovePickup(IBotExtinguisherItem desiredTool)
    {
        if (desiredTool is not IPickupable pickupable || pickupable.Rigidbody == null)
        {
            return false;
        }

        if (!TryStartPlanMovePickupTarget(pickupable, false))
        {
            return false;
        }

        SetExtinguishSubtask(BotExtinguishSubtask.MoveToTool, $"Moving to tool '{GetToolName(desiredTool)}'.");
        UpdateExtinguishDebugStage(
            ExtinguishDebugStage.MovingToExtinguisher,
            $"Moving to tool '{GetToolName(desiredTool)}' at {pickupable.Rigidbody.transform.position}.");
        return true;
    }

    private bool TryEnsureExtinguisherEquipped(IBotExtinguisherItem desiredTool, bool allowMoveToToolRoute = true, bool reportStatus = true)
    {
        BotToolAcquisitionOptions<IBotExtinguisherItem> options = new BotToolAcquisitionOptions<IBotExtinguisherItem>
        {
            BotTransform = transform,
            InventorySystem = inventorySystem,
            PickupDistance = pickupDistance,
            IsAvailableToBot = tool => tool.IsAvailableTo(gameObject),
            IsHeldByBot = tool => tool != null && tool.CurrentHolder == gameObject,
            SetActiveTool = tool => activeExtinguisher = tool,
            OnUnavailable = () => ReleaseCommittedToolIfMatches(desiredTool),
            OnBeforeAcquire = StopExtinguisher,
            ReportSearching = reportStatus ? toolName =>
            {
                SetExtinguishSubtask(BotExtinguishSubtask.AcquireTool, $"Acquiring tool '{toolName}'.");
                UpdateExtinguishDebugStage(ExtinguishDebugStage.SearchingExtinguisher, $"Acquiring tool '{toolName}'.");
            } : null,
            ReportPickingUp = reportStatus ? toolName =>
            {
                SetExtinguishSubtask(BotExtinguishSubtask.AcquireTool, $"Picking up extinguisher '{toolName}'.");
                UpdateExtinguishDebugStage(ExtinguishDebugStage.PickingUpExtinguisher, $"Picking up extinguisher '{toolName}'.");
            } : null,
            ReportMovingToTool = reportStatus ? (toolName, toolPosition) =>
            {
                SetExtinguishSubtask(BotExtinguishSubtask.MoveToTool, $"Moving to tool '{toolName}'.");
                UpdateExtinguishDebugStage(ExtinguishDebugStage.MovingToExtinguisher, $"Moving to tool '{toolName}' at {toolPosition}.");
            } : null,
            OnBeforePickup = TryDropActiveBulkySuppressionToolForReplacement,
            SetPickupWindow = SetPickupWindow,
            MoveToTool = toolPosition => MoveTo(toolPosition),
            AllowMoveToToolRoute = allowMoveToToolRoute
        };

        return BotToolAcquisitionUtility.TryEnsureToolEquipped(desiredTool, options);
    }

    private IBotExtinguisherItem ResolveHeldSuppressionTool()
    {
        if (activeExtinguisher != null && activeExtinguisher.CurrentHolder == gameObject)
        {
            return activeExtinguisher;
        }

        if (committedExtinguishTool != null && committedExtinguishTool.CurrentHolder == gameObject)
        {
            return committedExtinguishTool;
        }

        if (inventorySystem != null &&
            inventorySystem.ActiveItem is IBotExtinguisherItem equippedInventoryTool &&
            equippedInventoryTool.CurrentHolder == gameObject)
        {
            return equippedInventoryTool;
        }

        return null;
    }

    private bool TryRestoreHeldSuppressionTool(BotExtinguishCommandMode orderMode, IFireTarget fireTarget, out IBotExtinguisherItem heldTool)
    {
        heldTool = ResolveHeldSuppressionTool();
        if (heldTool == null ||
            !heldTool.HasUsableCharge ||
            !heldTool.IsAvailableTo(gameObject) ||
            !DoesToolMatchExtinguishMode(heldTool, orderMode) ||
            IsUnsafeSuppressionToolForFire(heldTool, fireTarget))
        {
            return false;
        }

        activeExtinguisher = heldTool;
        committedExtinguishTool = heldTool;
        return true;
    }

    private IBotExtinguisherItem SelectPreferredExtinguishTool(Vector3 orderPoint, Vector3 firePosition, IFireGroupTarget fireGroup, IFireTarget fireTarget, BotExtinguishCommandMode orderMode)
    {
        IBotExtinguisherItem bestTool = null;
        float bestScore = float.PositiveInfinity;
        if (inventorySystem != null)
        {
            System.Collections.Generic.List<IBotExtinguisherItem> inventoryTools = new System.Collections.Generic.List<IBotExtinguisherItem>();
            inventorySystem.CollectItems(inventoryTools);
            for (int i = 0; i < inventoryTools.Count; i++)
            {
                IBotExtinguisherItem candidate = inventoryTools[i];
                if (candidate == null || IsExtinguishToolTemporarilyRejected(candidate) || !candidate.HasUsableCharge || !candidate.IsAvailableTo(gameObject))
                {
                    continue;
                }

                if (IsUnsafeSuppressionToolForFire(candidate, fireTarget))
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
        }

        float searchRadiusSq = toolSearchRadius * toolSearchRadius;

        foreach (IBotExtinguisherItem extinguisher in BotRuntimeRegistry.ActiveExtinguisherItems)
        {
            EvaluateWorldToolCandidate(extinguisher, orderPoint, firePosition, fireGroup, fireTarget, orderMode, searchRadiusSq, ref bestTool, ref bestScore);
        }

        if (bestTool == null &&
            perceptionMemory != null &&
            perceptionMemory.TryGetNearestRecentExtinguisher(transform.position, toolSearchRadius, gameObject, out IBotExtinguisherItem rememberedTool))
        {
            EvaluateWorldToolCandidate(rememberedTool, orderPoint, firePosition, fireGroup, fireTarget, orderMode, searchRadiusSq, ref bestTool, ref bestScore);
        }

        if (bestTool == null &&
            BotRuntimeRegistry.SharedIncidentBlackboard.TryGetNearestRecentExtinguisher(transform.position, toolSearchRadius, gameObject, out IBotExtinguisherItem sharedTool))
        {
            EvaluateWorldToolCandidate(sharedTool, orderPoint, firePosition, fireGroup, fireTarget, orderMode, searchRadiusSq, ref bestTool, ref bestScore);
        }

        if (bestTool != null)
        {
            perceptionMemory?.RememberExtinguisher(bestTool);
            BotRuntimeRegistry.SharedIncidentBlackboard.RememberExtinguisher(bestTool);
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
        if (candidateComponent == null ||
            IsExtinguishToolTemporarilyRejected(candidate) ||
            candidate.IsHeld ||
            candidate.Rigidbody == null ||
            !candidate.HasUsableCharge ||
            !candidate.IsAvailableTo(gameObject))
        {
            return;
        }

        if (IsUnsafeSuppressionToolForFire(candidate, fireTarget))
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
            activeExtinguisher.CurrentHolder == gameObject &&
            activeExtinguisher.HasUsableCharge &&
            !IsUnsafeSuppressionToolForFire(activeExtinguisher, fireTarget) &&
            DoesToolMatchExtinguishMode(activeExtinguisher, orderMode))
        {
            committedExtinguishTool = activeExtinguisher;
            return activeExtinguisher;
        }

        IBotExtinguisherItem heldTool = ResolveHeldSuppressionTool();
        if (heldTool != null &&
            heldTool.HasUsableCharge &&
            heldTool.IsAvailableTo(gameObject) &&
            DoesToolMatchExtinguishMode(heldTool, orderMode) &&
            !IsUnsafeSuppressionToolForFire(heldTool, fireTarget))
        {
            committedExtinguishTool = heldTool;
            activeExtinguisher = heldTool;
            return heldTool;
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
        IFireGroupTarget previousFireGroupTarget = commandedFireGroupTarget;
        commandedPointFireTarget = mode == BotExtinguishCommandMode.PointFire && pointFireTarget != null && pointFireTarget.IsBurning
            ? pointFireTarget
            : null;
        commandedFireGroupTarget = mode == BotExtinguishCommandMode.FireGroup && fireGroupTarget != null && fireGroupTarget.HasActiveFires
            ? fireGroupTarget
            : null;

        if (!ReferenceEquals(previousFireGroupTarget, commandedFireGroupTarget))
        {
            ReleaseReservation(previousFireGroupTarget);
            if (commandedFireGroupTarget != null)
            {
                RefreshReservation(commandedFireGroupTarget);
            }
        }
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

            SetCurrentFireTarget(localTarget);
            return currentFireTarget;
        }

        if (commandedPointFireTarget != null && commandedPointFireTarget.IsBurning)
        {
            SetCurrentFireTarget(commandedPointFireTarget);
            return currentFireTarget;
        }

        SetCurrentFireTarget(null);
        commandedPointFireTarget = null;
        return currentFireTarget;
    }

    private IFireGroupTarget ResolveIssuedFireGroupTarget(Vector3 orderPoint)
    {
        if (commandedFireGroupTarget != null && commandedFireGroupTarget.HasActiveFires)
        {
            return commandedFireGroupTarget;
        }

        IFireGroupTarget previousFireGroupTarget = commandedFireGroupTarget;
        commandedFireGroupTarget = FindClosestActiveFireGroup(orderPoint);
        if (!ReferenceEquals(previousFireGroupTarget, commandedFireGroupTarget))
        {
            ReleaseReservation(previousFireGroupTarget);
            if (commandedFireGroupTarget != null)
            {
                RefreshReservation(commandedFireGroupTarget);
            }
        }

        return commandedFireGroupTarget;
    }

    private IFireTarget ResolveRepresentativeFireTarget(IFireGroupTarget fireGroup, Vector3 fromPosition)
    {
        if (fireGroup == null || !fireGroup.HasActiveFires)
        {
            return null;
        }

        Vector3 representativePosition = fireGroup.GetClosestActiveFirePosition(fromPosition);
        IFireTarget representativeTarget = null;
        float bestDistanceSq = float.PositiveInfinity;

        foreach (IFireTarget candidate in BotRuntimeRegistry.ActiveFireTargets)
        {
            if (candidate == null || !candidate.IsBurning)
            {
                continue;
            }

            float distanceSq = (candidate.GetWorldPosition() - representativePosition).sqrMagnitude;
            if (distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            representativeTarget = candidate;
        }

        return representativeTarget;
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

        if (IsUnsafeSuppressionToolForFire(tool, fireTarget))
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

    private bool IsExtinguishToolTemporarilyRejected(IBotExtinguisherItem tool)
    {
        if (tool == null || temporarilyRejectedExtinguishTool == null)
        {
            return false;
        }

        if (Time.time >= temporarilyRejectedExtinguishToolUntilTime)
        {
            temporarilyRejectedExtinguishTool = null;
            temporarilyRejectedExtinguishToolUntilTime = 0f;
            return false;
        }

        return ReferenceEquals(tool, temporarilyRejectedExtinguishTool);
    }

    private void TemporarilyRejectExtinguishTool(IBotExtinguisherItem tool)
    {
        if (tool == null)
        {
            return;
        }

        if (ReferenceEquals(activeExtinguisher, tool))
        {
            StopExtinguisher();
            activeExtinguisher = null;
        }

        if (ReferenceEquals(preferredExtinguishTool, tool))
        {
            preferredExtinguishTool = null;
        }

        if (ReferenceEquals(committedExtinguishTool, tool))
        {
            ReleaseCommittedTool();
        }

        temporarilyRejectedExtinguishTool = tool;
        temporarilyRejectedExtinguishToolUntilTime = Time.time + Mathf.Max(0.1f, blockedExtinguishToolRetryDelay);
    }

    private bool TryDropActiveBulkySuppressionToolForReplacement(IBotExtinguisherItem desiredTool)
    {
        IBotExtinguisherItem heldTool = ResolveHeldSuppressionTool();
        if (desiredTool == null ||
            heldTool == null ||
            ReferenceEquals(heldTool, desiredTool))
        {
            return true;
        }

        return TryDropBulkySuppressionTool(heldTool, "Switching suppression tool.", true);
    }

    private bool TryDropBulkySuppressionTool(IBotExtinguisherItem tool, string reason, bool forceDrop = false)
    {
        if (!(tool is IBulkyEquipment) ||
            tool is not IPickupable pickupable ||
            pickupable.Rigidbody == null ||
            inventorySystem == null)
        {
            return false;
        }

        if (!forceDrop && tool.HasUsableCharge)
        {
            return false;
        }

        bool wasCommittedTool = ReferenceEquals(committedExtinguishTool, tool);
        Quaternion dropRotation;
        Vector3 dropPosition = ResolveBulkyToolDropPosition(pickupable.Rigidbody.transform, out dropRotation);

        if (ReferenceEquals(activeExtinguisher, tool))
        {
            StopExtinguisher();
        }

        bool dropped = inventorySystem.DropItem(pickupable, dropPosition, dropRotation);
        if (!dropped)
        {
            return false;
        }

        if (wasCommittedTool)
        {
            ReleaseCommittedTool();
        }

        if (ReferenceEquals(activeExtinguisher, tool))
        {
            activeExtinguisher = null;
        }

        if (ReferenceEquals(preferredExtinguishTool, tool))
        {
            preferredExtinguishTool = null;
        }

        SetPickupWindow(false, null);
        LogVerboseExtinguish(
            VerboseExtinguishLogCategory.Tooling,
            $"drop-bulky:{GetToolName(tool)}",
            $"Dropped bulky suppression tool '{GetToolName(tool)}'. {reason}");
        return true;
    }

    private void TryDropTrackedBulkySuppressionTools(string reason, bool forceDrop = true)
    {
        IBotExtinguisherItem trackedActiveTool = activeExtinguisher;
        IBotExtinguisherItem trackedCommittedTool = committedExtinguishTool;

        TryDropBulkySuppressionTool(trackedActiveTool, reason, forceDrop);
        if (!ReferenceEquals(trackedCommittedTool, trackedActiveTool))
        {
            TryDropBulkySuppressionTool(trackedCommittedTool, reason, forceDrop);
        }
    }

    private Vector3 ResolveBulkyToolDropPosition(Transform itemTransform, out Quaternion dropRotation)
    {
        Vector3 offset = transform.TransformDirection(bulkyToolDropOffset);
        Vector3 desiredPosition = transform.position + offset;
        Vector3 rayOrigin = desiredPosition + Vector3.up * 1f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, Mathf.Max(0.5f, bulkyToolDropGroundProbeDistance), ~0, QueryTriggerInteraction.Ignore))
        {
            desiredPosition = hit.point;
        }
        else
        {
            desiredPosition.y = transform.position.y;
        }

        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (flatForward.sqrMagnitude <= 0.0001f)
        {
            flatForward = Vector3.forward;
        }

        dropRotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
        if (itemTransform != null)
        {
            desiredPosition.y += GetDropGroundOffset(itemTransform);
        }

        return desiredPosition;
    }

    private static float GetDropGroundOffset(Transform itemTransform)
    {
        if (itemTransform == null)
        {
            return 0.05f;
        }

        float maxExtent = 0.05f;
        Collider[] colliders = itemTransform.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            maxExtent = Mathf.Max(maxExtent, collider.bounds.extents.y);
        }

        return maxExtent + 0.02f;
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
