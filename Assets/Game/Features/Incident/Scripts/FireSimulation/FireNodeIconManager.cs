using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class FireNodeIconManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FireSimulationManager simulationManager;
    [SerializeField] private Camera referenceCamera;

    [Header("Icon")]
    [SerializeField] private bool showBurningNodeIcons = true;
    [SerializeField] private Sprite burningNodeIconSprite;
    [SerializeField] private Vector3 burningNodeIconOffset = new Vector3(0f, 0.85f, 0f);
    [SerializeField] [Min(8f)] private float burningNodeIconScreenSize = 42f;
    [SerializeField] [Min(0f)] private float burningNodeIconFadeStartDistance = 6f;
    [SerializeField] [Min(0f)] private float burningNodeIconVisibleDistance = 18f;
    [SerializeField] [Min(1)] private int maxVisibleBurningNodeIcons = 6;
    [SerializeField] [Min(0f)] private float maxBurningNodeIconHeightDifference = 4f;
    [SerializeField] private bool reverseDistanceFade;
    [SerializeField] private Color burningNodeIconColor = new Color(1f, 0.96f, 0.9f, 0.95f);

    [Header("Debug")]
    [SerializeField] private bool debugBurningNodeIcons;
    [SerializeField] private int debugLastSnapshotCount;
    [SerializeField] private int debugVisibleIconCandidateCount;
    [SerializeField] private int debugActiveIconCount;
    [SerializeField] private string debugResolvedCameraName;
    [SerializeField] private string debugResolvedDistanceSourceName;

    private readonly Stack<FireNodeIconView> pooledIcons = new Stack<FireNodeIconView>();
    private readonly Dictionary<int, FireNodeIconView> activeIconsByNode = new Dictionary<int, FireNodeIconView>();
    private readonly List<int> releaseIconScratch = new List<int>();
    private readonly HashSet<int> wantedIconNodeScratch = new HashSet<int>();
    private readonly List<IconCandidate> visibleIconCandidateScratch = new List<IconCandidate>();

    private Canvas runtimeIconCanvas;
    private Transform iconDistanceReference;

    private struct IconCandidate
    {
        public FireNodeSnapshot Snapshot;
        public float Distance;
        public float DistanceSqr;
        public float Alpha;
    }

    private void LateUpdate()
    {
        if (!showBurningNodeIcons)
        {
            DisableAllIcons();
            return;
        }

        FireSimulationManager targetSimulationManager = ResolveSimulationManager();
        IReadOnlyList<FireNodeSnapshot> snapshots = targetSimulationManager != null ? targetSimulationManager.NodeSnapshots : null;
        debugLastSnapshotCount = snapshots != null ? snapshots.Count : 0;
        if (snapshots == null || snapshots.Count == 0)
        {
            debugVisibleIconCandidateCount = 0;
            debugActiveIconCount = 0;
            DisableAllIcons();
            return;
        }

        Camera camera = ResolveCamera();
        debugResolvedCameraName = camera != null ? camera.name : "<none>";
        Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
        bool hasCamera = camera != null;

        Transform distanceReference = ResolveIconDistanceReference();
        Vector3 distanceReferencePosition = distanceReference != null ? distanceReference.position : cameraPosition;
        bool hasDistanceReference = distanceReference != null || hasCamera;
        debugResolvedDistanceSourceName = distanceReference != null ? distanceReference.name : debugResolvedCameraName;

        float fadeStart = Mathf.Max(0f, burningNodeIconFadeStartDistance);
        float visibleDistance = Mathf.Max(fadeStart, burningNodeIconVisibleDistance);
        float visibleDistanceSqr = visibleDistance * visibleDistance;
        float maxHeightDifference = Mathf.Max(0f, maxBurningNodeIconHeightDifference);
        int maxVisibleIcons = Mathf.Max(1, maxVisibleBurningNodeIcons);
        HashSet<int> wantedIcons = wantedIconNodeScratch;
        List<IconCandidate> visibleCandidates = visibleIconCandidateScratch;
        wantedIcons.Clear();
        visibleCandidates.Clear();

        for (int i = 0; i < snapshots.Count; i++)
        {
            FireNodeSnapshot snapshot = snapshots[i];
            float heightDifference = hasDistanceReference
                ? Mathf.Abs(snapshot.Position.y - distanceReferencePosition.y)
                : 0f;
            if (hasDistanceReference && heightDifference > maxHeightDifference)
            {
                continue;
            }

            float distanceSqr = hasDistanceReference
                ? (snapshot.Position - distanceReferencePosition).sqrMagnitude
                : 0f;
            if (hasDistanceReference && distanceSqr > visibleDistanceSqr)
            {
                continue;
            }

            float distance = hasDistanceReference ? Mathf.Sqrt(distanceSqr) : 0f;
            float alpha = hasDistanceReference
                ? ResolveDistanceFade(distance, fadeStart, visibleDistance, reverseDistanceFade)
                : 1f;
            if (alpha <= 0.001f)
            {
                continue;
            }

            visibleCandidates.Add(new IconCandidate
            {
                Snapshot = snapshot,
                Distance = distance,
                DistanceSqr = distanceSqr,
                Alpha = alpha
            });
        }

        visibleCandidates.Sort(static (left, right) =>
        {
            int distanceCompare = left.DistanceSqr.CompareTo(right.DistanceSqr);
            if (distanceCompare != 0)
            {
                return distanceCompare;
            }

            return left.Snapshot.NodeIndex.CompareTo(right.Snapshot.NodeIndex);
        });

        int visibleIconCandidates = visibleCandidates.Count;
        int iconCount = Mathf.Min(maxVisibleIcons, visibleCandidates.Count);
        for (int i = 0; i < iconCount; i++)
        {
            IconCandidate candidate = visibleCandidates[i];
            FireNodeSnapshot snapshot = candidate.Snapshot;
            wantedIcons.Add(snapshot.NodeIndex);
            if (!activeIconsByNode.TryGetValue(snapshot.NodeIndex, out FireNodeIconView iconView) || iconView == null)
            {
                iconView = AcquireIconFromPool();
                if (iconView == null)
                {
                    continue;
                }

                iconView.Bind(snapshot.NodeIndex);
                activeIconsByNode[snapshot.NodeIndex] = iconView;
            }

            iconView.Apply(
                camera,
                snapshot.Position + burningNodeIconOffset,
                candidate.Alpha,
                burningNodeIconScreenSize,
                burningNodeIconSprite,
                burningNodeIconColor);
        }

        debugVisibleIconCandidateCount = visibleIconCandidates;
        releaseIconScratch.Clear();
        foreach (KeyValuePair<int, FireNodeIconView> pair in activeIconsByNode)
        {
            if (!wantedIcons.Contains(pair.Key))
            {
                releaseIconScratch.Add(pair.Key);
            }
        }

        for (int i = 0; i < releaseIconScratch.Count; i++)
        {
            int nodeIndex = releaseIconScratch[i];
            if (activeIconsByNode.TryGetValue(nodeIndex, out FireNodeIconView iconView))
            {
                ReleaseIconToPool(iconView);
                activeIconsByNode.Remove(nodeIndex);
            }
        }

        debugActiveIconCount = activeIconsByNode.Count;
        if (debugBurningNodeIcons)
        {
            Debug.Log(
                $"[{nameof(FireNodeIconManager)}] snapshots={debugLastSnapshotCount}, iconCandidates={debugVisibleIconCandidateCount}, activeIcons={debugActiveIconCount}, camera={debugResolvedCameraName}, distanceSource={debugResolvedDistanceSourceName}",
                this);
        }
    }

    private void OnDisable()
    {
        DisableAllIcons();
    }

    private void OnDestroy()
    {
        if (runtimeIconCanvas != null)
        {
            Destroy(runtimeIconCanvas.gameObject);
        }
    }

    private FireSimulationManager ResolveSimulationManager()
    {
        if (simulationManager != null)
        {
            return simulationManager;
        }

        simulationManager = GetComponent<FireSimulationManager>();
        if (simulationManager == null)
        {
            simulationManager = FindAnyObjectByType<FireSimulationManager>(FindObjectsInactive.Include);
        }

        return simulationManager;
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

    private Transform ResolveIconDistanceReference()
    {
        if (iconDistanceReference != null)
        {
            return iconDistanceReference;
        }

        FirstPersonController controller = FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Exclude);
        if (controller != null)
        {
            iconDistanceReference = controller.transform;
            return iconDistanceReference;
        }

        PlayerVitals vitals = FindAnyObjectByType<PlayerVitals>(FindObjectsInactive.Exclude);
        if (vitals != null)
        {
            iconDistanceReference = vitals.transform;
            return iconDistanceReference;
        }

        Camera camera = ResolveCamera();
        iconDistanceReference = camera != null ? camera.transform : null;
        return iconDistanceReference;
    }

    private static float ResolveDistanceFade(float distance, float fadeStart, float visibleDistance, bool reverse)
    {
        if (!reverse)
        {
            if (distance <= fadeStart)
            {
                return 1f;
            }

            if (distance >= visibleDistance)
            {
                return 0f;
            }

            float t = Mathf.InverseLerp(visibleDistance, fadeStart, distance);
            return t * t * (3f - 2f * t);
        }

        if (distance <= fadeStart)
        {
            return 0f;
        }

        if (distance >= visibleDistance)
        {
            return 1f;
        }

        float reverseT = Mathf.InverseLerp(fadeStart, visibleDistance, distance);
        return reverseT * reverseT * (3f - 2f * reverseT);
    }

    private FireNodeIconView AcquireIconFromPool()
    {
        while (pooledIcons.Count > 0)
        {
            FireNodeIconView pooled = pooledIcons.Pop();
            if (pooled != null)
            {
                return pooled;
            }
        }

        GameObject iconObject = new GameObject("FireNodeIcon");
        iconObject.layer = gameObject.layer;
        iconObject.transform.SetParent(EnsureIconCanvas().transform, false);

        RectTransform rectTransform = iconObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.15f);
        rectTransform.anchoredPosition3D = Vector3.zero;

        iconObject.AddComponent<CanvasRenderer>();
        return iconObject.AddComponent<FireNodeIconView>();
    }

    private void ReleaseIconToPool(FireNodeIconView iconView)
    {
        if (iconView == null)
        {
            return;
        }

        iconView.Unbind();
        pooledIcons.Push(iconView);
    }

    private void DisableAllIcons()
    {
        foreach (KeyValuePair<int, FireNodeIconView> pair in activeIconsByNode)
        {
            ReleaseIconToPool(pair.Value);
        }

        activeIconsByNode.Clear();
    }

    private Canvas EnsureIconCanvas()
    {
        if (runtimeIconCanvas != null)
        {
            return runtimeIconCanvas;
        }

        GameObject canvasObject = new GameObject("RuntimeFireNodeIcons");
        canvasObject.layer = gameObject.layer;
        canvasObject.transform.SetParent(transform, false);

        runtimeIconCanvas = canvasObject.AddComponent<Canvas>();
        runtimeIconCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        runtimeIconCanvas.sortingOrder = 500;

        GraphicRaycaster raycaster = canvasObject.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return runtimeIconCanvas;
    }
}
