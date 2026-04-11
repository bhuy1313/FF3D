using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class KeyHintInventorySource : KeyHintSourceBase
{
    [Header("Behavior")]
    [SerializeField] private bool showOnlyDuringRunningMission = true;
    [SerializeField] private int maxVisibleSlots = 6;

    protected override void CollectHintsInternal(KeyHintContext context, List<KeyHintRequest> results)
    {
        if (context == null || results == null)
        {
            return;
        }

        if (showOnlyDuringRunningMission && context.MissionSystem != null && !context.IsMissionRunning)
        {
            return;
        }

        if (context.InventoryItemCount <= 0 || context.InventoryMaxSlots <= 0)
        {
            return;
        }

        GameObject heldObject = context.HeldObject;
        if (heldObject != null)
        {
            if (KeyHintGameplayUtility.FindComponentInTargetHierarchy<FireHose>(heldObject) != null ||
                KeyHintGameplayUtility.FindComponentInTargetHierarchy<Tool>(heldObject) != null ||
                KeyHintGameplayUtility.FindComponentInTargetHierarchy<IUsable>(heldObject) != null)
            {
                return;
            }
        }

        int slotCount = Mathf.Min(context.InventoryItemCount, context.InventoryMaxSlots, Mathf.Max(0, maxVisibleSlots));
        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            int oneBasedSlot = slotIndex + 1;
            results.Add(CreateHint(
                $"Slot{oneBasedSlot}",
                KeyHintGameplayUtility.GetDefaultActionLabelLocalizationKey($"Slot{oneBasedSlot}"),
                labelFallback: $"Equip Slot {oneBasedSlot}",
                priorityOffset: 20,
                sortOrder: 400 + slotIndex,
                groupId: "inventory"));
        }
    }
}
