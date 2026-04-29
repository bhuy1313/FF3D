using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "IncidentFireSpawnProfile",
    menuName = "FF3D/Incident/Fire Spawn Profile")]
public sealed class IncidentFireSpawnProfile : ScriptableObject
{
    [Header("Shared Placement")]
    [Tooltip("Maximum search radius from a seed placement to a child placement (used for both secondary and latent nodes).")]
    [FormerlySerializedAs("secondaryFireRange")]
    [FormerlySerializedAs("latentSpreadRange")]
    [SerializeField] [Min(0.1f)] private float placementRange = 4.2f;
    [Tooltip("Minimum spacing between any two placements (secondary or latent).")]
    [FormerlySerializedAs("minimumSecondaryFireSpacing")]
    [FormerlySerializedAs("minimumLatentNodeSpacing")]
    [SerializeField] [Min(0f)] private float minimumNodeSpacing = 2.2f;
    [Tooltip("Maximum placement attempts per node (shared by secondary and latent passes).")]
    [FormerlySerializedAs("placementAttemptsPerSecondaryFire")]
    [SerializeField] [Min(1)] private int placementAttemptsPerNode = 24;

    [Header("Secondary Fire Placement")]
    [Tooltip("Fallback active secondary fire count when payload does not specify a valid total fire count.")]
    [SerializeField] [Min(0)] private int secondaryFirePointCount = 3;
    [SerializeField] [Range(0f, 1f)] private float activeSecondaryIntensityScale = 0.65f;

    [Header("Latent Spread Nodes")]
    [SerializeField] private bool spawnLatentSpreadNodes = true;
    [SerializeField] [Min(0)] private int latentSpreadNodeCount = 4;
    [FormerlySerializedAs("latentSpreadSeedLimit")]
    [SerializeField] [Min(1)] private int latentSpreadPlacementsPerNode = 2;

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

    public float PlacementRange => placementRange;
    public float MinimumNodeSpacing => minimumNodeSpacing;
    public int PlacementAttemptsPerNode => placementAttemptsPerNode;
    public int SecondaryFirePointCount => secondaryFirePointCount;
    public float ActiveSecondaryIntensityScale => activeSecondaryIntensityScale;
    public bool SpawnLatentSpreadNodes => spawnLatentSpreadNodes;
    public int LatentSpreadNodeCount => latentSpreadNodeCount;
    public int LatentSpreadPlacementsPerNode => latentSpreadPlacementsPerNode;
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
