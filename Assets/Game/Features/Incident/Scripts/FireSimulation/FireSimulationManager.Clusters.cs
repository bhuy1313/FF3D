using System.Collections.Generic;
using UnityEngine;

public sealed partial class FireSimulationManager
{
    private void RebuildClusters()
    {
        clusterSnapshots.Clear();
        if (runtimeGraph == null)
        {
            return;
        }

        int clusterId = 0;
        bool[] visited = new bool[runtimeGraph.Count];
        float mergeDistanceSqr = simulationProfile.ClusterMergeDistance * simulationProfile.ClusterMergeDistance;

        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode seed = runtimeGraph.GetNode(i);
            if (seed == null || seed.IsRemoved || visited[i] || !ShouldRenderNodeInCluster(seed))
            {
                continue;
            }

            Queue<int> queue = new Queue<int>();
            List<int> clusterNodeIndices = new List<int>();
            queue.Enqueue(i);
            visited[i] = true;

            Vector3 center = Vector3.zero;
            Vector3 normal = Vector3.zero;
            float intensitySum = 0f;
            float maxRadiusSqr = 0f;
            int burningNodeCount = 0;
            Dictionary<FireHazardType, int> hazardCounts = new Dictionary<FireHazardType, int>();
            List<FireClusterMemberSnapshot> memberSnapshots = new List<FireClusterMemberSnapshot>();

            while (queue.Count > 0)
            {
                int nodeIndex = queue.Dequeue();
                FireRuntimeNode node = runtimeGraph.GetNode(nodeIndex);
                if (node == null || node.IsRemoved || !ShouldRenderNodeInCluster(node))
                {
                    continue;
                }

                clusterNodeIndices.Add(nodeIndex);
                burningNodeCount++;
                center += node.Position;
                normal += node.SurfaceNormal;
                float nodeIntensity = GetNodeVisualIntensity01(node);
                intensitySum += nodeIntensity;
                memberSnapshots.Add(new FireClusterMemberSnapshot(
                    node.Index,
                    node.Position,
                    node.SurfaceNormal,
                    nodeIntensity,
                    node.HazardType));

                if (!hazardCounts.ContainsKey(node.HazardType))
                {
                    hazardCounts[node.HazardType] = 0;
                }

                hazardCounts[node.HazardType]++;

                for (int neighborListIndex = 0; neighborListIndex < node.NeighborIndices.Count; neighborListIndex++)
                {
                    int neighborIndex = node.NeighborIndices[neighborListIndex];
                    if (neighborIndex < 0 || neighborIndex >= visited.Length || visited[neighborIndex])
                    {
                        continue;
                    }

                    FireRuntimeNode neighbor = runtimeGraph.GetNode(neighborIndex);
                    if (neighbor == null || neighbor.IsRemoved || !ShouldRenderNodeInCluster(neighbor))
                    {
                        continue;
                    }

                    if ((neighbor.Position - seed.Position).sqrMagnitude > mergeDistanceSqr)
                    {
                        continue;
                    }

                    visited[neighborIndex] = true;
                    queue.Enqueue(neighborIndex);
                }
            }

            if (burningNodeCount <= 0)
            {
                continue;
            }

            center /= burningNodeCount;
            Vector3 averageNormal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;

            for (int nodeListIndex = 0; nodeListIndex < clusterNodeIndices.Count; nodeListIndex++)
            {
                FireRuntimeNode node = runtimeGraph.GetNode(clusterNodeIndices[nodeListIndex]);
                if (node == null || node.IsRemoved || !ShouldRenderNodeInCluster(node))
                {
                    continue;
                }

                maxRadiusSqr = Mathf.Max(maxRadiusSqr, (node.Position - center).sqrMagnitude);
            }

            clusterSnapshots.Add(new FireClusterSnapshot(
                clusterId++,
                center,
                averageNormal,
                Mathf.Clamp01(intensitySum / burningNodeCount),
                Mathf.Sqrt(maxRadiusSqr),
                burningNodeCount,
                ResolveDominantHazardType(hazardCounts),
                memberSnapshots));
        }
    }

    private static FireHazardType ResolveDominantHazardType(Dictionary<FireHazardType, int> hazardCounts)
    {
        FireHazardType resolvedType = FireHazardType.OrdinaryCombustibles;
        int resolvedCount = -1;

        foreach (KeyValuePair<FireHazardType, int> pair in hazardCounts)
        {
            if (pair.Value <= resolvedCount)
            {
                continue;
            }

            resolvedType = pair.Key;
            resolvedCount = pair.Value;
        }

        return resolvedType;
    }

    private void SyncEffects()
    {
        FireEffectManager runtimeEffectManager = EnsureEffectManager();
        if (runtimeEffectManager == null || simulationProfile == null)
        {
            return;
        }

        runtimeEffectManager.SyncSnapshots(clusterSnapshots, simulationProfile.MaxClusterViews);
    }

    private void DisableEffects()
    {
        FireEffectManager runtimeEffectManager = EnsureEffectManager();
        if (runtimeEffectManager != null)
        {
            runtimeEffectManager.DisableAllViews();
        }
    }

    private FireEffectManager EnsureEffectManager()
    {
        if (effectManager == null)
        {
            effectManager = GetComponent<FireEffectManager>();
            if (effectManager == null)
            {
                effectManager = gameObject.AddComponent<FireEffectManager>();
            }
        }

        effectManager.Configure(clusterViewPrefab, clusterViewRoot);
        return effectManager;
    }
}
