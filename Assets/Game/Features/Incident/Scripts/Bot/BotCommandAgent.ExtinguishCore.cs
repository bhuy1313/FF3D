using UnityEngine;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private void ProcessExtinguishOrder()
    {
        if (inventorySystem == null ||
            navMeshAgent == null ||
            !navMeshAgent.enabled ||
            !navMeshAgent.isOnNavMesh ||
            behaviorContext == null ||
            !behaviorContext.TryGetExtinguishOrder(out Vector3 destination, out _, out _))
        {
            return;
        }

        SetExtinguishSubtask(BotExtinguishSubtask.AcquireTool, "Acquiring suppression tool.");
        preferredExtinguishTool = ResolveCommittedExtinguishTool(
            destination,
            destination,
            null,
            null,
            BotExtinguishCommandMode.PointFire);
        if (preferredExtinguishTool == null)
        {
            UpdateExtinguishDebugStage(ExtinguishDebugStage.NoReachableTool, $"No available Fire Extinguisher found for route to {destination}.");
            FailActiveExtinguishOrder("No available Fire Extinguisher found.", BotTaskStatus.Blocked);
            return;
        }

        if (!TryEnsureExtinguisherEquipped(preferredExtinguishTool))
        {
            ClearHeadAimFocus();
            ClearHandAimFocus();
            ResetExtinguishCrouchState();
            return;
        }

        currentExtinguishTargetPosition = destination;
        hasCurrentExtinguishTargetPosition = true;
        if (TryHandleProactiveExtinguishRoute(destination))
        {
            return;
        }

        CompleteExtinguishOrder("Reached extinguish destination.");
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
            ReportSearching = toolName =>
            {
                SetExtinguishSubtask(BotExtinguishSubtask.AcquireTool, $"Acquiring tool '{toolName}'.");
                UpdateExtinguishDebugStage(ExtinguishDebugStage.SearchingExtinguisher, $"Acquiring tool '{toolName}'.");
            },
            ReportPickingUp = toolName =>
            {
                SetExtinguishSubtask(BotExtinguishSubtask.AcquireTool, $"Picking up extinguisher '{toolName}'.");
                UpdateExtinguishDebugStage(ExtinguishDebugStage.PickingUpExtinguisher, $"Picking up extinguisher '{toolName}'.");
            },
            ReportMovingToTool = (toolName, toolPosition) =>
            {
                SetExtinguishSubtask(BotExtinguishSubtask.MoveToTool, $"Moving to tool '{toolName}'.");
                UpdateExtinguishDebugStage(ExtinguishDebugStage.MovingToExtinguisher, $"Moving to tool '{toolName}' at {toolPosition}.");
            },
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
            if (!IsRouteFireExtinguisherUsable(candidate))
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
        if (candidateComponent == null || candidate.IsHeld || candidate.Rigidbody == null || !IsRouteFireExtinguisherUsable(candidate))
        {
            return;
        }

        float distanceSq = (candidateComponent.transform.position - transform.position).sqrMagnitude;
        if (distanceSq > searchRadiusSq)
        {
            return;
        }

        float score = Mathf.Sqrt(distanceSq);
        if (score < bestScore)
        {
            bestScore = score;
            bestTool = candidate;
        }
    }

    private IBotExtinguisherItem ResolveCommittedExtinguishTool(Vector3 orderPoint, Vector3 firePosition, IFireGroupTarget fireGroup, IFireTarget fireTarget, BotExtinguishCommandMode orderMode)
    {
        if (IsRouteFireExtinguisherUsable(activeExtinguisher) &&
            activeExtinguisher.IsHeld &&
            activeExtinguisher.ClaimOwner == gameObject)
        {
            committedExtinguishTool = activeExtinguisher;
            return activeExtinguisher;
        }

        if (IsRouteFireExtinguisherUsable(committedExtinguishTool))
        {
            return committedExtinguishTool;
        }

        ReleaseCommittedTool();
        IBotExtinguisherItem selectedTool = SelectPreferredExtinguishTool(orderPoint, firePosition, fireGroup, fireTarget, orderMode);
        if (selectedTool == null)
        {
            return null;
        }

        if (!(selectedTool.IsHeld && selectedTool.ClaimOwner == gameObject) &&
            !selectedTool.TryClaim(gameObject))
        {
            return null;
        }

        committedExtinguishTool = selectedTool;
        return committedExtinguishTool;
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

    private bool IsRouteFireExtinguisherUsable(IBotExtinguisherItem tool)
    {
        return tool != null &&
               !UsesPreciseAim(tool) &&
               tool.HasUsableCharge &&
               tool.IsAvailableTo(gameObject);
    }
}
