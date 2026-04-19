using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class IncidentPayloadAnchor : MonoBehaviour
{
    [Header("Matching")]
    [SerializeField] private string fireOriginKey = "Unknown";
    [SerializeField] private string logicalLocationKey = string.Empty;
    [SerializeField] private bool isDefaultAnchor;

    [Header("Runtime Spawn")]
    [SerializeField] private Transform runtimeParent;
    [SerializeField] private Vector3 runtimeZoneSize = new Vector3(4f, 2.5f, 4f);
    [SerializeField] private bool createRuntimeSmokeHazard = true;
    [SerializeField] private SmokeHazard smokeHazard;
    [SerializeField] private HazardIsolationDevice[] hazardIsolationDevices = Array.Empty<HazardIsolationDevice>();

    [Header("Secondary Fire Placement")]
    [SerializeField] [Min(0)] private int secondaryFirePointCount = 3;
    [SerializeField] [Min(0.1f)] private float secondaryFireRange = 3f;
    [SerializeField] [Min(0f)] private float minimumSecondaryFireSpacing = 0.85f;
    [SerializeField] [Min(1)] private int placementAttemptsPerSecondaryFire = 8;
    [SerializeField] [Min(0.05f)] private float parabolaLaunchHeight = 0.35f;
    [SerializeField] [Min(0.1f)] private float parabolaApexHeight = 1.4f;
    [SerializeField] [Min(3)] private int parabolaSegments = 12;
    [SerializeField] [Min(0.01f)] private float parabolaCastRadius = 0.08f;
    [SerializeField] [Min(0f)] private float surfaceOffset = 0.03f;
    [SerializeField] private LayerMask firePlacementMask = ~0;
    [SerializeField] private QueryTriggerInteraction firePlacementTriggerInteraction = QueryTriggerInteraction.Ignore;

    private readonly List<Fire> runtimeFires = new List<Fire>();
    private Transform runtimeRoot;
    private FireGroup runtimeFireGroup;
    private SmokeHazard runtimeSmokeHazard;

    public string FireOriginKey => fireOriginKey;
    public string LogicalLocationKey => logicalLocationKey;
    public bool IsDefaultAnchor => isDefaultAnchor;
    public Transform RuntimeRoot => runtimeRoot;
    public FireGroup RuntimeFireGroup => runtimeFireGroup;
    public SmokeHazard RuntimeSmokeHazard => runtimeSmokeHazard;
    public IReadOnlyList<Fire> RuntimeFires => runtimeFires;
    public bool HasConfiguredHazardIsolationDevices => hazardIsolationDevices != null && hazardIsolationDevices.Length > 0;

    public bool MatchesFireOrigin(string key)
    {
        return MatchesKey(fireOriginKey, key);
    }

    public bool MatchesLogicalLocation(string key)
    {
        return MatchesKey(logicalLocationKey, key);
    }

    public void ApplyPayload(IncidentWorldSetupPayload payload, Fire defaultFirePrefab)
    {
        if (payload == null)
        {
            return;
        }

        if (defaultFirePrefab == null)
        {
            Debug.LogWarning($"{nameof(IncidentPayloadAnchor)} on '{name}' is missing a default fire prefab.", this);
            return;
        }

        ClearRuntimeObjects();

        runtimeRoot = CreateRuntimeRoot();
        SpawnRuntimeFires(payload, defaultFirePrefab, runtimeRoot);
        EnsureRuntimeFireGroup(runtimeRoot);
        ConfigureSmoke(payload, runtimeRoot);
        ConfigureHazardIsolationDevices(payload);
    }

    private Transform CreateRuntimeRoot()
    {
        Transform parent = runtimeParent != null ? runtimeParent : transform;
        GameObject runtimeObject = new GameObject("RuntimeIncident");
        runtimeObject.transform.SetParent(parent, false);
        runtimeObject.transform.localPosition = Vector3.zero;
        runtimeObject.transform.localRotation = Quaternion.identity;
        runtimeObject.transform.localScale = Vector3.one;
        return runtimeObject.transform;
    }

    private void SpawnRuntimeFires(IncidentWorldSetupPayload payload, Fire defaultFirePrefab, Transform parent)
    {
        runtimeFires.Clear();

        List<SpawnPlacement> placements = BuildRuntimeFirePlacements();
        for (int i = 0; i < placements.Count; i++)
        {
            SpawnPlacement placement = placements[i];
            Fire fireInstance = Instantiate(defaultFirePrefab, placement.Position, placement.Rotation, parent);
            fireInstance.name = $"{defaultFirePrefab.name}_{i + 1}";
            ConfigureFireInstance(fireInstance, payload);
            runtimeFires.Add(fireInstance);
        }
    }

    private List<SpawnPlacement> BuildRuntimeFirePlacements()
    {
        List<SpawnPlacement> placements = new List<SpawnPlacement>();
        Vector3 anchorPosition = transform.position;
        placements.Add(new SpawnPlacement(anchorPosition, ResolveFallbackRotation()));

        int requestedSecondaryCount = Mathf.Max(0, secondaryFirePointCount);
        if (requestedSecondaryCount <= 0)
        {
            return placements;
        }

        System.Random random = new System.Random(Guid.NewGuid().GetHashCode());
        int maxAttempts = Mathf.Max(requestedSecondaryCount, requestedSecondaryCount * Mathf.Max(1, placementAttemptsPerSecondaryFire));
        int attempts = 0;
        while (placements.Count - 1 < requestedSecondaryCount && attempts < maxAttempts)
        {
            attempts++;
            if (!TryFindSecondaryPlacement(random, placements, out SpawnPlacement placement))
            {
                continue;
            }

            placements.Add(placement);
        }

        if (placements.Count - 1 < requestedSecondaryCount)
        {
            Debug.LogWarning(
                $"{nameof(IncidentPayloadAnchor)} on '{name}' only found {placements.Count - 1}/{requestedSecondaryCount} secondary fire placements " +
                $"within range {secondaryFireRange:0.##}.",
                this);
        }

        return placements;
    }

    private void ConfigureFireInstance(Fire fireInstance, IncidentWorldSetupPayload payload)
    {
        if (fireInstance == null)
        {
            return;
        }

        fireInstance.SetFireHazardType(IncidentPayloadStartupTask.ResolveFireHazardType(payload.hazardType));
        fireInstance.SetRequiresIsolationToFullyExtinguish(payload.requiresIsolation);
        fireInstance.SetHazardSourceIsolated(false);
        fireInstance.SetSpreadEnabled(true);
        fireInstance.ConfigureSpreadProfile(
            IncidentPayloadStartupTask.ResolveSpreadInterval(payload.fireSpreadPreset),
            IncidentPayloadStartupTask.ResolveSpreadIgniteAmount(payload.fireSpreadPreset),
            IncidentPayloadStartupTask.ResolveSpreadThreshold(payload.fireSpreadPreset));
        fireInstance.SetBurningLevel01(payload.initialFireIntensity);
    }

    private void EnsureRuntimeFireGroup(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        BoxCollider boxCollider = parent.GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = parent.gameObject.AddComponent<BoxCollider>();
        }

        boxCollider.isTrigger = true;
        boxCollider.center = Vector3.zero;
        boxCollider.size = Vector3.Max(runtimeZoneSize, new Vector3(1f, 1f, 1f));

        runtimeFireGroup = parent.GetComponent<FireGroup>();
        if (runtimeFireGroup == null)
        {
            runtimeFireGroup = parent.gameObject.AddComponent<FireGroup>();
        }

        runtimeFireGroup.CollectFires();
    }

    private void ConfigureSmoke(IncidentWorldSetupPayload payload, Transform parent)
    {
        SmokeHazard targetSmokeHazard = smokeHazard;
        if (targetSmokeHazard == null && createRuntimeSmokeHazard && parent != null)
        {
            Transform existingSmokeObj = parent.Find("RuntimeSmoke");
            if (existingSmokeObj != null)
            {
                targetSmokeHazard = existingSmokeObj.GetComponent<SmokeHazard>();
            }
            else
            {
                GameObject smokeObj = new GameObject("RuntimeSmoke");
                smokeObj.transform.SetParent(parent, false);
                smokeObj.transform.localPosition = Vector3.zero;
                smokeObj.transform.localRotation = Quaternion.identity;
                smokeObj.transform.localScale = Vector3.one;
                targetSmokeHazard = smokeObj.AddComponent<SmokeHazard>();
            }

            BoxCollider trigger = parent.GetComponent<BoxCollider>();
            if (trigger != null)
            {
                targetSmokeHazard.SetTriggerZone(trigger);
            }
        }

        runtimeSmokeHazard = targetSmokeHazard;
        if (runtimeSmokeHazard == null)
        {
            return;
        }

        runtimeSmokeHazard.SetLinkedFires(runtimeFires.ToArray());
        runtimeSmokeHazard.SetStartSmokeDensity(payload.startSmokeDensity, applyImmediately: true);
        runtimeSmokeHazard.SetSmokeAccumulationMultiplier(payload.smokeAccumulationMultiplier);
    }

    private void ConfigureHazardIsolationDevices(IncidentWorldSetupPayload payload)
    {
        if (hazardIsolationDevices == null || hazardIsolationDevices.Length == 0)
        {
            return;
        }

        Fire[] linkedFires = runtimeFires.ToArray();
        FireHazardType fireHazardType = IncidentPayloadStartupTask.ResolveFireHazardType(payload.hazardType);
        for (int i = 0; i < hazardIsolationDevices.Length; i++)
        {
            HazardIsolationDevice device = hazardIsolationDevices[i];
            if (device == null)
            {
                continue;
            }

            device.SetLinkedFires(linkedFires);
            device.SetRuntimeHazardType(fireHazardType);
            device.SetRuntimeIsolationState(false, invokeEvents: false);
        }
    }

    private bool TryFindSecondaryPlacement(
        System.Random random,
        List<SpawnPlacement> existingPlacements,
        out SpawnPlacement placement)
    {
        placement = default;

        if (random == null || existingPlacements == null)
        {
            return false;
        }

        float horizontalDistance = Mathf.Lerp(
            Mathf.Min(minimumSecondaryFireSpacing, secondaryFireRange),
            Mathf.Max(minimumSecondaryFireSpacing, secondaryFireRange),
            (float)random.NextDouble());
        Vector3 launchPosition = transform.position + Vector3.up * Mathf.Max(0f, parabolaLaunchHeight);
        Vector3 travelDirection = GetRandomHorizontalDirection(random);
        Vector3 landingPosition = transform.position + travelDirection * horizontalDistance;
        Vector3 controlPoint = Vector3.Lerp(launchPosition, landingPosition, 0.5f) + Vector3.up * Mathf.Max(0.05f, parabolaApexHeight);

        Vector3 previousPoint = launchPosition;
        int segmentCount = Mathf.Max(3, parabolaSegments);
        for (int segmentIndex = 1; segmentIndex <= segmentCount; segmentIndex++)
        {
            float t = segmentIndex / (float)segmentCount;
            Vector3 nextPoint = EvaluateQuadraticBezier(launchPosition, controlPoint, landingPosition, t);
            Vector3 segment = nextPoint - previousPoint;
            float segmentLength = segment.magnitude;
            if (segmentLength <= 0.0001f)
            {
                previousPoint = nextPoint;
                continue;
            }

            if (Physics.SphereCast(
                    previousPoint,
                    Mathf.Max(0.01f, parabolaCastRadius),
                    segment / segmentLength,
                    out RaycastHit hit,
                    segmentLength,
                    firePlacementMask,
                    firePlacementTriggerInteraction) &&
                IsValidSecondaryHit(hit, existingPlacements))
            {
                Vector3 position = hit.point + hit.normal * Mathf.Max(0f, surfaceOffset);
                placement = new SpawnPlacement(position, ResolveSurfaceRotation(hit.normal));
                return true;
            }

            previousPoint = nextPoint;
        }

        return false;
    }

    private bool IsValidSecondaryHit(RaycastHit hit, List<SpawnPlacement> existingPlacements)
    {
        if (hit.collider == null)
        {
            return false;
        }

        Vector3 candidatePosition = hit.point + hit.normal * Mathf.Max(0f, surfaceOffset);
        float minimumSpacing = Mathf.Max(0f, minimumSecondaryFireSpacing);
        for (int i = 0; i < existingPlacements.Count; i++)
        {
            if ((existingPlacements[i].Position - candidatePosition).sqrMagnitude < minimumSpacing * minimumSpacing)
            {
                return false;
            }
        }

        return true;
    }

    private Quaternion ResolveFallbackRotation()
    {
        Vector3 forward = transform.forward.sqrMagnitude > 0f ? transform.forward : Vector3.forward;
        return Quaternion.LookRotation(forward, Vector3.up);
    }

    private Quaternion ResolveSurfaceRotation(Vector3 surfaceNormal)
    {
        Vector3 up = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, up);
        if (projectedForward.sqrMagnitude < 0.0001f)
        {
            projectedForward = Vector3.ProjectOnPlane(transform.right, up);
        }

        if (projectedForward.sqrMagnitude < 0.0001f)
        {
            projectedForward = Vector3.Cross(up, Vector3.right);
        }

        if (projectedForward.sqrMagnitude < 0.0001f)
        {
            projectedForward = Vector3.forward;
        }

        return Quaternion.LookRotation(projectedForward.normalized, up);
    }

    private static Vector3 EvaluateQuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
    {
        float clampedT = Mathf.Clamp01(t);
        float invT = 1f - clampedT;
        return (invT * invT * start) + (2f * invT * clampedT * control) + (clampedT * clampedT * end);
    }

    private static Vector3 GetRandomHorizontalDirection(System.Random random)
    {
        float angle = (float)(random.NextDouble() * Math.PI * 2d);
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
    }

    private void ClearRuntimeObjects()
    {
        if (runtimeRoot != null)
        {
            Destroy(runtimeRoot.gameObject);
        }

        runtimeRoot = null;
        runtimeFireGroup = null;
        runtimeSmokeHazard = null;
        runtimeFires.Clear();
    }

    private static bool MatchesKey(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private readonly struct SpawnPlacement
    {
        public SpawnPlacement(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
    }
}
