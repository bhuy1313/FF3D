using System.Collections.Generic;
using UnityEngine;

internal sealed class BotActivityDebug
{
    private int lastExtinguishDebugStage = -1;
    private int lastPathClearingDebugStage = -1;
    private string lastPathClearingFlowKey;
    private string lastPathClearingFlowMessage;
    private string lastRescueActivityKey;
    private string lastRescueActivityMessage;
    private readonly HashSet<string> seenPathActivityMessages = new HashSet<string>();
    private readonly HashSet<string> seenRescueActivityMessages = new HashSet<string>();
    private readonly HashSet<string> seenExtinguishActivityMessages = new HashSet<string>();
    private readonly HashSet<string> seenExtinguishV2ActivityMessages = new HashSet<string>();
    private string lastMoveCommandFlowKey;
    private string lastMoveStartFlowKey;

    internal bool HasExtinguishDebugStage => lastExtinguishDebugStage != -1;
    internal bool HasRescueActivity => !string.IsNullOrEmpty(lastRescueActivityKey) || !string.IsNullOrEmpty(lastRescueActivityMessage);

    internal bool TryUpdateExtinguishStage(int stageValue)
    {
        if (lastExtinguishDebugStage == stageValue)
        {
            return false;
        }

        lastExtinguishDebugStage = stageValue;
        return true;
    }

    internal void ResetExtinguish()
    {
        lastExtinguishDebugStage = -1;
        seenExtinguishActivityMessages.Clear();
        seenExtinguishV2ActivityMessages.Clear();
    }

    internal bool TryUpdatePathClearingStage(int stageValue)
    {
        if (lastPathClearingDebugStage == stageValue)
        {
            return false;
        }

        lastPathClearingDebugStage = stageValue;
        return true;
    }

    internal void ResetPathClearing()
    {
        lastPathClearingDebugStage = -1;
        lastPathClearingFlowKey = null;
        lastPathClearingFlowMessage = null;
        seenPathActivityMessages.Clear();
    }

    internal void ResetMovePathFlow()
    {
        lastMoveCommandFlowKey = null;
        lastMoveStartFlowKey = null;
        lastPathClearingFlowKey = null;
        lastPathClearingFlowMessage = null;
        seenPathActivityMessages.Clear();
    }

    internal void ResetRescue()
    {
        lastRescueActivityKey = null;
        lastRescueActivityMessage = null;
        seenRescueActivityMessages.Clear();
    }

    internal void LogExtinguish(MonoBehaviour owner, bool enabled, string normalizedDetail)
    {
        if (!enabled || string.IsNullOrEmpty(normalizedDetail))
        {
            return;
        }

        if (!seenExtinguishActivityMessages.Add(normalizedDetail))
        {
            return;
        }

        Debug.Log($"[BotExtinguish] [{owner.name}] {normalizedDetail}", owner);
    }

    internal void LogExtinguishV2(MonoBehaviour owner, string detail)
    {
        if (string.IsNullOrEmpty(detail))
        {
            return;
        }

        if (!seenExtinguishV2ActivityMessages.Add(detail))
        {
            return;
        }

        Debug.Log($"[BotExtinguishV2] [{owner.name}] {detail}", owner);
    }

    internal void LogRescue(MonoBehaviour owner, bool enabled, string key, string detail)
    {
        if (!enabled || string.IsNullOrEmpty(detail))
        {
            return;
        }

        if (!seenRescueActivityMessages.Add(detail))
        {
            lastRescueActivityKey = key;
            lastRescueActivityMessage = detail;
            return;
        }

        lastRescueActivityKey = key;
        lastRescueActivityMessage = detail;
        Debug.Log($"[BotRescue] [{owner.name}] {detail}", owner);
    }

    internal void LogPathFlow(MonoBehaviour owner, bool enabled, string key, string normalizedDetail)
    {
        if (!enabled || string.IsNullOrEmpty(normalizedDetail))
        {
            return;
        }

        if (lastPathClearingFlowKey == key)
        {
            return;
        }

        if (!seenPathActivityMessages.Add(normalizedDetail))
        {
            lastPathClearingFlowKey = key;
            lastPathClearingFlowMessage = normalizedDetail;
            return;
        }

        if (key.StartsWith("move-destination:"))
        {
            lastMoveCommandFlowKey = key;
        }

        if (key.StartsWith("move-start:"))
        {
            lastMoveStartFlowKey = key;
        }

        lastPathClearingFlowKey = key;
        lastPathClearingFlowMessage = normalizedDetail;
        Debug.Log($"[BotPathFlow] [{owner.name}] {normalizedDetail}", owner);
    }
}
