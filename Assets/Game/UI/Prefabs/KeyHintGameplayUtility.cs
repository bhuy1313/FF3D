using UnityEngine;

public static class KeyHintGameplayUtility
{
    public static string GetDefaultActionLabelLocalizationKey(string actionName)
    {
        return actionName switch
        {
            "Move" => "keyhint.action.move",
            "Look" => "keyhint.action.look",
            "Jump" => "keyhint.action.jump",
            "Sprint" => "keyhint.action.sprint",
            "Crouch" => "keyhint.action.crouch",
            "Interact" => "keyhint.action.interact",
            "Pickup" => "keyhint.action.pickup",
            "Use" => "keyhint.action.use",
            "Drop" => "keyhint.action.drop",
            "Grab" => "keyhint.action.grab",
            "ToolWheel" => "keyhint.action.tool_wheel",
            "CommandMove" => "keyhint.action.command_move",
            "CommandCancel" => "keyhint.action.command_cancel",
            "CommandCancelAllFollow" => "keyhint.action.command_cancel_all_follow",
            "ToggleBotOutline" => "keyhint.action.toggle_bot_outline",
            "CommandConfirm" => "keyhint.action.command_confirm",
            "ToggleSprayPattern" => "keyhint.action.toggle_spray_pattern",
            "IncreasePressure" => "keyhint.action.increase_pressure",
            "DecreasePressure" => "keyhint.action.decrease_pressure",
            _ when !string.IsNullOrWhiteSpace(actionName) && actionName.StartsWith("Slot", System.StringComparison.OrdinalIgnoreCase)
                => $"keyhint.action.{actionName.Trim().ToLowerInvariant()}",
            _ => string.Empty
        };
    }

    public static string GetContextLabelLocalizationKey(string fallbackLabel)
    {
        return fallbackLabel switch
        {
            "Deliver Victim" => "keyhint.context.deliver_victim",
            "Close Door" => "keyhint.context.close_door",
            "Open Door" => "keyhint.context.open_door",
            "Release Object" => "keyhint.context.release_object",
            "Place Object" => "keyhint.context.place_object",
            "Stabilize Victim" => "keyhint.context.stabilize_victim",
            "Carry Victim" => "keyhint.context.carry_victim",
            "Disconnect Hose" => "keyhint.context.disconnect_hose",
            "Connect Hose" => "keyhint.context.connect_hose",
            "Trigger Explosive" => "keyhint.context.trigger_explosive",
            "Spray Water" => "keyhint.context.spray_water",
            "Drop Hose" => "keyhint.context.drop_hose",
            "Break Target" => "keyhint.context.break_target",
            "Use Tool" => "keyhint.context.use_tool",
            "Drop Tool" => "keyhint.context.drop_tool",
            "Use Item" => "keyhint.context.use_item",
            "Drop Item" => "keyhint.context.drop_item",
            "Grab Object" => "keyhint.context.grab_object",
            "Pick Up" => "keyhint.context.pick_up",
            "Pick Up Hose" => "keyhint.context.pick_up_hose",
            "Pick Up Tool" => "keyhint.context.pick_up_tool",
            "Command Bot" => "keyhint.context.command_bot",
            "Confirm Command" => "keyhint.context.confirm_command",
            "Cancel Command" => "keyhint.context.cancel_command",
            _ => string.Empty
        };
    }

    public static T FindComponentInTargetHierarchy<T>(GameObject target) where T : class
    {
        if (target == null)
        {
            return null;
        }

        Component direct = target.GetComponent(typeof(T));
        if (direct is T typedDirect)
        {
            return typedDirect;
        }

        Rigidbody attachedBody = target.GetComponentInParent<Rigidbody>();
        if (attachedBody != null)
        {
            Component rigidbodyOwner = attachedBody.GetComponent(typeof(T));
            if (rigidbodyOwner is T typedRigidbodyOwner)
            {
                return typedRigidbodyOwner;
            }
        }

        Transform parent = target.transform.parent;
        while (parent != null)
        {
            Component parentComponent = parent.GetComponent(typeof(T));
            if (parentComponent is T typedParent)
            {
                return typedParent;
            }

            parent = parent.parent;
        }

        return null;
    }

    public static string ResolvePickupLabelFallback(GameObject target)
    {
        if (target == null)
        {
            return "Pick Up";
        }

        if (FindComponentInTargetHierarchy<FireHose>(target) != null)
        {
            return "Pick Up Hose";
        }

        if (FindComponentInTargetHierarchy<Tool>(target) != null)
        {
            return "Pick Up Tool";
        }

        return "Pick Up";
    }

    public static string GetDefaultActionLabelFallback(string actionName)
    {
        return actionName switch
        {
            "Move" => "Move",
            "Look" => "Look",
            "Jump" => "Jump",
            "Sprint" => "Sprint",
            "Crouch" => "Crouch",
            "Interact" => "Interact",
            "Pickup" => "Pick Up",
            "Use" => "Use",
            "Drop" => "Drop",
            "Grab" => "Grab",
            "Slot1" => "Slot 1",
            "Slot2" => "Slot 2",
            "Slot3" => "Slot 3",
            "Slot4" => "Slot 4",
            "Slot5" => "Slot 5",
            "Slot6" => "Slot 6",
            "ToolWheel" => "Tool Wheel",
            "CommandMove" => "Command Bot",
            "CommandCancel" => "Cancel Command",
            "CommandCancelAllFollow" => "Cancel All Follow",
            "ToggleBotOutline" => "Toggle Bot Outline",
            "CommandConfirm" => "Confirm Command",
            "ToggleSprayPattern" => "Toggle Spray",
            "IncreasePressure" => "Increase Pressure",
            "DecreasePressure" => "Decrease Pressure",
            _ => actionName ?? string.Empty
        };
    }

    public static string ResolveLocalizedLabel(KeyHintRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.LabelLocalizationKey))
        {
            return LanguageManager.Tr(request.LabelLocalizationKey, request.GetEffectiveLabelFallback());
        }

        return request.GetEffectiveLabelFallback();
    }
}
