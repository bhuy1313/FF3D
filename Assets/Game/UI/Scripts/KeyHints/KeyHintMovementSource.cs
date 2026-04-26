using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class KeyHintMovementSource : KeyHintSourceBase
{
    [Header("Behavior")]
    [SerializeField] private bool showOnlyDuringRunningMission = true;
    [SerializeField] private bool includeMove = true;
    [SerializeField] private bool includeSprint = true;
    [SerializeField] private bool includeJump = true;
    [SerializeField] private bool includeCrouch = true;

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

        if (includeMove)
        {
            results.Add(CreateHint("Move", KeyHintGameplayUtility.GetDefaultActionLabelLocalizationKey("Move"), KeyHintGameplayUtility.GetDefaultActionLabelFallback("Move"), sortOrder: 100, groupId: "movement"));
        }

        if (includeSprint)
        {
            results.Add(CreateHint("Sprint", KeyHintGameplayUtility.GetDefaultActionLabelLocalizationKey("Sprint"), KeyHintGameplayUtility.GetDefaultActionLabelFallback("Sprint"), sortOrder: 110, groupId: "movement"));
        }

        GameObject owner = context.InventorySystem != null ? context.InventorySystem.gameObject : null;
        GameObject occupyingObject = context.InteractionSystem != null
            ? context.InteractionSystem.CurrentHandOccupyingObject
            : context.HeldObject;
        bool jumpBlockedByHeldItem = KeyHintGameplayUtility.HeldObjectBlocksJump(occupyingObject, owner);

        if (includeJump && !jumpBlockedByHeldItem)
        {
            results.Add(CreateHint("Jump", KeyHintGameplayUtility.GetDefaultActionLabelLocalizationKey("Jump"), KeyHintGameplayUtility.GetDefaultActionLabelFallback("Jump"), sortOrder: 120, groupId: "movement"));
        }

        if (includeCrouch)
        {
            results.Add(CreateHint("Crouch", KeyHintGameplayUtility.GetDefaultActionLabelLocalizationKey("Crouch"), KeyHintGameplayUtility.GetDefaultActionLabelFallback("Crouch"), sortOrder: 130, groupId: "movement"));
        }
    }
}
