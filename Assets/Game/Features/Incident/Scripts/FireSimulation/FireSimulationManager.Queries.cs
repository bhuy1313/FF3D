using System.Collections.Generic;
using UnityEngine;

public sealed partial class FireSimulationManager
{
    public int GetTrackedNodeCount()
    {
        return CountNodes(node => node != null && node.IsTrackedByIncident && !node.IsRemoved);
    }

    public int GetHazardLinkedNodeCount()
    {
        return CountNodes(node => node != null && node.IsTrackedByIncident && !node.IsRemoved &&
            node.IncidentNodeKind != FireIncidentNodeKind.Late);
    }

    public int GetHazardLinkedBurningNodeCount()
    {
        return CountNodes(node => node != null && node.IsTrackedByIncident && node.IsBurning &&
            node.IncidentNodeKind != FireIncidentNodeKind.Late);
    }

    public void GetHazardLinkedNodes(List<FireRuntimeNode> buffer)
    {
        if (buffer == null)
        {
            return;
        }

        buffer.Clear();
        if (!initialized || runtimeGraph == null)
        {
            return;
        }

        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null || node.IsRemoved || !node.IsTrackedByIncident)
            {
                continue;
            }

            if (node.IncidentNodeKind == FireIncidentNodeKind.Late)
            {
                continue;
            }

            buffer.Add(node);
        }
    }

    public int GetExtinguishedTrackedNodeCount()
    {
        return CountNodes(node => node != null && node.IsTrackedByIncident && !node.IsBurning);
    }

    public int GetBurningTrackedNodeCount()
    {
        return CountNodes(node => node != null && node.IsTrackedByIncident && node.IsBurning);
    }

    public int GetBurningTrackedNodeCount(Bounds bounds)
    {
        return CountNodes(node => node != null && node.IsTrackedByIncident && node.IsBurning && bounds.Contains(node.Position));
    }

    public float GetBurningTrackedIntensitySum(Bounds bounds)
    {
        if (!initialized || runtimeGraph == null)
        {
            return 0f;
        }

        float intensitySum = 0f;
        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null || !node.IsTrackedByIncident || !node.IsBurning || !bounds.Contains(node.Position))
            {
                continue;
            }

            intensitySum += Mathf.Clamp01(node.Heat / Mathf.Max(0.01f, node.IgnitionThreshold));
        }

        return intensitySum;
    }

    public float GetNodeVisualIntensity01(int nodeIndex)
    {
        if (!initialized || runtimeGraph == null)
        {
            return 0f;
        }

        return GetNodeVisualIntensity01(runtimeGraph.GetNode(nodeIndex));
    }

    public bool IsNodeVisualActive(int nodeIndex)
    {
        if (!initialized || runtimeGraph == null)
        {
            return false;
        }

        return ShouldRenderNodeEffect(runtimeGraph.GetNode(nodeIndex));
    }

    private float GetNodeVisualIntensity01(FireRuntimeNode node)
    {
        if (node == null || node.IsRemoved)
        {
            return 0f;
        }

        return Mathf.InverseLerp(0f, Mathf.Max(0.01f, GetSpreadSaturationHeat(node)), node.Heat);
    }

    private bool ShouldRenderNodeEffect(FireRuntimeNode node)
    {
        if (node == null || node.IsRemoved || !node.IsTrackedByIncident)
        {
            return false;
        }

        float threshold = simulationProfile != null ? simulationProfile.VisualHeatThreshold : 0.01f;
        return node.Heat > threshold;
    }

    private float GetSpreadSaturationHeat(FireRuntimeNode node)
    {
        if (node == null)
        {
            return float.PositiveInfinity;
        }

        float maxHeat = simulationProfile != null ? simulationProfile.MaxHeat : 2f;
        return Mathf.Max(node.IgnitionThreshold, maxHeat);
    }

    private void RemoveRuntimeNode(FireRuntimeNode node)
    {
        if (node == null)
        {
            return;
        }

        node.IsRemoved = true;
        node.IsTrackedByIncident = false;
        node.Heat = 0f;
        node.PendingHeatDelta = 0f;
        node.SuppressionRecoveryTimer = 0f;
    }

    private int FindClosestNodeIndex(Vector3 worldPosition)
    {
        int bestIndex = -1;
        float bestDistanceSqr = float.PositiveInfinity;
        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null)
            {
                continue;
            }

            float distanceSqr = (node.Position - worldPosition).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            bestIndex = i;
        }

        return bestIndex;
    }

    private int CountNodes(System.Predicate<FireRuntimeNode> predicate)
    {
        if (!initialized || runtimeGraph == null || predicate == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node != null && !node.IsRemoved && predicate(node))
            {
                count++;
            }
        }

        return count;
    }

    private void LogNodeHeatProgress()
    {
        if (!initialized || runtimeGraph == null || runtimeGraph.Count <= 0)
        {
            Debug.Log($"{nameof(FireSimulationManager)} '{name}': runtime graph not initialized.", this);
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(nameof(FireSimulationManager))
            .Append(" '")
            .Append(name)
            .Append("' node heat:");

        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null)
            {
                continue;
            }

            string nodeId = node.Authoring != null ? node.Authoring.NodeId : $"Node{i}";
            sb.Append(" [")
                .Append(nodeId)
                .Append(": heat=")
                .Append(node.Heat.ToString("0.000"))
                .Append('/')
                .Append(node.IgnitionThreshold.ToString("0.000"))
                .Append(", burning=")
                .Append(node.IsBurning ? 'Y' : 'N')
                .Append(']');
        }

        Debug.Log(sb.ToString(), this);
    }

    private void LogRuntimeGraphTopology()
    {
        if (!initialized || runtimeGraph == null || runtimeGraph.Count <= 0)
        {
            Debug.Log($"{nameof(FireSimulationManager)} '{name}': no runtime graph topology to log.", this);
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(nameof(FireSimulationManager))
            .Append(" '")
            .Append(name)
            .Append("' graph topology:");

        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null)
            {
                continue;
            }

            string nodeId = node.Authoring != null ? node.Authoring.NodeId : $"Node{i}";
            sb.Append(" [")
                .Append(nodeId)
                .Append(" -> ");

            if (node.NeighborIndices.Count <= 0)
            {
                sb.Append("none");
            }
            else
            {
                for (int neighborListIndex = 0; neighborListIndex < node.NeighborIndices.Count; neighborListIndex++)
                {
                    if (neighborListIndex > 0)
                    {
                        sb.Append(", ");
                    }

                    FireRuntimeNode neighbor = runtimeGraph.GetNode(node.NeighborIndices[neighborListIndex]);
                    string neighborId = neighbor != null && neighbor.Authoring != null
                        ? neighbor.Authoring.NodeId
                        : $"Node{node.NeighborIndices[neighborListIndex]}";
                    sb.Append(neighborId);
                }
            }

            sb.Append(']');
        }

        Debug.Log(sb.ToString(), this);
    }
}
