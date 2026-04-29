using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class IncidentPayloadAnchor : MonoBehaviour
{
    private const float DownFacingSurfaceDotThreshold = -0.35f;
    private const float UpFacingSurfaceDotThreshold = 0.6f;
    private const float TopSurfaceProbePadding = 0.08f;

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
            payload,
            primaryPosition,
            primaryRotation,
            requestedSecondaryCount);
        List<FireIncidentPlacement> simulationPlacements = new List<FireIncidentPlacement>(activePlacements.Count);
        if (activePlacements.Count > 0)
        {
            simulationPlacements.Add(CreateIncidentPlacement(activePlacements[0], primaryIntensity, FireIncidentNodeKind.Primary));
        }

        for (int i = 1; i < activePlacements.Count; i++)
        {
            simulationPlacements.Add(CreateIncidentPlacement(activePlacements[i], secondaryIntensity, FireIncidentNodeKind.Secondary));
        }

        if (runtimeSpawnProfile.SpawnLatentSpreadNodes && runtimeSpawnProfile.LatentSpreadNodeCount > 0)
        {
            List<SpawnPlacement> latentPlacements = BuildLatentSpreadPlacements(payload, activePlacements);
            for (int i = 0; i < latentPlacements.Count; i++)
            {
                simulationPlacements.Add(CreateIncidentPlacement(latentPlacements[i], 0f, FireIncidentNodeKind.Late));
            }
        }

        if (!fireSimulationManager.ApplyIncidentPlacements(primaryHazardType, hazardSourceIsolated: false, simulationPlacements))
        {
            fireSimulationManager.BeginIncident(primaryHazardType, hazardSourceIsolated: false);
            for (int i = 0; i < simulationPlacements.Count; i++)
            {
                FireIncidentPlacement placement = simulationPlacements[i];
                fireSimulationManager.TrackClosestNode(placement.Position, placement.InitialIntensity01, placement.Kind);
            }
        }
    }

    private List<SpawnPlacement> BuildInitialSecondaryPlacements(
        IncidentWorldSetupPayload payload,
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

        System.Random random = new System.Random(IncidentSeedUtility.ResolvePlacementSeed(payload, "secondary"));
        int maxAttempts = Mathf.Max(
            requestedSecondaryCount,
            requestedSecondaryCount * Mathf.Max(1, runtimeSpawnProfile.PlacementAttemptsPerNode));
        int attempts = 0;
        while (placements.Count - 1 < requestedSecondaryCount && attempts < maxAttempts)
        {
            attempts++;
            if (!TryFindPlacement(
                    random,
                    primaryPosition,
                    primarySurfaceNormal,
                    runtimeSpawnProfile.PlacementRange,
                    Mathf.Max(0f, runtimeSpawnProfile.MinimumNodeSpacing),
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
                $"within range {runtimeSpawnProfile.PlacementRange:0.##}.",
                this);
        }

        return placements;
    }

    private List<SpawnPlacement> BuildLatentSpreadPlacements(
        IncidentWorldSetupPayload payload,
        List<SpawnPlacement> activePlacements)
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
        System.Random random = new System.Random(IncidentSeedUtility.ResolvePlacementSeed(payload, "latent"));
        Queue<SpawnPlacement> pendingSeeds = new Queue<SpawnPlacement>(activePlacements);
        int placementsPerNode = Mathf.Max(1, runtimeSpawnProfile.LatentSpreadPlacementsPerNode);
        int attemptsPerNode = Mathf.Max(1, runtimeSpawnProfile.PlacementAttemptsPerNode);
        while (pendingSeeds.Count > 0 && latentPlacements.Count < runtimeSpawnProfile.LatentSpreadNodeCount)
        {
            SpawnPlacement seed = pendingSeeds.Dequeue();
            int successfulPlacementsForSeed = 0;
            int attemptsForSeed = 0;
            while (successfulPlacementsForSeed < placementsPerNode &&
                   attemptsForSeed < attemptsPerNode &&
                   latentPlacements.Count < runtimeSpawnProfile.LatentSpreadNodeCount)
            {
                attemptsForSeed++;
                if (!TryFindPlacement(
                        random,
                        seed.Position,
                        seed.SurfaceNormal,
                        runtimeSpawnProfile.PlacementRange,
                        Mathf.Max(0f, runtimeSpawnProfile.MinimumNodeSpacing),
                        allPlacements,
                        out SpawnPlacement placement))
                {
                    continue;
                }

                latentPlacements.Add(placement);
                allPlacements.Add(placement);
                pendingSeeds.Enqueue(placement);
                successfulPlacementsForSeed++;
            }
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
        return TryFindPlacement(
            random,
            seedPosition,
            seedSurfaceNormal,
            placementRange,
            minimumSpacing,
            existingPlacements,
            debugTraces: null,
            out placement);
    }

    private bool TryFindPlacement(
        System.Random random,
        Vector3 seedPosition,
        Vector3 seedSurfaceNormal,
        float placementRange,
        float minimumSpacing,
        List<SpawnPlacement> existingPlacements,
        List<DebugPlacementTrace> debugTraces,
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
            List<Vector3> debugPath = debugTraces != null ? new List<Vector3>(segmentCount + 1) : null;
            debugPath?.Add(launchPosition);
            bool hadHit = false;
            bool accepted = false;
            bool promoted = false;
            Vector3 hitPoint = Vector3.zero;
            Vector3 resolvedPoint = Vector3.zero;

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

                debugPath?.Add(nextPoint);

                bool didHit = Physics.SphereCast(
                    previousPoint,
                    Mathf.Max(0.01f, runtimeSpawnProfile.ParabolaCastRadius),
                    segment / segmentLength,
                    out RaycastHit hit,
                    segmentLength,
                    firePlacementMask,
                    firePlacementTriggerInteraction);
                if (didHit)
                {
                    hadHit = true;
                    hitPoint = hit.point;
                    if (TryResolveDerivedPlacement(
                            hit,
                            existingPlacements,
                            minimumSpacing,
                            out SpawnPlacement resolvedPlacement,
                            out float weight,
                            out bool wasPromoted))
                    {
                        accepted = true;
                        promoted = wasPromoted;
                        resolvedPoint = resolvedPlacement.Position;
                        candidates.Add(new PlacementCandidate(resolvedPlacement, weight));
                    }

                    break;
                }

                previousPoint = nextPoint;
            }

            if (debugTraces != null)
            {
                debugTraces.Add(new DebugPlacementTrace(
                    debugPath != null ? debugPath.ToArray() : Array.Empty<Vector3>(),
                    hadHit,
                    accepted,
                    promoted,
                    hitPoint,
                    resolvedPoint));
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        placement = ChoosePlacementCandidate(random, candidates);
        return true;
    }

    private bool TryResolveDerivedPlacement(
        RaycastHit hit,
        List<SpawnPlacement> existingPlacements,
        float minimumSpacing,
        out SpawnPlacement placement,
        out float weight,
        out bool wasPromoted)
    {
        placement = default;
        weight = 0f;
        wasPromoted = false;

        RaycastHit resolvedHit = hit;
        if (IsDownFacingSurface(hit.normal) &&
            !TryPromoteUndersideHitToTopSurface(hit, out resolvedHit))
        {
            return false;
        }

        wasPromoted = resolvedHit.collider != null &&
            resolvedHit.point != hit.point &&
            IsDownFacingSurface(hit.normal);

        if (!IsValidPlacementHit(resolvedHit, existingPlacements, minimumSpacing))
        {
            return false;
        }

        Vector3 position = resolvedHit.point + resolvedHit.normal * Mathf.Max(0f, surfaceOffset);
        placement = new SpawnPlacement(position, ResolveSurfaceRotation(resolvedHit.normal), resolvedHit.normal);
        weight = CalculatePlacementSurfaceWeight(resolvedHit.normal);
        return true;
    }

    public bool TryCreateDebugPlacementSample(
        IncidentFireSpawnProfile fireSpawnProfile,
        Vector3 seedPosition,
        Vector3 seedSurfaceNormal,
        float placementRange,
        float minimumSpacing,
        IReadOnlyList<Vector3> existingPositions,
        int randomSeed,
        out DebugPlacementSample sample)
    {
        sample = default;
        if (fireSpawnProfile == null)
        {
            return false;
        }

        IncidentFireSpawnProfile previousProfile = runtimeSpawnProfile;
        runtimeSpawnProfile = fireSpawnProfile;
        try
        {
            List<SpawnPlacement> existingPlacements = new List<SpawnPlacement>();
            if (existingPositions != null)
            {
                for (int i = 0; i < existingPositions.Count; i++)
                {
                    existingPlacements.Add(new SpawnPlacement(existingPositions[i], Quaternion.identity, Vector3.up));
                }
            }

            List<DebugPlacementTrace> debugTraces = new List<DebugPlacementTrace>();
            System.Random random = new System.Random(randomSeed);
            bool success = TryFindPlacement(
                random,
                seedPosition,
                seedSurfaceNormal,
                placementRange,
                minimumSpacing,
                existingPlacements,
                debugTraces,
                out SpawnPlacement placement);
            sample = new DebugPlacementSample(
                seedPosition,
                seedSurfaceNormal,
                success,
                debugTraces.ToArray(),
                success ? placement.Position : seedPosition,
                success ? placement.SurfaceNormal : seedSurfaceNormal);
            return success;
        }
        finally
        {
            runtimeSpawnProfile = previousProfile;
        }
    }

    public bool TryCreateDebugActivePlacementSession(
        IncidentFireSpawnProfile fireSpawnProfile,
        Vector3 primaryPosition,
        Quaternion primaryRotation,
        int requestedSecondaryCount,
        int randomSeed,
        out DebugActivePlacementSession session)
    {
        session = default;
        if (fireSpawnProfile == null)
        {
            return false;
        }

        IncidentFireSpawnProfile previousProfile = runtimeSpawnProfile;
        runtimeSpawnProfile = fireSpawnProfile;
        try
        {
            Vector3 primarySurfaceNormal = ResolvePlacementNormal(primaryRotation);
            List<SpawnPlacement> placements = new List<SpawnPlacement>
            {
                new SpawnPlacement(primaryPosition, primaryRotation, primarySurfaceNormal)
            };
            List<DebugActivePlacementAttempt> attempts = new List<DebugActivePlacementAttempt>();
            requestedSecondaryCount = Mathf.Max(0, requestedSecondaryCount);
            if (requestedSecondaryCount <= 0)
            {
                session = new DebugActivePlacementSession(
                    primaryPosition,
                    primaryRotation,
                    CreateDebugPlacementResults(placements),
                    attempts.ToArray());
                return true;
            }

            System.Random random = new System.Random(randomSeed);
            int maxAttempts = Mathf.Max(
                requestedSecondaryCount,
                requestedSecondaryCount * Mathf.Max(1, runtimeSpawnProfile.PlacementAttemptsPerNode));
            int attemptIndex = 0;
            while (placements.Count - 1 < requestedSecondaryCount && attemptIndex < maxAttempts)
            {
                attemptIndex++;
                List<DebugPlacementTrace> traces = new List<DebugPlacementTrace>();
                bool success = TryFindPlacement(
                    random,
                    primaryPosition,
                    primarySurfaceNormal,
                    runtimeSpawnProfile.PlacementRange,
                    Mathf.Max(0f, runtimeSpawnProfile.MinimumNodeSpacing),
                    placements,
                    traces,
                    out SpawnPlacement placement);

                Vector3 placementPosition = success ? placement.Position : primaryPosition;
                Vector3 placementNormal = success ? placement.SurfaceNormal : primarySurfaceNormal;
                attempts.Add(new DebugActivePlacementAttempt(
                    attemptIndex,
                    success,
                    traces.ToArray(),
                    placementPosition,
                    placementNormal));
                if (!success)
                {
                    continue;
                }

                placements.Add(placement);
            }

            session = new DebugActivePlacementSession(
                primaryPosition,
                primaryRotation,
                CreateDebugPlacementResults(placements),
                attempts.ToArray());
            return true;
        }
        finally
        {
            runtimeSpawnProfile = previousProfile;
        }
    }

    public bool TryCreateDebugRuntimePlacementSession(
        IncidentFireSpawnProfile fireSpawnProfile,
        Vector3 primaryPosition,
        Quaternion primaryRotation,
        int requestedActiveFireCount,
        int activeRandomSeed,
        int latentRandomSeed,
        out DebugRuntimePlacementSession session)
    {
        session = default;
        if (fireSpawnProfile == null)
        {
            return false;
        }

        IncidentFireSpawnProfile previousProfile = runtimeSpawnProfile;
        runtimeSpawnProfile = fireSpawnProfile;
        try
        {
            Vector3 primarySurfaceNormal = ResolvePlacementNormal(primaryRotation);
            List<SpawnPlacement> activePlacements = new List<SpawnPlacement>
            {
                new SpawnPlacement(primaryPosition, primaryRotation, primarySurfaceNormal)
            };
            List<SpawnPlacement> latentPlacements = new List<SpawnPlacement>();
            List<DebugRuntimePlacementAttempt> attempts = new List<DebugRuntimePlacementAttempt>();

            int requestedSecondaryCount = Mathf.Max(0, requestedActiveFireCount - 1);
            if (requestedSecondaryCount > 0)
            {
                System.Random activeRandom = new System.Random(activeRandomSeed);
                int maxAttempts = Mathf.Max(
                    requestedSecondaryCount,
                    requestedSecondaryCount * Mathf.Max(1, runtimeSpawnProfile.PlacementAttemptsPerNode));
                int attemptIndex = 0;
                while (activePlacements.Count - 1 < requestedSecondaryCount && attemptIndex < maxAttempts)
                {
                    attemptIndex++;
                    List<DebugPlacementTrace> traces = new List<DebugPlacementTrace>();
                    bool success = TryFindPlacement(
                        activeRandom,
                        primaryPosition,
                        primarySurfaceNormal,
                        runtimeSpawnProfile.PlacementRange,
                        Mathf.Max(0f, runtimeSpawnProfile.MinimumNodeSpacing),
                        activePlacements,
                        traces,
                        out SpawnPlacement placement);

                    attempts.Add(new DebugRuntimePlacementAttempt(
                        attempts.Count + 1,
                        DebugPlacementPhase.ActiveSecondary,
                        primaryPosition,
                        primarySurfaceNormal,
                        success,
                        traces.ToArray(),
                        success ? placement.Position : primaryPosition,
                        success ? placement.SurfaceNormal : primarySurfaceNormal));
                    if (!success)
                    {
                        continue;
                    }

                    activePlacements.Add(placement);
                }
            }

            if (runtimeSpawnProfile.SpawnLatentSpreadNodes &&
                runtimeSpawnProfile.LatentSpreadNodeCount > 0 &&
                activePlacements.Count > 0)
            {
                List<SpawnPlacement> allPlacements = new List<SpawnPlacement>(activePlacements);
                System.Random latentRandom = new System.Random(latentRandomSeed);
                Queue<SpawnPlacement> pendingSeeds = new Queue<SpawnPlacement>(activePlacements);
                int placementsPerNode = Mathf.Max(1, runtimeSpawnProfile.LatentSpreadPlacementsPerNode);
                int attemptsPerNode = Mathf.Max(1, runtimeSpawnProfile.PlacementAttemptsPerNode);
                while (pendingSeeds.Count > 0 && latentPlacements.Count < runtimeSpawnProfile.LatentSpreadNodeCount)
                {
                    SpawnPlacement seed = pendingSeeds.Dequeue();
                    int successfulPlacementsForSeed = 0;
                    int attemptsForSeed = 0;
                    while (successfulPlacementsForSeed < placementsPerNode &&
                           attemptsForSeed < attemptsPerNode &&
                           latentPlacements.Count < runtimeSpawnProfile.LatentSpreadNodeCount)
                    {
                        attemptsForSeed++;
                        List<DebugPlacementTrace> traces = new List<DebugPlacementTrace>();
                        bool success = TryFindPlacement(
                            latentRandom,
                            seed.Position,
                            seed.SurfaceNormal,
                            runtimeSpawnProfile.PlacementRange,
                            Mathf.Max(0f, runtimeSpawnProfile.MinimumNodeSpacing),
                            allPlacements,
                            traces,
                            out SpawnPlacement placement);

                        attempts.Add(new DebugRuntimePlacementAttempt(
                            attempts.Count + 1,
                            DebugPlacementPhase.LatentSpread,
                            seed.Position,
                            seed.SurfaceNormal,
                            success,
                            traces.ToArray(),
                            success ? placement.Position : seed.Position,
                            success ? placement.SurfaceNormal : seed.SurfaceNormal));
                        if (!success)
                        {
                            continue;
                        }

                        latentPlacements.Add(placement);
                        allPlacements.Add(placement);
                        pendingSeeds.Enqueue(placement);
                        successfulPlacementsForSeed++;
                    }
                }
            }

            session = new DebugRuntimePlacementSession(
                primaryPosition,
                primaryRotation,
                CreateDebugPlacementResults(activePlacements),
                CreateDebugPlacementResults(latentPlacements),
                attempts.ToArray());
            return true;
        }
        finally
        {
            runtimeSpawnProfile = previousProfile;
        }
    }

    private static DebugPlacementResult[] CreateDebugPlacementResults(List<SpawnPlacement> placements)
    {
        if (placements == null || placements.Count == 0)
        {
            return Array.Empty<DebugPlacementResult>();
        }

        DebugPlacementResult[] results = new DebugPlacementResult[placements.Count];
        for (int i = 0; i < placements.Count; i++)
        {
            results[i] = new DebugPlacementResult(placements[i].Position, placements[i].SurfaceNormal);
        }

        return results;
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

    private bool TryPromoteUndersideHitToTopSurface(RaycastHit undersideHit, out RaycastHit topSurfaceHit)
    {
        topSurfaceHit = default;
        Collider targetCollider = undersideHit.collider;
        if (targetCollider == null)
        {
            return false;
        }

        Bounds bounds = targetCollider.bounds;
        float probePadding = Mathf.Max(TopSurfaceProbePadding, surfaceOffset + 0.02f);
        Vector3 rayOrigin = new Vector3(
            undersideHit.point.x,
            bounds.max.y + probePadding,
            undersideHit.point.z);
        float rayDistance = Mathf.Max(0.1f, (bounds.size.y + (probePadding * 2f)));
        Ray ray = new Ray(rayOrigin, Vector3.down);
        if (!targetCollider.Raycast(ray, out topSurfaceHit, rayDistance))
        {
            return false;
        }

        return Vector3.Dot(topSurfaceHit.normal.normalized, Vector3.up) >= UpFacingSurfaceDotThreshold;
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
        if (upDot >= UpFacingSurfaceDotThreshold)
        {
            return Mathf.Max(0.01f, runtimeSpawnProfile.FloorPlacementWeight);
        }

        if (upDot <= DownFacingSurfaceDotThreshold)
        {
            return Mathf.Max(0.01f, runtimeSpawnProfile.CeilingPlacementWeight);
        }

        return Mathf.Max(0.01f, runtimeSpawnProfile.WallPlacementWeight);
    }

    private static bool IsDownFacingSurface(Vector3 surfaceNormal)
    {
        Vector3 normalizedNormal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        return Vector3.Dot(normalizedNormal, Vector3.up) <= DownFacingSurfaceDotThreshold;
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

    private static FireIncidentPlacement CreateIncidentPlacement(
        SpawnPlacement placement,
        float initialIntensity01,
        FireIncidentNodeKind kind)
    {
        return new FireIncidentPlacement(
            placement.Position,
            placement.SurfaceNormal,
            initialIntensity01,
            kind);
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

    public readonly struct DebugPlacementTrace
    {
        public DebugPlacementTrace(
            Vector3[] pathPoints,
            bool hadHit,
            bool accepted,
            bool promotedToTopSurface,
            Vector3 hitPoint,
            Vector3 resolvedPoint)
        {
            PathPoints = pathPoints ?? Array.Empty<Vector3>();
            HadHit = hadHit;
            Accepted = accepted;
            PromotedToTopSurface = promotedToTopSurface;
            HitPoint = hitPoint;
            ResolvedPoint = resolvedPoint;
        }

        public Vector3[] PathPoints { get; }
        public bool HadHit { get; }
        public bool Accepted { get; }
        public bool PromotedToTopSurface { get; }
        public Vector3 HitPoint { get; }
        public Vector3 ResolvedPoint { get; }
    }

    public readonly struct DebugPlacementSample
    {
        public DebugPlacementSample(
            Vector3 seedPosition,
            Vector3 seedSurfaceNormal,
            bool success,
            DebugPlacementTrace[] traces,
            Vector3 placementPosition,
            Vector3 placementSurfaceNormal)
        {
            SeedPosition = seedPosition;
            SeedSurfaceNormal = seedSurfaceNormal.sqrMagnitude > 0.0001f ? seedSurfaceNormal.normalized : Vector3.up;
            Success = success;
            Traces = traces ?? Array.Empty<DebugPlacementTrace>();
            PlacementPosition = placementPosition;
            PlacementSurfaceNormal = placementSurfaceNormal.sqrMagnitude > 0.0001f ? placementSurfaceNormal.normalized : Vector3.up;
        }

        public Vector3 SeedPosition { get; }
        public Vector3 SeedSurfaceNormal { get; }
        public bool Success { get; }
        public DebugPlacementTrace[] Traces { get; }
        public Vector3 PlacementPosition { get; }
        public Vector3 PlacementSurfaceNormal { get; }
    }

    public readonly struct DebugActivePlacementAttempt
    {
        public DebugActivePlacementAttempt(
            int attemptIndex,
            bool success,
            DebugPlacementTrace[] traces,
            Vector3 placementPosition,
            Vector3 placementSurfaceNormal)
        {
            AttemptIndex = attemptIndex;
            Success = success;
            Traces = traces ?? Array.Empty<DebugPlacementTrace>();
            PlacementPosition = placementPosition;
            PlacementSurfaceNormal = placementSurfaceNormal.sqrMagnitude > 0.0001f ? placementSurfaceNormal.normalized : Vector3.up;
        }

        public int AttemptIndex { get; }
        public bool Success { get; }
        public DebugPlacementTrace[] Traces { get; }
        public Vector3 PlacementPosition { get; }
        public Vector3 PlacementSurfaceNormal { get; }
    }

    public readonly struct DebugActivePlacementSession
    {
        public DebugActivePlacementSession(
            Vector3 primaryPosition,
            Quaternion primaryRotation,
            DebugPlacementResult[] placements,
            DebugActivePlacementAttempt[] attempts)
        {
            PrimaryPosition = primaryPosition;
            PrimaryRotation = primaryRotation;
            Placements = placements ?? Array.Empty<DebugPlacementResult>();
            Attempts = attempts ?? Array.Empty<DebugActivePlacementAttempt>();
        }

        public Vector3 PrimaryPosition { get; }
        public Quaternion PrimaryRotation { get; }
        public DebugPlacementResult[] Placements { get; }
        public DebugActivePlacementAttempt[] Attempts { get; }
    }

    public readonly struct DebugPlacementResult
    {
        public DebugPlacementResult(Vector3 position, Vector3 surfaceNormal)
        {
            Position = position;
            SurfaceNormal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        }

        public Vector3 Position { get; }
        public Vector3 SurfaceNormal { get; }
    }

    public enum DebugPlacementPhase
    {
        ActiveSecondary = 0,
        LatentSpread = 1
    }

    public readonly struct DebugRuntimePlacementAttempt
    {
        public DebugRuntimePlacementAttempt(
            int attemptIndex,
            DebugPlacementPhase phase,
            Vector3 seedPosition,
            Vector3 seedSurfaceNormal,
            bool success,
            DebugPlacementTrace[] traces,
            Vector3 placementPosition,
            Vector3 placementSurfaceNormal)
        {
            AttemptIndex = attemptIndex;
            Phase = phase;
            SeedPosition = seedPosition;
            SeedSurfaceNormal = seedSurfaceNormal.sqrMagnitude > 0.0001f ? seedSurfaceNormal.normalized : Vector3.up;
            Success = success;
            Traces = traces ?? Array.Empty<DebugPlacementTrace>();
            PlacementPosition = placementPosition;
            PlacementSurfaceNormal = placementSurfaceNormal.sqrMagnitude > 0.0001f ? placementSurfaceNormal.normalized : Vector3.up;
        }

        public int AttemptIndex { get; }
        public DebugPlacementPhase Phase { get; }
        public Vector3 SeedPosition { get; }
        public Vector3 SeedSurfaceNormal { get; }
        public bool Success { get; }
        public DebugPlacementTrace[] Traces { get; }
        public Vector3 PlacementPosition { get; }
        public Vector3 PlacementSurfaceNormal { get; }
    }

    public readonly struct DebugRuntimePlacementSession
    {
        public DebugRuntimePlacementSession(
            Vector3 primaryPosition,
            Quaternion primaryRotation,
            DebugPlacementResult[] activePlacements,
            DebugPlacementResult[] latentPlacements,
            DebugRuntimePlacementAttempt[] attempts)
        {
            PrimaryPosition = primaryPosition;
            PrimaryRotation = primaryRotation;
            ActivePlacements = activePlacements ?? Array.Empty<DebugPlacementResult>();
            LatentPlacements = latentPlacements ?? Array.Empty<DebugPlacementResult>();
            Attempts = attempts ?? Array.Empty<DebugRuntimePlacementAttempt>();
        }

        public Vector3 PrimaryPosition { get; }
        public Quaternion PrimaryRotation { get; }
        public DebugPlacementResult[] ActivePlacements { get; }
        public DebugPlacementResult[] LatentPlacements { get; }
        public DebugRuntimePlacementAttempt[] Attempts { get; }
    }

}
