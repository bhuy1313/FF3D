using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class FireSurfaceNodeAuthoring : MonoBehaviour
{
    public enum SurfaceKind
    {
        Floor = 0,
        Wall = 1,
        Ceiling = 2,
        Object = 3
    }

    [Header("Identity")]
    [SerializeField] private string nodeId;

    [Header("Surface")]
    [SerializeField] private SurfaceKind surfaceKind = SurfaceKind.Object;
    [SerializeField] private Vector3 surfaceNormal = Vector3.up;

    [Header("Combustion")]
    [SerializeField] [Min(0f)] private float ignitionThresholdMultiplier = 1f;
    [SerializeField] private FireHazardType hazardType = FireHazardType.OrdinaryCombustibles;
    [SerializeField] private bool startIgnited;

    [Header("Graph")]
    [SerializeField] private bool autoConnectNearbyNodes = true;
    [SerializeField] [Min(0.1f)] private float autoConnectRadius = 2.5f;
    [SerializeField] private List<FireSurfaceNodeAuthoring> explicitNeighbors = new List<FireSurfaceNodeAuthoring>();
    [SerializeField] private List<FireSurfaceNodeAuthoring> resolvedNeighbors = new List<FireSurfaceNodeAuthoring>();

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color gizmoColor = new Color(1f, 0.45f, 0.1f, 0.8f);
    [SerializeField] private bool drawRuntimeHeatLabel = true;

    [Header("Runtime Debug")]
    [SerializeField] private float currentHeat;
    [SerializeField] private float currentIgnitionThreshold;
    [SerializeField] private float currentMaxHeat = 2f;
    [SerializeField] private bool currentIsBurning;
    [SerializeField] private bool currentIsTrackedByIncident;
    [SerializeField] private bool currentIsRemoved;
    [SerializeField] private FireIncidentNodeKind currentIncidentNodeKind = FireIncidentNodeKind.Late;
    [SerializeField] private FireHazardType currentHazardType = FireHazardType.OrdinaryCombustibles;

    public string NodeId => string.IsNullOrWhiteSpace(nodeId) ? gameObject.name : nodeId;
    public SurfaceKind SurfaceType => surfaceKind;
    public Vector3 SurfaceNormal => surfaceNormal.sqrMagnitude > 0.001f ? surfaceNormal.normalized : transform.up;
    public float IgnitionThresholdMultiplier => Mathf.Max(0.01f, ignitionThresholdMultiplier);
    public FireHazardType HazardType => hazardType;
    public bool StartIgnited => startIgnited;
    public bool AutoConnectNearbyNodes => autoConnectNearbyNodes;
    public float AutoConnectRadius => Mathf.Max(0.1f, autoConnectRadius);
    public IReadOnlyList<FireSurfaceNodeAuthoring> ExplicitNeighbors => explicitNeighbors;
    public IReadOnlyList<FireSurfaceNodeAuthoring> ResolvedNeighbors => resolvedNeighbors;
    public float CurrentHeat => currentHeat;
    public float CurrentIgnitionThreshold => currentIgnitionThreshold;
    public float CurrentMaxHeat => currentMaxHeat;
    public bool CurrentIsBurning => currentIsBurning;
    public bool CurrentIsTrackedByIncident => currentIsTrackedByIncident;
    public bool CurrentIsRemoved => currentIsRemoved;
    public FireIncidentNodeKind CurrentIncidentNodeKind => currentIncidentNodeKind;
    public FireHazardType CurrentHazardType => currentHazardType;

    public void ConfigureRuntimeNode(
        string runtimeNodeId,
        SurfaceKind runtimeSurfaceKind,
        Vector3 runtimeSurfaceNormal,
        float runtimeIgnitionThresholdMultiplier,
        FireHazardType runtimeHazardType,
        bool runtimeStartIgnited,
        bool runtimeAutoConnectNearbyNodes,
        float runtimeAutoConnectRadius,
        bool runtimeDrawGizmos,
        Color runtimeGizmoColor)
    {
        nodeId = runtimeNodeId;
        surfaceKind = runtimeSurfaceKind;
        surfaceNormal = runtimeSurfaceNormal.sqrMagnitude > 0.001f ? runtimeSurfaceNormal.normalized : Vector3.up;
        ignitionThresholdMultiplier = Mathf.Max(0.01f, runtimeIgnitionThresholdMultiplier);
        hazardType = runtimeHazardType;
        startIgnited = runtimeStartIgnited;
        autoConnectNearbyNodes = runtimeAutoConnectNearbyNodes;
        autoConnectRadius = Mathf.Max(0.1f, runtimeAutoConnectRadius);
        explicitNeighbors.Clear();
        resolvedNeighbors.Clear();
        drawGizmos = runtimeDrawGizmos;
        gizmoColor = runtimeGizmoColor;
    }

    public void SetResolvedNeighbors(IReadOnlyList<FireSurfaceNodeAuthoring> neighbors)
    {
        resolvedNeighbors.Clear();
        if (neighbors == null)
        {
            return;
        }

        for (int i = 0; i < neighbors.Count; i++)
        {
            FireSurfaceNodeAuthoring neighbor = neighbors[i];
            if (neighbor != null && neighbor != this && !resolvedNeighbors.Contains(neighbor))
            {
                resolvedNeighbors.Add(neighbor);
            }
        }
    }

    public void SetRuntimeDebugState(FireRuntimeNode runtimeNode, float maxHeat)
    {
        if (runtimeNode == null)
        {
            ClearRuntimeDebugState();
            return;
        }

        currentHeat = runtimeNode.Heat;
        currentIgnitionThreshold = runtimeNode.IgnitionThreshold;
        currentMaxHeat = Mathf.Max(runtimeNode.IgnitionThreshold, maxHeat);
        currentIsBurning = runtimeNode.IsBurning;
        currentIsTrackedByIncident = runtimeNode.IsTrackedByIncident;
        currentIsRemoved = runtimeNode.IsRemoved;
        currentIncidentNodeKind = runtimeNode.IncidentNodeKind;
        currentHazardType = runtimeNode.HazardType;
    }

    public void ClearRuntimeDebugState()
    {
        currentHeat = 0f;
        currentIgnitionThreshold = IgnitionThresholdMultiplier;
        currentMaxHeat = 2f;
        currentIsBurning = false;
        currentIsTrackedByIncident = false;
        currentIsRemoved = false;
        currentIncidentNodeKind = FireIncidentNodeKind.Late;
        currentHazardType = hazardType;
    }

    private void OnValidate()
    {
        ignitionThresholdMultiplier = Mathf.Max(0.01f, ignitionThresholdMultiplier);
        autoConnectRadius = Mathf.Max(0.1f, autoConnectRadius);
        if (surfaceNormal.sqrMagnitude <= 0.001f)
        {
            surfaceNormal = Vector3.up;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, 0.12f);
        Gizmos.DrawRay(transform.position, SurfaceNormal * 0.5f);

        if (!autoConnectNearbyNodes)
        {
            return;
        }

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.2f);
        Gizmos.DrawWireSphere(transform.position, AutoConnectRadius);

#if UNITY_EDITOR
        if (drawRuntimeHeatLabel)
        {
            Handles.Label(
                transform.position + (Vector3.up * 0.35f),
                $"{NodeId}\nHeat {currentHeat:0.00}/{currentMaxHeat:0.00}  Ignite {currentIgnitionThreshold:0.00}\n{currentHazardType} {(currentIsBurning ? "BURNING" : "cold")}");
        }
#endif
    }
}
