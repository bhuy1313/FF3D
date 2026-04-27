using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class DoorLockRandomizerStartupTask : SceneStartupTask
{
    [Header("Door Lock Randomization")]
    [SerializeField] [Range(0f, 100f)] private float lockedPercentage = 35f;
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

        for (int i = 0; i < doors.Length; i++)
        {
            Door door = doors[i];
            if (door == null)
            {
                continue;
            }

            // Optional: You could check if door is already open, etc.
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
                $"to {affectedCount} doors. Locked={lockedCount}.",
                this);
        }
    }

    private bool ResolveLockedState(Door door, int baseSeed)
    {
        string key = GetHierarchyPath(door.transform);
        int doorSeed = CombineHash(baseSeed, StableHash(key));
        System.Random random = new System.Random(doorSeed);
        double normalizedPercentage = Mathf.Clamp(lockedPercentage, 0f, 100f) / 100f;
        return random.NextDouble() < normalizedPercentage;
    }

    private int ResolveBaseSeed()
    {
        if (!useDeterministicSeed)
        {
            return Environment.TickCount ^ seedOffset;
        }

        int seed = StableHash(SceneManager.GetActiveScene().path);
        seed = CombineHash(seed, seedOffset);

        if (LoadingFlowState.TryGetPendingIncidentPayload(out IncidentWorldSetupPayload payload) && payload != null)
        {
            seed = CombineHash(seed, StableHash(payload.caseId));
            seed = CombineHash(seed, StableHash(payload.scenarioId));
            seed = CombineHash(seed, StableHash(payload.fireOrigin));
            seed = CombineHash(seed, StableHash(payload.logicalFireLocation));
            return seed;
        }

        if (LoadingFlowState.TryGetPendingCaseId(out string caseId))
        {
            seed = CombineHash(seed, StableHash(caseId));
        }

        return seed;
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            int hash = 23;
            for (int i = 0; i < value.Length; i++)
            {
                hash = (hash * 31) + value[i];
            }

            return hash;
        }
    }

    private static int CombineHash(int left, int right)
    {
        unchecked
        {
            return (left * 397) ^ right;
        }
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
