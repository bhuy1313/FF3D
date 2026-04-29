using UnityEngine;

// Minimal fire simulation profile. Behaviour rules are intentionally hard-coded:
//   - No ambient cooling, no wetness, no fuel depletion.
//   - Heat only changes via neighbour spread or active suppression.
//   - A node that reaches MaxHeat saturates and never accepts spread again.
//   - A node receiving suppression cannot accept spread until the recovery timer
//     elapses (suppressionRecoveryDelaySeconds).
//   - Extinguished nodes (heat <= 0) are removed from the runtime graph.
//   - Spread is uniform: surface kind / spread resistance / vertical orientation
//     are ignored.
[CreateAssetMenu(
    fileName = "FireSimulationProfile",
    menuName = "Game/Incident/Fire Simulation Profile")]
public sealed class FireSimulationProfile : ScriptableObject
{
    [Header("Tick Rates")]
    [SerializeField] [Min(0.02f)] private float simulationTickInterval = 0.1f;
    [SerializeField] [Min(0.05f)] private float clusterRefreshInterval = 0.2f;

    [Header("Heat")]
    [Tooltip("Heat ceiling. Once a node reaches this value it saturates and can " +
             "no longer accept spread from neighbours, even if it is later " +
             "partially suppressed.")]
    [SerializeField] [Min(0.01f)] private float maxHeat = 2f;
    [Tooltip("Heat threshold at which a node snaps to 0 and gets removed.")]
    [SerializeField] [Min(0f)] private float extinguishThreshold = 0.08f;
    [Tooltip("Minimum heat for VFX rendering.")]
    [SerializeField] [Min(0f)] private float visualHeatThreshold = 0.01f;
    [Tooltip("Seconds a suppressed node must wait before it can receive spread again.")]
    [SerializeField] [Min(0f)] private float suppressionRecoveryDelaySeconds = 1.5f;

    [Header("Spread")]
    [Tooltip("Heat transferred per second from a burning node to each neighbour.")]
    [SerializeField] [Min(0f)] private float neighborHeatTransferPerSecond = 0.5f;

    [Header("Clustering")]
    [SerializeField] [Min(0.1f)] private float clusterMergeDistance = 2.5f;
    [SerializeField] [Min(1)] private int maxClusterViews = 24;

    public float SimulationTickInterval => simulationTickInterval;
    public float ClusterRefreshInterval => clusterRefreshInterval;
    public float MaxHeat => maxHeat;
    public float ExtinguishThreshold => extinguishThreshold;
    public float VisualHeatThreshold => visualHeatThreshold;
    public float SuppressionRecoveryDelaySeconds => suppressionRecoveryDelaySeconds;
    public float NeighborHeatTransferPerSecond => neighborHeatTransferPerSecond;
    public float ClusterMergeDistance => clusterMergeDistance;
    public int MaxClusterViews => maxClusterViews;

    private void OnValidate()
    {
        simulationTickInterval = Mathf.Max(0.02f, simulationTickInterval);
        clusterRefreshInterval = Mathf.Max(0.05f, clusterRefreshInterval);
        maxHeat = Mathf.Max(0.01f, maxHeat);
        extinguishThreshold = Mathf.Max(0f, extinguishThreshold);
        visualHeatThreshold = Mathf.Max(0f, visualHeatThreshold);
        suppressionRecoveryDelaySeconds = Mathf.Max(0f, suppressionRecoveryDelaySeconds);
        neighborHeatTransferPerSecond = Mathf.Max(0f, neighborHeatTransferPerSecond);
        clusterMergeDistance = Mathf.Max(0.1f, clusterMergeDistance);
        maxClusterViews = Mathf.Max(1, maxClusterViews);
    }
}
