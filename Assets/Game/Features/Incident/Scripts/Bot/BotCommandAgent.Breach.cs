using System.Collections.Generic;
using UnityEngine;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private bool IsBreachCommandActive()
    {
        return behaviorContext != null &&
               behaviorContext.TryGetCommandIntentSnapshot(out BotCommandIntentPayload payload) &&
               payload.CommandType == BotCommandType.Breach;
    }

    private bool TryGetBreachIntent(out BotCommandIntentPayload intent)
    {
        intent = default;
        if (behaviorContext == null || !behaviorContext.TryGetCommandIntentSnapshot(out BotCommandIntentPayload payload))
        {
            return false;
        }

        if (payload.CommandType != BotCommandType.Breach)
        {
            return false;
        }

        intent = payload;
        return true;
    }

    private bool TryResolveBreachPryTarget(Vector3 orderPoint, out IBotPryTarget target)
    {
        target = null;
        if (IsUsableBreachPryTarget(currentBreachPryTarget))
        {
            target = currentBreachPryTarget;
            return true;
        }

        if (runtimeDecisionService == null)
        {
            return false;
        }

        target = runtimeDecisionService.ResolveNearestPryTarget(
            orderPoint,
            gameObject,
            Mathf.Max(1f, breachSearchRadius));
        return IsUsableBreachPryTarget(target);
    }

    private bool TryResolveBreachBreakableTarget(Vector3 orderPoint, out IBotBreakableTarget target)
    {
        target = null;
        if (currentBlockedBreakable != null &&
            !currentBlockedBreakable.IsBroken &&
            currentBlockedBreakable.CanBeClearedByBot &&
            !BotRuntimeRegistry.Reservations.IsReservedByOther(currentBlockedBreakable, gameObject))
        {
            target = currentBlockedBreakable;
            return true;
        }

        float bestDistanceSq = float.PositiveInfinity;
        float searchRadius = Mathf.Max(1f, breachSearchRadius);
        float searchRadiusSq = searchRadius * searchRadius;

        foreach (IBotBreakableTarget candidate in BotRuntimeRegistry.ActiveBreakableTargets)
        {
            if (candidate == null ||
                candidate.IsBroken ||
                !candidate.CanBeClearedByBot ||
                BotRuntimeRegistry.Reservations.IsReservedByOther(candidate, gameObject))
            {
                continue;
            }

            float distanceSq = (candidate.GetWorldPosition() - orderPoint).sqrMagnitude;
            if (distanceSq > searchRadiusSq || distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            target = candidate;
        }

        if (target == null &&
            perceptionMemory != null &&
            perceptionMemory.TryGetNearestRecentBreakable(orderPoint, searchRadius, out IBotBreakableTarget rememberedBreakable) &&
            !BotRuntimeRegistry.Reservations.IsReservedByOther(rememberedBreakable, gameObject))
        {
            target = rememberedBreakable;
        }

        if (target == null &&
            BotRuntimeRegistry.SharedIncidentBlackboard.TryGetNearestRecentBreakable(orderPoint, searchRadius, gameObject, out IBotBreakableTarget sharedBreakable))
        {
            target = sharedBreakable;
        }

        return target != null;
    }

    private void ProcessBreachPryTarget(IBotPryTarget target)
    {
        if (target == null)
        {
            return;
        }

        if (target.IsBreached && !target.IsPryInProgress)
        {
            CompleteBreachOrder("Breach completed.");
            return;
        }

        Vector3 targetPosition = target.GetWorldPosition();
        float interactionDistance = Mathf.Max(0.5f, breachInteractionDistance);
        if ((targetPosition - transform.position).sqrMagnitude > interactionDistance * interactionDistance)
        {
            SetBreakSubtask(BotBreakSubtask.MoveToObstacle, $"Moving to pry target '{GetDebugTargetName(target)}'.");
            TryNavigateTo(targetPosition);
            return;
        }

        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;
        }

        AimTowards(targetPosition);
        if (!target.IsPryInProgress && !target.CanBePriedOpen)
        {
            AbortBreachOrder("Assigned pry target can no longer be breached.");
            return;
        }

        if (target.IsPryInProgress)
        {
            SetBreakSubtask(BotBreakSubtask.Pry, $"Prying '{GetDebugTargetName(target)}'.");
            return;
        }

        IBotBreakTool pryTool = ResolvePreferredPryTool();
        if (pryTool == null)
        {
            AbortBreachOrder("No usable crowbar available for breach.");
            return;
        }

        if (!TryEnsureBreakToolEquipped(pryTool))
        {
            return;
        }

        if (!target.IsPryInProgress && !target.TryPryOpen(gameObject))
        {
            AbortBreachOrder("Failed to start prying the assigned breach target.");
            return;
        }

        SetBreakSubtask(BotBreakSubtask.Pry, $"Prying '{GetDebugTargetName(target)}'.");

        if (target.IsBreached && !target.IsPryInProgress)
        {
            CompleteBreachOrder("Breach completed.");
        }
    }

    private void ProcessBreachBreakableTarget(IBotBreakableTarget target)
    {
        if (target == null)
        {
            return;
        }

        if (target.IsBroken)
        {
            CompleteBreachOrder("Breach completed.");
            return;
        }

        IBotBreakTool breakTool = ResolveCommittedBreakTool();
        if (breakTool == null)
        {
            AbortBreachOrder("No usable breaching tool found.");
            return;
        }

        if (!TryEnsureBreakToolEquipped(breakTool))
        {
            return;
        }

        if (!HandleEquippedBreakToolAgainstTarget(activeBreakTool ?? breakTool, target))
        {
            AbortBreachOrder("Failed to breach the assigned obstacle.");
        }
    }

    private IBotBreakTool ResolvePreferredPryTool()
    {
        if (activeBreakTool != null && activeBreakTool.ToolKind == BreakToolKind.Crowbar)
        {
            return activeBreakTool;
        }

        if (committedBreakTool != null &&
            committedBreakTool.ToolKind == BreakToolKind.Crowbar &&
            committedBreakTool.IsAvailableTo(gameObject))
        {
            return committedBreakTool;
        }

        List<IBotBreakTool> inventoryTools = new List<IBotBreakTool>();
        inventorySystem?.CollectItems(inventoryTools);
        for (int i = 0; i < inventoryTools.Count; i++)
        {
            IBotBreakTool candidate = inventoryTools[i];
            if (candidate != null &&
                candidate.ToolKind == BreakToolKind.Crowbar &&
                candidate.IsAvailableTo(gameObject))
            {
                return candidate;
            }
        }

        foreach (IBotBreakTool candidate in BotRuntimeRegistry.ActiveBreakTools)
        {
            if (candidate != null &&
                candidate.ToolKind == BreakToolKind.Crowbar &&
                !candidate.IsHeld &&
                candidate.IsAvailableTo(gameObject))
            {
                return candidate;
            }
        }

        if (perceptionMemory != null &&
            perceptionMemory.TryGetNearestRecentBreakTool(transform.position, toolSearchRadius, gameObject, out IBotBreakTool rememberedTool) &&
            rememberedTool.ToolKind == BreakToolKind.Crowbar)
        {
            return rememberedTool;
        }

        if (BotRuntimeRegistry.SharedIncidentBlackboard.TryGetNearestRecentBreakTool(transform.position, toolSearchRadius, gameObject, out IBotBreakTool sharedTool) &&
            sharedTool.ToolKind == BreakToolKind.Crowbar)
        {
            return sharedTool;
        }

        return null;
    }

    private bool IsUsableBreachPryTarget(IBotPryTarget target)
    {
        return target != null &&
               !target.IsBreached &&
               (target.CanBePriedOpen || target.IsPryInProgress) &&
               !BotRuntimeRegistry.Reservations.IsReservedByOther(target, gameObject);
    }

    private bool IsNearBreachPoint(Vector3 point)
    {
        float threshold = Mathf.Max(
            breachInteractionDistance,
            behaviorContext != null ? behaviorContext.ArrivalDistance : 0.35f);
        return (point - transform.position).sqrMagnitude <= threshold * threshold;
    }

    private void CompleteBreachOrder(string detail)
    {
        lastBreakFailureReason = string.Empty;
        SetBreakSubtask(BotBreakSubtask.Complete, detail);
        CompleteCurrentTask(detail);
        if (behaviorContext != null)
        {
            if (behaviorContext.HasMoveOrder)
            {
                behaviorContext.ClearMoveOrder();
            }
            else
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
        ClearBreachRuntimeState();
    }

    private void AbortBreachOrder(string detail)
    {
        SetBreakFailureReason(detail);
        FailCurrentTask(detail, BotTaskStatus.Blocked);
        if (behaviorContext != null)
        {
            if (behaviorContext.HasMoveOrder)
            {
                behaviorContext.ClearMoveOrder();
            }
            else
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
        ClearBreachRuntimeState();
    }

    private void ClearBreachRuntimeState()
    {
        SetCurrentBreachPryTarget(null);
        SetCurrentBlockedBreakable(null);
        currentBreakSubtask = BotBreakSubtask.None;
        breakTaskDetail = "Awaiting break assignment.";
        lastBreakFailureReason = string.Empty;
        breakSubtaskStartedAtTime = 0f;
    }
}
