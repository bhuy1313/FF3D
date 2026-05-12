using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class FireEffectManager : MonoBehaviour
{
    private sealed class RenderGroup
    {
        public FireNodeSnapshot Snapshot;
        public Vector3 ScaleMultiplier = Vector3.one;
        public int RepresentativeNodeIndex;
    }

    [Header("Hazard Effect Prefabs")]
    [SerializeField] private FireNodeEffectView ordinaryEffectPrefab;
    [SerializeField] private FireNodeEffectView electricalEffectPrefab;
    [SerializeField] private FireNodeEffectView flammableLiquidEffectPrefab;
    [SerializeField] private FireNodeEffectView gasEffectPrefab;
    [Header("Pooling")]
    [SerializeField] private Transform effectRoot;
    [SerializeField] private Transform pooledEffectRoot;
    [SerializeField] [Min(0)] private int maxVisibleEffects = 32;
    [Tooltip("Optional reference camera for distance culling. Falls back to Camera.main.")]
    [SerializeField] private Camera referenceCamera;
    [Header("Clustered VFX")]
    [SerializeField] private bool enableEffectClustering = true;
    [FormerlySerializedAs("clusterTargetSize")]
    [SerializeField] [Min(2)] private int clusterMinSize = 3;
    [Tooltip("Maximum node count per clustered effect. Set <= 0 for no explicit cap.")]
    [SerializeField] [Min(0)] private int clusterMaxSize = 6;
    [SerializeField] [Min(0f)] private float clusterMergeDistance = 1.9f;
    [SerializeField] [Min(0f)] private float clusterHeightTolerance = 0.9f;
    [SerializeField] [Min(0f)] private float clusterScaleBonusPerExtraNode = 0.3f;
    [Header("Debug")]
    [SerializeField] private int debugInputSnapshotCount;
    [SerializeField] private int debugRenderGroupCount;
    [SerializeField] private int debugClusteredGroupCount;
    [SerializeField] private int debugClusteredNodeCount;
    [SerializeField] private int debugBestSameHazardNeighborCount;
    [SerializeField] private float debugBestHorizontalNeighborDistance = -1f;
    [SerializeField] private float debugBestHeightDelta = -1f;

    private readonly Dictionary<FireHazardType, Stack<FireNodeEffectView>> pooledByHazard =
        new Dictionary<FireHazardType, Stack<FireNodeEffectView>>();
    private readonly Dictionary<int, FireNodeEffectView> activeByNode =
        new Dictionary<int, FireNodeEffectView>();
    private readonly List<FireNodeEffectView> retiringViews = new List<FireNodeEffectView>();
    private readonly List<int> releaseScratch = new List<int>();
    private readonly List<int> retireScratch = new List<int>();
    private readonly List<int> sortedIndices = new List<int>();
    private readonly List<float> sortedDistancesSqr = new List<float>();
    private readonly HashSet<int> wantedNodeScratch = new HashSet<int>();
    private readonly List<RenderGroup> renderGroups = new List<RenderGroup>();
    private readonly List<int> candidateGroupIndices = new List<int>();
    private readonly Queue<int> clusterSearchQueue = new Queue<int>();

    private void OnDestroy()
    {
        if (pooledEffectRoot != null && pooledEffectRoot != transform)
        {
            Destroy(pooledEffectRoot.gameObject);
        }
    }

    public void Configure(
        FireNodeEffectView ordinary,
        FireNodeEffectView electrical,
        FireNodeEffectView flammableLiquid,
        FireNodeEffectView gas,
        Transform root,
        int maxEffects)
    {
        if (ordinary != null) ordinaryEffectPrefab = ordinary;
        if (electrical != null) electricalEffectPrefab = electrical;
        if (flammableLiquid != null) flammableLiquidEffectPrefab = flammableLiquid;
        if (gas != null) gasEffectPrefab = gas;
        if (root != null) effectRoot = root;
        if (maxEffects > 0) maxVisibleEffects = maxEffects;
        EnsurePooledEffectRoot();
    }

    public void SyncNodes(IReadOnlyList<FireNodeSnapshot> snapshots)
    {
        TickRetiringViews();

        int snapshotCount = snapshots != null ? snapshots.Count : 0;
        debugInputSnapshotCount = snapshotCount;
        if (snapshotCount <= 0)
        {
            debugRenderGroupCount = 0;
            debugClusteredGroupCount = 0;
            debugClusteredNodeCount = 0;
            RetireAllActive();
            return;
        }

        Camera camera = ResolveCamera();
        Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
        bool hasCamera = camera != null;
        BuildRenderGroups(snapshots);

        sortedIndices.Clear();
        sortedDistancesSqr.Clear();
        for (int i = 0; i < renderGroups.Count; i++)
        {
            FireNodeSnapshot snapshot = renderGroups[i].Snapshot;
            float distanceSqr = hasCamera
                ? (snapshot.Position - cameraPosition).sqrMagnitude
                : 0f;

            InsertSorted(i, distanceSqr);
        }

        int visibleCount = Mathf.Min(sortedIndices.Count, Mathf.Max(0, maxVisibleEffects));

        // Build wanted set + bind/apply.
        releaseScratch.Clear();
        HashSet<int> wantedNodes = wantedNodeScratch;
        wantedNodes.Clear();

        for (int i = 0; i < visibleCount; i++)
        {
            RenderGroup renderGroup = renderGroups[sortedIndices[i]];
            FireNodeSnapshot snapshot = renderGroup.Snapshot;
            wantedNodes.Add(renderGroup.RepresentativeNodeIndex);

            if (activeByNode.TryGetValue(renderGroup.RepresentativeNodeIndex, out FireNodeEffectView existing))
            {
                if (existing.BoundHazardType != snapshot.HazardType)
                {
                    BeginRetire(existing);
                    activeByNode.Remove(renderGroup.RepresentativeNodeIndex);
                    existing = null;
                }
            }

            if (existing == null)
            {
                existing = AcquireFromPool(snapshot.HazardType);
                if (existing == null)
                {
                    continue;
                }

                existing.Bind(renderGroup.RepresentativeNodeIndex, snapshot.HazardType);
                activeByNode[renderGroup.RepresentativeNodeIndex] = existing;
            }

            existing.ApplySnapshot(snapshot, renderGroup.ScaleMultiplier);
        }

        // Retire any active view not in wanted.
        retireScratch.Clear();
        foreach (KeyValuePair<int, FireNodeEffectView> pair in activeByNode)
        {
            if (!wantedNodes.Contains(pair.Key))
            {
                retireScratch.Add(pair.Key);
            }
        }

        for (int i = 0; i < retireScratch.Count; i++)
        {
            int nodeIndex = retireScratch[i];
            if (activeByNode.TryGetValue(nodeIndex, out FireNodeEffectView view))
            {
                BeginRetire(view);
                activeByNode.Remove(nodeIndex);
            }
        }
    }

    private void BuildRenderGroups(IReadOnlyList<FireNodeSnapshot> snapshots)
    {
        renderGroups.Clear();
        debugRenderGroupCount = 0;
        debugClusteredGroupCount = 0;
        debugClusteredNodeCount = 0;
        debugBestSameHazardNeighborCount = 0;
        debugBestHorizontalNeighborDistance = -1f;
        debugBestHeightDelta = -1f;
        if (snapshots == null || snapshots.Count == 0)
        {
            return;
        }

        bool[] consumed = new bool[snapshots.Count];
        float mergeDistanceSqr = clusterMergeDistance * clusterMergeDistance;
        for (int i = 0; i < snapshots.Count; i++)
        {
            if (consumed[i])
            {
                continue;
            }

            FireNodeSnapshot anchor = snapshots[i];
            if (!enableEffectClustering)
            {
                consumed[i] = true;
                    renderGroups.Add(new RenderGroup
                    {
                        Snapshot = anchor,
                        RepresentativeNodeIndex = anchor.NodeIndex,
                        ScaleMultiplier = Vector3.one
                    });
                continue;
            }

            BuildConnectedCluster(snapshots, consumed, i, mergeDistanceSqr);
            if (candidateGroupIndices.Count < clusterMinSize)
            {
                for (int groupIndex = 0; groupIndex < candidateGroupIndices.Count; groupIndex++)
                {
                    int snapshotIndex = candidateGroupIndices[groupIndex];
                    consumed[snapshotIndex] = true;
                    FireNodeSnapshot member = snapshots[snapshotIndex];
                    renderGroups.Add(new RenderGroup
                    {
                        Snapshot = member,
                        RepresentativeNodeIndex = member.NodeIndex,
                        ScaleMultiplier = Vector3.one
                    });
                }

                continue;
            }

            Vector3 averagePosition = Vector3.zero;
            Vector3 averageNormal = Vector3.zero;
            float maxIntensity = 0f;
            int representativeNodeIndex = anchor.NodeIndex;
            FireIncidentNodeKind representativeKind = anchor.Kind;
            for (int groupIndex = 0; groupIndex < candidateGroupIndices.Count; groupIndex++)
            {
                int snapshotIndex = candidateGroupIndices[groupIndex];
                consumed[snapshotIndex] = true;
                FireNodeSnapshot member = snapshots[snapshotIndex];
                averagePosition += member.Position;
                averageNormal += member.SurfaceNormal;
                maxIntensity = Mathf.Max(maxIntensity, member.Intensity);
            }

            float inverseCount = 1f / candidateGroupIndices.Count;
            averagePosition *= inverseCount;
            averageNormal = averageNormal.sqrMagnitude > 0.001f ? averageNormal.normalized : anchor.SurfaceNormal;

            renderGroups.Add(new RenderGroup
            {
                Snapshot = new FireNodeSnapshot(
                    anchor.NodeIndex,
                    averagePosition,
                    averageNormal,
                    maxIntensity,
                    anchor.HazardType,
                    representativeKind),
                RepresentativeNodeIndex = representativeNodeIndex,
                ScaleMultiplier = new Vector3(
                    1f + (candidateGroupIndices.Count - 1) * clusterScaleBonusPerExtraNode,
                    1f,
                    1f + (candidateGroupIndices.Count - 1) * clusterScaleBonusPerExtraNode)
            });
            debugClusteredGroupCount++;
            debugClusteredNodeCount += candidateGroupIndices.Count;
        }

        debugRenderGroupCount = renderGroups.Count;
    }

    private void BuildConnectedCluster(
        IReadOnlyList<FireNodeSnapshot> snapshots,
        bool[] consumed,
        int startIndex,
        float mergeDistanceSqr)
    {
        candidateGroupIndices.Clear();
        clusterSearchQueue.Clear();
        candidateGroupIndices.Add(startIndex);
        clusterSearchQueue.Enqueue(startIndex);
        FireHazardType hazardType = snapshots[startIndex].HazardType;
        int maxClusterSize = clusterMaxSize > 0 ? Mathf.Max(clusterMinSize, clusterMaxSize) : int.MaxValue;

        while (clusterSearchQueue.Count > 0)
        {
            int currentIndex = clusterSearchQueue.Dequeue();
            FireNodeSnapshot current = snapshots[currentIndex];
            int sameHazardNeighborCount = 0;

            for (int i = 0; i < snapshots.Count; i++)
            {
                if (i == currentIndex || consumed[i] || candidateGroupIndices.Contains(i))
                {
                    continue;
                }

                FireNodeSnapshot candidate = snapshots[i];
                if (candidate.HazardType != hazardType)
                {
                    continue;
                }

                sameHazardNeighborCount++;
                float heightDelta = Mathf.Abs(candidate.Position.y - current.Position.y);
                float horizontalDistanceSqr = GetHorizontalDistanceSqr(current.Position, candidate.Position);
                float horizontalDistance = Mathf.Sqrt(horizontalDistanceSqr);
                UpdateClusterDebug(sameHazardNeighborCount, horizontalDistance, heightDelta);

                if (heightDelta > clusterHeightTolerance || horizontalDistanceSqr > mergeDistanceSqr)
                {
                    continue;
                }

                candidateGroupIndices.Add(i);
                if (candidateGroupIndices.Count >= maxClusterSize)
                {
                    return;
                }

                clusterSearchQueue.Enqueue(i);
            }
        }
    }

    private void UpdateClusterDebug(int sameHazardNeighborCount, float horizontalDistance, float heightDelta)
    {
        if (sameHazardNeighborCount > debugBestSameHazardNeighborCount)
        {
            debugBestSameHazardNeighborCount = sameHazardNeighborCount;
        }

        if (debugBestHorizontalNeighborDistance < 0f || horizontalDistance < debugBestHorizontalNeighborDistance)
        {
            debugBestHorizontalNeighborDistance = horizontalDistance;
            debugBestHeightDelta = heightDelta;
        }
    }

    private static float GetHorizontalDistanceSqr(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    public void DisableAll()
    {
        foreach (KeyValuePair<int, FireNodeEffectView> pair in activeByNode)
        {
            ReleaseToPool(pair.Value);
        }

        activeByNode.Clear();

        for (int i = 0; i < retiringViews.Count; i++)
        {
            ReleaseToPool(retiringViews[i]);
        }

        retiringViews.Clear();
    }

    private void RetireAllActive()
    {
        releaseScratch.Clear();
        foreach (KeyValuePair<int, FireNodeEffectView> pair in activeByNode)
        {
            releaseScratch.Add(pair.Key);
        }

        for (int i = 0; i < releaseScratch.Count; i++)
        {
            int nodeIndex = releaseScratch[i];
            if (activeByNode.TryGetValue(nodeIndex, out FireNodeEffectView view))
            {
                BeginRetire(view);
                activeByNode.Remove(nodeIndex);
            }
        }
    }

    private void TickRetiringViews()
    {
        for (int i = retiringViews.Count - 1; i >= 0; i--)
        {
            FireNodeEffectView view = retiringViews[i];
            if (view == null || view.TickRetire(Time.deltaTime))
            {
                retiringViews.RemoveAt(i);
                ReleaseToPool(view);
            }
        }
    }

    private void BeginRetire(FireNodeEffectView view)
    {
        if (view == null)
        {
            return;
        }

        view.BeginRetire();
        retiringViews.Add(view);
    }

    private void InsertSorted(int snapshotIndex, float distanceSqr)
    {
        // Keep both lists sorted ascending by distanceSqr; bounded by maxVisibleEffects to avoid full sort.
        int cap = Mathf.Max(1, maxVisibleEffects);
        if (sortedIndices.Count >= cap && distanceSqr >= sortedDistancesSqr[sortedDistancesSqr.Count - 1])
        {
            return;
        }

        int insertAt = sortedDistancesSqr.Count;
        for (int i = 0; i < sortedDistancesSqr.Count; i++)
        {
            if (distanceSqr < sortedDistancesSqr[i])
            {
                insertAt = i;
                break;
            }
        }

        sortedIndices.Insert(insertAt, snapshotIndex);
        sortedDistancesSqr.Insert(insertAt, distanceSqr);

        if (sortedIndices.Count > cap)
        {
            sortedIndices.RemoveAt(sortedIndices.Count - 1);
            sortedDistancesSqr.RemoveAt(sortedDistancesSqr.Count - 1);
        }
    }

    private FireNodeEffectView AcquireFromPool(FireHazardType hazardType)
    {
        FireNodeEffectView prefab = ResolvePrefab(hazardType);
        if (prefab == null)
        {
            return null;
        }

        if (pooledByHazard.TryGetValue(hazardType, out Stack<FireNodeEffectView> stack) && stack.Count > 0)
        {
            FireNodeEffectView pooled = stack.Pop();
            if (pooled != null)
            {
                Transform activeParent = effectRoot != null ? effectRoot : transform;
                pooled.transform.SetParent(activeParent, false);
                return pooled;
            }
        }

        Transform parent = effectRoot != null ? effectRoot : transform;
        FireNodeEffectView instance = Instantiate(prefab, parent);
        instance.Unbind();
        return instance;
    }

    private void ReleaseToPool(FireNodeEffectView view)
    {
        if (view == null)
        {
            return;
        }

        FireHazardType hazardType = view.BoundHazardType;
        view.Unbind();

        if (!pooledByHazard.TryGetValue(hazardType, out Stack<FireNodeEffectView> stack))
        {
            stack = new Stack<FireNodeEffectView>();
            pooledByHazard[hazardType] = stack;
        }

        Transform poolRoot = EnsurePooledEffectRoot();
        if (poolRoot != null)
        {
            view.transform.SetParent(poolRoot, false);
            view.transform.localPosition = Vector3.zero;
            view.transform.localRotation = Quaternion.identity;
            view.transform.localScale = Vector3.one;
        }

        stack.Push(view);
    }

    private Transform EnsurePooledEffectRoot()
    {
        if (pooledEffectRoot != null)
        {
            return pooledEffectRoot;
        }

        GameObject poolObject = new GameObject("RuntimeFireEffectPool");
        poolObject.transform.SetParent(transform, false);
        pooledEffectRoot = poolObject.transform;
        return pooledEffectRoot;
    }

    private FireNodeEffectView ResolvePrefab(FireHazardType hazardType)
    {
        switch (hazardType)
        {
            case FireHazardType.Electrical:
                return electricalEffectPrefab != null ? electricalEffectPrefab : ordinaryEffectPrefab;
            case FireHazardType.FlammableLiquid:
                return flammableLiquidEffectPrefab != null ? flammableLiquidEffectPrefab : ordinaryEffectPrefab;
            case FireHazardType.GasFed:
                return gasEffectPrefab != null ? gasEffectPrefab : ordinaryEffectPrefab;
            default:
                return ordinaryEffectPrefab;
        }
    }

    private Camera ResolveCamera()
    {
        if (referenceCamera != null)
        {
            return referenceCamera;
        }

        Camera main = Camera.main;
        if (main != null)
        {
            referenceCamera = main;
        }

        return referenceCamera;
    }

}
