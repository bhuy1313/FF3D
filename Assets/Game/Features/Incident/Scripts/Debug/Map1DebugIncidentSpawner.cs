using System;
using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-300)]
[DisallowMultipleComponent]
public class Map1DebugIncidentSpawner : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugSpawning = true;
    [SerializeField] private string debugFireOrigin = "Laundry_WasherOutlet";
    [SerializeField] private string debugLogicalFireLocation = "Laundry";
    [SerializeField] private string debugHazardType = "Electrical";
    [SerializeField] [Range(0.1f, 1f)] private float debugInitialFireIntensity = 0.65f;
    [SerializeField] [Range(1, 5)] private int debugInitialFireCount = 3;
    [SerializeField] private string debugSeverityBand = "Medium";

    private IncidentWorldSetupPayload pendingDebugPayload;
    private bool shouldDirectApplyPayload;

    private void Awake()
    {
        if (!enableDebugSpawning)
        {
            return;
        }

        if (LoadingFlowState.TryGetPendingIncidentPayload(out _))
        {
            Debug.Log("[Map1DebugIncidentSpawner] Found pending payload from normal flow. Debug injection skipped.");
            return;
        }

        pendingDebugPayload = new IncidentWorldSetupPayload
        {
            caseId = "debug_case",
            scenarioId = "debug_scenario",
            fireOrigin = debugFireOrigin,
            logicalFireLocation = debugLogicalFireLocation,
            hazardType = debugHazardType,
            isolationType = ResolveIsolationType(debugHazardType),
            requiresIsolation = true,
            initialFireIntensity = debugInitialFireIntensity,
            initialFireCount = debugInitialFireCount,
            fireSpreadPreset = "Moderate",
            startSmokeDensity = 0.2f,
            smokeAccumulationMultiplier = 1f,
            ventilationPreset = "Neutral",
            occupantRiskPreset = "Manageable",
            severityBand = debugSeverityBand,
            confidenceScore = 1f,
        };

        LoadingFlowState.SetPendingIncidentPayload(pendingDebugPayload);
        shouldDirectApplyPayload =
            FindAnyObjectByType<SceneStartupFlow>(FindObjectsInactive.Include) == null &&
            FindAnyObjectByType<IncidentPayloadStartupTask>(FindObjectsInactive.Include) == null;

        Debug.Log(
            $"[Map1DebugIncidentSpawner] Injected debug payload for origin '{debugFireOrigin}'. " +
            $"DirectApply={shouldDirectApplyPayload}.");
    }

    private void Start()
    {
        if (!enableDebugSpawning || !shouldDirectApplyPayload || pendingDebugPayload == null)
        {
            return;
        }

        StartCoroutine(ApplyPayloadDirectly());
    }

    private IEnumerator ApplyPayloadDirectly()
    {
        yield return null;

        if (!LoadingFlowState.TryGetPendingIncidentPayload(out IncidentWorldSetupPayload payload) || payload == null)
        {
            yield break;
        }

        IncidentMapSetupRoot setupRoot = FindAnyObjectByType<IncidentMapSetupRoot>(FindObjectsInactive.Include);
        if (setupRoot == null)
        {
            Debug.LogWarning("[Map1DebugIncidentSpawner] No IncidentMapSetupRoot found for direct payload apply.", this);
            yield break;
        }

        IncidentFirePrefabLibrary firePrefabLibrary =
            FindAnyObjectByType<IncidentFirePrefabLibrary>(FindObjectsInactive.Include);

        Debug.Log("[Map1DebugIncidentSpawner] Applying debug payload directly through IncidentMapSetupRoot.", this);
        yield return setupRoot.ApplyPayload(null, payload, firePrefabLibrary);

        if (setupRoot.LastResolvedAnchor != null)
        {
            LoadingFlowState.ClearPendingIncidentPayload();
        }
        else
        {
            Debug.LogWarning("[Map1DebugIncidentSpawner] Debug payload apply did not resolve any anchor.", this);
        }
    }

    private static string ResolveIsolationType(string hazard)
    {
        if (string.Equals(hazard, "Electrical", StringComparison.OrdinalIgnoreCase))
        {
            return "Electrical";
        }

        if (string.Equals(hazard, "Gas", StringComparison.OrdinalIgnoreCase))
        {
            return "Gas";
        }

        return "None";
    }
}
