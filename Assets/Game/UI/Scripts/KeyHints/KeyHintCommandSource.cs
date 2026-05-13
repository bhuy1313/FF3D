using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class KeyHintCommandSource : KeyHintSourceBase
{
    [Header("Behavior")]
    [SerializeField] private bool showOnlyDuringRunningMission = true;

    protected override void CollectHintsInternal(KeyHintContext context, List<KeyHintRequest> results)
    {
        if (context == null || results == null || context.CommandSystem == null)
        {
            return;
        }

        if (showOnlyDuringRunningMission && context.MissionSystem != null && !context.IsMissionRunning)
        {
            return;
        }

        if (context.IsAwaitingCommandDestination)
        {
            results.Add(CreateHint("CommandConfirm", KeyHintGameplayUtility.GetContextLabelLocalizationKey("Confirm Command"), "Confirm Command", priorityOffset: 40, sortOrder: 40, groupId: "command"));
            if (context.CommandSystem.PendingCommandType == BotCommandType.Extinguish)
            {
                results.Add(CreateHint("CommandAlternateConfirm", KeyHintGameplayUtility.GetContextLabelLocalizationKey("Precision Extinguish"), "Precision Extinguish", priorityOffset: 39, sortOrder: 41, groupId: "command"));
            }

            results.Add(CreateHint("CommandCancel", KeyHintGameplayUtility.GetContextLabelLocalizationKey("Cancel Command"), "Cancel Command", priorityOffset: 38, sortOrder: 42, groupId: "command"));
            return;
        }

        if (context.HoveredCommandTarget != null)
        {
            results.Add(CreateHint("CommandMove", KeyHintGameplayUtility.GetContextLabelLocalizationKey("Command Bot"), "Command Bot", priorityOffset: 20, sortOrder: 200, groupId: "command"));
        }
    }
}
