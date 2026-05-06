using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class WindowLockRandomizerStartupTask : SceneStartupTask
{
    [Header("Window Lock Randomization")]
    [SerializeField] [Range(0f, 100f)] private float lockedPercentage = 35f;
    [SerializeField] [Range(0f, 100f)] private float openPercentage;
    [SerializeField] private bool includeInactiveWindows = true;
    [SerializeField] private bool useDeterministicSeed;
    [SerializeField] private int seedOffset;
    [SerializeField] private bool logSummary;

    protected override IEnumerator Execute(SceneStartupFlow startupFlow)
    {
        Window[] windows = FindObjectsByType<Window>(
            includeInactiveWindows ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);

        if (windows == null || windows.Length == 0)
        {
            yield break;
        }

        int baseSeed = ResolveBaseSeed();
        int affectedCount = 0;
        int lockedCount = 0;
        int openedCount = 0;

        for (int i = 0; i < windows.Length; i++)
        {
            Window window = windows[i];
            if (window == null)
            {
                continue;
            }

            bool shouldOpen = !window.IsBroken && ResolveOpenState(window, baseSeed);
            if (shouldOpen)
            {
                window.SetLockedState(false);
                window.SetOpenState(true);
                openedCount++;
                affectedCount++;
                continue;
            }

            bool shouldLock = window.IsBroken ? false : ResolveLockedState(window, baseSeed);
            window.SetLockedState(shouldLock);
            affectedCount++;

            if (shouldLock)
            {
                lockedCount++;
            }
        }

        if (logSummary)
        {
            Debug.Log(
                $"{nameof(WindowLockRandomizerStartupTask)} applied lockedPercentage={lockedPercentage:0.0}% " +
                $"openPercentage={openPercentage:0.0}% to {affectedCount} windows. " +
                $"Locked={lockedCount}, Opened={openedCount}.",
                this);
        }
    }

    private bool ResolveLockedState(Window window, int baseSeed)
    {
        string key = GetHierarchyPath(window.transform);
        int windowSeed = IncidentSeedUtility.CombineHash(baseSeed, IncidentSeedUtility.StableHash(key));
        System.Random random = new System.Random(windowSeed);
        double normalizedPercentage = Mathf.Clamp(lockedPercentage, 0f, 100f) / 100f;
        return random.NextDouble() < normalizedPercentage;
    }

    private bool ResolveOpenState(Window window, int baseSeed)
    {
        string key = GetHierarchyPath(window.transform);
        int windowSeed = IncidentSeedUtility.CombineHash(baseSeed, IncidentSeedUtility.StableHash(key + "/open"));
        System.Random random = new System.Random(windowSeed);
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
