using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class KeyHintHeldItemSource : KeyHintSourceBase
{
    [Header("Behavior")]
    [SerializeField] private bool showOnlyDuringRunningMission = true;

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

        GameObject heldObject = context.HeldObject;
        if (heldObject == null)
        {
            return;
        }

        FireHose heldFireHose = KeyHintGameplayUtility.FindComponentInTargetHierarchy<FireHose>(heldObject);
        Tool heldTool = KeyHintGameplayUtility.FindComponentInTargetHierarchy<Tool>(heldObject);
        IUsable heldUsable = KeyHintGameplayUtility.FindComponentInTargetHierarchy<IUsable>(heldObject);
        Breakable targetBreakable = KeyHintGameplayUtility.FindComponentInTargetHierarchy<Breakable>(context.CurrentTarget);

        if (heldFireHose != null)
        {
            results.Add(CreateHint("Use", KeyHintGameplayUtility.GetContextLabelLocalizationKey("Spray Water"), "Spray Water", priorityOffset: 65, sortOrder: 300, groupId: "held-item"));
            results.Add(CreateHint("ToggleSprayPattern", KeyHintGameplayUtility.GetDefaultActionLabelLocalizationKey("ToggleSprayPattern"), KeyHintGameplayUtility.GetDefaultActionLabelFallback("ToggleSprayPattern"), priorityOffset: 64, sortOrder: 301, groupId: "held-item"));
            results.Add(CreateHint("IncreasePressure", KeyHintGameplayUtility.GetDefaultActionLabelLocalizationKey("IncreasePressure"), KeyHintGameplayUtility.GetDefaultActionLabelFallback("IncreasePressure"), priorityOffset: 63, sortOrder: 302, groupId: "held-item"));
            results.Add(CreateHint("DecreasePressure", KeyHintGameplayUtility.GetDefaultActionLabelLocalizationKey("DecreasePressure"), KeyHintGameplayUtility.GetDefaultActionLabelFallback("DecreasePressure"), priorityOffset: 62, sortOrder: 303, groupId: "held-item"));
            results.Add(CreateHint("Drop", KeyHintGameplayUtility.GetContextLabelLocalizationKey("Drop Hose"), "Drop Hose", priorityOffset: 61, sortOrder: 304, groupId: "held-item"));
            return;
        }

        if (heldTool != null)
        {
            string useLabel = targetBreakable != null ? "Break Target" : "Use Tool";
            results.Add(CreateHint("Use", KeyHintGameplayUtility.GetContextLabelLocalizationKey(useLabel), useLabel, priorityOffset: 60, sortOrder: 310, groupId: "held-item"));
            results.Add(CreateHint("Drop", KeyHintGameplayUtility.GetContextLabelLocalizationKey("Drop Tool"), "Drop Tool", priorityOffset: 59, sortOrder: 311, groupId: "held-item"));
            return;
        }

        if (heldUsable != null)
        {
            results.Add(CreateHint("Use", KeyHintGameplayUtility.GetContextLabelLocalizationKey("Use Item"), "Use Item", priorityOffset: 58, sortOrder: 320, groupId: "held-item"));
            results.Add(CreateHint("Drop", KeyHintGameplayUtility.GetContextLabelLocalizationKey("Drop Item"), "Drop Item", priorityOffset: 57, sortOrder: 321, groupId: "held-item"));
        }
    }
}
