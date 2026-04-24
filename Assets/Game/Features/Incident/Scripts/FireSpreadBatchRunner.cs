using System.Collections.Generic;
using UnityEngine;

public sealed class FireSpreadBatchRunner : MonoBehaviour
{
    private const string RunnerObjectName = "[FireSpreadBatchRunner]";
    private const float TargetCoveragePerFrame = 0.2f;
    private const int MinBudgetPerFrame = 1;
    private const int MaxBudgetPerFrame = 24;

    private static FireSpreadBatchRunner instance;

    private readonly List<Fire> registeredFires = new List<Fire>();
    private int nextProcessIndex;

    public static void Register(Fire fire)
    {
        if (fire == null)
        {
            return;
        }

        EnsureInstance().RegisterInternal(fire);
    }

    public static void Unregister(Fire fire)
    {
        if (instance == null || fire == null)
        {
            return;
        }

        instance.UnregisterInternal(fire);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    private static FireSpreadBatchRunner EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject runnerObject = new GameObject(RunnerObjectName);
        runnerObject.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(runnerObject);
        instance = runnerObject.AddComponent<FireSpreadBatchRunner>();
        return instance;
    }

    private void Update()
    {
        if (registeredFires.Count == 0)
        {
            nextProcessIndex = 0;
            return;
        }

        int processedCount = 0;
        int checkedCount = 0;
        int processBudget = Mathf.Clamp(
            Mathf.CeilToInt(registeredFires.Count * TargetCoveragePerFrame),
            MinBudgetPerFrame,
            MaxBudgetPerFrame);
        int maxChecks = registeredFires.Count;

        while (processedCount < processBudget && checkedCount < maxChecks && registeredFires.Count > 0)
        {
            if (nextProcessIndex >= registeredFires.Count)
            {
                nextProcessIndex = 0;
            }

            Fire fire = registeredFires[nextProcessIndex];
            checkedCount++;

            if (fire == null)
            {
                registeredFires.RemoveAt(nextProcessIndex);
                continue;
            }

            nextProcessIndex++;
            processedCount++;

            if (!fire.isActiveAndEnabled)
            {
                continue;
            }

            fire.ProcessSpreadBatch();
        }
    }

    private void RegisterInternal(Fire fire)
    {
        if (registeredFires.Contains(fire))
        {
            return;
        }

        registeredFires.Add(fire);
    }

    private void UnregisterInternal(Fire fire)
    {
        int removeIndex = registeredFires.IndexOf(fire);
        if (removeIndex < 0)
        {
            return;
        }

        registeredFires.RemoveAt(removeIndex);
        if (removeIndex < nextProcessIndex)
        {
            nextProcessIndex--;
        }

        nextProcessIndex = Mathf.Clamp(nextProcessIndex, 0, registeredFires.Count);
    }
}
