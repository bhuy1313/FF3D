using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FireSimulationManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FireSurfaceGraph surfaceGraph;
    [SerializeField] private FireSimulationProfile simulationProfile;
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

    [Header("Boot")]
    [SerializeField] private bool initializeOnEnable = true;
    [SerializeField] private bool logInitializationWarnings = true;
    [Header("Debug")]
    [SerializeField] private bool logNodeHeatProgress;
    [SerializeField] [Min(0.1f)] private float nodeHeatLogInterval = 0.5f;
    [SerializeField] private bool autoBindSceneConsumers = true;

    private readonly List<FireClusterSnapshot> clusterSnapshots = new List<FireClusterSnapshot>();
    private readonly List<FireSurfaceNodeAuthoring> runtimeIncidentNodes = new List<FireSurfaceNodeAuthoring>();
    private FireRuntimeGraph runtimeGraph;
    private float simulationTickAccumulator;
    private float clusterRefreshAccumulator;
    private float nodeHeatLogAccumulator;
    private bool initialized;
    private FireHazardType activeIncidentHazardType = FireHazardType.OrdinaryCombustibles;
    private bool activeHazardSourceIsolated;
    private Transform runtimeIncidentNodeRoot;

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
        NotifyStateChanged();
    }

    public int IgniteClosestNode(Vector3 worldPosition, float ignitionHeat)
    {
        if (!initialized || runtimeGraph == null)
        {
            return -1;
        }

        int bestIndex = -1;
        float bestDistanceSqr = float.PositiveInfinity;
        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null)
            {
                continue;
            }

            float distanceSqr = (node.Position - worldPosition).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            bestIndex = i;
        }

        if (bestIndex >= 0)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(bestIndex);
            node.IsTrackedByIncident = true;
            node.HazardType = activeIncidentHazardType;
            node.Heat = Mathf.Max(node.Heat, ignitionHeat);
            NotifyStateChanged();
        }

        return bestIndex;
    }

    public void BeginIncident(FireHazardType hazardType, bool hazardSourceIsolated)
    {
        if (!initialized)
        {
            InitializeRuntimeGraph();
        }

        ResetRuntimeStateToBaseline(useAuthoringIgnition: false);
        activeIncidentHazardType = hazardType;
        activeHazardSourceIsolated = hazardSourceIsolated;
        RebuildClusters();
        SyncEffects();
        NotifyStateChanged();
    }

    public bool ApplyIncidentPlacements(
        FireHazardType hazardType,
        bool hazardSourceIsolated,
        IReadOnlyList<FireIncidentPlacement> placements)
    {
        if (surfaceGraph == null || placements == null || placements.Count <= 0)
        {
            return false;
        }

        RebuildRuntimeIncidentNodes(placements, hazardType);
        InitializeRuntimeGraph();
        if (!initialized || runtimeGraph == null)
        {
            return false;
        }

        BeginIncident(hazardType, hazardSourceIsolated);
        for (int i = 0; i < placements.Count; i++)
        {
            FireIncidentPlacement placement = placements[i];
            TrackClosestNode(placement.Position, placement.InitialIntensity01);
        }

        return true;
    }

    public int TrackClosestNode(Vector3 worldPosition, float normalizedHeat)
    {
        if (!initialized || runtimeGraph == null)
        {
            return -1;
        }

        int nodeIndex = FindClosestNodeIndex(worldPosition);
        if (nodeIndex < 0)
        {
            return -1;
        }

        FireRuntimeNode node = runtimeGraph.GetNode(nodeIndex);
        if (node == null)
        {
            return -1;
        }

        node.IsTrackedByIncident = true;
        float clampedHeat01 = Mathf.Clamp01(normalizedHeat);
        if (clampedHeat01 > 0f)
        {
            node.HazardType = activeIncidentHazardType;
        }

        float heat = clampedHeat01 > 0f
            ? Mathf.Lerp(node.IgnitionThreshold, node.IgnitionThreshold * 2f, clampedHeat01)
            : 0f;
        node.Heat = Mathf.Max(node.Heat, heat);
        RebuildClusters();
        SyncEffects();
        NotifyStateChanged();
        return nodeIndex;
    }

    public void SetRuntimeHazardIsolation(bool isolated)
    {
        activeHazardSourceIsolated = isolated;
        NotifyStateChanged();
    }

    public void SetActiveIncidentHazardType(FireHazardType hazardType)
    {
        activeIncidentHazardType = hazardType;
        NotifyStateChanged();
    }

    public int GetTrackedNodeCount()
    {
        return CountNodes(node => node != null && node.IsTrackedByIncident);
    }

    public int GetExtinguishedTrackedNodeCount()
    {
        return CountNodes(node => node != null && node.IsTrackedByIncident && !node.IsBurning);
    }

    public int GetBurningTrackedNodeCount()
    {
        return CountNodes(node => node != null && node.IsTrackedByIncident && node.IsBurning);
    }

    public int GetBurningTrackedNodeCount(Bounds bounds)
    {
        return CountNodes(node => node != null && node.IsTrackedByIncident && node.IsBurning && bounds.Contains(node.Position));
    }

    public float GetBurningTrackedIntensitySum(Bounds bounds)
    {
        if (!initialized || runtimeGraph == null)
        {
            return 0f;
        }

        float intensitySum = 0f;
        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null || !node.IsTrackedByIncident || !node.IsBurning || !bounds.Contains(node.Position))
            {
                continue;
            }

            intensitySum += Mathf.Clamp01(node.Heat / Mathf.Max(0.01f, node.IgnitionThreshold));
        }

        return intensitySum;
    }

    public void ApplyDraftHeatInBounds(Bounds bounds, float amount)
    {
        if (!initialized || runtimeGraph == null || amount <= 0f)
        {
            return;
        }

        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null || !node.IsTrackedByIncident || !node.IsBurning || !bounds.Contains(node.Position))
            {
                continue;
            }

            float intensityScale = Mathf.Max(0.35f, Mathf.Clamp01(node.Heat / Mathf.Max(0.01f, node.IgnitionThreshold)));
            node.Heat += amount * intensityScale;
        }

        NotifyStateChanged();
    }

    public int ApplySuppressionSphere(
        Vector3 worldCenter,
        float radius,
        float amount,
        FireSuppressionAgent agent)
    {
        if (!initialized || runtimeGraph == null || radius <= 0f || amount <= 0f)
        {
            return 0;
        }

        float effectiveness = ResolveSuppressionEffectiveness(agent);
        bool worsens = agent == FireSuppressionAgent.Water && activeIncidentHazardType == FireHazardType.FlammableLiquid;
        if (Mathf.Approximately(effectiveness, 0f) && !worsens)
        {
            return 0;
        }

        int affectedNodeCount = 0;
        float radiusSqr = radius * radius;
        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null || !node.IsTrackedByIncident)
            {
                continue;
            }

            float distanceSqr = (node.Position - worldCenter).sqrMagnitude;
            if (distanceSqr > radiusSqr)
            {
                continue;
            }

            float normalizedFalloff = 1f - Mathf.Clamp01(Mathf.Sqrt(distanceSqr) / radius);
            if (worsens)
            {
                node.Heat += amount * 0.6f * normalizedFalloff;
            }
            else
            {
                float heatRemoval = amount * effectiveness * normalizedFalloff;
                if (agent == FireSuppressionAgent.Water)
                {
                    node.Wetness += amount * normalizedFalloff;
                }

                node.Heat = Mathf.Max(0f, node.Heat - heatRemoval);
            }

            affectedNodeCount++;
        }

        if (affectedNodeCount > 0)
        {
            NotifyStateChanged();
        }

        return affectedNodeCount;
    }

    public int ApplySuppressionSphere(
        Vector3 worldCenter,
        float radius,
        float wetnessAmount,
        float heatRemovalAmount)
    {
        if (!initialized || runtimeGraph == null || radius <= 0f)
        {
            return 0;
        }

        int affectedNodeCount = 0;
        float radiusSqr = radius * radius;
        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null || !node.IsTrackedByIncident)
            {
                continue;
            }

            float distanceSqr = (node.Position - worldCenter).sqrMagnitude;
            if (distanceSqr > radiusSqr)
            {
                continue;
            }

            float normalizedFalloff = 1f - Mathf.Clamp01(Mathf.Sqrt(distanceSqr) / radius);
            node.Wetness += wetnessAmount * normalizedFalloff;
            node.Heat = Mathf.Max(0f, node.Heat - heatRemovalAmount * normalizedFalloff);
            affectedNodeCount++;
        }

        if (affectedNodeCount > 0)
        {
            NotifyStateChanged();
        }

        return affectedNodeCount;
    }

    private bool TickSimulation(float deltaTime)
    {
        bool changed = false;
        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null)
            {
                continue;
            }

            changed |= TickNode(node, deltaTime);
        }

        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null)
            {
                continue;
            }

            if (!Mathf.Approximately(node.PendingHeatDelta, 0f))
            {
                node.Heat = Mathf.Max(0f, node.Heat + node.PendingHeatDelta);
                node.PendingHeatDelta = 0f;
                changed = true;
            }

            if (!Mathf.Approximately(node.PendingWetnessDelta, 0f))
            {
                node.Wetness = Mathf.Max(0f, node.Wetness + node.PendingWetnessDelta);
                node.PendingWetnessDelta = 0f;
                changed = true;
            }

            if (node.RemainingFuel <= 0f)
            {
                node.Heat = Mathf.Min(node.Heat, simulationProfile.BurnedOutHeatRetention);
            }
            else if (node.Heat < simulationProfile.ExtinguishThreshold)
            {
                node.Heat = 0f;
            }
        }

        return changed;
    }

    private bool TickNode(FireRuntimeNode node, float deltaTime)
    {
        float previousHeat = node.Heat;
        float previousWetness = node.Wetness;
        float previousFuel = node.RemainingFuel;
        node.Wetness = Mathf.Max(0f, node.Wetness - simulationProfile.WetnessRecoveryPerSecond * deltaTime);
        node.Heat = Mathf.Max(0f, node.Heat - simulationProfile.AmbientCoolingPerSecond * deltaTime);
        node.Heat = Mathf.Max(0f, node.Heat - node.Wetness * simulationProfile.WetnessCoolingPerSecond * deltaTime);

        if (node.IsBurning)
        {
            node.RemainingFuel = Mathf.Max(0f, node.RemainingFuel - simulationProfile.FuelBurnPerSecond * deltaTime);
            SpreadHeatToNeighbors(node, deltaTime);
        }

        return
            !Mathf.Approximately(previousHeat, node.Heat) ||
            !Mathf.Approximately(previousWetness, node.Wetness) ||
            !Mathf.Approximately(previousFuel, node.RemainingFuel);
    }

    private void SpreadHeatToNeighbors(FireRuntimeNode source, float deltaTime)
    {
        for (int i = 0; i < source.NeighborIndices.Count; i++)
        {
            FireRuntimeNode target = runtimeGraph.GetNode(source.NeighborIndices[i]);
            if (target == null || target.RemainingFuel <= 0f)
            {
                continue;
            }

            if (source.IsTrackedByIncident)
            {
                target.IsTrackedByIncident = true;
                target.HazardType = activeIncidentHazardType;
            }

            float heatTransfer = source.Heat * simulationProfile.NeighborHeatTransferPerSecond * deltaTime;
            heatTransfer *= 1f - target.SpreadResistance;
            heatTransfer *= source.SurfaceType == target.SurfaceType
                ? simulationProfile.SameSurfaceTransferMultiplier
                : simulationProfile.CrossSurfaceTransferMultiplier;

            float verticalAlignment = Vector3.Dot(source.SurfaceNormal, target.SurfaceNormal);
            heatTransfer *= Mathf.Lerp(simulationProfile.VerticalSpreadBias, 1f, Mathf.InverseLerp(-1f, 1f, verticalAlignment));
            target.PendingHeatDelta += heatTransfer;
        }
    }

    private void RebuildClusters()
    {
        clusterSnapshots.Clear();
        if (runtimeGraph == null)
        {
            return;
        }

        int clusterId = 0;
        bool[] visited = new bool[runtimeGraph.Count];
        float mergeDistanceSqr = simulationProfile.ClusterMergeDistance * simulationProfile.ClusterMergeDistance;

        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode seed = runtimeGraph.GetNode(i);
            if (seed == null || visited[i] || !seed.IsBurning)
            {
                continue;
            }

            Queue<int> queue = new Queue<int>();
            List<int> clusterNodeIndices = new List<int>();
            queue.Enqueue(i);
            visited[i] = true;

            Vector3 center = Vector3.zero;
            Vector3 normal = Vector3.zero;
            float intensitySum = 0f;
            float maxRadiusSqr = 0f;
            int burningNodeCount = 0;
            Dictionary<FireHazardType, int> hazardCounts = new Dictionary<FireHazardType, int>();
            List<FireClusterMemberSnapshot> memberSnapshots = new List<FireClusterMemberSnapshot>();

            while (queue.Count > 0)
            {
                int nodeIndex = queue.Dequeue();
                FireRuntimeNode node = runtimeGraph.GetNode(nodeIndex);
                if (node == null || !node.IsBurning)
                {
                    continue;
                }

                clusterNodeIndices.Add(nodeIndex);
                burningNodeCount++;
                center += node.Position;
                normal += node.SurfaceNormal;
                float nodeIntensity = Mathf.Clamp01(node.Heat / Mathf.Max(0.01f, node.IgnitionThreshold));
                intensitySum += nodeIntensity;
                memberSnapshots.Add(new FireClusterMemberSnapshot(
                    node.Position,
                    node.SurfaceNormal,
                    nodeIntensity,
                    node.HazardType));

                if (!hazardCounts.ContainsKey(node.HazardType))
                {
                    hazardCounts[node.HazardType] = 0;
                }

                hazardCounts[node.HazardType]++;

                for (int neighborListIndex = 0; neighborListIndex < node.NeighborIndices.Count; neighborListIndex++)
                {
                    int neighborIndex = node.NeighborIndices[neighborListIndex];
                    if (neighborIndex < 0 || neighborIndex >= visited.Length || visited[neighborIndex])
                    {
                        continue;
                    }

                    FireRuntimeNode neighbor = runtimeGraph.GetNode(neighborIndex);
                    if (neighbor == null || !neighbor.IsBurning)
                    {
                        continue;
                    }

                    if ((neighbor.Position - seed.Position).sqrMagnitude > mergeDistanceSqr)
                    {
                        continue;
                    }

                    visited[neighborIndex] = true;
                    queue.Enqueue(neighborIndex);
                }
            }

            if (burningNodeCount <= 0)
            {
                continue;
            }

            center /= burningNodeCount;
            Vector3 averageNormal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;

            for (int nodeListIndex = 0; nodeListIndex < clusterNodeIndices.Count; nodeListIndex++)
            {
                FireRuntimeNode node = runtimeGraph.GetNode(clusterNodeIndices[nodeListIndex]);
                if (node == null || !node.IsBurning)
                {
                    continue;
                }

                maxRadiusSqr = Mathf.Max(maxRadiusSqr, (node.Position - center).sqrMagnitude);
            }

            clusterSnapshots.Add(new FireClusterSnapshot(
                clusterId++,
                center,
                averageNormal,
                Mathf.Clamp01(intensitySum / burningNodeCount),
                Mathf.Sqrt(maxRadiusSqr),
                burningNodeCount,
                ResolveDominantHazardType(hazardCounts),
                memberSnapshots));
        }
    }

    private void SyncEffects()
    {
        FireEffectManager runtimeEffectManager = EnsureEffectManager();
        if (runtimeEffectManager == null || simulationProfile == null)
        {
            return;
        }

        runtimeEffectManager.SyncSnapshots(clusterSnapshots, simulationProfile.MaxClusterViews);
    }

    private void DisableEffects()
    {
        FireEffectManager runtimeEffectManager = EnsureEffectManager();
        if (runtimeEffectManager != null)
        {
            runtimeEffectManager.DisableAllViews();
        }
    }

    private FireEffectManager EnsureEffectManager()
    {
        if (effectManager == null)
        {
            effectManager = GetComponent<FireEffectManager>();
            if (effectManager == null)
            {
                effectManager = gameObject.AddComponent<FireEffectManager>();
            }
        }

        effectManager.Configure(clusterViewPrefab, clusterViewRoot);
        return effectManager;
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

    private void RebuildRuntimeIncidentNodes(IReadOnlyList<FireIncidentPlacement> placements, FireHazardType hazardType)
    {
        ClearRuntimeIncidentNodes();
        if (placements == null || placements.Count <= 0)
        {
            surfaceGraph?.ClearRuntimeNodeOverrides();
            return;
        }

        Transform root = EnsureRuntimeIncidentNodeRoot();
        for (int i = 0; i < placements.Count; i++)
        {
            FireIncidentPlacement placement = placements[i];
            GameObject nodeObject = new GameObject($"RuntimeFireNode_{i + 1}");
            nodeObject.transform.SetParent(root, false);
            nodeObject.transform.position = placement.Position;
            nodeObject.transform.rotation = ResolveRuntimeNodeRotation(placement.SurfaceNormal);
            nodeObject.transform.localScale = Vector3.one;

            FireSurfaceNodeAuthoring node = nodeObject.AddComponent<FireSurfaceNodeAuthoring>();
            FireHazardType nodeHazardType = placement.InitialIntensity01 > 0f
                ? hazardType
                : FireHazardType.OrdinaryCombustibles;
            node.ConfigureRuntimeNode(
                $"RuntimeIncidentNode_{i + 1}",
                ResolveRuntimeSurfaceKind(placement.SurfaceNormal),
                placement.SurfaceNormal,
                runtimeNodeInitialFuel,
                runtimeNodeIgnitionThresholdMultiplier,
                runtimeNodeSpreadResistance,
                nodeHazardType,
                runtimeStartIgnited: false,
                runtimeAutoConnectNearbyNodes: true,
                runtimeAutoConnectRadius: runtimeNodeAutoConnectRadius,
                runtimeDrawGizmos: runtimeNodeDrawGizmos,
                runtimeGizmoColor: runtimeNodeGizmoColor);
            runtimeIncidentNodes.Add(node);
        }

        surfaceGraph.SetRuntimeNodeOverrides(runtimeIncidentNodes);
    }

    private void ClearRuntimeIncidentNodes()
    {
        surfaceGraph?.ClearRuntimeNodeOverrides();
        for (int i = 0; i < runtimeIncidentNodes.Count; i++)
        {
            FireSurfaceNodeAuthoring node = runtimeIncidentNodes[i];
            if (node != null)
            {
                Destroy(node.gameObject);
            }
        }

        runtimeIncidentNodes.Clear();
    }

    private Transform EnsureRuntimeIncidentNodeRoot()
    {
        if (runtimeIncidentNodeRoot != null)
        {
            return runtimeIncidentNodeRoot;
        }

        Transform parent = surfaceGraph != null ? surfaceGraph.transform : transform;
        GameObject rootObject = new GameObject("RuntimeIncidentNodes");
        runtimeIncidentNodeRoot = rootObject.transform;
        runtimeIncidentNodeRoot.SetParent(parent, false);
        runtimeIncidentNodeRoot.localPosition = Vector3.zero;
        runtimeIncidentNodeRoot.localRotation = Quaternion.identity;
        runtimeIncidentNodeRoot.localScale = Vector3.one;
        return runtimeIncidentNodeRoot;
    }

    private static Quaternion ResolveRuntimeNodeRotation(Vector3 surfaceNormal)
    {
        Vector3 up = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, up);
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(Vector3.right, up);
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.Cross(up, Vector3.right);
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }

        return Quaternion.LookRotation(forward.normalized, up);
    }

    private static FireSurfaceNodeAuthoring.SurfaceKind ResolveRuntimeSurfaceKind(Vector3 surfaceNormal)
    {
        Vector3 normalizedNormal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        float upDot = Vector3.Dot(normalizedNormal, Vector3.up);
        if (upDot >= 0.6f)
        {
            return FireSurfaceNodeAuthoring.SurfaceKind.Floor;
        }

        if (upDot <= -0.35f)
        {
            return FireSurfaceNodeAuthoring.SurfaceKind.Ceiling;
        }

        return FireSurfaceNodeAuthoring.SurfaceKind.Wall;
    }

    private static FireHazardType ResolveDominantHazardType(Dictionary<FireHazardType, int> hazardCounts)
    {
        FireHazardType resolvedType = FireHazardType.OrdinaryCombustibles;
        int resolvedCount = -1;

        foreach (KeyValuePair<FireHazardType, int> pair in hazardCounts)
        {
            if (pair.Value <= resolvedCount)
            {
                continue;
            }

            resolvedType = pair.Key;
            resolvedCount = pair.Value;
        }

        return resolvedType;
    }

    private void ResetRuntimeStateToBaseline(bool useAuthoringIgnition)
    {
        if (runtimeGraph == null)
        {
            return;
        }

        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null)
            {
                continue;
            }

            node.HazardType = node.Authoring != null ? node.Authoring.HazardType : FireHazardType.OrdinaryCombustibles;
            node.RemainingFuel = node.InitialFuel;
            node.Wetness = 0f;
            node.PendingHeatDelta = 0f;
            node.PendingWetnessDelta = 0f;
            node.IsTrackedByIncident = false;
            node.Heat = useAuthoringIgnition && node.AuthoringStartIgnited
                ? Mathf.Max(0.01f, node.IgnitionThreshold)
                : 0f;
        }
    }

    private int FindClosestNodeIndex(Vector3 worldPosition)
    {
        int bestIndex = -1;
        float bestDistanceSqr = float.PositiveInfinity;
        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null)
            {
                continue;
            }

            float distanceSqr = (node.Position - worldPosition).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            bestIndex = i;
        }

        return bestIndex;
    }

    private int CountNodes(System.Predicate<FireRuntimeNode> predicate)
    {
        if (!initialized || runtimeGraph == null || predicate == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (predicate(node))
            {
                count++;
            }
        }

        return count;
    }

    private float ResolveSuppressionEffectiveness(FireSuppressionAgent agent)
    {
        switch (activeIncidentHazardType)
        {
            case FireHazardType.Electrical:
                switch (agent)
                {
                    case FireSuppressionAgent.Water:
                        return activeHazardSourceIsolated ? 0.8f : 0f;
                    case FireSuppressionAgent.CO2:
                        return activeHazardSourceIsolated ? 1f : 1.35f;
                    default:
                        return activeHazardSourceIsolated ? 1.05f : 1.25f;
                }

            case FireHazardType.FlammableLiquid:
                switch (agent)
                {
                    case FireSuppressionAgent.Water:
                        return 0f;
                    case FireSuppressionAgent.CO2:
                        return 1f;
                    default:
                        return 1.2f;
                }

            case FireHazardType.GasFed:
                switch (agent)
                {
                    case FireSuppressionAgent.Water:
                        return activeHazardSourceIsolated ? 0.85f : 0.3f;
                    case FireSuppressionAgent.CO2:
                        return activeHazardSourceIsolated ? 1f : 0.4f;
                    default:
                        return activeHazardSourceIsolated ? 1.1f : 0.45f;
                }

            default:
                switch (agent)
                {
                    case FireSuppressionAgent.Water:
                        return 1f;
                    case FireSuppressionAgent.CO2:
                        return 0.55f;
                    default:
                        return 0.8f;
                }
        }
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    private void LogNodeHeatProgress()
    {
        if (!initialized || runtimeGraph == null || runtimeGraph.Count <= 0)
        {
            Debug.Log($"{nameof(FireSimulationManager)} '{name}': runtime graph not initialized.", this);
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(nameof(FireSimulationManager))
            .Append(" '")
            .Append(name)
            .Append("' node heat:");

        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null)
            {
                continue;
            }

            string nodeId = node.Authoring != null ? node.Authoring.NodeId : $"Node{i}";
            sb.Append(" [")
                .Append(nodeId)
                .Append(": heat=")
                .Append(node.Heat.ToString("0.000"))
                .Append('/')
                .Append(node.IgnitionThreshold.ToString("0.000"))
                .Append(", fuel=")
                .Append(node.RemainingFuel.ToString("0.000"))
                .Append(", burning=")
                .Append(node.IsBurning ? 'Y' : 'N')
                .Append(']');
        }

        Debug.Log(sb.ToString(), this);
    }

    private void LogRuntimeGraphTopology()
    {
        if (!initialized || runtimeGraph == null || runtimeGraph.Count <= 0)
        {
            Debug.Log($"{nameof(FireSimulationManager)} '{name}': no runtime graph topology to log.", this);
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(nameof(FireSimulationManager))
            .Append(" '")
            .Append(name)
            .Append("' graph topology:");

        for (int i = 0; i < runtimeGraph.Count; i++)
        {
            FireRuntimeNode node = runtimeGraph.GetNode(i);
            if (node == null)
            {
                continue;
            }

            string nodeId = node.Authoring != null ? node.Authoring.NodeId : $"Node{i}";
            sb.Append(" [")
                .Append(nodeId)
                .Append(" -> ");

            if (node.NeighborIndices.Count <= 0)
            {
                sb.Append("none");
            }
            else
            {
                for (int neighborListIndex = 0; neighborListIndex < node.NeighborIndices.Count; neighborListIndex++)
                {
                    if (neighborListIndex > 0)
                    {
                        sb.Append(", ");
                    }

                    FireRuntimeNode neighbor = runtimeGraph.GetNode(node.NeighborIndices[neighborListIndex]);
                    string neighborId = neighbor != null && neighbor.Authoring != null
                        ? neighbor.Authoring.NodeId
                        : $"Node{node.NeighborIndices[neighborListIndex]}";
                    sb.Append(neighborId);
                }
            }

            sb.Append(']');
        }

        Debug.Log(sb.ToString(), this);
    }
}
