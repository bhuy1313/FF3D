using UnityEngine;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
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

        if (inventorySystem != null && inventorySystem.EquippedRoot != null)
        {
            viewPoint = inventorySystem.EquippedRoot;
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

    private void OnDrawGizmosSelected()
    {
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

        if (viewPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(viewPoint.position, viewPoint.forward * rayLength);
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
                Vector3 aimOrigin = viewPoint != null ? viewPoint.position : transform.position;
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
                Gizmos.DrawRay(viewPoint != null ? viewPoint.position : transform.position, (viewPoint != null ? viewPoint.forward : transform.forward) * rayLength);
            }
        }
    }
}
