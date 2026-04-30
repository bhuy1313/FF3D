using UnityEngine;

public sealed partial class FireSimulationManager
{
    private bool TickSimulation(float deltaTime)
    {
        bool changed = false;

        // Phase 1: decay suppression timers and queue spread from currently burning nodes.
        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null || node.IsRemoved)
            {
                continue;
            }

            changed |= TickNode(node, deltaTime);
        }

        // Phase 2: apply queued heat deltas, clamp, mark saturation, remove if extinguished.
        float maxHeat = simulationProfile != null ? simulationProfile.MaxHeat : 2f;
        float extinguishThreshold = simulationProfile != null ? simulationProfile.ExtinguishThreshold : 0.08f;

        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
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

            // Snap residual heat to 0 only after active suppression so ramp-up
            // via slow neighbour spread is not constantly killed off. The
            // SuppressionRecoveryTimer is set whenever a player/bot dampens this
            // node, so this branch only fires once the node has actually been
            // worked on.
            if (node.SuppressionRecoveryTimer > 0f &&
                node.Heat > 0f &&
                node.Heat < extinguishThreshold)
            {
                node.Heat = 0f;
            }

            // Only remove nodes that were actually engaged in the incident — Late
            // nodes that never got touched by spread keep Heat=0 forever and must
            // remain in the graph so neighbours can still ignite them later.
            if (node.IsTrackedByIncident && node.Heat <= 0.0001f)
            {
                RemoveRuntimeNode(node);
                changed = true;
            }
        }

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

        if (node.IsBurning)
        {
            SpreadHeatToNeighbors(node, deltaTime);
        }

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
            target.PendingHeatDelta = Mathf.Max(target.PendingHeatDelta, spreadHeat);
        }
    }
}
