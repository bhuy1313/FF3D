using UnityEngine;

public sealed partial class FireSimulationManager
{
    private bool TickSimulation(float deltaTime)
    {
        bool changed = false;
        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null || node.IsRemoved)
            {
                continue;
            }

            changed |= TickNode(node, deltaTime);
        }

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

            if (!Mathf.Approximately(node.PendingWetnessDelta, 0f))
            {
                node.Wetness = Mathf.Max(0f, node.Wetness + node.PendingWetnessDelta);
                node.PendingWetnessDelta = 0f;
                changed = true;
            }

            if (node.RemainingFuel <= 0f)
            {
                node.Heat = Mathf.Min(node.Heat, simulationProfile.BurnedOutHeatRetention);
            }
            else if (node.Heat < simulationProfile.ExtinguishThreshold)
            {
                node.Heat = 0f;
            }

            if (simulationProfile != null && node.Heat >= GetSpreadSaturationHeat(node))
            {
                node.HasReachedSpreadSaturation = true;
            }

            if (ShouldRemoveNodeAtZeroHeat(node))
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

        float previousHeat = node.Heat;
        float previousWetness = node.Wetness;
        float previousFuel = node.RemainingFuel;
        float previousSuppressionTimer = node.SuppressionRecoveryTimer;
        node.Wetness = Mathf.Max(0f, node.Wetness - simulationProfile.WetnessRecoveryPerSecond * deltaTime);
        node.SuppressionRecoveryTimer = Mathf.Max(0f, node.SuppressionRecoveryTimer - deltaTime);
        node.Heat = Mathf.Max(0f, node.Heat - simulationProfile.AmbientCoolingPerSecond * deltaTime);
        node.Heat = Mathf.Max(0f, node.Heat - node.Wetness * simulationProfile.WetnessCoolingPerSecond * deltaTime);

        if (node.IsBurning)
        {
            node.RemainingFuel = Mathf.Max(0f, node.RemainingFuel - simulationProfile.FuelBurnPerSecond * deltaTime);
            SpreadHeatToNeighbors(node, deltaTime);
        }

        return
            !Mathf.Approximately(previousHeat, node.Heat) ||
            !Mathf.Approximately(previousWetness, node.Wetness) ||
            !Mathf.Approximately(previousFuel, node.RemainingFuel) ||
            !Mathf.Approximately(previousSuppressionTimer, node.SuppressionRecoveryTimer);
    }

    private void SpreadHeatToNeighbors(FireRuntimeNode source, float deltaTime)
    {
        if (source == null || source.IsRemoved)
        {
            return;
        }

        for (int i = 0; i < source.NeighborIndices.Count; i++)
        {
            FireRuntimeNode target = runtimeGraph.GetNode(source.NeighborIndices[i]);
            if (target == null || target.IsRemoved || target.RemainingFuel <= 0f)
            {
                continue;
            }

            if (simulationProfile != null)
            {
                if (simulationProfile.StopReceivingSpreadAfterSaturation && target.HasReachedSpreadSaturation)
                {
                    continue;
                }

                if (simulationProfile.BlockIncomingSpreadDuringSuppressionRecovery && target.SuppressionRecoveryTimer > 0f)
                {
                    continue;
                }
            }

            if (source.IsTrackedByIncident)
            {
                target.IsTrackedByIncident = true;
                if (target.IncidentNodeKind != FireIncidentNodeKind.Late)
                {
                    target.HazardType = activeIncidentHazardType;
                }
            }

            float heatTransfer = source.Heat * simulationProfile.NeighborHeatTransferPerSecond * deltaTime;
            heatTransfer *= 1f - target.SpreadResistance;
            if (target.SuppressionRecoveryTimer > 0f)
            {
                heatTransfer *= simulationProfile.SuppressionRecoveryHeatMultiplier;
            }
            heatTransfer *= source.SurfaceType == target.SurfaceType
                ? simulationProfile.SameSurfaceTransferMultiplier
                : simulationProfile.CrossSurfaceTransferMultiplier;

            float verticalAlignment = Vector3.Dot(source.SurfaceNormal, target.SurfaceNormal);
            heatTransfer *= Mathf.Lerp(simulationProfile.VerticalSpreadBias, 1f, Mathf.InverseLerp(-1f, 1f, verticalAlignment));
            target.PendingHeatDelta += heatTransfer;
        }
    }
}
