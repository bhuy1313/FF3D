using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FireSurfaceGraph : MonoBehaviour
{
    [SerializeField] private bool collectNodesFromChildren = true;
    [SerializeField] private bool includeInactiveNodes = true;
    [SerializeField] private List<FireSurfaceNodeAuthoring> explicitNodes = new List<FireSurfaceNodeAuthoring>();

    public FireRuntimeGraph BuildRuntimeGraph()
    {
        List<FireSurfaceNodeAuthoring> authoringNodes = BuildAuthoringNodeList();
        List<FireRuntimeNode> runtimeNodes = new List<FireRuntimeNode>(authoringNodes.Count);
        Dictionary<FireSurfaceNodeAuthoring, int> indexLookup = new Dictionary<FireSurfaceNodeAuthoring, int>(authoringNodes.Count);

        for (int i = 0; i < authoringNodes.Count; i++)
        {
            FireSurfaceNodeAuthoring authoring = authoringNodes[i];
            runtimeNodes.Add(new FireRuntimeNode(i, authoring));
            indexLookup[authoring] = i;
        }

        for (int i = 0; i < authoringNodes.Count; i++)
        {
            FireSurfaceNodeAuthoring source = authoringNodes[i];
            FireRuntimeNode runtimeNode = runtimeNodes[i];

            AddExplicitNeighbors(source, runtimeNode, indexLookup);
            AddAutoNeighbors(source, runtimeNode, authoringNodes, indexLookup);
        }

        return new FireRuntimeGraph(runtimeNodes);
    }

    private List<FireSurfaceNodeAuthoring> BuildAuthoringNodeList()
    {
        List<FireSurfaceNodeAuthoring> results = new List<FireSurfaceNodeAuthoring>();
        HashSet<FireSurfaceNodeAuthoring> seen = new HashSet<FireSurfaceNodeAuthoring>();

        for (int i = 0; i < explicitNodes.Count; i++)
        {
            FireSurfaceNodeAuthoring node = explicitNodes[i];
            if (node == null || !seen.Add(node))
            {
                continue;
            }

            results.Add(node);
        }

        if (collectNodesFromChildren)
        {
            FireSurfaceNodeAuthoring[] childNodes = GetComponentsInChildren<FireSurfaceNodeAuthoring>(includeInactiveNodes);
            for (int i = 0; i < childNodes.Length; i++)
            {
                FireSurfaceNodeAuthoring node = childNodes[i];
                if (node == null || !seen.Add(node))
                {
                    continue;
                }

                results.Add(node);
            }
        }

        return results;
    }

    private static void AddExplicitNeighbors(
        FireSurfaceNodeAuthoring source,
        FireRuntimeNode runtimeNode,
        Dictionary<FireSurfaceNodeAuthoring, int> indexLookup)
    {
        IReadOnlyList<FireSurfaceNodeAuthoring> neighbors = source.ExplicitNeighbors;
        for (int i = 0; i < neighbors.Count; i++)
        {
            FireSurfaceNodeAuthoring neighbor = neighbors[i];
            if (neighbor == null || !indexLookup.TryGetValue(neighbor, out int neighborIndex))
            {
                continue;
            }

            if (neighborIndex == runtimeNode.Index || runtimeNode.NeighborIndices.Contains(neighborIndex))
            {
                continue;
            }

            runtimeNode.NeighborIndices.Add(neighborIndex);
        }
    }

    private static void AddAutoNeighbors(
        FireSurfaceNodeAuthoring source,
        FireRuntimeNode runtimeNode,
        List<FireSurfaceNodeAuthoring> allNodes,
        Dictionary<FireSurfaceNodeAuthoring, int> indexLookup)
    {
        if (!source.AutoConnectNearbyNodes)
        {
            return;
        }

        float maxDistanceSqr = source.AutoConnectRadius * source.AutoConnectRadius;
        Vector3 sourcePosition = source.transform.position;
        for (int i = 0; i < allNodes.Count; i++)
        {
            FireSurfaceNodeAuthoring candidate = allNodes[i];
            if (candidate == null || candidate == source)
            {
                continue;
            }

            if (!indexLookup.TryGetValue(candidate, out int candidateIndex))
            {
                continue;
            }

            if (runtimeNode.NeighborIndices.Contains(candidateIndex))
            {
                continue;
            }

            if ((candidate.transform.position - sourcePosition).sqrMagnitude > maxDistanceSqr)
            {
                continue;
            }

            runtimeNode.NeighborIndices.Add(candidateIndex);
        }
    }
}
