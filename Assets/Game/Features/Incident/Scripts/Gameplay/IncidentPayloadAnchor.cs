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
    [SerializeField] private float spawnSpacing = 0.8f;
    [SerializeField] private Vector3 runtimeZoneSize = new Vector3(4f, 2.5f, 4f);
    [SerializeField] private bool createRuntimeSmokeHazard = true;
    [SerializeField] private SmokeHazard smokeHazard;
    [SerializeField] private HazardIsolationDevice[] hazardIsolationDevices = Array.Empty<HazardIsolationDevice>();
    [SerializeField] private IncidentFireSpawnSocket[] explicitSpawnSockets = Array.Empty<IncidentFireSpawnSocket>();
    [SerializeField] private bool includeInactiveSpawnSockets = true;

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
    public bool HasConfiguredSpawnSockets => ResolveSpawnSockets().Length > 0;

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

        int requestedCount = Mathf.Max(1, payload.initialFireCount);
        IncidentFireSpawnSocket[] spawnSockets = ResolveSpawnSockets();
        List<IncidentFireSpawnSocket> selectedSockets = SelectSpawnSockets(payload, spawnSockets, requestedCount);
        for (int i = 0; i < requestedCount; i++)
        {
            Vector3 worldPosition;
            Quaternion worldRotation;
            if (i < selectedSockets.Count && selectedSockets[i] != null)
            {
                IncidentFireSpawnSocket socket = selectedSockets[i];
                worldPosition = socket.WorldPosition;
                worldRotation = socket.WorldRotation != Quaternion.identity
                    ? socket.WorldRotation
                    : ResolveFallbackRotation();
            }
            else
            {
                Vector3 offset = CalculateSpawnOffset(i, requestedCount);
                worldPosition = transform.position + offset;
                worldRotation = ResolveFallbackRotation();
            }

            Fire fireInstance = Instantiate(defaultFirePrefab, worldPosition, worldRotation, parent);
            fireInstance.name = $"{defaultFirePrefab.name}_{i + 1}";
            ConfigureFireInstance(fireInstance, payload);
            runtimeFires.Add(fireInstance);
        }
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

    private Vector3 CalculateSpawnOffset(int index, int totalCount)
    {
        if (index <= 0 || totalCount <= 1)
        {
            return Vector3.zero;
        }

        Vector3 forward = transform.forward;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        Vector3 right = transform.right;
        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.right;
        }

        switch (index)
        {
            case 1:
                return forward * spawnSpacing;
            case 2:
                return -forward * spawnSpacing;
            case 3:
                return right * spawnSpacing;
            case 4:
                return -right * spawnSpacing;
            default:
                int ringIndex = index - 1;
                float angle = (ringIndex / Mathf.Max(1f, totalCount - 1f)) * Mathf.PI * 2f;
                Vector3 radial = (right * Mathf.Cos(angle)) + (forward * Mathf.Sin(angle));
                return radial.normalized * spawnSpacing;
        }
    }

    private List<IncidentFireSpawnSocket> SelectSpawnSockets(
        IncidentWorldSetupPayload payload,
        IncidentFireSpawnSocket[] spawnSockets,
        int requestedCount)
    {
        List<IncidentFireSpawnSocket> results = new List<IncidentFireSpawnSocket>();
        if (spawnSockets == null || spawnSockets.Length <= 0 || requestedCount <= 0)
        {
            return results;
        }

        System.Random random = new System.Random(BuildSelectionSeed(payload));
        List<IncidentFireSpawnSocket> remainingSockets = new List<IncidentFireSpawnSocket>(spawnSockets.Length);
        for (int i = 0; i < spawnSockets.Length; i++)
        {
            if (spawnSockets[i] != null)
            {
                remainingSockets.Add(spawnSockets[i]);
            }
        }

        IncidentFireSpawnSocket primarySocket = PickWeightedSocket(remainingSockets, random, requirePrimary: true);
        if (primarySocket == null)
        {
            primarySocket = PickWeightedSocket(remainingSockets, random, requirePrimary: false);
        }

        if (primarySocket != null)
        {
            results.Add(primarySocket);
            remainingSockets.Remove(primarySocket);
        }

        while (results.Count < requestedCount && remainingSockets.Count > 0)
        {
            IncidentFireSpawnSocket secondarySocket = PickWeightedSocket(remainingSockets, random, requirePrimary: false, requireSecondary: true);
            if (secondarySocket == null)
            {
                break;
            }

            results.Add(secondarySocket);
            remainingSockets.Remove(secondarySocket);
        }

        return results;
    }

    private IncidentFireSpawnSocket[] ResolveSpawnSockets()
    {
        if (explicitSpawnSockets != null && explicitSpawnSockets.Length > 0)
        {
            return explicitSpawnSockets;
        }

        return GetComponentsInChildren<IncidentFireSpawnSocket>(includeInactiveSpawnSockets);
    }

    private IncidentFireSpawnSocket PickWeightedSocket(
        List<IncidentFireSpawnSocket> candidates,
        System.Random random,
        bool requirePrimary,
        bool requireSecondary = false)
    {
        if (candidates == null || candidates.Count <= 0)
        {
            return null;
        }

        int totalWeight = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            IncidentFireSpawnSocket candidate = candidates[i];
            if (!IsSocketEligible(candidate, requirePrimary, requireSecondary))
            {
                continue;
            }

            totalWeight += candidate.SelectionWeight;
        }

        if (totalWeight <= 0)
        {
            return null;
        }

        int roll = random.Next(0, totalWeight);
        for (int i = 0; i < candidates.Count; i++)
        {
            IncidentFireSpawnSocket candidate = candidates[i];
            if (!IsSocketEligible(candidate, requirePrimary, requireSecondary))
            {
                continue;
            }

            roll -= candidate.SelectionWeight;
            if (roll < 0)
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsSocketEligible(IncidentFireSpawnSocket socket, bool requirePrimary, bool requireSecondary)
    {
        if (socket == null)
        {
            return false;
        }

        if (requirePrimary && !socket.CanSpawnPrimary)
        {
            return false;
        }

        if (requireSecondary && !socket.CanSpawnSecondary)
        {
            return false;
        }

        return true;
    }

    private int BuildSelectionSeed(IncidentWorldSetupPayload payload)
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + GetStableHash(caseSensitiveValue: payload != null ? payload.caseId : string.Empty);
            hash = (hash * 31) + GetStableHash(caseSensitiveValue: payload != null ? payload.scenarioId : string.Empty);
            hash = (hash * 31) + GetStableHash(caseSensitiveValue: payload != null ? payload.fireOrigin : string.Empty);
            hash = (hash * 31) + GetStableHash(caseSensitiveValue: payload != null ? payload.logicalFireLocation : string.Empty);
            return hash;
        }
    }

    private Quaternion ResolveFallbackRotation()
    {
        Vector3 forward = transform.forward.sqrMagnitude > 0f ? transform.forward : Vector3.forward;
        return Quaternion.LookRotation(forward, Vector3.up);
    }

    private static int GetStableHash(string caseSensitiveValue)
    {
        if (string.IsNullOrEmpty(caseSensitiveValue))
        {
            return 0;
        }

        unchecked
        {
            int hash = 23;
            for (int i = 0; i < caseSensitiveValue.Length; i++)
            {
                hash = (hash * 31) + caseSensitiveValue[i];
            }

            return hash;
        }
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
}
