using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FireEffectManager : MonoBehaviour
{
    [SerializeField] private FireClusterView clusterViewPrefab;
    [SerializeField] private Transform clusterViewRoot;

    private readonly List<FireClusterView> clusterViews = new List<FireClusterView>();

    public void Configure(FireClusterView viewPrefab, Transform viewRoot)
    {
        if (viewPrefab != null)
        {
            clusterViewPrefab = viewPrefab;
        }

        if (viewRoot != null)
        {
            clusterViewRoot = viewRoot;
        }
    }

    public void SyncSnapshots(IReadOnlyList<FireClusterSnapshot> snapshots, int maxVisibleViews)
    {
        if (clusterViewPrefab == null)
        {
            return;
        }

        int snapshotCount = snapshots != null ? snapshots.Count : 0;
        int visibleClusterCount = Mathf.Min(snapshotCount, Mathf.Max(0, maxVisibleViews));
        EnsureClusterViewCapacity(visibleClusterCount);

        for (int i = 0; i < visibleClusterCount; i++)
        {
            FireClusterView view = clusterViews[i];
            FireClusterSnapshot snapshot = snapshots[i];
            view.Bind(snapshot.ClusterId);
            view.ApplySnapshot(snapshot);
        }

        for (int i = visibleClusterCount; i < clusterViews.Count; i++)
        {
            clusterViews[i].Unbind();
        }
    }

    public void DisableAllViews()
    {
        for (int i = 0; i < clusterViews.Count; i++)
        {
            clusterViews[i].Unbind();
        }
    }

    private void EnsureClusterViewCapacity(int count)
    {
        while (clusterViews.Count < count)
        {
            Transform parent = clusterViewRoot != null ? clusterViewRoot : transform;
            FireClusterView view = Instantiate(clusterViewPrefab, parent);
            view.Unbind();
            clusterViews.Add(view);
        }
    }
}
