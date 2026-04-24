using System.Collections.Generic;
using UnityEngine;

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
    [SerializeField] [Min(0.1f)] private float initialFuel = 1f;
    [SerializeField] [Min(0f)] private float ignitionThresholdMultiplier = 1f;
    [SerializeField] [Range(0f, 1f)] private float spreadResistance = 0.15f;
    [SerializeField] private FireHazardType hazardType = FireHazardType.OrdinaryCombustibles;
    [SerializeField] private bool startIgnited;

    [Header("Graph")]
    [SerializeField] private bool autoConnectNearbyNodes = true;
    [SerializeField] [Min(0.1f)] private float autoConnectRadius = 2.5f;
    [SerializeField] private List<FireSurfaceNodeAuthoring> explicitNeighbors = new List<FireSurfaceNodeAuthoring>();

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color gizmoColor = new Color(1f, 0.45f, 0.1f, 0.8f);

    public string NodeId => string.IsNullOrWhiteSpace(nodeId) ? gameObject.name : nodeId;
    public SurfaceKind SurfaceType => surfaceKind;
    public Vector3 SurfaceNormal => surfaceNormal.sqrMagnitude > 0.001f ? surfaceNormal.normalized : transform.up;
    public float InitialFuel => Mathf.Max(0f, initialFuel);
    public float IgnitionThresholdMultiplier => Mathf.Max(0.01f, ignitionThresholdMultiplier);
    public float SpreadResistance => Mathf.Clamp01(spreadResistance);
    public FireHazardType HazardType => hazardType;
    public bool StartIgnited => startIgnited;
    public bool AutoConnectNearbyNodes => autoConnectNearbyNodes;
    public float AutoConnectRadius => Mathf.Max(0.1f, autoConnectRadius);
    public IReadOnlyList<FireSurfaceNodeAuthoring> ExplicitNeighbors => explicitNeighbors;

    private void OnValidate()
    {
        initialFuel = Mathf.Max(0.1f, initialFuel);
        ignitionThresholdMultiplier = Mathf.Max(0.01f, ignitionThresholdMultiplier);
        spreadResistance = Mathf.Clamp01(spreadResistance);
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
    }
}
