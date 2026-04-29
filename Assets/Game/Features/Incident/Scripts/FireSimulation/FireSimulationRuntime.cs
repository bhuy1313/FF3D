using System.Collections.Generic;
using UnityEngine;

public readonly struct FireClusterMemberSnapshot
{
    public FireClusterMemberSnapshot(
        int nodeIndex,
        Vector3 position,
        Vector3 surfaceNormal,
        float intensity,
        FireHazardType hazardType)
    {
        NodeIndex = nodeIndex;
        Position = position;
        SurfaceNormal = surfaceNormal;
        Intensity = intensity;
        HazardType = hazardType;
    }

    public int NodeIndex { get; }
    public Vector3 Position { get; }
    public Vector3 SurfaceNormal { get; }
    public float Intensity { get; }
    public FireHazardType HazardType { get; }
}

public sealed class FireRuntimeNode
{
    public FireRuntimeNode(int index, FireSurfaceNodeAuthoring authoring)
    {
        Index = index;
        Authoring = authoring;
        Position = authoring != null ? authoring.transform.position : Vector3.zero;
        SurfaceNormal = authoring != null ? authoring.SurfaceNormal : Vector3.up;
        SurfaceType = authoring != null ? authoring.SurfaceType : FireSurfaceNodeAuthoring.SurfaceKind.Object;
        AuthoringStartIgnited = authoring != null && authoring.StartIgnited;
        HazardType = authoring != null ? authoring.HazardType : FireHazardType.OrdinaryCombustibles;
        IgnitionThreshold = authoring != null ? authoring.IgnitionThresholdMultiplier : 1f;
        Heat = AuthoringStartIgnited ? IgnitionThreshold : 0f;
    }

    public int Index { get; }
    public FireSurfaceNodeAuthoring Authoring { get; }
    public Vector3 Position { get; }
    public Vector3 SurfaceNormal { get; }
    public FireSurfaceNodeAuthoring.SurfaceKind SurfaceType { get; }
    public FireHazardType HazardType { get; set; }
    public List<int> NeighborIndices { get; } = new List<int>();
    public bool AuthoringStartIgnited { get; }
    public float Heat { get; set; }
    public float PendingHeatDelta { get; set; }
    public float SuppressionRecoveryTimer { get; set; }
    public float IgnitionThreshold { get; }
    public bool IsTrackedByIncident { get; set; }
    public FireIncidentNodeKind IncidentNodeKind { get; set; } = FireIncidentNodeKind.Late;
    public bool HasReachedSpreadSaturation { get; set; }
    public bool IsRemoved { get; set; }
    public bool IsBurning => !IsRemoved && Heat >= IgnitionThreshold;
}

public sealed class FireRuntimeGraph
{
    private readonly List<FireRuntimeNode> nodes;

    public FireRuntimeGraph(List<FireRuntimeNode> nodes)
    {
        this.nodes = nodes ?? new List<FireRuntimeNode>();
    }

    public IReadOnlyList<FireRuntimeNode> Nodes => nodes;
    public int Count => nodes.Count;

    public FireRuntimeNode GetNode(int index)
    {
        if (index < 0 || index >= nodes.Count)
        {
            return null;
        }

        return nodes[index];
    }
}

public readonly struct FireClusterSnapshot
{
    public FireClusterSnapshot(
        int clusterId,
        Vector3 center,
        Vector3 averageNormal,
        float intensity,
        float radius,
        int burningNodeCount,
        FireHazardType dominantHazardType,
        IReadOnlyList<FireClusterMemberSnapshot> members)
    {
        ClusterId = clusterId;
        Center = center;
        AverageNormal = averageNormal;
        Intensity = intensity;
        Radius = radius;
        BurningNodeCount = burningNodeCount;
        DominantHazardType = dominantHazardType;
        Members = members ?? System.Array.Empty<FireClusterMemberSnapshot>();
    }

    public int ClusterId { get; }
    public Vector3 Center { get; }
    public Vector3 AverageNormal { get; }
    public float Intensity { get; }
    public float Radius { get; }
    public int BurningNodeCount { get; }
    public FireHazardType DominantHazardType { get; }
    public IReadOnlyList<FireClusterMemberSnapshot> Members { get; }
}
