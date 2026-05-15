using UnityEngine;

public sealed partial class FireSimulationManager
{
    private bool TickSimulation(float deltaTime)
    {
        bool changed = false;

        // Phase 1: decay suppression timers only on nodes that currently need it.
        for (int i = recoveryTimerNodeIndices.Count - 1; i >= 0; i--)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(recoveryTimerNodeIndices[i]);
            if (node == null || node.IsRemoved)
            {
                continue;
            }

            if (TickNode(node, deltaTime))
            {
                MarkNodeDirty(node);
                changed = true;
            }
        }

        // Phase 1b: queue spread only from burning nodes that still have valid targets.
        for (int i = activeSpreadNodeIndices.Count - 1; i >= 0; i--)
        {
            int nodeIndex = activeSpreadNodeIndices[i];
            FireRuntimeNode source = runtimeGraph.GetNode(nodeIndex);
            if (!ShouldKeepNodeInSpreadPool(source))
            {
                RemoveNodeFromSpreadPool(nodeIndex);
                continue;
            }

            SpreadHeatToNeighbors(source, deltaTime);
            if (!ShouldKeepNodeInSpreadPool(source))
            {
                RemoveNodeFromSpreadPool(nodeIndex);
            }
        }

        BeginProcessingDirtyNodes();

        // Phase 2: apply queued heat deltas, clamp, mark saturation, remove if extinguished.
        float maxHeat = simulationProfile != null ? simulationProfile.MaxHeat : 2f;
        for (int i = 0; i < processingNodeIndices.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(processingNodeIndices[i]);
            if (node == null || node.IsRemoved)
            {
                continue;
            }

            if (!Mathf.Approximately(node.PendingHeatDelta, 0f))
            {
                node.Heat = Mathf.Max(0f, node.Heat + node.PendingHeatDelta);
                node.PendingHeatDelta = 0f;
                changed = true;
            }

            if (node.Heat >= maxHeat)
            {
                node.Heat = maxHeat;
                node.HasReachedSpreadSaturation = true;
            }

            if (node.IsBurning)
            {
                node.HasEverBurned = true;
            }

            // Snap residual heat to 0 while a node is still in its
            // suppression recovery window. The guard prevents ramp-up via slow
            // neighbour spread from being killed off. Using IgnitionThreshold
            // (instead of the much smaller extinguishThreshold) ensures that
            // nodes suppressed to e.g. 0.5 heat are properly cleaned up before
            // the timer expires, rather than lingering as zombie nodes that are
            // not burning but never removed.
            if (node.SuppressionRecoveryTimer > 0f &&
                node.Heat > 0f &&
                node.Heat < node.IgnitionThreshold)
            {
                node.Heat = 0f;
            }

            // Only remove nodes that were actually engaged in the incident — Late
            // nodes that never got touched by spread keep Heat=0 forever and must
            // remain in the graph so neighbours can still ignite them later.
            if (node.IsTrackedByIncident && node.HasEverBurned && node.Heat <= 0.0001f)
            {
                RemoveRuntimeNode(node);
                changed = true;
            }

            RefreshNodeRuntimeMembership(node);
        }

        ClearDirtyNodes();
        UpdateSimulationSleepState();
        return changed;
    }

    private bool TickNode(FireRuntimeNode node, float deltaTime)
    {
        if (node == null || node.IsRemoved)
        {
            return false;
        }

        float previousSuppressionTimer = node.SuppressionRecoveryTimer;
        node.SuppressionRecoveryTimer = Mathf.Max(0f, node.SuppressionRecoveryTimer - deltaTime);
        return !Mathf.Approximately(previousSuppressionTimer, node.SuppressionRecoveryTimer);
    }

    private void SpreadHeatToNeighbors(FireRuntimeNode source, float deltaTime)
    {
        if (source == null || source.IsRemoved || simulationProfile == null)
        {
            return;
        }

        float transferPerSecond = simulationProfile.NeighborHeatTransferPerSecond;
        if (transferPerSecond <= 0f)
        {
            return;
        }

        for (int i = 0; i < source.NeighborIndices.Count; i++)
        {
            FireRuntimeNode target = runtimeGraph.GetNode(source.NeighborIndices[i]);
            if (target == null || target.IsRemoved)
            {
                continue;
            }

            // Saturated nodes never accept spread again, even if subsequently cooled.
            if (target.HasReachedSpreadSaturation)
            {
                continue;
            }

            // While a node is recovering from active suppression it cannot reignite
            // from neighbours.
            if (target.SuppressionRecoveryTimer > 0f)
            {
                continue;
            }

            // Propagate incident-tracking so Late nodes lit via spread are counted
            // as part of the active incident.
            if (source.IsTrackedByIncident)
            {
                target.IsTrackedByIncident = true;
                if (target.IncidentNodeKind != FireIncidentNodeKind.Late)
                {
                    target.HazardType = activeIncidentHazardType;
                }
            }

            float spreadHeat = source.Heat * transferPerSecond * deltaTime;
            float nextPendingHeat = Mathf.Max(target.PendingHeatDelta, spreadHeat);
            if (!Mathf.Approximately(nextPendingHeat, target.PendingHeatDelta))
            {
                target.PendingHeatDelta = nextPendingHeat;
                MarkNodeDirty(target);
            }
        }
    }

    private void RefreshAllRuntimePools()
    {
        activeSpreadNodeIndices.Clear();
        activeSpreadNodeIndexLookup.Clear();
        burningTrackedNodeIndices.Clear();
        burningTrackedNodeIndexLookup.Clear();
        recoveryTimerNodeIndices.Clear();
        recoveryTimerNodeIndexLookup.Clear();
        visualActiveNodeIndices.Clear();
        visualActiveNodeIndexLookup.Clear();

        if (runtimeGraph == null)
        {
            UpdateRuntimeDebugCounts();
            SyncActiveSpreadDebugEntries();
            simulationSleeping = true;
            return;
        }

        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            RefreshNodeRuntimeMembership(runtimeGraph.GetNode(i));
        }

        UpdateRuntimeDebugCounts();
        SyncActiveSpreadDebugEntries();
        UpdateSimulationSleepState();
    }

    private void RefreshNodeRuntimeMembership(FireRuntimeNode node)
    {
        if (node == null)
        {
            return;
        }

        if (ShouldKeepNodeInSpreadPool(node))
        {
            AddNodeToSpreadPool(node.Index);
            return;
        }

        RemoveNodeFromSpreadPool(node.Index);

        if (node.IsTrackedByIncident && node.IsBurning && !node.IsRemoved)
        {
            AddNodeToBurningTrackedPool(node.Index);
        }
        else
        {
            RemoveNodeFromBurningTrackedPool(node.Index);
        }

        if (!node.IsRemoved && node.SuppressionRecoveryTimer > 0f)
        {
            AddNodeToRecoveryTimerPool(node.Index);
        }
        else
        {
            RemoveNodeFromRecoveryTimerPool(node.Index);
        }

        if (ShouldRenderNodeEffect(node))
        {
            AddNodeToVisualActivePool(node.Index);
        }
        else
        {
            RemoveNodeFromVisualActivePool(node.Index);
        }
    }

    private bool ShouldKeepNodeInSpreadPool(FireRuntimeNode node)
    {
        if (node == null || node.IsRemoved || !node.IsBurning)
        {
            return false;
        }

        return HasSpreadReceivableNeighbor(node);
    }

    private bool HasSpreadReceivableNeighbor(FireRuntimeNode source)
    {
        if (source == null || runtimeGraph == null)
        {
            return false;
        }

        for (int i = 0; i < source.NeighborIndices.Count; i++)
        {
            FireRuntimeNode target = runtimeGraph.GetNode(source.NeighborIndices[i]);
            if (CanReceiveSpread(target))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanReceiveSpread(FireRuntimeNode node)
    {
        return node != null &&
            !node.IsRemoved &&
            !node.HasReachedSpreadSaturation &&
            node.SuppressionRecoveryTimer <= 0f;
    }

    private void AddNodeToSpreadPool(int nodeIndex)
    {
        if (nodeIndex < 0 || !activeSpreadNodeIndexLookup.Add(nodeIndex))
        {
            return;
        }

        activeSpreadNodeIndices.Add(nodeIndex);
        UpdateRuntimeDebugCounts();
        SyncActiveSpreadDebugEntries();
    }

    private void RemoveNodeFromSpreadPool(int nodeIndex)
    {
        if (nodeIndex < 0 || !activeSpreadNodeIndexLookup.Remove(nodeIndex))
        {
            return;
        }

        activeSpreadNodeIndices.Remove(nodeIndex);
        UpdateRuntimeDebugCounts();
        SyncActiveSpreadDebugEntries();
    }

    private void AddNodeToBurningTrackedPool(int nodeIndex)
    {
        if (nodeIndex >= 0 && burningTrackedNodeIndexLookup.Add(nodeIndex))
        {
            burningTrackedNodeIndices.Add(nodeIndex);
            UpdateRuntimeDebugCounts();
        }
    }

    private void RemoveNodeFromBurningTrackedPool(int nodeIndex)
    {
        if (nodeIndex >= 0 && burningTrackedNodeIndexLookup.Remove(nodeIndex))
        {
            burningTrackedNodeIndices.Remove(nodeIndex);
            UpdateRuntimeDebugCounts();
        }
    }

    private void AddNodeToRecoveryTimerPool(int nodeIndex)
    {
        if (nodeIndex >= 0 && recoveryTimerNodeIndexLookup.Add(nodeIndex))
        {
            recoveryTimerNodeIndices.Add(nodeIndex);
            UpdateRuntimeDebugCounts();
        }
    }

    private void RemoveNodeFromRecoveryTimerPool(int nodeIndex)
    {
        if (nodeIndex >= 0 && recoveryTimerNodeIndexLookup.Remove(nodeIndex))
        {
            recoveryTimerNodeIndices.Remove(nodeIndex);
            UpdateRuntimeDebugCounts();
        }
    }

    private void AddNodeToVisualActivePool(int nodeIndex)
    {
        if (nodeIndex >= 0 && visualActiveNodeIndexLookup.Add(nodeIndex))
        {
            visualActiveNodeIndices.Add(nodeIndex);
            UpdateRuntimeDebugCounts();
        }
    }

    private void RemoveNodeFromVisualActivePool(int nodeIndex)
    {
        if (nodeIndex >= 0 && visualActiveNodeIndexLookup.Remove(nodeIndex))
        {
            visualActiveNodeIndices.Remove(nodeIndex);
            UpdateRuntimeDebugCounts();
        }
    }

    private void WakeSimulation()
    {
        simulationSleeping = false;
    }

    private void UpdateSimulationSleepState()
    {
        simulationSleeping = activeSpreadNodeIndices.Count == 0 && recoveryTimerNodeIndices.Count == 0;
    }

    private void UpdateRuntimeDebugCounts()
    {
        debugActiveSpreadNodeCount = activeSpreadNodeIndices.Count;
        debugBurningTrackedNodeCount = burningTrackedNodeIndices.Count;
        debugRecoveryTimerNodeCount = recoveryTimerNodeIndices.Count;
        debugVisualActiveNodeCount = visualActiveNodeIndices.Count;
    }

    private void SyncActiveSpreadDebugEntries()
    {
        activeSpreadNodeDebugEntries.Clear();
        if (runtimeGraph == null)
        {
            return;
        }

        for (int i = 0; i < activeSpreadNodeIndices.Count; i++)
        {
            int nodeIndex = activeSpreadNodeIndices[i];
            FireRuntimeNode node = runtimeGraph.GetNode(nodeIndex);
            if (node == null)
            {
                activeSpreadNodeDebugEntries.Add($"[{nodeIndex}] <missing>");
                continue;
            }

            string nodeId = node.Authoring != null ? node.Authoring.NodeId : $"Node{nodeIndex}";
            activeSpreadNodeDebugEntries.Add(
                $"[{nodeIndex}] {nodeId} | Heat={node.Heat:0.00}/{node.IgnitionThreshold:0.00} | Neighbors={node.NeighborIndices.Count}");
        }
    }
}
