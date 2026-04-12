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

    private readonly List<Fire> runtimeFires = new List<Fire>();
    private Transform runtimeRoot;
    private FireGroup runtimeFireGroup;
    private SmokeHazard runtimeSmokeHazard;

    public string FireOriginKey => fireOriginKey;
    public string LogicalLocationKey => logicalLocationKey;
    public bool IsDefaultAnchor => isDefaultAnchor;

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
        for (int i = 0; i < requestedCount; i++)
        {
            Vector3 offset = CalculateSpawnOffset(i, requestedCount);
            Vector3 worldPosition = transform.position + offset;
            Quaternion worldRotation = Quaternion.LookRotation(transform.forward.sqrMagnitude > 0f ? transform.forward : Vector3.forward, Vector3.up);
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
            targetSmokeHazard = parent.GetComponent<SmokeHazard>();
            if (targetSmokeHazard == null)
            {
                targetSmokeHazard = parent.gameObject.AddComponent<SmokeHazard>();
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
