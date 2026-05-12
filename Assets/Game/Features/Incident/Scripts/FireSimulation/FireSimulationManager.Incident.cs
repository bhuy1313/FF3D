using System.Collections.Generic;
using UnityEngine;

public sealed partial class FireSimulationManager
{
    public int IgniteClosestNode(Vector3 worldPosition, float ignitionHeat)
    {
        if (!initialized || runtimeGraph == null)
        {
            return -1;
        }

        int bestIndex = -1;
        float bestDistanceSqr = float.PositiveInfinity;
        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null || node.IsRemoved)
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

        if (bestIndex >= 0)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(bestIndex);
            node.IsTrackedByIncident = true;
            if (node.IncidentNodeKind != FireIncidentNodeKind.Late)
            {
                node.HazardType = activeIncidentHazardType;
            }
            node.Heat = Mathf.Max(node.Heat, ignitionHeat);
            RefreshNodeSpreadPoolMembership(node);
            NotifyStateChanged();
        }

        return bestIndex;
    }

    public void BeginIncident(FireHazardType hazardType, bool hazardSourceIsolated)
    {
        if (!initialized)
        {
            InitializeRuntimeGraph();
        }

        ResetRuntimeStateToBaseline(useAuthoringIgnition: false);
        activeIncidentHazardType = hazardType;
        activeHazardSourceIsolated = hazardSourceIsolated;
        RefreshAllSpreadPoolMembership();
        MarkVisualStateDirty();
        NotifyStateChanged();
    }

    public bool ApplyIncidentPlacements(
        FireHazardType hazardType,
        bool hazardSourceIsolated,
        IReadOnlyList<FireIncidentPlacement> placements)
    {
        if (surfaceGraph == null || placements == null || placements.Count <= 0)
        {
            return false;
        }

        RebuildRuntimeIncidentNodes(placements, hazardType);
        InitializeRuntimeGraph();
        if (!initialized || runtimeGraph == null)
        {
            return false;
        }

        BeginIncident(hazardType, hazardSourceIsolated);
        for (int i = 0; i < placements.Count; i++)
        {
            FireIncidentPlacement placement = placements[i];
            FireSurfaceNodeAuthoring runtimeNodeAuthoring = i < runtimeIncidentNodes.Count
                ? runtimeIncidentNodes[i]
                : null;
            if (!TrackRuntimeNode(runtimeNodeAuthoring, placement.InitialIntensity01, placement.Kind))
            {
                TrackClosestNode(placement.Position, placement.InitialIntensity01, placement.Kind);
            }
        }

        return true;
    }

    public int TrackClosestNode(Vector3 worldPosition, float normalizedHeat)
    {
        return TrackClosestNode(worldPosition, normalizedHeat, FireIncidentNodeKind.Late);
    }

    public int TrackClosestNode(Vector3 worldPosition, float normalizedHeat, FireIncidentNodeKind kind)
    {
        if (!initialized || runtimeGraph == null)
        {
            return -1;
        }

        int nodeIndex = FindClosestNodeIndex(worldPosition);
        if (nodeIndex < 0)
        {
            return -1;
        }

        FireRuntimeNode node = runtimeGraph.GetNode(nodeIndex);
        if (node == null)
        {
            return -1;
        }

        node.IsTrackedByIncident = true;
        node.IncidentNodeKind = kind;
        float clampedHeat01 = Mathf.Clamp01(normalizedHeat);
        if (clampedHeat01 > 0f && kind != FireIncidentNodeKind.Late)
        {
            node.HazardType = activeIncidentHazardType;
        }

        float maxHeat = simulationProfile != null
            ? Mathf.Max(node.IgnitionThreshold, simulationProfile.MaxHeat)
            : node.IgnitionThreshold * 2f;
        float heat = clampedHeat01 > 0f
            ? Mathf.Lerp(node.IgnitionThreshold, maxHeat, clampedHeat01)
            : 0f;
        node.Heat = Mathf.Max(node.Heat, heat);
        if (node.IsBurning)
        {
            node.HasEverBurned = true;
        }

        RefreshNodeSpreadPoolMembership(node);
        MarkVisualStateDirty();
        NotifyStateChanged();
        return nodeIndex;
    }

    public void SetRuntimeHazardIsolation(bool isolated)
    {
        activeHazardSourceIsolated = isolated;
        NotifyStateChanged();
    }

    public void SetActiveIncidentHazardType(FireHazardType hazardType)
    {
        activeIncidentHazardType = hazardType;
        NotifyStateChanged();
    }

    private void RebuildRuntimeIncidentNodes(IReadOnlyList<FireIncidentPlacement> placements, FireHazardType hazardType)
    {
        ClearRuntimeIncidentNodes();
        if (placements == null || placements.Count <= 0)
        {
            surfaceGraph?.ClearRuntimeNodeOverrides();
            return;
        }

        Transform root = EnsureRuntimeIncidentNodeRoot();
        for (int i = 0; i < placements.Count; i++)
        {
            FireIncidentPlacement placement = placements[i];
            GameObject nodeObject = new GameObject($"RuntimeFireNode_{i + 1}");
            nodeObject.transform.SetParent(root, false);
            nodeObject.transform.position = placement.Position;
            nodeObject.transform.rotation = ResolveRuntimeNodeRotation(placement.SurfaceNormal);
            nodeObject.transform.localScale = Vector3.one;

            FireSurfaceNodeAuthoring node = nodeObject.AddComponent<FireSurfaceNodeAuthoring>();
            FireHazardType nodeHazardType = placement.InitialIntensity01 > 0f
                ? hazardType
                : FireHazardType.OrdinaryCombustibles;
            node.ConfigureRuntimeNode(
                $"RuntimeIncidentNode_{i + 1}",
                ResolveRuntimeSurfaceKind(placement.SurfaceNormal),
                placement.SurfaceNormal,
                runtimeNodeIgnitionThresholdMultiplier,
                nodeHazardType,
                runtimeStartIgnited: false,
                runtimeAutoConnectNearbyNodes: true,
                runtimeAutoConnectRadius: runtimeNodeAutoConnectRadius,
                runtimeDrawGizmos: runtimeNodeDrawGizmos,
                runtimeGizmoColor: runtimeNodeGizmoColor);
            runtimeIncidentNodes.Add(node);
        }

        surfaceGraph.SetRuntimeNodeOverrides(runtimeIncidentNodes);
    }

    private void ClearRuntimeIncidentNodes()
    {
        surfaceGraph?.ClearRuntimeNodeOverrides();
        for (int i = 0; i < runtimeIncidentNodes.Count; i++)
        {
            FireSurfaceNodeAuthoring node = runtimeIncidentNodes[i];
            if (node != null)
            {
                Destroy(node.gameObject);
            }
        }

        runtimeIncidentNodes.Clear();
    }

    private bool TrackRuntimeNode(
        FireSurfaceNodeAuthoring authoring,
        float normalizedHeat,
        FireIncidentNodeKind kind)
    {
        if (!initialized || runtimeGraph == null || authoring == null)
        {
            return false;
        }

        FireRuntimeNode node = FindRuntimeNode(authoring);
        if (node == null)
        {
            return false;
        }

        ApplyIncidentState(node, normalizedHeat, kind);
        return true;
    }

    private FireRuntimeNode FindRuntimeNode(FireSurfaceNodeAuthoring authoring)
    {
        if (!initialized || runtimeGraph == null || authoring == null)
        {
            return null;
        }

        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node != null && node.Authoring == authoring)
            {
                return node;
            }
        }

        return null;
    }

    private void ApplyIncidentState(FireRuntimeNode node, float normalizedHeat, FireIncidentNodeKind kind)
    {
        if (node == null)
        {
            return;
        }

        node.IsTrackedByIncident = true;
        node.IncidentNodeKind = kind;
        float clampedHeat01 = Mathf.Clamp01(normalizedHeat);
        if (clampedHeat01 > 0f && kind != FireIncidentNodeKind.Late)
        {
            node.HazardType = activeIncidentHazardType;
        }

        float maxHeat = simulationProfile != null
            ? Mathf.Max(node.IgnitionThreshold, simulationProfile.MaxHeat)
            : node.IgnitionThreshold * 2f;
        float heat = clampedHeat01 > 0f
            ? Mathf.Lerp(node.IgnitionThreshold, maxHeat, clampedHeat01)
            : 0f;
        node.Heat = Mathf.Max(node.Heat, heat);
        if (node.IsBurning)
        {
            node.HasEverBurned = true;
        }

        RefreshNodeSpreadPoolMembership(node);
        MarkVisualStateDirty();
        NotifyStateChanged();
    }

    private Transform EnsureRuntimeIncidentNodeRoot()
    {
        if (runtimeIncidentNodeRoot != null)
        {
            return runtimeIncidentNodeRoot;
        }

        Transform parent = surfaceGraph != null ? surfaceGraph.transform : transform;
        GameObject rootObject = new GameObject("RuntimeIncidentNodes");
        runtimeIncidentNodeRoot = rootObject.transform;
        runtimeIncidentNodeRoot.SetParent(parent, false);
        runtimeIncidentNodeRoot.localPosition = Vector3.zero;
        runtimeIncidentNodeRoot.localRotation = Quaternion.identity;
        runtimeIncidentNodeRoot.localScale = Vector3.one;
        return runtimeIncidentNodeRoot;
    }

    private static Quaternion ResolveRuntimeNodeRotation(Vector3 surfaceNormal)
    {
        Vector3 up = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, up);
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(Vector3.right, up);
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.Cross(up, Vector3.right);
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }

        return Quaternion.LookRotation(forward.normalized, up);
    }

    private static FireSurfaceNodeAuthoring.SurfaceKind ResolveRuntimeSurfaceKind(Vector3 surfaceNormal)
    {
        Vector3 normalizedNormal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        float upDot = Vector3.Dot(normalizedNormal, Vector3.up);
        if (upDot >= 0.6f)
        {
            return FireSurfaceNodeAuthoring.SurfaceKind.Floor;
        }

        if (upDot <= -0.35f)
        {
            return FireSurfaceNodeAuthoring.SurfaceKind.Ceiling;
        }

        return FireSurfaceNodeAuthoring.SurfaceKind.Wall;
    }

    private void ResetRuntimeStateToBaseline(bool useAuthoringIgnition)
    {
        if (runtimeGraph == null)
        {
            return;
        }

        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null)
            {
                continue;
            }

            node.HazardType = node.Authoring != null ? node.Authoring.HazardType : FireHazardType.OrdinaryCombustibles;
            node.PendingHeatDelta = 0f;
            node.IsTrackedByIncident = false;
            node.IncidentNodeKind = FireIncidentNodeKind.Late;
            node.SuppressionRecoveryTimer = 0f;
            node.HasEverBurned = useAuthoringIgnition && node.AuthoringStartIgnited;
            node.HasReachedSpreadSaturation = false;
            node.IsRemoved = false;
            node.Heat = useAuthoringIgnition && node.AuthoringStartIgnited
                ? Mathf.Max(0.01f, node.IgnitionThreshold)
                : 0f;
        }

        RefreshAllSpreadPoolMembership();
    }
}
