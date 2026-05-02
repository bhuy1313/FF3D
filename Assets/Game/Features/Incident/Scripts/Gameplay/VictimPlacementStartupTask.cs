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

    [Header("Mission Sync")]
    [SerializeField] private IncidentMissionSystem incidentMissionSystem;
    [SerializeField] private bool refreshMissionObjectivesAfterSpawn = true;
    [SerializeField] private bool logSummary = true;

    protected override IEnumerator Execute(SceneStartupFlow startupFlow)
    {
        if (!LoadingFlowState.TryGetPendingIncidentPayload(out IncidentWorldSetupPayload payload) || payload == null)
        {
            yield break;
        }

        if (victimPrefab == null)
        {
            Debug.LogWarning($"{nameof(VictimPlacementStartupTask)}: Missing victim prefab.", this);
            yield break;
        }

        List<VictimSpawnPoint> spawnPoints = CollectSpawnPoints();
        if (spawnPoints.Count == 0)
        {
            Debug.LogWarning($"{nameof(VictimPlacementStartupTask)}: No victim spawn points found.", this);
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

        if (clearExistingVictimsBeforeSpawn)
        {
            ClearExistingVictims();
        }

        List<VictimSpawnPoint> selectedPoints = SelectSpawnPoints(payload, spawnPoints, spawnCount);
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

            ConfigureVictim(victimInstance, spawnPoint, payload);
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
                $"estimatedKnown={payload.estimatedTrappedCountKnown}.",
                this);
        }
    }

    private List<VictimSpawnPoint> CollectSpawnPoints()
    {
        HashSet<VictimSpawnPoint> seen = new HashSet<VictimSpawnPoint>();
        List<VictimSpawnPoint> results = new List<VictimSpawnPoint>();

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
        int targetCount)
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

    private GameObject SpawnVictim(VictimSpawnPoint spawnPoint)
    {
        Pose pose = spawnPoint.ResolveSpawnPose();
        Transform parent = runtimeParent != null ? runtimeParent : null;
        return Instantiate(victimPrefab, pose.position, pose.rotation, parent);
    }

    private static void ConfigureVictim(GameObject victimInstance, VictimSpawnPoint spawnPoint, IncidentWorldSetupPayload payload)
    {
        if (victimInstance == null)
        {
            return;
        }

        victimInstance.name = string.IsNullOrWhiteSpace(spawnPoint.name)
            ? victimInstance.name
            : $"{victimInstance.name}_{spawnPoint.name}";

        VictimCondition victimCondition = victimInstance.GetComponent<VictimCondition>();
        if (victimCondition == null)
        {
            return;
        }

        float targetCondition = ResolveInitialCondition(spawnPoint, payload);
        victimCondition.RestoreCondition(99999f);
        victimCondition.ApplyConditionDamage(Mathf.Max(0f, victimCondition.MaxCondition - targetCondition));
    }

    private static float ResolveInitialCondition(VictimSpawnPoint spawnPoint, IncidentWorldSetupPayload payload)
    {
        if (spawnPoint != null && spawnPoint.PreferCriticalVictims)
        {
            return 20f;
        }

        if (spawnPoint != null && spawnPoint.PreferUrgentVictims)
        {
            return 50f;
        }

        if (payload != null && string.Equals(payload.severityBand, "High", StringComparison.OrdinalIgnoreCase))
        {
            return 25f;
        }

        if (payload != null && string.Equals(payload.severityBand, "Medium", StringComparison.OrdinalIgnoreCase))
        {
            return 55f;
        }

        return 85f;
    }

    private void ClearExistingVictims()
    {
        Rescuable[] rescuables = FindObjectsByType<Rescuable>(FindObjectsInactive.Include);
        for (int i = 0; i < rescuables.Length; i++)
        {
            Rescuable rescuable = rescuables[i];
            if (rescuable == null)
            {
                continue;
            }

            if (runtimeParent != null && rescuable.transform.parent != runtimeParent)
            {
                continue;
            }

            Destroy(rescuable.gameObject);
        }
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
