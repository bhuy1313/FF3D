using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-300)]
[DisallowMultipleComponent]
public class DebugIncidentPayloadSpawner : MonoBehaviour
{
    private enum DebugIncidentPreset
    {
        Map3EquipmentRoom = 0,
        Map3Hallway = 1,
        Map3StorageRoom = 2,
        Custom = 3
    }

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugSpawning = true;
    [SerializeField] private DebugIncidentPreset preset = DebugIncidentPreset.Map3EquipmentRoom;

    [Header("Core Payload")]
    [SerializeField] private string caseId = "CT-EQ-01";
    [SerializeField] private string scenarioId = "equipment_room_fire";
    [SerializeField] private string fireOrigin = "EquipmentRoom_Panel";
    [SerializeField] private string logicalFireLocation = "Equipment Room";
    [SerializeField] private string hazardType = "Electrical";
    [SerializeField] private string isolationType = "Electrical";
    [SerializeField] private bool requiresIsolation = true;
    [SerializeField] private string severityBand = "Medium";

    [Header("Scene Setup")]
    [SerializeField, Range(0.1f, 1f)] private float initialFireIntensity = 0.65f;
    [SerializeField, Min(1)] private int initialFireCount = 3;
    [SerializeField] private string fireSpreadPreset = "Moderate";
    [SerializeField, Min(0f)] private float startSmokeDensity = 0.25f;
    [SerializeField, Min(0f)] private float smokeAccumulationMultiplier = 1f;
    [SerializeField] private string ventilationPreset = "Neutral";
    [SerializeField] private string occupantRiskPreset = "Manageable";

    [Header("Victim Estimate")]
    [SerializeField] private bool estimatedTrappedCountKnown;
    [SerializeField, Min(0)] private int estimatedTrappedCountMin;
    [SerializeField, Min(0)] private int estimatedTrappedCountMax;

    [Header("Report Snapshot")]
    [SerializeField] private string reportAddress = "Westbridge Research Center, 100 Main Street";
    [SerializeField] private string reportFireLocation = "Equipment Room";
    [SerializeField] private string reportOccupantRisk = "Unknown";
    [SerializeField] private string reportHazard = "Electrical panel";
    [SerializeField] private string reportSpreadStatus = "Contained";
    [SerializeField] private string reportCallerSafety = "Caller outside";

    private IncidentWorldSetupPayload pendingDebugPayload;
    private bool shouldDirectApplyPayload;

    private void Awake()
    {
        if (!enableDebugSpawning)
        {
            return;
        }

        ApplyPresetIfNeeded();

        if (LoadingFlowState.TryGetPendingIncidentPayload(out _))
        {
            string activeSceneName = SceneManager.GetActiveScene().name;
            if (LoadingFlowState.WasSceneActivatedFromLoadingFlow(activeSceneName))
            {
                Debug.Log("[DebugIncidentPayloadSpawner] Found pending payload from normal flow. Debug injection skipped.");
                return;
            }

            LoadingFlowState.ClearPendingIncidentPayload();
            Debug.Log($"[DebugIncidentPayloadSpawner] Cleared stale pending payload before direct scene debug play in '{activeSceneName}'.");
        }

        pendingDebugPayload = new IncidentWorldSetupPayload
        {
            caseId = ResolveText(caseId, "debug_case"),
            scenarioId = ResolveText(scenarioId, "debug_scenario"),
            fireOrigin = ResolveText(fireOrigin, "Debug_FireOrigin"),
            logicalFireLocation = ResolveText(logicalFireLocation, "Debug Location"),
            hazardType = ResolveText(hazardType, "OrdinaryCombustibles"),
            isolationType = ResolveText(isolationType, "None"),
            requiresIsolation = requiresIsolation,
            initialFireIntensity = Mathf.Clamp(initialFireIntensity, 0.1f, 1f),
            initialFireCount = Mathf.Max(1, initialFireCount),
            fireSpreadPreset = ResolveText(fireSpreadPreset, "Moderate"),
            startSmokeDensity = Mathf.Max(0f, startSmokeDensity),
            smokeAccumulationMultiplier = Mathf.Max(0f, smokeAccumulationMultiplier),
            ventilationPreset = ResolveText(ventilationPreset, "Neutral"),
            occupantRiskPreset = ResolveText(occupantRiskPreset, "Manageable"),
            severityBand = ResolveText(severityBand, "Medium"),
            estimatedTrappedCountKnown = estimatedTrappedCountKnown,
            estimatedTrappedCountMin = estimatedTrappedCountKnown ? Mathf.Max(0, estimatedTrappedCountMin) : 0,
            estimatedTrappedCountMax = estimatedTrappedCountKnown ? Mathf.Max(estimatedTrappedCountMin, estimatedTrappedCountMax) : 0,
            confidenceScore = 1f,
            reportSnapshot = BuildReportSnapshot(),
            appliedSignals = BuildAppliedSignals()
        };

        LoadingFlowState.SetPendingIncidentPayload(pendingDebugPayload);
        shouldDirectApplyPayload =
            FindAnyObjectByType<SceneStartupFlow>(FindObjectsInactive.Include) == null &&
            FindAnyObjectByType<IncidentPayloadStartupTask>(FindObjectsInactive.Include) == null;

        Debug.Log(
            $"[DebugIncidentPayloadSpawner] Injected preset '{preset}' for scenario '{pendingDebugPayload.scenarioId}'. " +
            $"DirectApply={shouldDirectApplyPayload}.",
            this);
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
            Debug.LogWarning("[DebugIncidentPayloadSpawner] No IncidentMapSetupRoot found for direct payload apply.", this);
            yield break;
        }

        Debug.Log("[DebugIncidentPayloadSpawner] Applying debug payload directly through IncidentMapSetupRoot.", this);
        yield return setupRoot.ApplyPayload(null, payload, firePrefabLibrary: null);

        if (setupRoot.LastResolvedAnchor != null)
        {
            Debug.Log("[DebugIncidentPayloadSpawner] Debug payload applied successfully and kept available for runtime UI consumers.", this);
        }
        else
        {
            Debug.LogWarning("[DebugIncidentPayloadSpawner] Debug payload apply did not resolve any anchor.", this);
        }
    }

    private void OnValidate()
    {
        initialFireIntensity = Mathf.Clamp(initialFireIntensity, 0.1f, 1f);
        initialFireCount = Mathf.Max(1, initialFireCount);
        estimatedTrappedCountMin = Mathf.Max(0, estimatedTrappedCountMin);
        estimatedTrappedCountMax = Mathf.Max(estimatedTrappedCountMin, estimatedTrappedCountMax);
        startSmokeDensity = Mathf.Max(0f, startSmokeDensity);
        smokeAccumulationMultiplier = Mathf.Max(0f, smokeAccumulationMultiplier);

        ApplyPresetIfNeeded();
        NormalizeEmptyFields();
    }

    private void ApplyPresetIfNeeded()
    {
        if (preset == DebugIncidentPreset.Custom)
        {
            return;
        }

        switch (preset)
        {
            case DebugIncidentPreset.Map3EquipmentRoom:
                caseId = "CT-EQ-01";
                scenarioId = "equipment_room_fire";
                fireOrigin = "EquipmentRoom_Panel";
                logicalFireLocation = "Equipment Room";
                hazardType = "Electrical";
                isolationType = "Electrical";
                requiresIsolation = true;
                severityBand = "Medium";
                initialFireIntensity = 0.65f;
                initialFireCount = 3;
                fireSpreadPreset = "Moderate";
                startSmokeDensity = 0.2f;
                smokeAccumulationMultiplier = 1f;
                ventilationPreset = "Neutral";
                occupantRiskPreset = "Manageable";
                estimatedTrappedCountKnown = false;
                estimatedTrappedCountMin = 0;
                estimatedTrappedCountMax = 0;
                reportAddress = "Westbridge Research Center, 100 Main Street";
                reportFireLocation = "Equipment Room";
                reportOccupantRisk = "Unknown";
                reportHazard = "High-voltage electrical panel";
                reportSpreadStatus = "Contained";
                reportCallerSafety = "Caller outside";
                break;

            case DebugIncidentPreset.Map3Hallway:
                caseId = "CT-HW-01";
                scenarioId = "hallway_fire";
                fireOrigin = "Hallway_MainCorridor";
                logicalFireLocation = "Hallway";
                hazardType = "OrdinaryCombustibles";
                isolationType = "None";
                requiresIsolation = false;
                severityBand = "High";
                initialFireIntensity = 0.75f;
                initialFireCount = 4;
                fireSpreadPreset = "Fast";
                startSmokeDensity = 0.45f;
                smokeAccumulationMultiplier = 1.35f;
                ventilationPreset = "Neutral";
                occupantRiskPreset = "High";
                estimatedTrappedCountKnown = true;
                estimatedTrappedCountMin = 1;
                estimatedTrappedCountMax = 3;
                reportAddress = "Westbridge Research Center, 100 Main Street";
                reportFireLocation = "Hallway";
                reportOccupantRisk = "Researchers behind lab doors";
                reportHazard = "Heavy smoke in corridor";
                reportSpreadStatus = "Spreading";
                reportCallerSafety = "Caller outside";
                break;

            case DebugIncidentPreset.Map3StorageRoom:
                caseId = "CT-ST-01";
                scenarioId = "storage_room_fire";
                fireOrigin = "StorageRoom_BackWing";
                logicalFireLocation = "Storage Room";
                hazardType = "OrdinaryCombustibles";
                isolationType = "None";
                requiresIsolation = false;
                severityBand = "Medium";
                initialFireIntensity = 0.7f;
                initialFireCount = 4;
                fireSpreadPreset = "Fast";
                startSmokeDensity = 0.35f;
                smokeAccumulationMultiplier = 1.2f;
                ventilationPreset = "Neutral";
                occupantRiskPreset = "Manageable";
                estimatedTrappedCountKnown = false;
                estimatedTrappedCountMin = 0;
                estimatedTrappedCountMax = 0;
                reportAddress = "Westbridge Research Center, 100 Main Street";
                reportFireLocation = "Storage Room";
                reportOccupantRisk = "Unknown";
                reportHazard = "Combustible lab storage contents";
                reportSpreadStatus = "Spreading";
                reportCallerSafety = "Caller outside";
                break;
        }
    }

    private void NormalizeEmptyFields()
    {
        caseId = ResolveText(caseId, "debug_case");
        scenarioId = ResolveText(scenarioId, "debug_scenario");
        fireOrigin = ResolveText(fireOrigin, "Debug_FireOrigin");
        logicalFireLocation = ResolveText(logicalFireLocation, "Debug Location");
        hazardType = ResolveText(hazardType, "OrdinaryCombustibles");
        isolationType = ResolveText(isolationType, "None");
        severityBand = ResolveText(severityBand, "Medium");
        fireSpreadPreset = ResolveText(fireSpreadPreset, "Moderate");
        ventilationPreset = ResolveText(ventilationPreset, "Neutral");
        occupantRiskPreset = ResolveText(occupantRiskPreset, "Manageable");
        reportAddress = ResolveText(reportAddress, "Westbridge Research Center, 100 Main Street");
        reportFireLocation = ResolveText(reportFireLocation, logicalFireLocation);
        reportOccupantRisk = ResolveText(reportOccupantRisk, occupantRiskPreset);
        reportHazard = ResolveText(reportHazard, hazardType);
        reportSpreadStatus = ResolveText(reportSpreadStatus, fireSpreadPreset);
        reportCallerSafety = ResolveText(reportCallerSafety, "Unknown");
    }

    private IncidentWorldSetupReportSnapshot BuildReportSnapshot()
    {
        return new IncidentWorldSetupReportSnapshot
        {
            address = ResolveText(reportAddress, "Westbridge Research Center, 100 Main Street"),
            fireLocation = ResolveText(reportFireLocation, logicalFireLocation),
            occupantRisk = ResolveText(reportOccupantRisk, occupantRiskPreset),
            hazard = ResolveText(reportHazard, hazardType),
            spreadStatus = ResolveText(reportSpreadStatus, fireSpreadPreset),
            callerSafety = ResolveText(reportCallerSafety, "Unknown"),
            severity = ResolveText(severityBand, "Medium")
        };
    }

    private List<string> BuildAppliedSignals()
    {
        return new List<string>
        {
            $"Debug preset injected: {preset}",
            $"Scenario: {scenarioId}",
            $"Location: {logicalFireLocation}",
            $"Hazard: {hazardType}"
        };
    }

    private static string ResolveText(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
