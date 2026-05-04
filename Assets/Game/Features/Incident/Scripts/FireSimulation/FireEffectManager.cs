using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FireEffectManager : MonoBehaviour
{
    [Header("Hazard Effect Prefabs")]
    [SerializeField] private FireNodeEffectView ordinaryEffectPrefab;
    [SerializeField] private FireNodeEffectView electricalEffectPrefab;
    [SerializeField] private FireNodeEffectView flammableLiquidEffectPrefab;
    [SerializeField] private FireNodeEffectView gasEffectPrefab;
    [Header("Pooling")]
    [SerializeField] private Transform effectRoot;
    [SerializeField] [Min(0)] private int maxVisibleEffects = 32;
    [Tooltip("Optional reference camera for distance culling. Falls back to Camera.main.")]
    [SerializeField] private Camera referenceCamera;
    [Tooltip("Effects further than this distance from the reference camera are hidden. Set <= 0 to disable distance culling.")]
    [SerializeField] private float maxVisibleDistance = 0f;

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
    }

    public void SetMaxVisibleDistance(float distance)
    {
        maxVisibleDistance = Mathf.Max(0f, distance);
    }

    public void SyncNodes(IReadOnlyList<FireNodeSnapshot> snapshots)
    {
        TickRetiringViews();

        int snapshotCount = snapshots != null ? snapshots.Count : 0;
        if (snapshotCount <= 0)
        {
            RetireAllActive();
            return;
        }

        Camera camera = ResolveCamera();
        Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
        bool hasCamera = camera != null;
        float maxDistanceSqr = maxVisibleDistance > 0f ? maxVisibleDistance * maxVisibleDistance : float.PositiveInfinity;

        sortedIndices.Clear();
        sortedDistancesSqr.Clear();
        for (int i = 0; i < snapshotCount; i++)
        {
            FireNodeSnapshot snapshot = snapshots[i];
            float distanceSqr = hasCamera
                ? (snapshot.Position - cameraPosition).sqrMagnitude
                : 0f;
            if (distanceSqr > maxDistanceSqr)
            {
                continue;
            }

            InsertSorted(i, distanceSqr);
        }

        int visibleCount = Mathf.Min(sortedIndices.Count, Mathf.Max(0, maxVisibleEffects));

        // Build wanted set + bind/apply.
        releaseScratch.Clear();
        HashSet<int> wantedNodes = ScratchSet;
        wantedNodes.Clear();

        for (int i = 0; i < visibleCount; i++)
        {
            FireNodeSnapshot snapshot = snapshots[sortedIndices[i]];
            wantedNodes.Add(snapshot.NodeIndex);

            if (activeByNode.TryGetValue(snapshot.NodeIndex, out FireNodeEffectView existing))
            {
                if (existing.BoundHazardType != snapshot.HazardType)
                {
                    BeginRetire(existing);
                    activeByNode.Remove(snapshot.NodeIndex);
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

                existing.Bind(snapshot.NodeIndex, snapshot.HazardType);
                activeByNode[snapshot.NodeIndex] = existing;
            }

            existing.ApplySnapshot(snapshot);
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

        stack.Push(view);
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

    private static readonly HashSet<int> ScratchSet = new HashSet<int>();
}
