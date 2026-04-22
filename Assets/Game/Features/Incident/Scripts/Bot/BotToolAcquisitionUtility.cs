using System;
using UnityEngine;

namespace TrueJourney.BotBehavior
{
    internal sealed class BotToolAcquisitionOptions<TTool> where TTool : class
    {
        public Transform BotTransform;
        public BotInventorySystem InventorySystem;
        public float PickupDistance;
        public Func<TTool, bool> IsAvailableToBot;
        public Func<TTool, bool> IsHeldByBot;
        public Action<TTool> SetActiveTool;
        public Action OnUnavailable;
        public Action OnBeforeAcquire;
        public Action<string> ReportSearching;
        public Action<string> ReportPickingUp;
        public Action<string, Vector3> ReportMovingToTool;
        public Action<string> LogHeld;
        public Action<string> LogEquipped;
        public Action<string> LogPickedUp;
        public Func<TTool, bool> OnBeforePickup;
        public Action<bool, IPickupable> SetPickupWindow;
        public Action<Vector3> MoveToTool;
        public bool AllowMoveToToolRoute = true;
    }

    internal static class BotToolAcquisitionUtility
    {
        public static bool TryEnsureToolEquipped<TTool>(TTool desiredTool, BotToolAcquisitionOptions<TTool> options)
            where TTool : class
        {
            if (desiredTool == null || options == null)
            {
                return false;
            }

            if (options.InventorySystem == null || options.BotTransform == null)
            {
                return false;
            }

            if (options.IsAvailableToBot == null || options.IsHeldByBot == null || options.SetActiveTool == null)
            {
                return false;
            }

            if (!options.IsAvailableToBot(desiredTool))
            {
                options.OnUnavailable?.Invoke();
                return false;
            }

            string toolName = GetToolName(desiredTool);
            if (options.IsHeldByBot(desiredTool))
            {
                if (desiredTool is IPickupable heldPickupable)
                {
                    bool equippedFromInventory = options.InventorySystem.TryEquipItem(heldPickupable);
                    if (!equippedFromInventory && options.InventorySystem.TryPickup(heldPickupable))
                    {
                        options.InventorySystem.TryEquipItem(heldPickupable);
                    }
                }

                options.SetActiveTool(desiredTool);
                options.SetPickupWindow?.Invoke(false, null);
                options.LogHeld?.Invoke(toolName);
                options.LogEquipped?.Invoke(toolName);
                return true;
            }

            if (!(desiredTool is IPickupable pickupable))
            {
                return false;
            }

            if (options.InventorySystem.TryEquipItem(pickupable))
            {
                options.SetActiveTool(desiredTool);
                options.SetPickupWindow?.Invoke(false, null);
                options.LogEquipped?.Invoke(toolName);
                return true;
            }

            options.OnBeforeAcquire?.Invoke();
            options.ReportSearching?.Invoke(toolName);

            if (!(desiredTool is Component toolComponent) || toolComponent == null)
            {
                return false;
            }

            Vector3 toolPosition = toolComponent.transform.position;
            if (Vector3.Distance(options.BotTransform.position, toolPosition) <= options.PickupDistance)
            {
                options.ReportPickingUp?.Invoke(toolName);
                if (options.OnBeforePickup != null && !options.OnBeforePickup(desiredTool))
                {
                    return false;
                }

                options.SetPickupWindow?.Invoke(true, pickupable);
                if (!options.InventorySystem.TryPickup(pickupable))
                {
                    return false;
                }

                options.SetPickupWindow?.Invoke(false, null);
                if (options.InventorySystem.TryEquipItem(pickupable))
                {
                    options.SetActiveTool(desiredTool);
                    options.LogPickedUp?.Invoke(toolName);
                    return true;
                }

                return false;
            }

            if (!options.AllowMoveToToolRoute)
            {
                return false;
            }

            options.ReportMovingToTool?.Invoke(toolName, toolPosition);
            options.SetPickupWindow?.Invoke(true, pickupable);
            options.MoveToTool?.Invoke(toolPosition);
            return false;
        }

        private static string GetToolName<TTool>(TTool tool) where TTool : class
        {
            return tool is Component component && component != null
                ? component.name
                : "(unknown tool)";
        }
    }
}
