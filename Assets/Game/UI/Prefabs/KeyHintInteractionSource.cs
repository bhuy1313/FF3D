using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class KeyHintInteractionSource : KeyHintSourceBase
{
    [Header("Behavior")]
    [SerializeField] private bool showOnlyDuringRunningMission = true;

    protected override void CollectHintsInternal(KeyHintContext context, List<KeyHintRequest> results)
    {
        if (context == null || results == null || context.InteractionSystem == null)
        {
            return;
        }

        if (showOnlyDuringRunningMission && context.MissionSystem != null && !context.IsMissionRunning)
        {
            return;
        }

        GameObject currentTarget = context.CurrentTarget;
        GameObject heldObject = context.HeldObject;
        bool canPickupMoreItems = context.InventorySystem != null && context.InventoryItemCount < context.InventoryMaxSlots;

        bool targetIsPickupable = KeyHintGameplayUtility.FindComponentInTargetHierarchy<IPickupable>(currentTarget) != null;
        bool targetIsGrabbable = KeyHintGameplayUtility.FindComponentInTargetHierarchy<IGrabbable>(currentTarget) != null;
        SafeZone targetSafeZone = KeyHintGameplayUtility.FindComponentInTargetHierarchy<SafeZone>(currentTarget);
        Door targetDoor = KeyHintGameplayUtility.FindComponentInTargetHierarchy<Door>(currentTarget);
        Rescuable targetRescuable = KeyHintGameplayUtility.FindComponentInTargetHierarchy<Rescuable>(currentTarget);
        FireHoseConnectionPoint targetHoseConnection = KeyHintGameplayUtility.FindComponentInTargetHierarchy<FireHoseConnectionPoint>(currentTarget);
        Breakable targetBreakable = KeyHintGameplayUtility.FindComponentInTargetHierarchy<Breakable>(currentTarget);
        Explosive targetExplosive = KeyHintGameplayUtility.FindComponentInTargetHierarchy<Explosive>(currentTarget);
        IInteractable targetInteractable = KeyHintGameplayUtility.FindComponentInTargetHierarchy<IInteractable>(currentTarget);
        ICommandable targetCommandable = KeyHintGameplayUtility.FindComponentInTargetHierarchy<ICommandable>(currentTarget);
        FireHose heldFireHose = KeyHintGameplayUtility.FindComponentInTargetHierarchy<FireHose>(heldObject);

        if (context.IsCarryingRescuable)
        {
            if (targetSafeZone != null)
            {
                results.Add(CreateHint("Interact", KeyHintGameplayUtility.GetContextLabelLocalizationKey("Deliver Victim"), "Deliver Victim", priorityOffset: 70, sortOrder: 10, groupId: "interaction"));
            }
            else if (targetDoor != null)
            {
                string label = targetDoor.IsOpen ? "Close Door" : "Open Door";
                results.Add(CreateHint("Interact", KeyHintGameplayUtility.GetContextLabelLocalizationKey(label), label, priorityOffset: 60, sortOrder: 20, groupId: "interaction"));
            }

            return;
        }

        if (context.IsGrabActive)
        {
            results.Add(CreateHint("Grab", KeyHintGameplayUtility.GetContextLabelLocalizationKey("Release Object"), "Release Object", priorityOffset: 80, sortOrder: 10, groupId: "interaction"));
            results.Add(CreateHint("Use", KeyHintGameplayUtility.GetContextLabelLocalizationKey("Place Object"), "Place Object", priorityOffset: 79, sortOrder: 11, groupId: "interaction"));
            if (targetDoor != null)
            {
                string label = targetDoor.IsOpen ? "Close Door" : "Open Door";
                results.Add(CreateHint("Interact", KeyHintGameplayUtility.GetContextLabelLocalizationKey(label), label, priorityOffset: 60, sortOrder: 20, groupId: "interaction"));
            }

            return;
        }

        if (targetRescuable != null)
        {
            string label = targetRescuable.RequiresStabilization ? "Stabilize Victim" : "Carry Victim";
            results.Add(CreateHint("Interact", KeyHintGameplayUtility.GetContextLabelLocalizationKey(label), label, priorityOffset: 70, sortOrder: 10, groupId: "interaction"));
        }
        else if (targetHoseConnection != null && heldFireHose != null)
        {
            bool isConnectedToFocusedPoint = ReferenceEquals(targetHoseConnection.ConnectedHose, heldFireHose);
            string label = isConnectedToFocusedPoint ? "Disconnect Hose" : "Connect Hose";
            results.Add(CreateHint("Interact", KeyHintGameplayUtility.GetContextLabelLocalizationKey(label), label, priorityOffset: 65, sortOrder: 12, groupId: "interaction"));
        }
        else if (targetDoor != null)
        {
            string label = targetDoor.IsOpen ? "Close Door" : "Open Door";
            results.Add(CreateHint("Interact", KeyHintGameplayUtility.GetContextLabelLocalizationKey(label), label, priorityOffset: 60, sortOrder: 20, groupId: "interaction"));
        }
        else if (targetExplosive != null)
        {
            results.Add(CreateHint("Interact", KeyHintGameplayUtility.GetContextLabelLocalizationKey("Trigger Explosive"), "Trigger Explosive", priorityOffset: 55, sortOrder: 25, groupId: "interaction"));
        }
        else if (targetInteractable != null &&
                 targetCommandable == null &&
                 heldObject == null &&
                 !targetIsPickupable &&
                 !targetIsGrabbable &&
                 targetSafeZone == null &&
                 targetHoseConnection == null &&
                 targetBreakable == null)
        {
            results.Add(CreateHint("Interact", KeyHintGameplayUtility.GetDefaultActionLabelLocalizationKey("Interact"), KeyHintGameplayUtility.GetDefaultActionLabelFallback("Interact"), priorityOffset: 40, sortOrder: 30, groupId: "interaction"));
        }

        if (targetIsPickupable && canPickupMoreItems)
        {
            string label = KeyHintGameplayUtility.ResolvePickupLabelFallback(currentTarget);
            results.Add(CreateHint("Pickup", KeyHintGameplayUtility.GetContextLabelLocalizationKey(label), label, priorityOffset: 50, sortOrder: 40, groupId: "interaction"));
        }

        if (targetIsGrabbable && heldObject == null && !context.IsCarryingRescuable)
        {
            results.Add(CreateHint("Grab", KeyHintGameplayUtility.GetContextLabelLocalizationKey("Grab Object"), "Grab Object", priorityOffset: 45, sortOrder: 50, groupId: "interaction"));
        }
    }
}
