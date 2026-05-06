using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class DoorLockRandomizerStartupTask : SceneStartupTask
{
    [Header("Door Lock Randomization")]
    [SerializeField] [Range(0f, 100f)] private float lockedPercentage = 35f;
    [SerializeField] [Range(0f, 100f)] private float openPercentage;
    [SerializeField] private bool forceUnlockDoorsWhenOpening = true;
    [SerializeField] private bool includeInactiveDoors = true;
    [SerializeField] private bool useDeterministicSeed;
    [SerializeField] private int seedOffset;
    [SerializeField] private bool logSummary;

    protected override IEnumerator Execute(SceneStartupFlow startupFlow)
    {
        Door[] doors = FindObjectsByType<Door>(
            includeInactiveDoors ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);

        if (doors == null || doors.Length == 0)
        {
            yield break;
        }

        int baseSeed = ResolveBaseSeed();
        int affectedCount = 0;
        int lockedCount = 0;
        int openedCount = 0;

        for (int i = 0; i < doors.Length; i++)
        {
            Door door = doors[i];
            if (door == null)
            {
                continue;
            }

            bool shouldOpen = ResolveOpenState(door, baseSeed);
            if (shouldOpen)
            {
                door.SetOpenState(true, forceUnlockDoorsWhenOpening);
                openedCount++;
                affectedCount++;
                continue;
            }

            bool shouldLock = ResolveLockedState(door, baseSeed);
            
            if (shouldLock)
            {
                door.SetLockState(Door.DoorLockMode.SoftLockedCrowbar, true);
                lockedCount++;
            }
            
            affectedCount++;
        }

        if (logSummary)
        {
            Debug.Log(
                $"{nameof(DoorLockRandomizerStartupTask)} applied lockedPercentage={lockedPercentage:0.0}% " +
                $"openPercentage={openPercentage:0.0}% to {affectedCount} doors. " +
                $"Locked={lockedCount}, Opened={openedCount}.",
                this);
        }
    }

    private bool ResolveLockedState(Door door, int baseSeed)
    {
        string key = GetHierarchyPath(door.transform);
        int doorSeed = IncidentSeedUtility.CombineHash(baseSeed, IncidentSeedUtility.StableHash(key));
        System.Random random = new System.Random(doorSeed);
        double normalizedPercentage = Mathf.Clamp(lockedPercentage, 0f, 100f) / 100f;
        return random.NextDouble() < normalizedPercentage;
    }

    private bool ResolveOpenState(Door door, int baseSeed)
    {
        string key = GetHierarchyPath(door.transform);
        int doorSeed = IncidentSeedUtility.CombineHash(baseSeed, IncidentSeedUtility.StableHash(key + "/open"));
        System.Random random = new System.Random(doorSeed);
        double normalizedPercentage = Mathf.Clamp(openPercentage, 0f, 100f) / 100f;
        return random.NextDouble() < normalizedPercentage;
    }

    private int ResolveBaseSeed()
    {
        if (!useDeterministicSeed)
        {
            return Environment.TickCount ^ seedOffset;
        }

        int seed = IncidentSeedUtility.StableHash(SceneManager.GetActiveScene().path);
        seed = IncidentSeedUtility.CombineHash(seed, seedOffset);

        if (LoadingFlowState.TryGetPendingIncidentPayload(out IncidentWorldSetupPayload payload) && payload != null)
        {
            seed = IncidentSeedUtility.CombineHash(seed, IncidentSeedUtility.StableHash(payload.caseId));
            seed = IncidentSeedUtility.CombineHash(seed, IncidentSeedUtility.StableHash(payload.scenarioId));
            seed = IncidentSeedUtility.CombineHash(seed, IncidentSeedUtility.StableHash(payload.fireOrigin));
            seed = IncidentSeedUtility.CombineHash(seed, IncidentSeedUtility.StableHash(payload.logicalFireLocation));
            return seed;
        }

        if (LoadingFlowState.TryGetPendingCaseId(out string caseId))
        {
            seed = IncidentSeedUtility.CombineHash(seed, IncidentSeedUtility.StableHash(caseId));
        }

        return seed;
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
