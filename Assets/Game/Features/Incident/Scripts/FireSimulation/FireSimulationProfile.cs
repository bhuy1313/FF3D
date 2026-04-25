using UnityEngine;

[CreateAssetMenu(
    fileName = "FireSimulationProfile",
    menuName = "Game/Incident/Fire Simulation Profile")]
public sealed class FireSimulationProfile : ScriptableObject
{
    [Header("Tick Rates")]
    [SerializeField] [Min(0.02f)] private float simulationTickInterval = 0.1f;
    [SerializeField] [Min(0.05f)] private float clusterRefreshInterval = 0.2f;

    [Header("Heat")]
    [SerializeField] [Min(0f)] private float ambientCoolingPerSecond = 0.12f;
    [SerializeField] [Min(0f)] private float wetnessCoolingPerSecond = 0.8f;
    [SerializeField] [Min(0f)] private float wetnessRecoveryPerSecond = 0.2f;
    [SerializeField] [Min(0f)] private float passiveIgnitionThreshold = 1f;
    [SerializeField] [Min(0f)] private float extinguishThreshold = 0.08f;
    [SerializeField] [Min(0f)] private float suppressionRecoveryDelaySeconds = 1.5f;
    [SerializeField] [Range(0f, 1f)] private float suppressionRecoveryHeatMultiplier = 0.15f;

    [Header("Fuel")]
    [SerializeField] [Min(0f)] private float fuelBurnPerSecond = 0.1f;
    [SerializeField] [Range(0f, 1f)] private float burnedOutHeatRetention = 0.15f;

    [Header("Spread")]
    [SerializeField] [Min(0f)] private float neighborHeatTransferPerSecond = 0.5f;
    [SerializeField] [Range(0f, 4f)] private float sameSurfaceTransferMultiplier = 1.2f;
    [SerializeField] [Range(0f, 4f)] private float crossSurfaceTransferMultiplier = 0.65f;
    [SerializeField] [Range(0f, 4f)] private float verticalSpreadBias = 0.85f;

    [Header("Clustering")]
    [SerializeField] [Min(0.1f)] private float clusterMergeDistance = 2.5f;
    [SerializeField] [Min(1)] private int maxClusterViews = 24;

    public float SimulationTickInterval => simulationTickInterval;
    public float ClusterRefreshInterval => clusterRefreshInterval;
    public float AmbientCoolingPerSecond => ambientCoolingPerSecond;
    public float WetnessCoolingPerSecond => wetnessCoolingPerSecond;
    public float WetnessRecoveryPerSecond => wetnessRecoveryPerSecond;
    public float PassiveIgnitionThreshold => passiveIgnitionThreshold;
    public float ExtinguishThreshold => extinguishThreshold;
    public float SuppressionRecoveryDelaySeconds => suppressionRecoveryDelaySeconds;
    public float SuppressionRecoveryHeatMultiplier => suppressionRecoveryHeatMultiplier;
    public float FuelBurnPerSecond => fuelBurnPerSecond;
    public float BurnedOutHeatRetention => burnedOutHeatRetention;
    public float NeighborHeatTransferPerSecond => neighborHeatTransferPerSecond;
    public float SameSurfaceTransferMultiplier => sameSurfaceTransferMultiplier;
    public float CrossSurfaceTransferMultiplier => crossSurfaceTransferMultiplier;
    public float VerticalSpreadBias => verticalSpreadBias;
    public float ClusterMergeDistance => clusterMergeDistance;
    public int MaxClusterViews => maxClusterViews;

    private void OnValidate()
    {
        simulationTickInterval = Mathf.Max(0.02f, simulationTickInterval);
        clusterRefreshInterval = Mathf.Max(0.05f, clusterRefreshInterval);
        ambientCoolingPerSecond = Mathf.Max(0f, ambientCoolingPerSecond);
        wetnessCoolingPerSecond = Mathf.Max(0f, wetnessCoolingPerSecond);
        wetnessRecoveryPerSecond = Mathf.Max(0f, wetnessRecoveryPerSecond);
        passiveIgnitionThreshold = Mathf.Max(0f, passiveIgnitionThreshold);
        extinguishThreshold = Mathf.Max(0f, extinguishThreshold);
        suppressionRecoveryDelaySeconds = Mathf.Max(0f, suppressionRecoveryDelaySeconds);
        suppressionRecoveryHeatMultiplier = Mathf.Clamp01(suppressionRecoveryHeatMultiplier);
        fuelBurnPerSecond = Mathf.Max(0f, fuelBurnPerSecond);
        burnedOutHeatRetention = Mathf.Clamp01(burnedOutHeatRetention);
        neighborHeatTransferPerSecond = Mathf.Max(0f, neighborHeatTransferPerSecond);
        sameSurfaceTransferMultiplier = Mathf.Max(0f, sameSurfaceTransferMultiplier);
        crossSurfaceTransferMultiplier = Mathf.Max(0f, crossSurfaceTransferMultiplier);
        verticalSpreadBias = Mathf.Max(0f, verticalSpreadBias);
        clusterMergeDistance = Mathf.Max(0.1f, clusterMergeDistance);
        maxClusterViews = Mathf.Max(1, maxClusterViews);
    }
}
