using UnityEngine;
using TrueJourney.BotBehavior;
using System.Collections.Generic;

public partial class BotCommandAgent
{
    internal void AbortActiveRescueOrder()
    {
        FailCurrentTask("Rescue order aborted.", BotTaskStatus.Blocked);
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
        activityDebug?.ResetRescue();
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
