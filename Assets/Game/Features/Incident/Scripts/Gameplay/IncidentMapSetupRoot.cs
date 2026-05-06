using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class IncidentMapSetupRoot : MonoBehaviour
{
    [Header("Fire Spawn")]
    [SerializeField] private IncidentFireSpawnProfile fireSpawnProfile;
    [SerializeField] private FireSimulationManager fireSimulationManager;

    [Header("Tasks")]
    [SerializeField] private List<IncidentMapSetupTask> explicitTasks = new List<IncidentMapSetupTask>();

    [Header("Diagnostics")]
    [SerializeField] private bool logSetupTasks = true;

    private IncidentMapSetupTask activeTask;
    private int totalTaskCount;
    private int completedTaskCount;
    private readonly List<string> lastWarnings = new List<string>();

    public IncidentWorldSetupPayload LastAppliedPayload { get; private set; }
    public IncidentPayloadAnchor LastResolvedAnchor { get; private set; }
    public IncidentOriginArea LastResolvedOriginArea { get; private set; }
    public IReadOnlyList<string> LastWarnings => lastWarnings;
    public IncidentFireSpawnProfile FireSpawnProfile => fireSpawnProfile;
    public FireSimulationManager FireSimulationManager => fireSimulationManager;
    public IReadOnlyList<IncidentMapSetupTask> ExplicitTasks => explicitTasks;
    public IncidentMapSetupTask ActiveTask => activeTask;
    public int TotalTaskCount => totalTaskCount;
    public int CompletedTaskCount => completedTaskCount;
    public bool HasCompletedAllTasks => completedTaskCount >= totalTaskCount && totalTaskCount > 0;

    private void Awake()
    {
        ResolveFireSimulationManager();
        BindFireSimulationConsumers();
    }

    private void OnValidate()
    {
        ResolveFireSimulationManager();
    }

    public IEnumerator ApplyPayload(
        SceneStartupFlow startupFlow,
        IncidentWorldSetupPayload payload,
        IncidentFirePrefabLibrary firePrefabLibrary)
    {
        LastAppliedPayload = payload;
        LastResolvedAnchor = null;
        LastResolvedOriginArea = null;
        lastWarnings.Clear();

        if (payload == null)
        {
            yield break;
        }

        ResolveFireSimulationManager();
        BindFireSimulationConsumers();

        List<IncidentMapSetupTask> tasks = BuildTaskList();
        totalTaskCount = tasks.Count;
        completedTaskCount = 0;
        activeTask = null;
        IncidentMapSetupContext context = new IncidentMapSetupContext(
            payload,
            startupFlow,
            this,
            firePrefabLibrary,
            fireSpawnProfile,
            fireSimulationManager,
            lastWarnings);

        for (int i = 0; i < tasks.Count; i++)
        {
            IncidentMapSetupTask task = tasks[i];
            if (task == null)
            {
                continue;
            }

            activeTask = task;
            if (logSetupTasks)
            {
                Debug.Log(
                    $"{nameof(IncidentMapSetupRoot)} running task '{task.TaskName}' for payload " +
                    $"caseId='{payload.caseId}', scenarioId='{payload.scenarioId}'.",
                    task);
            }

            yield return task.Apply(context);
            completedTaskCount++;
        }

        LastResolvedAnchor = context.ResolvedAnchor;
        LastResolvedOriginArea = context.ResolvedOriginArea;
        activeTask = null;
    }

    public void BindFireSimulationConsumers()
    {
        if (fireSimulationManager == null)
        {
            return;
        }

        FireGroup[] fireGroups = GetComponentsInChildren<FireGroup>(true);
        for (int i = 0; i < fireGroups.Length; i++)
        {
            FireGroup fireGroup = fireGroups[i];
            if (fireGroup != null)
            {
                fireGroup.SetFireSimulationManager(fireSimulationManager);
            }
        }

        SmokeHazard[] smokeHazards = GetComponentsInChildren<SmokeHazard>(true);
        for (int i = 0; i < smokeHazards.Length; i++)
        {
            SmokeHazard smokeHazard = smokeHazards[i];
            if (smokeHazard != null)
            {
                smokeHazard.SetFireSimulationManager(fireSimulationManager);
            }
        }

        HazardIsolationDevice[] hazardDevices = GetComponentsInChildren<HazardIsolationDevice>(true);
        for (int i = 0; i < hazardDevices.Length; i++)
        {
            HazardIsolationDevice hazardDevice = hazardDevices[i];
            if (hazardDevice != null)
            {
                hazardDevice.SetFireSimulationManager(fireSimulationManager);
            }
        }

        FireExtinguisher[] extinguishers = GetComponentsInChildren<FireExtinguisher>(true);
        for (int i = 0; i < extinguishers.Length; i++)
        {
            FireExtinguisher extinguisher = extinguishers[i];
            if (extinguisher != null)
            {
                extinguisher.SetFireSimulationManager(fireSimulationManager);
            }
        }

        FireHose[] hoses = GetComponentsInChildren<FireHose>(true);
        for (int i = 0; i < hoses.Length; i++)
        {
            FireHose hose = hoses[i];
            if (hose != null)
            {
                hose.SetFireSimulationManager(fireSimulationManager);
            }
        }
    }

    private List<IncidentMapSetupTask> BuildTaskList()
    {
        List<IncidentMapSetupTask> results = new List<IncidentMapSetupTask>();
        HashSet<IncidentMapSetupTask> seen = new HashSet<IncidentMapSetupTask>();

        for (int i = 0; i < explicitTasks.Count; i++)
        {
            IncidentMapSetupTask task = explicitTasks[i];
            if (task == null || !seen.Add(task))
            {
                continue;
            }

            results.Add(task);
        }

        return results;
    }

    private void ResolveFireSimulationManager()
    {
        if (fireSimulationManager == null)
        {
            fireSimulationManager = GetComponentInChildren<FireSimulationManager>(true);
        }

        if (fireSimulationManager == null)
        {
            fireSimulationManager = GetComponentInParent<FireSimulationManager>(true);
        }
    }
}
