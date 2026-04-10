using UnityEngine;
using TrueJourney.BotBehavior;
using System.Collections.Generic;

public partial class BotCommandAgent
{
    [Header("Rescue Task Flow")]
    [SerializeField] private BotRescueSubtask currentRescueSubtask;
    [SerializeField] private string rescueTaskDetail = "Awaiting rescue assignment.";
    [SerializeField] private string lastRescueFailureReason;
    [SerializeField] private float rescueSubtaskStartedAtTime;

    internal void AbortActiveRescueOrder()
    {
        lastRescueFailureReason = "Rescue order aborted.";
        FailCurrentTask(lastRescueFailureReason, BotTaskStatus.Blocked);
        if (behaviorContext != null)
        {
            behaviorContext.ClearRescueOrder();
        }

        ClearRescueRuntimeState();
        ClearRouteFireRuntime();
        if (navMeshAgent != null)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
        }
    }

    internal void CompleteActiveRescueOrder()
    {
        lastRescueFailureReason = string.Empty;
        SetRescueSubtask(BotRescueSubtask.CompleteRescue, "Rescue completed.");
        CompleteCurrentTask("Rescue order completed.");
        if (behaviorContext != null)
        {
            behaviorContext.ClearRescueOrder();
        }

        ClearRescueRuntimeState();
        ClearRouteFireRuntime();
        if (navMeshAgent != null)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
        }
    }

    private Transform GetRescueCarryAnchor()
    {
        if (rescueCarryAnchor != null)
        {
            return rescueCarryAnchor;
        }

        if (runtimeRescueCarryAnchor == null)
        {
            GameObject anchorObject = new GameObject("RescueCarryAnchor");
            runtimeRescueCarryAnchor = anchorObject.transform;
            runtimeRescueCarryAnchor.SetParent(transform, false);
            runtimeRescueCarryAnchor.localPosition = rescueCarryLocalPosition;
            runtimeRescueCarryAnchor.localRotation = Quaternion.identity;
        }

        return runtimeRescueCarryAnchor;
    }

    private void ClearRescueRuntimeState()
    {
        SetCurrentRescueTarget(null);
        currentSafeZoneTarget = null;
        currentRescueSubtask = BotRescueSubtask.None;
        rescueTaskDetail = "Awaiting rescue assignment.";
        lastRescueFailureReason = string.Empty;
        rescueSubtaskStartedAtTime = 0f;
        activityDebug?.ResetRescue();
    }

    internal void SetRescueSubtask(BotRescueSubtask subtask, string detail)
    {
        if (currentRescueSubtask != subtask)
        {
            rescueSubtaskStartedAtTime = Application.isPlaying ? Time.time : 0f;
        }

        currentRescueSubtask = subtask;
        rescueTaskDetail = string.IsNullOrWhiteSpace(detail) ? "Executing rescue order." : detail;
    }

    internal void SetRescueFailureReason(string detail)
    {
        lastRescueFailureReason = string.IsNullOrWhiteSpace(detail) ? string.Empty : detail;
    }

    internal string GetActiveRescueTaskDetail()
    {
        if (!string.IsNullOrWhiteSpace(lastRescueFailureReason))
        {
            return lastRescueFailureReason;
        }

        return string.IsNullOrWhiteSpace(rescueTaskDetail)
            ? "Executing rescue order."
            : rescueTaskDetail;
    }

    internal void ReacquireRescueTarget(string detail)
    {
        SetCurrentRescueTarget(null);
        currentSafeZoneTarget = null;
        lastRescueFailureReason = string.Empty;
        SetRescueSubtask(BotRescueSubtask.Recover, detail);
    }

    internal void FailActiveRescueOrder(string detail, BotTaskStatus failureStatus = BotTaskStatus.Failed)
    {
        SetRescueFailureReason(detail);
        FailCurrentTask(detail, failureStatus);
        if (behaviorContext != null)
        {
            behaviorContext.ClearRescueOrder();
        }

        ClearRescueRuntimeState();
        ClearRouteFireRuntime();
        if (navMeshAgent != null)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
        }
    }

    private void UnequipCurrentToolsForCarry()
    {
        List<IPickupable> toolsToUnequip = new List<IPickupable>(8);

        if (inventorySystem != null)
        {
            inventorySystem.ClearEquippedSelection();
        }

        CollectToolForUnequip(toolsToUnequip, activeExtinguisher as IPickupable);
        CollectToolForUnequip(toolsToUnequip, activeBreakTool as IPickupable);

        foreach (IBotExtinguisherItem extinguisher in BotRuntimeRegistry.ActiveExtinguisherItems)
        {
            if (extinguisher == null || !extinguisher.IsHeld || extinguisher.ClaimOwner != gameObject)
            {
                continue;
            }

            CollectToolForUnequip(toolsToUnequip, extinguisher as IPickupable);
        }

        foreach (IBotBreakTool breakTool in BotRuntimeRegistry.ActiveBreakTools)
        {
            if (breakTool == null || !breakTool.IsHeldBy(gameObject))
            {
                continue;
            }

            CollectToolForUnequip(toolsToUnequip, breakTool as IPickupable);
        }

        for (int i = 0; i < toolsToUnequip.Count; i++)
        {
            ForceUnequipTool(toolsToUnequip[i]);
        }

        ClearExtinguishRuntimeState();
        ClearBlockedPathRuntime();
        activeExtinguisher = null;
        activeBreakTool = null;
    }

    private static void CollectToolForUnequip(List<IPickupable> toolsToUnequip, IPickupable pickupable)
    {
        if (toolsToUnequip == null || pickupable == null || toolsToUnequip.Contains(pickupable))
        {
            return;
        }

        toolsToUnequip.Add(pickupable);
    }

    private void ForceUnequipTool(IPickupable pickupable)
    {
        if (pickupable == null || inventorySystem == null)
        {
            return;
        }

        inventorySystem.ForceUnequipItem(pickupable);
    }
}
