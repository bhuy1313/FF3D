using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class VictimPlacementStartupTask : SceneStartupTask
{
    [Header("Victim Source")]
    [SerializeField] private GameObject victimPrefab;
    [SerializeField] private Transform runtimeParent;
    [SerializeField] private VictimSpawnPoint[] explicitSpawnPoints = Array.Empty<VictimSpawnPoint>();
    [SerializeField] private bool includeInactiveSpawnPoints = true;

    [Header("Spawn Rules")]
    [SerializeField] [Min(0)] private int fallbackVictimCountWhenUnknown;
    [SerializeField] [Min(0)] private int compromisedRiskFallbackVictimCount = 1;
    [SerializeField] [Min(1)] private int maxVictimCount = 4;
    [SerializeField] private bool deterministicPlacement = true;
    [SerializeField] private bool clearExistingVictimsBeforeSpawn;

    [Header("Fire Clearance")]
    [SerializeField] private bool removeFireNodesNearSpawnedVictims = true;
    [SerializeField] [Min(0f)] private float spawnedVictimFireClearRadius = 1.5f;
    [SerializeField] private bool avoidHazardLinkedFireNodesWhenSelectingSpawnPoints = true;

    [Header("Mission Sync")]
    [SerializeField] private IncidentMissionSystem incidentMissionSystem;
    [SerializeField] private bool refreshMissionObjectivesAfterSpawn = true;
    [SerializeField] private bool logSummary = true;

    protected override IEnumerator Execute(SceneStartupFlow startupFlow)
    {
        IncidentMapSetupRoot setupRoot = ResolveMapSetupRoot(startupFlow);
        IncidentWorldSetupPayload payload = ResolvePayload(setupRoot);
        if (payload == null)
        {
            yield break;
        }

        if (victimPrefab == null)
        {
            Debug.LogWarning($"{nameof(VictimPlacementStartupTask)}: Missing victim prefab.", this);
            yield break;
        }

        IncidentOriginArea resolvedOriginArea = setupRoot != null ? setupRoot.LastResolvedOriginArea : null;
        List<VictimSpawnPoint> spawnPoints = CollectSpawnPoints(resolvedOriginArea);
        if (spawnPoints.Count == 0)
        {
            Debug.LogWarning(
                resolvedOriginArea != null
                    ? $"{nameof(VictimPlacementStartupTask)}: No victim spawn points found for origin area '{resolvedOriginArea.EffectiveAreaKey}'."
                    : $"{nameof(VictimPlacementStartupTask)}: No victim spawn points found.",
                resolvedOriginArea != null ? resolvedOriginArea : this);
            yield break;
        }

        int spawnCount = ResolveVictimCount(payload);
        if (spawnCount <= 0)
        {
            if (refreshMissionObjectivesAfterSpawn)
            {
                RefreshMissionObjectives(startupFlow);
            }

            yield break;
        }

        if (clearExistingVictimsBeforeSpawn && ClearExistingVictims() > 0)
        {
            yield return null;
        }

        FireSimulationManager fireSimulationManager = ResolveFireSimulationManager(setupRoot);
        List<VictimSpawnPoint> selectedPoints = SelectSpawnPoints(payload, spawnPoints, spawnCount, fireSimulationManager);
        int spawnedCount = 0;
        for (int i = 0; i < selectedPoints.Count; i++)
        {
            VictimSpawnPoint spawnPoint = selectedPoints[i];
            if (spawnPoint == null)
            {
                continue;
            }

            GameObject victimInstance = SpawnVictim(spawnPoint);
            if (victimInstance == null)
            {
                continue;
            }

            RemoveFireNodesNearVictim(setupRoot, victimInstance);
            spawnedCount++;
        }

        if (refreshMissionObjectivesAfterSpawn)
        {
            RefreshMissionObjectives(startupFlow);
        }

        if (logSummary)
        {
            Debug.Log(
                $"{nameof(VictimPlacementStartupTask)} spawned {spawnedCount} victim(s) for logicalLocation='{payload.logicalFireLocation}', " +
                $"originArea='{(resolvedOriginArea != null ? resolvedOriginArea.EffectiveAreaKey : "none")}', " +
                $"estimatedKnown={payload.estimatedTrappedCountKnown}.",
                this);
        }
    }

    private List<VictimSpawnPoint> CollectSpawnPoints(IncidentOriginArea resolvedOriginArea)
    {
        HashSet<VictimSpawnPoint> seen = new HashSet<VictimSpawnPoint>();
        List<VictimSpawnPoint> results = new List<VictimSpawnPoint>();

        if (resolvedOriginArea != null)
        {
            VictimSpawnPoint[] areaPoints = resolvedOriginArea.CollectVictimSpawnPoints(includeInactiveSpawnPoints);
            for (int i = 0; i < areaPoints.Length; i++)
            {
                VictimSpawnPoint point = areaPoints[i];
                if (point != null && seen.Add(point))
                {
                    results.Add(point);
                }
            }

            return results;
        }

        if (explicitSpawnPoints != null)
        {
            for (int i = 0; i < explicitSpawnPoints.Length; i++)
            {
                VictimSpawnPoint point = explicitSpawnPoints[i];
                if (point != null && seen.Add(point))
                {
                    results.Add(point);
                }
            }
        }

        VictimSpawnPoint[] discovered = FindObjectsByType<VictimSpawnPoint>(
            includeInactiveSpawnPoints ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
        for (int i = 0; i < discovered.Length; i++)
        {
            VictimSpawnPoint point = discovered[i];
            if (point != null && seen.Add(point))
            {
                results.Add(point);
            }
        }

        return results;
    }

    private int ResolveVictimCount(IncidentWorldSetupPayload payload)
    {
        if (payload == null)
        {
            return 0;
        }

        int resolvedCount = 0;
        if (payload.estimatedTrappedCountKnown)
        {
            int min = Mathf.Max(0, payload.estimatedTrappedCountMin);
            int max = Mathf.Max(min, payload.estimatedTrappedCountMax);
            resolvedCount = deterministicPlacement
                ? min
                : UnityEngine.Random.Range(min, max + 1);
        }
        else if (string.Equals(payload.occupantRiskPreset, "Compromised", StringComparison.OrdinalIgnoreCase))
        {
            resolvedCount = compromisedRiskFallbackVictimCount;
        }
        else
        {
            resolvedCount = fallbackVictimCountWhenUnknown;
        }

        return Mathf.Clamp(resolvedCount, 0, Mathf.Max(1, maxVictimCount));
    }

    private List<VictimSpawnPoint> SelectSpawnPoints(
        IncidentWorldSetupPayload payload,
        List<VictimSpawnPoint> allPoints,
        int targetCount,
        FireSimulationManager fireSimulationManager)
    {
        List<VictimSpawnPoint> exactMatches = new List<VictimSpawnPoint>();
        List<VictimSpawnPoint> originMatches = new List<VictimSpawnPoint>();
        List<VictimSpawnPoint> fallbackMatches = new List<VictimSpawnPoint>();

        for (int i = 0; i < allPoints.Count; i++)
        {
            VictimSpawnPoint point = allPoints[i];
            if (point == null)
            {
                continue;
            }

            if (point.MatchesLogicalLocation(payload.logicalFireLocation))
            {
                exactMatches.Add(point);
                continue;
            }

            if (point.MatchesFireOrigin(payload.fireOrigin))
            {
                originMatches.Add(point);
                continue;
            }

            if (point.FallbackCandidate)
            {
                fallbackMatches.Add(point);
            }
        }

        if (deterministicPlacement)
        {
            SortDeterministically(exactMatches, payload, "victim-exact");
            SortDeterministically(originMatches, payload, "victim-origin");
            SortDeterministically(fallbackMatches, payload, "victim-fallback");
        }
        else
        {
            ShuffleRandomly(exactMatches);
            ShuffleRandomly(originMatches);
            ShuffleRandomly(fallbackMatches);
        }

        PreferSpawnPointsAwayFromHazardFire(exactMatches, fireSimulationManager);
        PreferSpawnPointsAwayFromHazardFire(originMatches, fireSimulationManager);
        PreferSpawnPointsAwayFromHazardFire(fallbackMatches, fireSimulationManager);

        List<VictimSpawnPoint> results = new List<VictimSpawnPoint>(targetCount);
        AppendUntil(results, exactMatches, targetCount);
        AppendUntil(results, originMatches, targetCount);
        AppendUntil(results, fallbackMatches, targetCount);
        return results;
    }

    private static void AppendUntil(List<VictimSpawnPoint> target, List<VictimSpawnPoint> source, int targetCount)
    {
        if (target == null || source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count && target.Count < targetCount; i++)
        {
            VictimSpawnPoint point = source[i];
            if (point != null && !target.Contains(point))
            {
                target.Add(point);
            }
        }
    }

    private void SortDeterministically(List<VictimSpawnPoint> points, IncidentWorldSetupPayload payload, string discriminator)
    {
        if (points == null || points.Count <= 1)
        {
            return;
        }

        int seed = IncidentSeedUtility.ResolvePlacementSeed(payload, discriminator);
        points.Sort((left, right) =>
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int leftSeed = IncidentSeedUtility.CombineHash(seed, IncidentSeedUtility.StableHash(GetHierarchyPath(left.transform)));
            int rightSeed = IncidentSeedUtility.CombineHash(seed, IncidentSeedUtility.StableHash(GetHierarchyPath(right.transform)));
            return leftSeed.CompareTo(rightSeed);
        });
    }

    private static void ShuffleRandomly(List<VictimSpawnPoint> points)
    {
        if (points == null || points.Count <= 1)
        {
            return;
        }

        for (int i = points.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            (points[i], points[swapIndex]) = (points[swapIndex], points[i]);
        }
    }

    private void PreferSpawnPointsAwayFromHazardFire(List<VictimSpawnPoint> points, FireSimulationManager fireSimulationManager)
    {
        if (!avoidHazardLinkedFireNodesWhenSelectingSpawnPoints ||
            fireSimulationManager == null ||
            spawnedVictimFireClearRadius <= 0f ||
            points == null ||
            points.Count <= 1)
        {
            return;
        }

        points.Sort((left, right) =>
        {
            bool leftNearFire = IsSpawnPointNearHazardFire(left, fireSimulationManager);
            bool rightNearFire = IsSpawnPointNearHazardFire(right, fireSimulationManager);
            if (leftNearFire == rightNearFire)
            {
                return 0;
            }

            return leftNearFire ? 1 : -1;
        });
    }

    private bool IsSpawnPointNearHazardFire(VictimSpawnPoint spawnPoint, FireSimulationManager fireSimulationManager)
    {
        if (spawnPoint == null || fireSimulationManager == null)
        {
            return false;
        }

        Pose pose = spawnPoint.ResolveSpawnPose();
        return fireSimulationManager.IsNearHazardLinkedFireNode(pose.position, spawnedVictimFireClearRadius);
    }

    private GameObject SpawnVictim(VictimSpawnPoint spawnPoint)
    {
        Pose pose = spawnPoint.ResolveSpawnPose();
        Transform parent = runtimeParent != null ? runtimeParent : null;
        return Instantiate(victimPrefab, pose.position, pose.rotation, parent);
    }

    private void RemoveFireNodesNearVictim(IncidentMapSetupRoot setupRoot, GameObject victimInstance)
    {
        if (!removeFireNodesNearSpawnedVictims ||
            spawnedVictimFireClearRadius <= 0f ||
            victimInstance == null)
        {
            return;
        }

        FireSimulationManager fireSimulationManager = setupRoot != null
            ? setupRoot.FireSimulationManager
            : FindAnyObjectByType<FireSimulationManager>(FindObjectsInactive.Include);
        fireSimulationManager?.RemoveIncidentNodesInRadius(
            victimInstance.transform.position,
            spawnedVictimFireClearRadius);
    }

    private static FireSimulationManager ResolveFireSimulationManager(IncidentMapSetupRoot setupRoot)
    {
        return setupRoot != null
            ? setupRoot.FireSimulationManager
            : FindAnyObjectByType<FireSimulationManager>(FindObjectsInactive.Include);
    }

    private int ClearExistingVictims()
    {
        int clearedCount = 0;
        HashSet<GameObject> destroyedObjects = new HashSet<GameObject>();

        Rescuable[] rescuables = FindObjectsByType<Rescuable>(FindObjectsInactive.Include);
        for (int i = 0; i < rescuables.Length; i++)
        {
            Rescuable rescuable = rescuables[i];
            if (rescuable == null)
            {
                continue;
            }

            if (destroyedObjects.Add(rescuable.gameObject))
            {
                Destroy(rescuable.gameObject);
                clearedCount++;
            }
        }

        VictimCondition[] victimConditions = FindObjectsByType<VictimCondition>(FindObjectsInactive.Include);
        for (int i = 0; i < victimConditions.Length; i++)
        {
            VictimCondition victimCondition = victimConditions[i];
            if (victimCondition == null)
            {
                continue;
            }

            if (destroyedObjects.Add(victimCondition.gameObject))
            {
                Destroy(victimCondition.gameObject);
                clearedCount++;
            }
        }

        return clearedCount;
    }

    private void RefreshMissionObjectives(SceneStartupFlow startupFlow)
    {
        IncidentMissionSystem mission = incidentMissionSystem;
        if (mission == null && startupFlow != null)
        {
            mission = startupFlow.FindSceneObject<IncidentMissionSystem>();
        }

        if (mission == null)
        {
            mission = FindAnyObjectByType<IncidentMissionSystem>(FindObjectsInactive.Include);
        }

        mission?.RefreshObjectives();
    }

    private static IncidentWorldSetupPayload ResolvePayload(IncidentMapSetupRoot setupRoot)
    {
        if (setupRoot != null && setupRoot.LastAppliedPayload != null)
        {
            return setupRoot.LastAppliedPayload;
        }

        return LoadingFlowState.TryGetPendingIncidentPayload(out IncidentWorldSetupPayload payload)
            ? payload
            : null;
    }

    private IncidentMapSetupRoot ResolveMapSetupRoot(SceneStartupFlow startupFlow)
    {
        IncidentMapSetupRoot setupRoot = null;
        if (startupFlow != null)
        {
            setupRoot = startupFlow.FindSceneObject<IncidentMapSetupRoot>();
        }

        if (setupRoot == null)
        {
            setupRoot = FindAnyObjectByType<IncidentMapSetupRoot>(FindObjectsInactive.Include);
        }

        return setupRoot;
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        string path = target.name;
        Transform current = target.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
