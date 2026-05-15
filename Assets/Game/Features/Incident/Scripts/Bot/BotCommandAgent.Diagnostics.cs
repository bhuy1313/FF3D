using UnityEngine;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private static GUIStyle commandPlanOverlayBoxStyle;
    private static GUIStyle commandPlanOverlayLabelStyle;

    private void LogVerboseExtinguish(VerboseExtinguishLogCategory category, string key, string detail)
    {
        return;
    }

    private static string GetDebugTargetName(object target)
    {
        if (target is Component component && component != null)
        {
            return component.name;
        }

        return target != null ? target.GetType().Name : "null";
    }

    private static string GetToolName(IBotExtinguisherItem tool)
    {
        Component component = tool as Component;
        return component != null ? component.name : "(unknown tool)";
    }

    private static string GetBreakToolName(IBotBreakTool tool)
    {
        Component component = tool as Component;
        return component != null ? component.name : "(unknown break tool)";
    }

    private static string GetPickupableName(IPickupable pickupable)
    {
        if (pickupable is Component component && component != null)
        {
            return component.name;
        }

        return "(unknown item)";
    }

    private void SetPickupWindow(bool enabled, IPickupable target = null)
    {
        if (interactionSensor == null)
        {
            return;
        }

        interactionSensor.SetPickupWindow(enabled, target);
    }

    private void ResolveViewPointReference()
    {
        if (viewPoint != null)
        {
            return;
        }

        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < childTransforms.Length; i++)
        {
            if (childTransforms[i] != null && childTransforms[i].name == "ViewPoint")
            {
                viewPoint = childTransforms[i];
                return;
            }
        }

        if (inventorySystem != null && inventorySystem.EquippedRoot != null)
        {
            viewPoint = inventorySystem.EquippedRoot;
        }
    }

    private void LogRescueActivity(string key, string detail)
    {
        activityDebug?.LogRescue(this, enableActivityDebug, key, detail);
    }

    private bool HasPendingCommittedBreakTool()
    {
        return committedBreakTool != null && !committedBreakTool.IsHeldBy(gameObject);
    }

    private void RefreshPathClearingResumeGrace()
    {
        pathClearingResumeGraceUntilTime = Time.time + Mathf.Max(0.05f, pathClearingResumeGraceTime);
    }

    private void LogVerbosePathClearing(VerbosePathClearingLogCategory category, string key, string detail)
    {
        return;
    }

    private void LogPathClearingFlow(string key, string detail)
    {
        string normalizedDetail = NormalizePathClearingFlowMessage(key, detail);
        activityDebug?.LogPathFlow(this, enableActivityDebug, key, normalizedDetail);
    }

    private void LogPickupDebug(string detail)
    {
        if (!enablePickupDebug || string.IsNullOrWhiteSpace(detail))
        {
            return;
        }

        Debug.Log($"[BotPickup] [{name}] {detail}", this);
    }

    private static string FormatFlowVectorKey(Vector3 value)
    {
        return $"{Mathf.RoundToInt(value.x * 10f)}:{Mathf.RoundToInt(value.y * 10f)}:{Mathf.RoundToInt(value.z * 10f)}";
    }

    private static string NormalizePathClearingFlowMessage(string key, string detail)
    {
        if (string.IsNullOrEmpty(key))
        {
            return detail;
        }

        if (key.StartsWith("create-path:"))
        {
            return null;
        }

        if (key.StartsWith("move-destination:"))
        {
            int vectorIndex = detail.IndexOf('(');
            return vectorIndex >= 0
                ? $"Received Move order to {detail.Substring(vectorIndex)}"
                : "Received Move order.";
        }

        if (key.StartsWith("move-start:") || key.StartsWith("move-breaktool:"))
        {
            return "Moving.";
        }

        if (key.StartsWith("sensor-blocker"))
        {
            return "Blocker detected.";
        }

        if (key.StartsWith("candidate-breaktool:"))
        {
            string toolName = TryGetFlowKeyName(key, "candidate-breaktool:");
            return string.IsNullOrEmpty(toolName) ? "Searching for breaching tool." : $"Searching for {toolName}.";
        }

        if (key.StartsWith("discard-breaktool:") || key.StartsWith("discard-breaktool-route:") || key.StartsWith("discard-breaktool-move:"))
        {
            string toolName =
                TryGetFlowKeyName(key, "discard-breaktool:") ??
                TryGetFlowKeyName(key, "discard-breaktool-route:") ??
                TryGetFlowKeyName(key, "discard-breaktool-move:");
            return string.IsNullOrEmpty(toolName) ? "Discarding breaching tool." : $"Discarding {toolName}.";
        }

        if (key.StartsWith("retry-breaktool"))
        {
            return "Searching for another breaching tool.";
        }

        if (key.StartsWith("no-break-tool:"))
        {
            return "No usable tool available.";
        }

        if (key.StartsWith("stop-breaktool:"))
        {
            return "Stopped.";
        }

        return detail;
    }

    private static string TryGetFlowKeyName(string key, string prefix)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(prefix) || !key.StartsWith(prefix))
        {
            return null;
        }

        int nameStartIndex = prefix.Length;
        int nameEndIndex = key.IndexOf(':', nameStartIndex);
        if (nameEndIndex < 0)
        {
            return key.Substring(nameStartIndex);
        }

        return key.Substring(nameStartIndex, nameEndIndex - nameStartIndex);
    }

    private void UpdatePathClearingDebugStage(PathClearingDebugStage stage, string detail)
    {
        int stageValue = (int)stage;
        if (activityDebug == null || !activityDebug.TryUpdatePathClearingStage(stageValue))
        {
            return;
        }
    }

    private void OnGUI()
    {
        if (!showCommandPlanOverlay || !ShouldDrawCommandPlanOverlay())
        {
            return;
        }

        Camera targetCamera = Camera.main;
        if (targetCamera == null)
        {
            return;
        }

        Vector3 anchorWorldPosition = transform.position + Vector3.up * 2.1f;
        Vector3 screenPoint = targetCamera.WorldToScreenPoint(anchorWorldPosition);
        if (screenPoint.z <= 0f)
        {
            return;
        }

        EnsureCommandPlanOverlayStyles();
        BuildCommandPlanOverlayLines();
        if (commandPlanDebugLines.Count == 0)
        {
            return;
        }

        float lineHeight = commandPlanOverlayLabelStyle.lineHeight > 0f
            ? commandPlanOverlayLabelStyle.lineHeight
            : 16f;
        float overlayHeight = 12f + (commandPlanDebugLines.Count * lineHeight);
        Rect overlayRect = new Rect(
            screenPoint.x + commandPlanOverlayScreenOffset.x,
            Screen.height - screenPoint.y + commandPlanOverlayScreenOffset.y,
            Mathf.Max(180f, commandPlanOverlayWidth),
            overlayHeight);

        GUI.Box(overlayRect, GUIContent.none, commandPlanOverlayBoxStyle);

        Rect labelRect = new Rect(
            overlayRect.x + 8f,
            overlayRect.y + 6f,
            overlayRect.width - 16f,
            overlayRect.height - 12f);
        string overlayText = string.Join("\n", commandPlanDebugLines);
        GUI.Label(labelRect, overlayText, commandPlanOverlayLabelStyle);
    }

    private bool ShouldDrawCommandPlanOverlay()
    {
        if (!isActiveAndEnabled)
        {
            return false;
        }

        if (planProcessor != null && planProcessor.HasActivePlan)
        {
            return true;
        }

        if (behaviorContext == null)
        {
            return false;
        }

        return behaviorContext.HasExtinguishOrder ||
               behaviorContext.HasRescueOrder ||
               behaviorContext.HasFollowOrder ||
               behaviorContext.HasMoveOrder ||
               IsBreachCommandActive() ||
               IsHazardIsolationCommandActive() ||
               HasMovePickupTarget;
    }

    private void BuildCommandPlanOverlayLines()
    {
        commandPlanDebugLines.Clear();

        string activeCommand = "None";
        if (behaviorContext != null && behaviorContext.TryGetCommandIntentSnapshot(out BotCommandIntentPayload payload))
        {
            activeCommand = payload.CommandType.ToString();
        }

        commandPlanDebugLines.Add($"{name}");
        commandPlanDebugLines.Add($"Command: {activeCommand}");

        string activePlanName = planProcessor != null && !string.IsNullOrWhiteSpace(planProcessor.ActivePlanName)
            ? planProcessor.ActivePlanName
            : "(none)";
        commandPlanDebugLines.Add($"Plan: {activePlanName}");

        string currentTaskName = planProcessor != null && !string.IsNullOrWhiteSpace(planProcessor.CurrentTaskName)
            ? planProcessor.CurrentTaskName
            : "(idle)";
        commandPlanDebugLines.Add($"Current: {currentTaskName}");

        if (currentTask != null && currentTask.HasTask)
        {
            string runtimeTaskLine = $"Runtime Task: {currentTask.TaskType}";
            if (!string.IsNullOrWhiteSpace(currentTask.Detail))
            {
                runtimeTaskLine += $" | {currentTask.Detail}";
            }

            commandPlanDebugLines.Add(runtimeTaskLine);
        }

        string runtimeInterrupt = GetRuntimeInterruptDebugLine();
        if (!string.IsNullOrWhiteSpace(runtimeInterrupt))
        {
            commandPlanDebugLines.Add($"Interrupt: {runtimeInterrupt}");
        }

        if (!string.IsNullOrWhiteSpace(activeCommandPlanKey))
        {
            commandPlanDebugLines.Add($"Key: {activeCommandPlanKey}");
        }

        if (planProcessor == null || planProcessor.PendingTaskCount <= 0)
        {
            commandPlanDebugLines.Add("Queue: (empty)");
            return;
        }

        int maxQueuedTasks = Mathf.Max(1, commandPlanOverlayMaxQueuedTasks);
        commandPlanPendingTaskNames.Clear();
        planProcessor.CopyPendingTaskNames(commandPlanPendingTaskNames, maxQueuedTasks);
        int copiedCount = commandPlanPendingTaskNames.Count;

        commandPlanDebugLines.Insert(4, "Queue:");
        for (int i = 0; i < copiedCount; i++)
        {
            commandPlanDebugLines.Add($" - {commandPlanPendingTaskNames[i]}");
        }

        int remainingCount = planProcessor.PendingTaskCount - copiedCount;
        if (remainingCount > 0)
        {
            commandPlanDebugLines.Add($" - ... (+{remainingCount})");
        }
    }

    private string GetRuntimeInterruptDebugLine()
    {
        string safeMovementDebugLine = GetSafeMovementDebugLine();
        if (!string.IsNullOrWhiteSpace(safeMovementDebugLine))
        {
            return safeMovementDebugLine;
        }

        if (IsPathClearingV2Active)
        {
            return $"PathClearV2/{pathClearingV2State.Step} | {GetPathClearingV2TaskDetail()}";
        }

        if (IsRouteFireV2Active)
        {
            return $"RouteFireV2/{routeFireV2State.Step} | {GetRouteFireV2TaskDetail()}";
        }

        if (IsRouteFireClearingActive())
        {
            string fireName = currentRouteBlockingFire != null
                ? GetDebugTargetName(currentRouteBlockingFire)
                : "UnknownFire";
            return $"RouteFire/{currentRouteFirePhase} | {fireName}";
        }

        if (currentBlockedBreakable != null &&
            !currentBlockedBreakable.IsBroken &&
            currentBlockedBreakable.CanBeClearedByBot)
        {
            return $"PathClear/{currentBreakSubtask} | {GetActiveBreakTaskDetail()}";
        }

        if (behaviorContext != null && behaviorContext.HasExtinguishOrder && currentExtinguishSubtask != BotExtinguishSubtask.None)
        {
            return $"Extinguish/{currentExtinguishSubtask} | {GetActiveExtinguishTaskDetail()}";
        }

        if (behaviorContext != null && behaviorContext.HasRescueOrder && currentRescueSubtask != BotRescueSubtask.None)
        {
            return $"Rescue/{currentRescueSubtask} | {GetActiveRescueTaskDetail()}";
        }

        if (behaviorContext != null && behaviorContext.HasRescueOrder && IsRescueV2Active)
        {
            return $"RescueV2/{rescueV2State.Step} | {GetActiveRescueTaskDetail()}";
        }

        return string.Empty;
    }

    private static void EnsureCommandPlanOverlayStyles()
    {
        if (commandPlanOverlayBoxStyle == null)
        {
            commandPlanOverlayBoxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(8, 8, 6, 6)
            };
        }

        if (commandPlanOverlayLabelStyle == null)
        {
            commandPlanOverlayLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                richText = false
            };
        }
    }

    private void OnDrawGizmosSelected()
    {
        DrawSafeMovementGizmo();

        if (drawDestinationGizmo && hasIssuedDestination)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(lastIssuedDestination, 0.3f);
            Gizmos.DrawLine(transform.position, lastIssuedDestination);
        }

        if (!drawAimGizmo)
        {
            return;
        }

        float rayLength = Mathf.Max(0.1f, aimGizmoLength);

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, transform.forward * rayLength);

        if (handAimTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(GetPreciseAimOrigin(), GetPreciseAimForward() * rayLength);
        }

        if (behaviorContext != null && behaviorContext.HasFollowOrder && followTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, followTarget.position);
            Gizmos.DrawWireSphere(followTarget.position, 0.2f);
        }

        if (behaviorContext != null && behaviorContext.HasRescueOrder && currentRescueTarget != null)
        {
            Vector3 rescuePosition = currentRescueTarget.GetWorldPosition();
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, rescuePosition);
            Gizmos.DrawWireSphere(rescuePosition, 0.2f);

            if (currentSafeZoneTarget != null)
            {
                Vector3 safeZonePosition = currentSafeZoneTarget.GetWorldPosition();
                Gizmos.color = Color.white;
                Gizmos.DrawLine(rescuePosition, safeZonePosition);
                Gizmos.DrawWireSphere(safeZonePosition, 0.25f);
            }
        }

        if (behaviorContext != null && behaviorContext.HasExtinguishOrder)
        {
            if (hasCurrentExtinguishTargetPosition)
            {
                Vector3 firePosition = currentExtinguishTargetPosition;
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, firePosition);
                Gizmos.DrawWireSphere(firePosition, 0.2f);
            }

            if (hasCurrentExtinguishAimPoint)
            {
                Vector3 aimOrigin = preferredExtinguishTool != null && preferredExtinguishTool.RequiresPreciseAim
                    ? GetPreciseAimOrigin()
                    : transform.position;
                Gizmos.color = new Color(1f, 0.5f, 0f);
                Gizmos.DrawLine(aimOrigin, currentExtinguishAimPoint);
                Gizmos.DrawWireSphere(currentExtinguishAimPoint, 0.15f);
            }

            if (currentExtinguishTrajectoryPointCount >= 2)
            {
                Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.9f);
                for (int i = 1; i < currentExtinguishTrajectoryPointCount; i++)
                {
                    Gizmos.DrawLine(currentExtinguishTrajectoryPoints[i - 1], currentExtinguishTrajectoryPoints[i]);
                }
            }
            else if (preferredExtinguishTool != null && preferredExtinguishTool.RequiresPreciseAim)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(GetPreciseAimOrigin(), GetPreciseAimForward() * rayLength);
            }
        }
    }
}
