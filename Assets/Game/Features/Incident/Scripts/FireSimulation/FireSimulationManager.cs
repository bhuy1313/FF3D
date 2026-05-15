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
    [SerializeField] private FireNodeIconManager fireNodeIconManager;
    [SerializeField] private FireNodeEffectView ordinaryEffectPrefab;
    [SerializeField] private FireNodeEffectView electricalEffectPrefab;
    [SerializeField] private FireNodeEffectView flammableLiquidEffectPrefab;
    [SerializeField] private FireNodeEffectView gasEffectPrefab;
    [SerializeField] private Transform effectRoot;
    [Header("Runtime Incident Nodes")]
    [SerializeField] [Min(0.01f)] private float runtimeNodeIgnitionThresholdMultiplier = 1f;
    [SerializeField] [Min(0.1f)] private float runtimeNodeAutoConnectRadius = 2.6f;
    [SerializeField] private bool runtimeNodeDrawGizmos = true;
    [SerializeField] private Color runtimeNodeGizmoColor = new Color(1f, 0.45f, 0.1f, 0.8f);

    [Header("Boot")]
    [SerializeField] private bool initializeOnEnable = true;
    [SerializeField] private bool logInitializationWarnings = true;
    [Header("Debug")]
    [SerializeField] private bool logNodeHeatProgress;
    [SerializeField] [Min(0.1f)] private float nodeHeatLogInterval = 0.5f;
    [SerializeField] private bool autoBindSceneConsumers = true;
    [SerializeField] private bool autoRegisterBotFireTargets = true;
    [Header("Effects")]
    [Tooltip("Forces fire effect snapshots and clustering to resync periodically even when simulation state was not marked dirty. Set <= 0 to only sync on dirty state changes.")]
    [SerializeField] [Min(0f)] private float effectSyncInterval = 0.75f;
    [Header("Runtime Debug")]
    [SerializeField] private bool simulationSleeping;
    [SerializeField] private int debugActiveSpreadNodeCount;
    [SerializeField] private int debugBurningTrackedNodeCount;
    [SerializeField] private int debugRecoveryTimerNodeCount;
    [SerializeField] private int debugVisualActiveNodeCount;

    private readonly List<FireNodeSnapshot> nodeSnapshots = new List<FireNodeSnapshot>();
    private readonly List<FireSurfaceNodeAuthoring> runtimeIncidentNodes = new List<FireSurfaceNodeAuthoring>();
    private readonly List<FireSimulationBotTarget> botFireTargets = new List<FireSimulationBotTarget>();
    private readonly List<FireSimulationAreaGroupTarget> botFireGroups = new List<FireSimulationAreaGroupTarget>();
    private readonly List<int> activeSpreadNodeIndices = new List<int>();
    private readonly HashSet<int> activeSpreadNodeIndexLookup = new HashSet<int>();
    private readonly List<int> burningTrackedNodeIndices = new List<int>();
    private readonly HashSet<int> burningTrackedNodeIndexLookup = new HashSet<int>();
    private readonly List<int> recoveryTimerNodeIndices = new List<int>();
    private readonly HashSet<int> recoveryTimerNodeIndexLookup = new HashSet<int>();
    private readonly List<int> visualActiveNodeIndices = new List<int>();
    private readonly HashSet<int> visualActiveNodeIndexLookup = new HashSet<int>();
    private readonly List<int> dirtyNodeIndices = new List<int>();
    private readonly HashSet<int> dirtyNodeIndexLookup = new HashSet<int>();
    private readonly List<int> processingNodeIndices = new List<int>();
    private readonly HashSet<int> processingNodeIndexLookup = new HashSet<int>();
    [SerializeField] private List<string> activeSpreadNodeDebugEntries = new List<string>();
    private FireRuntimeGraph runtimeGraph;
    private float simulationTickAccumulator;
    private float nodeHeatLogAccumulator;
    private float effectSyncAccumulator;
    private bool initialized;
    private FireHazardType activeIncidentHazardType = FireHazardType.OrdinaryCombustibles;
    private bool activeHazardSourceIsolated;
    private Transform runtimeIncidentNodeRoot;
    private Transform runtimeBotFireTargetRoot;
    private Transform runtimeFireEffectRoot;
    private bool nodeSnapshotsDirty = true;

    public IReadOnlyList<FireNodeSnapshot> NodeSnapshots => nodeSnapshots;
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

        if (simulationSleeping)
        {
            return;
        }

        simulationTickAccumulator += Time.deltaTime;
        nodeHeatLogAccumulator += Time.deltaTime;

        float simulationTickInterval = simulationProfile.SimulationTickInterval;
        while (simulationTickAccumulator >= simulationTickInterval)
        {
            simulationTickAccumulator -= simulationTickInterval;
            if (TickSimulation(simulationTickInterval))
            {
                MarkVisualStateDirty();
                NotifyStateChanged();
            }
        }

        if (logNodeHeatProgress && nodeHeatLogAccumulator >= Mathf.Max(0.1f, nodeHeatLogInterval))
        {
            nodeHeatLogAccumulator = 0f;
            LogNodeHeatProgress();
        }
    }

    private void LateUpdate()
    {
        if (!initialized || runtimeGraph == null || simulationProfile == null)
        {
            return;
        }

        if (effectSyncInterval > 0f)
        {
            effectSyncAccumulator += Time.deltaTime;
        }

        RefreshVisualStateIfNeeded();
    }

    public void InitializeRuntimeGraph()
    {
        initialized = false;
        simulationTickAccumulator = 0f;
        nodeHeatLogAccumulator = 0f;
        effectSyncAccumulator = 0f;
        nodeSnapshots.Clear();
        ClearDirtyNodes();

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
            nodeSnapshotsDirty = false;
            return;
        }

        runtimeGraph = surfaceGraph.BuildRuntimeGraph();
        initialized = runtimeGraph != null;
        simulationSleeping = false;
        BindSceneConsumers();
        EnsureEffectManager();
        ResetRuntimeStateToBaseline(useAuthoringIgnition: true);
        if (logNodeHeatProgress)
        {
            LogRuntimeGraphTopology();
        }

        MarkVisualStateDirty();
        RefreshVisualStateIfNeeded();
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

    private void RefreshVisualStateIfNeeded()
    {
        bool shouldForceSync = effectSyncInterval > 0f && effectSyncAccumulator >= effectSyncInterval;
        if (!nodeSnapshotsDirty && !shouldForceSync)
        {
            return;
        }

        BuildNodeSnapshots();
        SyncEffects();
        nodeSnapshotsDirty = false;
        effectSyncAccumulator = 0f;
    }

    private void MarkVisualStateDirty()
    {
        nodeSnapshotsDirty = true;
    }

    private void MarkNodeDirty(FireRuntimeNode node)
    {
        if (node == null)
        {
            return;
        }

        MarkNodeDirty(node.Index);
    }

    private void MarkNodeDirty(int nodeIndex)
    {
        if (nodeIndex < 0)
        {
            return;
        }

        if (dirtyNodeIndexLookup.Add(nodeIndex))
        {
            dirtyNodeIndices.Add(nodeIndex);
        }

        AddProcessingNode(nodeIndex);
    }

    private void BeginProcessingDirtyNodes()
    {
        processingNodeIndices.Clear();
        processingNodeIndexLookup.Clear();

        for (int i = 0; i < dirtyNodeIndices.Count; i++)
        {
            AddProcessingNode(dirtyNodeIndices[i]);
        }

        for (int i = 0; i < activeSpreadNodeIndices.Count; i++)
        {
            AddProcessingNode(activeSpreadNodeIndices[i]);
        }

        for (int i = 0; i < recoveryTimerNodeIndices.Count; i++)
        {
            AddProcessingNode(recoveryTimerNodeIndices[i]);
        }
    }

    private void AddProcessingNode(int nodeIndex)
    {
        if (nodeIndex >= 0 && processingNodeIndexLookup.Add(nodeIndex))
        {
            processingNodeIndices.Add(nodeIndex);
        }
    }

    private void ClearDirtyNodes()
    {
        dirtyNodeIndices.Clear();
        dirtyNodeIndexLookup.Clear();
        processingNodeIndices.Clear();
        processingNodeIndexLookup.Clear();
    }
}
