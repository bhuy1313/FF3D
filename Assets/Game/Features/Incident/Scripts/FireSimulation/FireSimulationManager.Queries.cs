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
        return initialized && runtimeGraph != null ? burningTrackedNodeIndices.Count : 0;
    }

    public int GetBurningTrackedNodeCount(Bounds bounds)
    {
        return CountNodes(node => node != null && node.IsTrackedByIncident && node.IsBurning && bounds.Contains(node.Position));
    }

    public void GetBurningTrackedStats(Bounds bounds, out int burningCount, out float intensitySum)
    {
        burningCount = 0;
        intensitySum = 0f;

        if (!initialized || runtimeGraph == null)
        {
            return;
        }

        for (int i = 0; i < burningTrackedNodeIndices.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(burningTrackedNodeIndices[i]);
            if (node == null || !bounds.Contains(node.Position))
            {
                continue;
            }

            burningCount++;
            intensitySum += Mathf.Clamp01(node.Heat / Mathf.Max(0.01f, node.IgnitionThreshold));
        }
    }

    public bool HasActiveFire(Bounds bounds)
    {
        return GetBurningTrackedNodeCount(bounds) > 0;
    }

    public void GetBurningNodes(Bounds bounds, List<FireRuntimeNode> buffer)
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

        for (int i = 0; i < burningTrackedNodeIndices.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(burningTrackedNodeIndices[i]);
            if (node == null || !bounds.Contains(node.Position))
            {
                continue;
            }

            buffer.Add(node);
        }
    }

    public float GetBurningTrackedIntensitySum(Bounds bounds)
    {
        GetBurningTrackedStats(bounds, out _, out float intensitySum);
        return intensitySum;
    }

    public float GetAreaIntensity01(Bounds bounds)
    {
        GetBurningTrackedStats(bounds, out int burningCount, out float intensitySum);
        if (burningCount <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01(intensitySum / burningCount);
    }

    public Vector3 GetClosestBurningNodePosition(Bounds bounds, Vector3 fromPosition, Vector3 fallbackPosition)
    {
        if (!initialized || runtimeGraph == null)
        {
            return fallbackPosition;
        }

        float bestDistanceSqr = float.PositiveInfinity;
        Vector3 bestPosition = fallbackPosition;
        bool found = false;

        for (int i = 0; i < burningTrackedNodeIndices.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(burningTrackedNodeIndices[i]);
            if (node == null || !bounds.Contains(node.Position))
            {
                continue;
            }

            float distanceSqr = (node.Position - fromPosition).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            bestPosition = node.Position;
            found = true;
        }

        return found ? bestPosition : fallbackPosition;
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

    public bool IsNearHazardLinkedFireNode(Vector3 center, float radius)
    {
        if (!initialized || runtimeGraph == null || radius <= 0f)
        {
            return false;
        }

        float radiusSqr = radius * radius;
        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null ||
                node.IsRemoved ||
                !node.IsTrackedByIncident ||
                node.IncidentNodeKind == FireIncidentNodeKind.Late)
            {
                continue;
            }

            if ((node.Position - center).sqrMagnitude <= radiusSqr)
            {
                return true;
            }
        }

        return false;
    }

    public int RemoveIncidentNodesInRadius(Vector3 center, float radius)
    {
        if (!initialized || runtimeGraph == null || radius <= 0f)
        {
            return 0;
        }

        float radiusSqr = radius * radius;
        int burningTrackedCount = GetBurningTrackedNodeCount();
        int removedCount = 0;
        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null || node.IsRemoved || !node.IsTrackedByIncident)
            {
                continue;
            }

            if ((node.Position - center).sqrMagnitude > radiusSqr)
            {
                continue;
            }

            if (node.IncidentNodeKind == FireIncidentNodeKind.Primary)
            {
                continue;
            }

            if (node.IsBurning && burningTrackedCount - removedCount <= 1)
            {
                continue;
            }

            RemoveRuntimeNode(node);
            removedCount++;
        }

        if (removedCount > 0)
        {
            surfaceGraph?.SetRuntimeNodeOverrides(runtimeIncidentNodes);
            MarkVisualStateDirty();
            NotifyStateChanged();
        }

        return removedCount;
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
        return node.Heat >= threshold;
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

        RemoveNodeFromSpreadPool(node.Index);
        RemoveNodeFromBurningTrackedPool(node.Index);
        RemoveNodeFromRecoveryTimerPool(node.Index);
        RemoveNodeFromVisualActivePool(node.Index);
        node.IsRemoved = true;
        node.IsTrackedByIncident = false;
        node.Heat = 0f;
        node.PendingHeatDelta = 0f;
        node.SuppressionRecoveryTimer = 0f;
        node.HasEverBurned = false;

        FireSurfaceNodeAuthoring authoring = node.Authoring;
        if (authoring != null && runtimeIncidentNodes.Remove(authoring))
        {
            Destroy(authoring.gameObject);
        }
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
