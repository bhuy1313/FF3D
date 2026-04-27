using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "IncidentFireSpawnProfile",
    menuName = "FF3D/Incident/Fire Spawn Profile")]
public sealed class IncidentFireSpawnProfile : ScriptableObject
{
    [Header("Secondary Fire Placement")]
    [Tooltip("Fallback active secondary fire count when payload does not specify a valid total fire count.")]
    [SerializeField] [Min(0)] private int secondaryFirePointCount = 3;
    [SerializeField] [Min(0.1f)] private float secondaryFireRange = 4.2f;
    [SerializeField] [Min(0f)] private float minimumSecondaryFireSpacing = 2.2f;
    [SerializeField] [Min(1)] private int placementAttemptsPerSecondaryFire = 24;
    [SerializeField] [Range(0f, 1f)] private float activeSecondaryIntensityScale = 0.65f;

    [Header("Latent Spread Nodes")]
    [SerializeField] private bool spawnLatentSpreadNodes = true;
    [SerializeField] [Min(0)] private int latentSpreadNodeCount = 4;
    [FormerlySerializedAs("latentSpreadSeedLimit")]
    [SerializeField] [Min(1)] private int latentSpreadPlacementsPerNode = 2;
    [SerializeField] [Min(0.1f)] private float latentSpreadRange = 4.2f;
    [SerializeField] [Min(0f)] private float minimumLatentNodeSpacing = 2.2f;

    [Header("Surface Sampling")]
    [SerializeField] [Min(0.05f)] private float parabolaLaunchHeight = 0.35f;
    [SerializeField] [Min(0.1f)] private float parabolaApexHeight = 1.4f;
    [SerializeField] [Min(0f)] private float parabolaApexHeightJitter = 0.75f;
    [SerializeField] [Min(0f)] private float parabolaLateralOffset = 0.6f;
    [SerializeField] [Min(3)] private int parabolaSegments = 12;
    [SerializeField] [Min(0.01f)] private float parabolaCastRadius = 0.08f;
    [SerializeField] [Min(1)] private int placementTrajectoryVariants = 4;
    [SerializeField] [Min(0.01f)] private float floorPlacementWeight = 0.65f;
    [SerializeField] [Min(0.01f)] private float wallPlacementWeight = 1.35f;
    [SerializeField] [Min(0.01f)] private float ceilingPlacementWeight = 0.2f;

    public int SecondaryFirePointCount => secondaryFirePointCount;
    public float SecondaryFireRange => secondaryFireRange;
    public float MinimumSecondaryFireSpacing => minimumSecondaryFireSpacing;
    public int PlacementAttemptsPerSecondaryFire => placementAttemptsPerSecondaryFire;
    public float ActiveSecondaryIntensityScale => activeSecondaryIntensityScale;
    public bool SpawnLatentSpreadNodes => spawnLatentSpreadNodes;
    public int LatentSpreadNodeCount => latentSpreadNodeCount;
    public int LatentSpreadPlacementsPerNode => latentSpreadPlacementsPerNode;
    public float LatentSpreadRange => latentSpreadRange;
    public float MinimumLatentNodeSpacing => minimumLatentNodeSpacing;
    public float ParabolaLaunchHeight => parabolaLaunchHeight;
    public float ParabolaApexHeight => parabolaApexHeight;
    public float ParabolaApexHeightJitter => parabolaApexHeightJitter;
    public float ParabolaLateralOffset => parabolaLateralOffset;
    public int ParabolaSegments => parabolaSegments;
    public float ParabolaCastRadius => parabolaCastRadius;
    public int PlacementTrajectoryVariants => placementTrajectoryVariants;
    public float FloorPlacementWeight => floorPlacementWeight;
    public float WallPlacementWeight => wallPlacementWeight;
    public float CeilingPlacementWeight => ceilingPlacementWeight;
}
