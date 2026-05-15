using System;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class VictimIconManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera referenceCamera;
    [SerializeField] private IncidentMapSetupRoot explicitMapSetupRoot;

    [Header("Icon")]
    [SerializeField] private bool showVictimIcons = true;
    [SerializeField] private Sprite victimIconSprite;
    [SerializeField] private Vector3 victimIconOffset = new Vector3(0f, 2.1f, 0f);
    [SerializeField] [Min(8f)] private float victimIconScreenSize = 44f;
    [SerializeField] [Min(0f)] private float estimatedVictimIconRevealDistance = 10f;
    [SerializeField] [Min(0f)] private float victimIconFadeStartDistance = 8f;
    [SerializeField] [Min(0f)] private float victimIconVisibleDistance = 32f;
    [SerializeField] [Min(1)] private int maxVisibleVictimIcons = 8;
    [SerializeField] [Min(0.05f)] private float victimCacheRefreshInterval = 0.5f;
    [SerializeField] private bool hideEstimatedVictimIconsWhenDirectLineOfSightExists = true;
    [SerializeField] private Vector3 victimLineOfSightProbeOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] [Min(0.01f)] private float victimLineOfSightProbeRadius = 0.15f;
    [SerializeField] private LayerMask victimLineOfSightBlockerMask = ~0;
    [SerializeField] private Color estimatedVictimIconColor = new Color(1f, 0.84f, 0.28f, 0.92f);
    [SerializeField] private Color confirmedVictimIconColor = new Color(1f, 0.34f, 0.34f, 0.96f);

    [Header("Debug")]
    [SerializeField] private bool debugVictimIcons;
    [SerializeField] private int debugVictimCandidateCount;
    [SerializeField] private int debugActiveVictimIconCount;
    [SerializeField] private string debugResolvedCameraName;
    [SerializeField] private string debugResolvedDistanceSourceName;
    [SerializeField] private string debugVictimLocationIntelMode;
    [SerializeField] private bool debugShouldRevealVictimIconsAtStart;
    [SerializeField] private int debugConfiguredVisibleVictimIconCount;
    [SerializeField] private float debugResolvedEstimatedVictimIconRevealDistance;

    private readonly Stack<VictimIconView> pooledIcons = new Stack<VictimIconView>();
    private readonly Dictionary<VictimCondition, VictimIconView> activeIconsByVictim = new Dictionary<VictimCondition, VictimIconView>();
    private readonly List<VictimCondition> releaseScratch = new List<VictimCondition>();
    private readonly HashSet<VictimCondition> wantedVictims = new HashSet<VictimCondition>();
    private readonly List<IconCandidate> visibleCandidates = new List<IconCandidate>();
    private readonly List<VictimCondition> cachedVictims = new List<VictimCondition>();
    private readonly Dictionary<VictimCondition, VictimRuntimeCache> victimRuntimeCache = new Dictionary<VictimCondition, VictimRuntimeCache>();
    private RaycastHit[] lineOfSightHits = new RaycastHit[16];

    private Canvas runtimeIconCanvas;
    private Transform iconDistanceReference;
    private bool payloadResolved;
    private bool shouldRevealVictimIconsAtStart;
    private string victimLocationIntelMode = CallPhaseVictimLocationIntelMode.None.ToString();
    private int configuredVisibleVictimIconCount;
    private float configuredEstimatedVictimIconRevealDistance;
    private int nextBindingId = 1;
    private float nextVictimCacheRefreshTime;

    private struct IconCandidate
    {
        public VictimCondition Victim;
        public float DistanceSqr;
        public float Alpha;
    }

    private struct VictimRuntimeCache
    {
        public Rescuable Rescuable;
        public Collider Collider;
    }

    private void OnEnable()
    {
        nextVictimCacheRefreshTime = 0f;
        RefreshVictimCache(force: true);
    }

    private void LateUpdate()
    {
        ResolvePayloadSettings();
        if (!showVictimIcons || !shouldRevealVictimIconsAtStart)
        {
            DisableAllIcons();
            return;
        }

        Camera camera = ResolveCamera();
        debugResolvedCameraName = camera != null ? camera.name : "<none>";
        Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
        bool hasCamera = camera != null;

        Transform distanceReference = ResolveDistanceReference();
        Vector3 distanceReferencePosition = distanceReference != null ? distanceReference.position : cameraPosition;
        bool hasDistanceReference = distanceReference != null || hasCamera;
        debugResolvedDistanceSourceName = distanceReference != null ? distanceReference.name : debugResolvedCameraName;

        bool isEstimatedIntel = IsEstimatedIntelMode(victimLocationIntelMode);
        float visibleDistance = ResolveVisibleDistance(isEstimatedIntel);
        float fadeStart = ResolveFadeStartDistance(visibleDistance, isEstimatedIntel);
        float visibleDistanceSqr = visibleDistance * visibleDistance;
        int maxVisibleIcons = Mathf.Max(1, configuredVisibleVictimIconCount > 0 ? configuredVisibleVictimIconCount : maxVisibleVictimIcons);
        Color iconColor = ResolveIconColor(victimLocationIntelMode);

        RefreshVictimCache(force: false);
        wantedVictims.Clear();
        visibleCandidates.Clear();

        for (int i = 0; i < cachedVictims.Count; i++)
        {
            VictimCondition victim = cachedVictims[i];
            if (!IsVictimEligibleForIcon(victim, victimRuntimeCache))
            {
                continue;
            }

            Vector3 victimPosition = victim.transform.position;
            float distanceSqr = hasDistanceReference
                ? (victimPosition - distanceReferencePosition).sqrMagnitude
                : 0f;
            if (hasDistanceReference && distanceSqr > visibleDistanceSqr)
            {
                continue;
            }

            float distance = hasDistanceReference ? Mathf.Sqrt(distanceSqr) : 0f;
            if (isEstimatedIntel &&
                hideEstimatedVictimIconsWhenDirectLineOfSightExists &&
                HasDirectLineOfSight(distanceReference != null ? distanceReference : (camera != null ? camera.transform : null), victim))
            {
                continue;
            }

            float alpha = hasDistanceReference
                ? ResolveDistanceFade(distance, fadeStart, visibleDistance)
                : 1f;
            if (alpha <= 0.001f)
            {
                continue;
            }

            visibleCandidates.Add(new IconCandidate
            {
                Victim = victim,
                DistanceSqr = distanceSqr,
                Alpha = alpha
            });
        }

        SortVisibleCandidates(isEstimatedIntel);

        debugVictimCandidateCount = visibleCandidates.Count;
        int iconCount = Mathf.Min(maxVisibleIcons, visibleCandidates.Count);
        for (int i = 0; i < iconCount; i++)
        {
            IconCandidate candidate = visibleCandidates[i];
            VictimCondition victim = candidate.Victim;
            if (victim == null)
            {
                continue;
            }

            wantedVictims.Add(victim);
            if (!activeIconsByVictim.TryGetValue(victim, out VictimIconView iconView) || iconView == null)
            {
                iconView = AcquireIconFromPool();
                if (iconView == null)
                {
                    continue;
                }

                iconView.Bind(nextBindingId++);
                activeIconsByVictim[victim] = iconView;
            }

            iconView.Apply(
                camera,
                victim.transform.position + victimIconOffset,
                candidate.Alpha,
                victimIconScreenSize,
                victimIconSprite,
                iconColor);
        }

        releaseScratch.Clear();
        foreach (KeyValuePair<VictimCondition, VictimIconView> pair in activeIconsByVictim)
        {
            if (pair.Key == null || !wantedVictims.Contains(pair.Key))
            {
                releaseScratch.Add(pair.Key);
            }
        }

        for (int i = 0; i < releaseScratch.Count; i++)
        {
            VictimCondition victim = releaseScratch[i];
            if (activeIconsByVictim.TryGetValue(victim, out VictimIconView iconView))
            {
                ReleaseIconToPool(iconView);
                activeIconsByVictim.Remove(victim);
            }
        }

        debugActiveVictimIconCount = activeIconsByVictim.Count;
        if (debugVictimIcons)
        {
            Debug.Log(
                $"[{nameof(VictimIconManager)}] candidates={debugVictimCandidateCount}, activeIcons={debugActiveVictimIconCount}, intelMode={victimLocationIntelMode}, reveal={shouldRevealVictimIconsAtStart}, camera={debugResolvedCameraName}, distanceSource={debugResolvedDistanceSourceName}",
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

    private void ResolvePayloadSettings()
    {
        if (payloadResolved)
        {
            return;
        }

        payloadResolved = true;
        victimLocationIntelMode = CallPhaseVictimLocationIntelMode.None.ToString();
        shouldRevealVictimIconsAtStart = false;
        configuredVisibleVictimIconCount = 0;
        configuredEstimatedVictimIconRevealDistance = 0f;

        IncidentWorldSetupPayload payload = ResolvePayload();
        if (payload == null)
        {
            SyncDebugState();
            return;
        }

        victimLocationIntelMode = !string.IsNullOrWhiteSpace(payload.victimLocationIntelMode)
            ? payload.victimLocationIntelMode.Trim()
            : CallPhaseVictimLocationIntelMode.None.ToString();
        shouldRevealVictimIconsAtStart = payload.shouldRevealVictimIconsAtStart;
        configuredVisibleVictimIconCount = Mathf.Max(0, payload.visibleVictimIconCount);
        configuredEstimatedVictimIconRevealDistance = Mathf.Max(0f, payload.estimatedVictimIconRevealDistance);
        SyncDebugState();
    }

    private void SyncDebugState()
    {
        debugVictimLocationIntelMode = victimLocationIntelMode;
        debugShouldRevealVictimIconsAtStart = shouldRevealVictimIconsAtStart;
        debugConfiguredVisibleVictimIconCount = configuredVisibleVictimIconCount;
        debugResolvedEstimatedVictimIconRevealDistance = configuredEstimatedVictimIconRevealDistance;
    }

    private IncidentWorldSetupPayload ResolvePayload()
    {
        IncidentMapSetupRoot setupRoot = ResolveMapSetupRoot();
        if (setupRoot != null && setupRoot.LastAppliedPayload != null)
        {
            return setupRoot.LastAppliedPayload;
        }

        return LoadingFlowState.TryGetPendingIncidentPayload(out IncidentWorldSetupPayload payload)
            ? payload
            : null;
    }

    private IncidentMapSetupRoot ResolveMapSetupRoot()
    {
        if (explicitMapSetupRoot != null)
        {
            return explicitMapSetupRoot;
        }

        explicitMapSetupRoot = GetComponent<IncidentMapSetupRoot>();
        if (explicitMapSetupRoot == null)
        {
            explicitMapSetupRoot = FindAnyObjectByType<IncidentMapSetupRoot>(FindObjectsInactive.Include);
        }

        return explicitMapSetupRoot;
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

    private Transform ResolveDistanceReference()
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

    private static bool IsVictimEligibleForIcon(
        VictimCondition victim,
        IReadOnlyDictionary<VictimCondition, VictimRuntimeCache> runtimeCache)
    {
        if (victim == null || !victim.isActiveAndEnabled || !victim.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!victim.IsAlive || victim.IsExtracted)
        {
            return false;
        }

        Rescuable rescuable = null;
        if (runtimeCache != null && runtimeCache.TryGetValue(victim, out VictimRuntimeCache cached))
        {
            rescuable = cached.Rescuable;
        }

        if (rescuable == null)
        {
            rescuable = victim.GetComponent<Rescuable>();
        }

        return rescuable == null || !rescuable.IsRescued;
    }

    private bool HasDirectLineOfSight(Transform observer, VictimCondition victim)
    {
        if (observer == null || victim == null)
        {
            return false;
        }

        Vector3 origin = observer.position;
        Vector3 target = victim.transform.position + victimLineOfSightProbeOffset;
        Vector3 direction = target - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
        {
            return true;
        }

        direction /= distance;
        Collider victimCollider = GetVictimCollider(victim);
        int hitCount = SphereCastNonAlloc(origin, direction, distance);
        if (hitCount <= 0)
        {
            return true;
        }

        float nearestRelevantDistance = float.PositiveInfinity;
        Collider nearestRelevantCollider = null;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = lineOfSightHits[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            if (ShouldIgnoreLineOfSightHit(hitCollider, observer, victim))
            {
                continue;
            }

            if (lineOfSightHits[i].distance >= nearestRelevantDistance)
            {
                continue;
            }

            nearestRelevantDistance = lineOfSightHits[i].distance;
            nearestRelevantCollider = hitCollider;
        }

        if (nearestRelevantCollider == null)
        {
            return true;
        }

        Transform hitTransform = nearestRelevantCollider.transform;
        return (victimCollider != null && nearestRelevantCollider == victimCollider) ||
            hitTransform == victim.transform ||
            hitTransform.IsChildOf(victim.transform);
    }

    private static bool ShouldIgnoreLineOfSightHit(Collider hitCollider, Transform observer, VictimCondition victim)
    {
        if (hitCollider == null)
        {
            return true;
        }

        Transform hitTransform = hitCollider.transform;
        if (hitTransform == null)
        {
            return true;
        }

        if (observer != null && (hitTransform == observer || hitTransform.IsChildOf(observer)))
        {
            return true;
        }

        if (victim != null)
        {
            Transform victimTransform = victim.transform;
            if (hitTransform == victimTransform || hitTransform.IsChildOf(victimTransform))
            {
                return false;
            }
        }

        if (hitCollider.GetComponentInParent<VictimCondition>() != null)
        {
            return true;
        }

        if (hitCollider.GetComponentInParent<Rescuable>() != null)
        {
            return true;
        }

        return false;
    }

    private static float ResolveDistanceFade(float distance, float fadeStart, float visibleDistance)
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

    private void SortVisibleCandidates(bool isEstimatedIntel)
    {
        if (visibleCandidates.Count <= 1)
        {
            return;
        }

        if (isEstimatedIntel)
        {
            visibleCandidates.Sort(CompareEstimatedCandidatesByDistance);
            return;
        }

        visibleCandidates.Sort(CompareCandidatesStable);
    }

    private static int CompareEstimatedCandidatesByDistance(IconCandidate left, IconCandidate right)
    {
        int distanceCompare = left.DistanceSqr.CompareTo(right.DistanceSqr);
        if (distanceCompare != 0)
        {
            return distanceCompare;
        }

        return CompareCandidatesStable(left, right);
    }

    private static int CompareCandidatesStable(IconCandidate left, IconCandidate right)
    {
        if (ReferenceEquals(left.Victim, right.Victim))
        {
            return 0;
        }

        if (left.Victim == null)
        {
            return 1;
        }

        if (right.Victim == null)
        {
            return -1;
        }

        int xCompare = left.Victim.transform.position.x.CompareTo(right.Victim.transform.position.x);
        if (xCompare != 0)
        {
            return xCompare;
        }

        int zCompare = left.Victim.transform.position.z.CompareTo(right.Victim.transform.position.z);
        if (zCompare != 0)
        {
            return zCompare;
        }

        return left.Victim.transform.position.y.CompareTo(right.Victim.transform.position.y);
    }

    private float ResolveVisibleDistance(bool isEstimatedIntel)
    {
        if (!isEstimatedIntel)
        {
            return Mathf.Max(0f, victimIconVisibleDistance);
        }

        float configuredDistance = configuredEstimatedVictimIconRevealDistance > 0f
            ? configuredEstimatedVictimIconRevealDistance
            : estimatedVictimIconRevealDistance;
        return Mathf.Max(0f, configuredDistance);
    }

    private float ResolveFadeStartDistance(float visibleDistance, bool isEstimatedIntel)
    {
        if (visibleDistance <= 0f)
        {
            return 0f;
        }

        if (!isEstimatedIntel)
        {
            return Mathf.Min(Mathf.Max(0f, victimIconFadeStartDistance), visibleDistance);
        }

        float proportionalFadeStart = visibleDistance * 0.6f;
        return Mathf.Min(visibleDistance, Mathf.Max(0f, proportionalFadeStart));
    }

    private static bool IsEstimatedIntelMode(string intelMode)
    {
        return string.Equals(intelMode, CallPhaseVictimLocationIntelMode.Estimated.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private Color ResolveIconColor(string intelMode)
    {
        if (string.Equals(intelMode, CallPhaseVictimLocationIntelMode.Confirmed.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return confirmedVictimIconColor;
        }

        return estimatedVictimIconColor;
    }

    private void RefreshVictimCache(bool force)
    {
        float now = Time.unscaledTime;
        if (!force && now < nextVictimCacheRefreshTime)
        {
            return;
        }

        nextVictimCacheRefreshTime = now + Mathf.Max(0.05f, victimCacheRefreshInterval);
        cachedVictims.Clear();

        VictimCondition[] victims = FindObjectsByType<VictimCondition>(FindObjectsInactive.Exclude);
        for (int i = 0; i < victims.Length; i++)
        {
            VictimCondition victim = victims[i];
            if (victim == null)
            {
                continue;
            }

            cachedVictims.Add(victim);
            victimRuntimeCache[victim] = new VictimRuntimeCache
            {
                Rescuable = victim.GetComponent<Rescuable>(),
                Collider = victim.GetComponentInChildren<Collider>()
            };
        }

        releaseScratch.Clear();
        foreach (KeyValuePair<VictimCondition, VictimRuntimeCache> pair in victimRuntimeCache)
        {
            if (pair.Key == null || !cachedVictims.Contains(pair.Key))
            {
                releaseScratch.Add(pair.Key);
            }
        }

        for (int i = 0; i < releaseScratch.Count; i++)
        {
            victimRuntimeCache.Remove(releaseScratch[i]);
        }
    }

    private Collider GetVictimCollider(VictimCondition victim)
    {
        if (victim == null)
        {
            return null;
        }

        if (victimRuntimeCache.TryGetValue(victim, out VictimRuntimeCache cache))
        {
            if (cache.Collider == null)
            {
                cache.Collider = victim.GetComponentInChildren<Collider>();
                victimRuntimeCache[victim] = cache;
            }

            return cache.Collider;
        }

        return victim.GetComponentInChildren<Collider>();
    }

    private int SphereCastNonAlloc(Vector3 origin, Vector3 direction, float distance)
    {
        float radius = Mathf.Max(0.01f, victimLineOfSightProbeRadius);
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            radius,
            direction,
            lineOfSightHits,
            distance,
            victimLineOfSightBlockerMask,
            QueryTriggerInteraction.Ignore);
        if (hitCount < lineOfSightHits.Length)
        {
            return hitCount;
        }

        lineOfSightHits = new RaycastHit[lineOfSightHits.Length * 2];
        return Physics.SphereCastNonAlloc(
            origin,
            radius,
            direction,
            lineOfSightHits,
            distance,
            victimLineOfSightBlockerMask,
            QueryTriggerInteraction.Ignore);
    }

    private VictimIconView AcquireIconFromPool()
    {
        while (pooledIcons.Count > 0)
        {
            VictimIconView pooled = pooledIcons.Pop();
            if (pooled != null)
            {
                return pooled;
            }
        }

        GameObject iconObject = new GameObject("VictimIcon");
        iconObject.layer = gameObject.layer;
        iconObject.transform.SetParent(EnsureIconCanvas().transform, false);

        RectTransform rectTransform = iconObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.15f);
        rectTransform.anchoredPosition3D = Vector3.zero;

        iconObject.AddComponent<CanvasRenderer>();
        return iconObject.AddComponent<VictimIconView>();
    }

    private void ReleaseIconToPool(VictimIconView iconView)
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
        foreach (KeyValuePair<VictimCondition, VictimIconView> pair in activeIconsByVictim)
        {
            ReleaseIconToPool(pair.Value);
        }

        activeIconsByVictim.Clear();
    }

    private Canvas EnsureIconCanvas()
    {
        if (runtimeIconCanvas != null)
        {
            return runtimeIconCanvas;
        }

        GameObject canvasObject = new GameObject("RuntimeVictimIcons");
        canvasObject.layer = gameObject.layer;
        canvasObject.transform.SetParent(transform, false);

        runtimeIconCanvas = canvasObject.AddComponent<Canvas>();
        runtimeIconCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        runtimeIconCanvas.sortingOrder = 520;

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
