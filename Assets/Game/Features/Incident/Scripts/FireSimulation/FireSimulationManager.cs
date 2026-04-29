using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed partial class FireSimulationManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FireSurfaceGraph surfaceGraph;
    [SerializeField] private FireSimulationProfile simulationProfile;
    [SerializeField] private FireSuppressionProfile suppressionProfile;
    [SerializeField] private FireEffectManager effectManager;
    [SerializeField] private FireClusterView clusterViewPrefab;
    [SerializeField] private Transform clusterViewRoot;
    [Header("Runtime Incident Nodes")]
    [SerializeField] [Min(0.1f)] private float runtimeNodeInitialFuel = 1.1f;
    [SerializeField] [Min(0.01f)] private float runtimeNodeIgnitionThresholdMultiplier = 1f;
    [SerializeField] [Range(0f, 1f)] private float runtimeNodeSpreadResistance = 0.1f;
    [SerializeField] [Min(0.1f)] private float runtimeNodeAutoConnectRadius = 2.6f;
    [SerializeField] private bool runtimeNodeDrawGizmos = true;
    [SerializeField] private Color runtimeNodeGizmoColor = new Color(1f, 0.45f, 0.1f, 0.8f);
    [SerializeField] private bool removeRuntimeNodeWhenHeatReachesZero;

    [Header("Boot")]
    [SerializeField] private bool initializeOnEnable = true;
    [SerializeField] private bool logInitializationWarnings = true;
    [Header("Debug")]
    [SerializeField] private bool logNodeHeatProgress;
    [SerializeField] [Min(0.1f)] private float nodeHeatLogInterval = 0.5f;
    [SerializeField] private bool autoBindSceneConsumers = true;
    [SerializeField] private bool autoRegisterBotFireTargets = true;

    private readonly List<FireClusterSnapshot> clusterSnapshots = new List<FireClusterSnapshot>();
    private readonly List<FireSurfaceNodeAuthoring> runtimeIncidentNodes = new List<FireSurfaceNodeAuthoring>();
    private readonly List<FireSimulationBotTarget> botFireTargets = new List<FireSimulationBotTarget>();
    private readonly List<FireSimulationAreaGroupTarget> botFireGroups = new List<FireSimulationAreaGroupTarget>();
    private FireRuntimeGraph runtimeGraph;
    private float simulationTickAccumulator;
    private float clusterRefreshAccumulator;
    private float nodeHeatLogAccumulator;
    private bool initialized;
    private FireHazardType activeIncidentHazardType = FireHazardType.OrdinaryCombustibles;
    private bool activeHazardSourceIsolated;
    private Transform runtimeIncidentNodeRoot;
    private Transform runtimeBotFireTargetRoot;

    public IReadOnlyList<FireClusterSnapshot> ClusterSnapshots => clusterSnapshots;
    public FireRuntimeGraph RuntimeGraph => runtimeGraph;
    public bool IsInitialized => initialized;
    public FireHazardType ActiveIncidentHazardType => activeIncidentHazardType;
    public bool ActiveHazardSourceIsolated => activeHazardSourceIsolated;
    public event System.Action StateChanged;

    private void OnEnable()
    {
        if (initializeOnEnable)
        {
            InitializeRuntimeGraph();
        }
    }

    private void OnDestroy()
    {
        if (fallbackSuppressionProfile != null)
        {
            Destroy(fallbackSuppressionProfile);
            fallbackSuppressionProfile = null;
        }
    }

    private void Update()
    {
        if (!initialized || runtimeGraph == null || simulationProfile == null)
        {
            return;
        }

        simulationTickAccumulator += Time.deltaTime;
        clusterRefreshAccumulator += Time.deltaTime;
        nodeHeatLogAccumulator += Time.deltaTime;

        float simulationTickInterval = simulationProfile.SimulationTickInterval;
        while (simulationTickAccumulator >= simulationTickInterval)
        {
            simulationTickAccumulator -= simulationTickInterval;
            if (TickSimulation(simulationTickInterval))
            {
                NotifyStateChanged();
            }
        }

        if (clusterRefreshAccumulator >= simulationProfile.ClusterRefreshInterval)
        {
            clusterRefreshAccumulator = 0f;
            RebuildClusters();
            SyncEffects();
        }

        if (logNodeHeatProgress && nodeHeatLogAccumulator >= Mathf.Max(0.1f, nodeHeatLogInterval))
        {
            nodeHeatLogAccumulator = 0f;
            LogNodeHeatProgress();
        }
    }

    public void InitializeRuntimeGraph()
    {
        initialized = false;
        simulationTickAccumulator = 0f;
        clusterRefreshAccumulator = 0f;
        nodeHeatLogAccumulator = 0f;
        clusterSnapshots.Clear();

        if (surfaceGraph == null || simulationProfile == null)
        {
            if (logInitializationWarnings)
            {
                Debug.LogWarning(
                    $"{nameof(FireSimulationManager)} requires both {nameof(FireSurfaceGraph)} and {nameof(FireSimulationProfile)}.",
                    this);
            }

            runtimeGraph = null;
            DisableEffects();
            SyncBotFireTargets();
            SyncBotFireGroups();
            return;
        }

        runtimeGraph = surfaceGraph.BuildRuntimeGraph();
        initialized = runtimeGraph != null;
        BindSceneConsumers();
        EnsureEffectManager();
        ResetRuntimeStateToBaseline(useAuthoringIgnition: true);
        if (logNodeHeatProgress)
        {
            LogRuntimeGraphTopology();
        }

        RebuildClusters();
        SyncEffects();
        SyncBotFireTargets();
        SyncBotFireGroups();
        NotifyStateChanged();
    }

    private void BindSceneConsumers()
    {
        if (!autoBindSceneConsumers)
        {
            return;
        }

        SmokeHazard[] smokeHazards = FindObjectsByType<SmokeHazard>(FindObjectsInactive.Include);
        for (int i = 0; i < smokeHazards.Length; i++)
        {
            SmokeHazard smokeHazard = smokeHazards[i];
            if (smokeHazard != null && smokeHazard.gameObject.scene.IsValid())
            {
                smokeHazard.SetFireSimulationManager(this);
            }
        }

        HazardIsolationDevice[] hazardDevices = FindObjectsByType<HazardIsolationDevice>(FindObjectsInactive.Include);
        for (int i = 0; i < hazardDevices.Length; i++)
        {
            HazardIsolationDevice hazardDevice = hazardDevices[i];
            if (hazardDevice != null && hazardDevice.gameObject.scene.IsValid())
            {
                hazardDevice.SetFireSimulationManager(this);
            }
        }

        FireExtinguisher[] extinguishers = FindObjectsByType<FireExtinguisher>(FindObjectsInactive.Include);
        for (int i = 0; i < extinguishers.Length; i++)
        {
            FireExtinguisher extinguisher = extinguishers[i];
            if (extinguisher != null && extinguisher.gameObject.scene.IsValid())
            {
                extinguisher.SetFireSimulationManager(this);
            }
        }

        FireHose[] hoses = FindObjectsByType<FireHose>(FindObjectsInactive.Include);
        for (int i = 0; i < hoses.Length; i++)
        {
            FireHose hose = hoses[i];
            if (hose != null && hose.gameObject.scene.IsValid())
            {
                hose.SetFireSimulationManager(this);
            }
        }
    }

    private void NotifyStateChanged()
    {
        SyncBotFireTargets();
        SyncBotFireGroups();
        StateChanged?.Invoke();
    }
}
