using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class IncidentPayloadAnchor : MonoBehaviour
{
    [Header("Matching")]
    [SerializeField] private string fireOriginKey = "Unknown";
    [SerializeField] private string logicalLocationKey = string.Empty;
    [SerializeField] private bool isDefaultAnchor;

    [Header("Runtime Spawn")]
    [SerializeField] private Transform runtimeParent;
    [SerializeField] private Vector3 runtimeZoneSize = new Vector3(4f, 2.5f, 4f);
    [SerializeField] private bool createRuntimeSmokeHazard = true;
    [FormerlySerializedAs("smokeHazard")]
    [SerializeField] private SmokeHazard smokeHazard;
    [SerializeField] private HazardIsolationDevice[] hazardIsolationDevices = Array.Empty<HazardIsolationDevice>();

    [SerializeField] [Min(0f)] private float surfaceOffset = 0.03f;
    [SerializeField] private LayerMask firePlacementMask = ~0;
    [SerializeField] private QueryTriggerInteraction firePlacementTriggerInteraction = QueryTriggerInteraction.Ignore;

    private Transform runtimeRoot;
    private SmokeHazard runtimeSmokeHazard;
    private IncidentFireSpawnProfile runtimeSpawnProfile;

    public string FireOriginKey => fireOriginKey;
    public string LogicalLocationKey => logicalLocationKey;
    public bool IsDefaultAnchor => isDefaultAnchor;
    public Transform RuntimeRoot => runtimeRoot;
    public SmokeHazard RuntimeSmokeHazard => runtimeSmokeHazard;
    public bool HasConfiguredHazardIsolationDevices => hazardIsolationDevices != null && hazardIsolationDevices.Length > 0;
    public Vector3 RuntimeZoneSize => runtimeZoneSize;
    public LayerMask FirePlacementMask => firePlacementMask;
    public QueryTriggerInteraction FirePlacementTriggerInteraction => firePlacementTriggerInteraction;
    public float SurfaceOffset => surfaceOffset;

    private void OnValidate()
    {
        ResolveSmokeHazardReference();
    }

    public bool MatchesFireOrigin(string key)
    {
        return MatchesKey(fireOriginKey, key);
    }

    public bool MatchesLogicalLocation(string key)
    {
        return MatchesKey(logicalLocationKey, key);
    }

    public bool ApplyPayload(
        IncidentWorldSetupPayload payload,
        IncidentFirePrefabLibrary firePrefabLibrary,
        IncidentFireSpawnProfile fireSpawnProfile = null,
        FireSimulationManager fireSimulationManager = null)
    {
        return ApplyPayloadFromResolvedSource(
            payload,
            firePrefabLibrary,
            fireSpawnProfile,
            fireSimulationManager,
            transform.position,
            ResolveFallbackRotation());
    }

    public bool ApplyPayloadFromResolvedSource(
        IncidentWorldSetupPayload payload,
        IncidentFirePrefabLibrary firePrefabLibrary,
        IncidentFireSpawnProfile fireSpawnProfile,
        FireSimulationManager fireSimulationManager,
        Vector3 primaryPosition,
        Quaternion primaryRotation)
    {
        if (payload == null)
        {
            return false;
        }

        if (fireSpawnProfile == null)
        {
            Debug.LogWarning($"{nameof(IncidentPayloadAnchor)} on '{name}' requires an {nameof(IncidentFireSpawnProfile)}.", this);
            return false;
        }

        runtimeSpawnProfile = fireSpawnProfile;
        ClearRuntimeObjects();

        if (fireSimulationManager == null)
        {
            Debug.LogWarning(
                $"{nameof(IncidentPayloadAnchor)} on '{name}' requires a {nameof(FireSimulationManager)}. " +
                "Legacy Fire-prefab spawning has been removed.",
                this);
            return false;
        }

        runtimeRoot = CreateRuntimeRoot();
        ApplyPayloadToSimulation(payload, fireSimulationManager, primaryPosition, primaryRotation);
        ConfigureSmoke(payload, runtimeRoot, fireSimulationManager);
        ConfigureHazardIsolationDevices(payload, fireSimulationManager);
        return true;
    }

    public void SetSmokeHazard(SmokeHazard value)
    {
        smokeHazard = value;
    }

    private Transform CreateRuntimeRoot()
    {
        Transform parent = runtimeParent != null ? runtimeParent : transform;
        GameObject runtimeObject = new GameObject("RuntimeIncident");
        runtimeObject.transform.SetParent(parent, false);
        runtimeObject.transform.localPosition = Vector3.zero;
        runtimeObject.transform.localRotation = Quaternion.identity;
        runtimeObject.transform.localScale = Vector3.one;
        return runtimeObject.transform;
    }

    private void ApplyPayloadToSimulation(
        IncidentWorldSetupPayload payload,
        FireSimulationManager fireSimulationManager,
        Vector3 primaryPosition,
        Quaternion primaryRotation)
    {
        FireHazardType primaryHazardType = IncidentPayloadStartupTask.ResolveFireHazardType(payload.hazardType);

        int requestedActiveFireCount = ResolveRequestedTotalFireCount(payload);
        int requestedSecondaryCount = Mathf.Max(0, requestedActiveFireCount - 1);
        float primaryIntensity = Mathf.Clamp01(payload.initialFireIntensity);
        float secondaryIntensity = Mathf.Clamp01(primaryIntensity * Mathf.Clamp01(runtimeSpawnProfile.ActiveSecondaryIntensityScale));

        List<SpawnPlacement> activePlacements = BuildInitialSecondaryPlacements(
            primaryPosition,
            primaryRotation,
            requestedSecondaryCount);
        List<FireIncidentPlacement> simulationPlacements = new List<FireIncidentPlacement>(activePlacements.Count);
        if (activePlacements.Count > 0)
        {
            simulationPlacements.Add(CreateIncidentPlacement(activePlacements[0], primaryIntensity));
        }

        for (int i = 1; i < activePlacements.Count; i++)
        {
            simulationPlacements.Add(CreateIncidentPlacement(activePlacements[i], secondaryIntensity));
        }

        if (runtimeSpawnProfile.SpawnLatentSpreadNodes && runtimeSpawnProfile.LatentSpreadNodeCount > 0)
        {
            List<SpawnPlacement> latentPlacements = BuildLatentSpreadPlacements(activePlacements);
            for (int i = 0; i < latentPlacements.Count; i++)
            {
                simulationPlacements.Add(CreateIncidentPlacement(latentPlacements[i], 0f));
            }
        }

        if (!fireSimulationManager.ApplyIncidentPlacements(primaryHazardType, hazardSourceIsolated: false, simulationPlacements))
        {
            fireSimulationManager.BeginIncident(primaryHazardType, hazardSourceIsolated: false);
            for (int i = 0; i < simulationPlacements.Count; i++)
            {
                FireIncidentPlacement placement = simulationPlacements[i];
                fireSimulationManager.TrackClosestNode(placement.Position, placement.InitialIntensity01);
            }
        }
    }

    private List<SpawnPlacement> BuildInitialSecondaryPlacements(
        Vector3 primaryPosition,
        Quaternion primaryRotation,
        int requestedSecondaryCount)
    {
        Vector3 primarySurfaceNormal = ResolvePlacementNormal(primaryRotation);
        List<SpawnPlacement> placements = new List<SpawnPlacement>
        {
            new SpawnPlacement(primaryPosition, primaryRotation, primarySurfaceNormal)
        };
        requestedSecondaryCount = Mathf.Max(0, requestedSecondaryCount);
        if (requestedSecondaryCount <= 0)
        {
            return placements;
        }

        System.Random random = new System.Random(Guid.NewGuid().GetHashCode());
        int maxAttempts = Mathf.Max(
            requestedSecondaryCount,
            requestedSecondaryCount * Mathf.Max(1, runtimeSpawnProfile.PlacementAttemptsPerSecondaryFire));
        int attempts = 0;
        while (placements.Count - 1 < requestedSecondaryCount && attempts < maxAttempts)
        {
            attempts++;
            if (!TryFindPlacement(
                    random,
                    primaryPosition,
                    primarySurfaceNormal,
                    runtimeSpawnProfile.SecondaryFireRange,
                    Mathf.Max(0f, runtimeSpawnProfile.MinimumSecondaryFireSpacing),
                    placements,
                    out SpawnPlacement placement))
            {
                continue;
            }

            placements.Add(placement);
        }

        if (placements.Count - 1 < requestedSecondaryCount)
        {
            Debug.LogWarning(
                $"{nameof(IncidentPayloadAnchor)} on '{name}' only found {placements.Count - 1}/{requestedSecondaryCount} active secondary placements " +
                $"within range {runtimeSpawnProfile.SecondaryFireRange:0.##}.",
                this);
        }

        return placements;
    }

    private List<SpawnPlacement> BuildLatentSpreadPlacements(List<SpawnPlacement> activePlacements)
    {
        List<SpawnPlacement> latentPlacements = new List<SpawnPlacement>();
        if (!runtimeSpawnProfile.SpawnLatentSpreadNodes ||
            runtimeSpawnProfile.LatentSpreadNodeCount <= 0 ||
            activePlacements == null ||
            activePlacements.Count == 0)
        {
            return latentPlacements;
        }

        List<SpawnPlacement> allPlacements = new List<SpawnPlacement>(activePlacements);
        List<SpawnPlacement> currentFrontier = new List<SpawnPlacement>();
        int seedCount = Mathf.Min(activePlacements.Count, Mathf.Max(1, runtimeSpawnProfile.LatentSpreadSeedLimit));
        for (int i = 0; i < seedCount; i++)
        {
            currentFrontier.Add(activePlacements[i]);
        }

        System.Random random = new System.Random(Guid.NewGuid().GetHashCode());
        int depthLimit = Mathf.Max(1, runtimeSpawnProfile.LatentSpreadBranchDepth);
        for (int depth = 0; depth < depthLimit && latentPlacements.Count < runtimeSpawnProfile.LatentSpreadNodeCount; depth++)
        {
            if (currentFrontier.Count == 0)
            {
                break;
            }

            List<SpawnPlacement> nextFrontier = new List<SpawnPlacement>();
            int attemptsPerDepth =
                Mathf.Max(1, currentFrontier.Count) * Mathf.Max(1, runtimeSpawnProfile.PlacementAttemptsPerSecondaryFire);
            int attempts = 0;
            while (attempts < attemptsPerDepth && latentPlacements.Count < runtimeSpawnProfile.LatentSpreadNodeCount)
            {
                attempts++;
                SpawnPlacement seed = currentFrontier[random.Next(currentFrontier.Count)];
                if (!TryFindPlacement(
                        random,
                        seed.Position,
                        seed.SurfaceNormal,
                        runtimeSpawnProfile.LatentSpreadRange,
                        Mathf.Max(0f, runtimeSpawnProfile.MinimumLatentNodeSpacing),
                        allPlacements,
                        out SpawnPlacement placement))
                {
                    continue;
                }

                latentPlacements.Add(placement);
                allPlacements.Add(placement);
                nextFrontier.Add(placement);
            }

            currentFrontier = nextFrontier;
        }

        return latentPlacements;
    }

    private bool TryFindPlacement(
        System.Random random,
        Vector3 seedPosition,
        Vector3 seedSurfaceNormal,
        float placementRange,
        float minimumSpacing,
        List<SpawnPlacement> existingPlacements,
        out SpawnPlacement placement)
    {
        placement = default;
        if (random == null || existingPlacements == null)
        {
            return false;
        }

        Vector3 surfaceUp = seedSurfaceNormal.sqrMagnitude > 0.0001f ? seedSurfaceNormal.normalized : Vector3.up;
        int segmentCount = Mathf.Max(3, runtimeSpawnProfile.ParabolaSegments);
        int variantCount = Mathf.Max(1, runtimeSpawnProfile.PlacementTrajectoryVariants);
        List<PlacementCandidate> candidates = new List<PlacementCandidate>(variantCount);
        for (int variantIndex = 0; variantIndex < variantCount; variantIndex++)
        {
            BuildSurfaceBasis(surfaceUp, out Vector3 surfaceForward, out Vector3 surfaceRight);
            float horizontalDistance = Mathf.Lerp(
                Mathf.Min(minimumSpacing, placementRange),
                Mathf.Max(minimumSpacing, placementRange),
                (float)random.NextDouble());
            float planarAngle = (float)(random.NextDouble() * Math.PI * 2d);
            Vector3 travelDirection =
                ((surfaceForward * Mathf.Cos(planarAngle)) + (surfaceRight * Mathf.Sin(planarAngle))).normalized;
            Vector3 lateralDirection = Vector3.Cross(surfaceUp, travelDirection).normalized;
            Vector3 launchPosition = seedPosition + surfaceUp * Mathf.Max(0f, runtimeSpawnProfile.ParabolaLaunchHeight);
            Vector3 landingPosition = seedPosition + travelDirection * horizontalDistance;
            float apexHeight = Mathf.Max(
                0.05f,
                runtimeSpawnProfile.ParabolaApexHeight + GetSignedRandomRange(random, runtimeSpawnProfile.ParabolaApexHeightJitter));
            float lateralOffset = GetSignedRandomRange(random, runtimeSpawnProfile.ParabolaLateralOffset);
            Vector3 controlPoint =
                Vector3.Lerp(launchPosition, landingPosition, 0.5f) +
                (surfaceUp * apexHeight) +
                (lateralDirection * lateralOffset);

            Vector3 previousPoint = launchPosition;
            for (int segmentIndex = 1; segmentIndex <= segmentCount; segmentIndex++)
            {
                float t = segmentIndex / (float)segmentCount;
                Vector3 nextPoint = EvaluateQuadraticBezier(launchPosition, controlPoint, landingPosition, t);
                Vector3 segment = nextPoint - previousPoint;
                float segmentLength = segment.magnitude;
                if (segmentLength <= 0.0001f)
                {
                    previousPoint = nextPoint;
                    continue;
                }

                if (Physics.SphereCast(
                        previousPoint,
                        Mathf.Max(0.01f, runtimeSpawnProfile.ParabolaCastRadius),
                        segment / segmentLength,
                        out RaycastHit hit,
                        segmentLength,
                        firePlacementMask,
                        firePlacementTriggerInteraction) &&
                    IsValidPlacementHit(hit, existingPlacements, minimumSpacing))
                {
                    Vector3 position = hit.point + hit.normal * Mathf.Max(0f, surfaceOffset);
                    float weight = CalculatePlacementSurfaceWeight(hit.normal);
                    candidates.Add(new PlacementCandidate(
                        new SpawnPlacement(position, ResolveSurfaceRotation(hit.normal), hit.normal),
                        weight));
                    break;
                }

                previousPoint = nextPoint;
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        placement = ChoosePlacementCandidate(random, candidates);
        return true;
    }

    private int ResolveRequestedTotalFireCount(IncidentWorldSetupPayload payload)
    {
        if (payload != null && payload.initialFireCount > 0)
        {
            return payload.initialFireCount;
        }

        return Mathf.Max(1, runtimeSpawnProfile.SecondaryFirePointCount + 1);
    }

    private void ConfigureSmoke(IncidentWorldSetupPayload payload, Transform parent, FireSimulationManager fireSimulationManager)
    {
        SmokeHazard targetSmokeHazard = ResolveSmokeHazardReference();
        if (targetSmokeHazard == null && createRuntimeSmokeHazard && parent != null)
        {
            Transform existingSmokeObj = parent.Find("RuntimeSmoke");
            if (existingSmokeObj != null)
            {
                targetSmokeHazard = existingSmokeObj.GetComponent<SmokeHazard>();
            }
            else
            {
                GameObject smokeObj = new GameObject("RuntimeSmoke");
                smokeObj.transform.SetParent(parent, false);
                smokeObj.transform.localPosition = Vector3.zero;
                smokeObj.transform.localRotation = Quaternion.identity;
                smokeObj.transform.localScale = Vector3.one;
                targetSmokeHazard = smokeObj.AddComponent<SmokeHazard>();
            }

            BoxCollider trigger = parent.GetComponent<BoxCollider>();
            if (trigger != null)
            {
                targetSmokeHazard.SetTriggerZone(trigger);
            }
        }

        runtimeSmokeHazard = targetSmokeHazard;
        if (runtimeSmokeHazard == null)
        {
            return;
        }

        Collider sharedAreaVolume = GetComponent<Collider>();
        if (sharedAreaVolume != null)
        {
            runtimeSmokeHazard.SetTriggerZone(sharedAreaVolume);
        }

        runtimeSmokeHazard.SetFireSimulationManager(fireSimulationManager);
        runtimeSmokeHazard.SetStartSmokeDensity(payload.startSmokeDensity, applyImmediately: true);
        runtimeSmokeHazard.SetSmokeAccumulationMultiplier(payload.smokeAccumulationMultiplier);
    }

    private void ConfigureHazardIsolationDevices(IncidentWorldSetupPayload payload, FireSimulationManager fireSimulationManager)
    {
        if (hazardIsolationDevices == null || hazardIsolationDevices.Length == 0 || payload == null)
        {
            return;
        }

        FireHazardType fireHazardType = IncidentPayloadStartupTask.ResolveFireHazardType(payload.hazardType);
        for (int i = 0; i < hazardIsolationDevices.Length; i++)
        {
            HazardIsolationDevice device = hazardIsolationDevices[i];
            if (device == null)
            {
                continue;
            }

            device.SetFireSimulationManager(fireSimulationManager);
            device.SetRuntimeHazardType(fireHazardType);
            device.SetRuntimeIsolationState(false, invokeEvents: false);
        }
    }

    private bool IsValidPlacementHit(RaycastHit hit, List<SpawnPlacement> existingPlacements, float minimumSpacing)
    {
        if (hit.collider == null)
        {
            return false;
        }

        Vector3 candidatePosition = hit.point + hit.normal * Mathf.Max(0f, surfaceOffset);
        float minSpacing = Mathf.Max(0f, minimumSpacing);
        for (int i = 0; i < existingPlacements.Count; i++)
        {
            if ((existingPlacements[i].Position - candidatePosition).sqrMagnitude < minSpacing * minSpacing)
            {
                return false;
            }
        }

        return true;
    }

    private Quaternion ResolveFallbackRotation()
    {
        Vector3 forward = transform.forward.sqrMagnitude > 0f ? transform.forward : Vector3.forward;
        return Quaternion.LookRotation(forward, Vector3.up);
    }

    private Quaternion ResolveSurfaceRotation(Vector3 surfaceNormal)
    {
        Vector3 up = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, up);
        if (projectedForward.sqrMagnitude < 0.0001f)
        {
            projectedForward = Vector3.ProjectOnPlane(transform.right, up);
        }

        if (projectedForward.sqrMagnitude < 0.0001f)
        {
            projectedForward = Vector3.Cross(up, Vector3.right);
        }

        if (projectedForward.sqrMagnitude < 0.0001f)
        {
            projectedForward = Vector3.forward;
        }

        return Quaternion.LookRotation(projectedForward.normalized, up);
    }

    private Vector3 ResolvePlacementNormal(Quaternion rotation)
    {
        Vector3 normal = rotation * Vector3.up;
        return normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
    }

    private static void BuildSurfaceBasis(Vector3 surfaceUp, out Vector3 forward, out Vector3 right)
    {
        Vector3 referenceForward = Mathf.Abs(Vector3.Dot(surfaceUp, Vector3.forward)) < 0.95f
            ? Vector3.forward
            : Vector3.right;
        forward = Vector3.ProjectOnPlane(referenceForward, surfaceUp);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(Vector3.up, surfaceUp);
        }

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.Cross(surfaceUp, Vector3.right);
        }

        forward.Normalize();
        right = Vector3.Cross(surfaceUp, forward);
        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.Cross(surfaceUp, Vector3.forward);
        }

        right.Normalize();
    }

    private float CalculatePlacementSurfaceWeight(Vector3 surfaceNormal)
    {
        Vector3 normalizedNormal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        float upDot = Vector3.Dot(normalizedNormal, Vector3.up);
        if (upDot >= 0.6f)
        {
            return Mathf.Max(0.01f, runtimeSpawnProfile.FloorPlacementWeight);
        }

        if (upDot <= -0.35f)
        {
            return Mathf.Max(0.01f, runtimeSpawnProfile.CeilingPlacementWeight);
        }

        return Mathf.Max(0.01f, runtimeSpawnProfile.WallPlacementWeight);
    }

    private static SpawnPlacement ChoosePlacementCandidate(System.Random random, List<PlacementCandidate> candidates)
    {
        if (random == null || candidates == null || candidates.Count == 0)
        {
            return default;
        }

        float totalWeight = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight += Mathf.Max(0.01f, candidates[i].Weight);
        }

        float pick = (float)random.NextDouble() * totalWeight;
        for (int i = 0; i < candidates.Count; i++)
        {
            pick -= Mathf.Max(0.01f, candidates[i].Weight);
            if (pick <= 0f)
            {
                return candidates[i].Placement;
            }
        }

        return candidates[candidates.Count - 1].Placement;
    }

    private static Vector3 EvaluateQuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
    {
        float clampedT = Mathf.Clamp01(t);
        float invT = 1f - clampedT;
        return (invT * invT * start) + (2f * invT * clampedT * control) + (clampedT * clampedT * end);
    }

    private static float GetSignedRandomRange(System.Random random, float magnitude)
    {
        if (random == null || magnitude <= 0f)
        {
            return 0f;
        }

        return (((float)random.NextDouble() * 2f) - 1f) * magnitude;
    }

    private static FireIncidentPlacement CreateIncidentPlacement(SpawnPlacement placement, float initialIntensity01)
    {
        return new FireIncidentPlacement(
            placement.Position,
            placement.SurfaceNormal,
            initialIntensity01);
    }

    private void ClearRuntimeObjects()
    {
        if (runtimeRoot != null)
        {
            Destroy(runtimeRoot.gameObject);
        }

        runtimeRoot = null;
        runtimeSmokeHazard = null;
    }

    private SmokeHazard ResolveSmokeHazardReference()
    {
        if (smokeHazard == null)
        {
            smokeHazard = GetComponentInChildren<SmokeHazard>(true);
        }

        return smokeHazard;
    }

    private static bool MatchesKey(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string ValueOrUnknown(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
    }

    private readonly struct SpawnPlacement
    {
        public SpawnPlacement(Vector3 position, Quaternion rotation, Vector3 surfaceNormal)
        {
            Position = position;
            Rotation = rotation;
            SurfaceNormal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 SurfaceNormal { get; }
    }

    private readonly struct PlacementCandidate
    {
        public PlacementCandidate(SpawnPlacement placement, float weight)
        {
            Placement = placement;
            Weight = weight;
        }

        public SpawnPlacement Placement { get; }
        public float Weight { get; }
    }

}
