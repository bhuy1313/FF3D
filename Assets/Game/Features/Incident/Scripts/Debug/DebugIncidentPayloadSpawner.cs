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
        Map1Kitchen = 0,
        Map1Laundry = 1,
        Map3EquipmentRoom = 2,
        Map3Hallway = 3,
        Map3StorageRoom = 4,
        Map3HallwayEstimatedVictimIntel = 5,
        Map3HallwayConfirmedVictimIntel = 6,
        Map3StorageRoomConfirmedVictimIntel = 7,
        Custom = 8
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
    [SerializeField] private CallPhaseVictimLocationIntelMode victimLocationIntelMode =
        CallPhaseVictimLocationIntelMode.None;
    [SerializeField, Min(0)] private int visibleVictimIconCount;
    [SerializeField, Min(0f)] private float estimatedVictimIconRevealDistance = 10f;

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
            victimLocationIntelMode = victimLocationIntelMode.ToString(),
            shouldRevealVictimIconsAtStart = victimLocationIntelMode != CallPhaseVictimLocationIntelMode.None,
            visibleVictimIconCount = Mathf.Max(0, visibleVictimIconCount),
            estimatedVictimIconRevealDistance = Mathf.Max(0f, estimatedVictimIconRevealDistance),
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
        visibleVictimIconCount = Mathf.Max(0, visibleVictimIconCount);
        estimatedVictimIconRevealDistance = Mathf.Max(0f, estimatedVictimIconRevealDistance);
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
            case DebugIncidentPreset.Map1Kitchen:
                caseId = "SH-HK-01";
                scenarioId = "kitchen_fire_house_call";
                fireOrigin = "Kitchen_StoveTop";
                logicalFireLocation = "Kitchen";
                hazardType = "Gas";
                isolationType = "Gas";
                requiresIsolation = true;
                severityBand = "High";
                initialFireIntensity = 0.8f;
                initialFireCount = 2;
                fireSpreadPreset = "Moderate";
                startSmokeDensity = 0.55f;
                smokeAccumulationMultiplier = 1.25f;
                ventilationPreset = "Neutral";
                occupantRiskPreset = "High";
                estimatedTrappedCountKnown = true;
                estimatedTrappedCountMin = 1;
                estimatedTrappedCountMax = 1;
                victimLocationIntelMode = CallPhaseVictimLocationIntelMode.Estimated;
                visibleVictimIconCount = 1;
                estimatedVictimIconRevealDistance = 10f;
                reportAddress = "27 Maple Street";
                reportFireLocation = "Kitchen";
                reportOccupantRisk = "Child trapped upstairs";
                reportHazard = "Gas cylinder near kitchen";
                reportSpreadStatus = "Spreading toward dining area";
                reportCallerSafety = "Outside house";
                break;

            case DebugIncidentPreset.Map1Laundry:
                caseId = "SH-LD-01";
                scenarioId = "suburban_house_laundry_fire";
                fireOrigin = "Laundry_WasherOutlet";
                logicalFireLocation = "Laundry Room";
                hazardType = "Electrical";
                isolationType = "Electrical";
                requiresIsolation = true;
                severityBand = "High";
                initialFireIntensity = 0.78f;
                initialFireCount = 2;
                fireSpreadPreset = "Moderate";
                startSmokeDensity = 0.5f;
                smokeAccumulationMultiplier = 1.2f;
                ventilationPreset = "Neutral";
                occupantRiskPreset = "High";
                estimatedTrappedCountKnown = true;
                estimatedTrappedCountMin = 1;
                estimatedTrappedCountMax = 1;
                victimLocationIntelMode = CallPhaseVictimLocationIntelMode.None;
                visibleVictimIconCount = 0;
                estimatedVictimIconRevealDistance = 0f;
                reportAddress = "52 Pine Street";
                reportFireLocation = "Laundry Room";
                reportOccupantRisk = "Adult still inside";
                reportHazard = "Gas dryer line and exposed wiring";
                reportSpreadStatus = "Spreading toward kitchen";
                reportCallerSafety = "Outside front yard";
                break;

            case DebugIncidentPreset.Map3EquipmentRoom:
                caseId = "CT-EQ-01";
                scenarioId = "equipment_room_fire";
                fireOrigin = "EquipmentRoom";
                logicalFireLocation = "Equipment Room";
                hazardType = "Electrical";
                isolationType = "Electrical";
                requiresIsolation = true;
                severityBand = "High";
                initialFireIntensity = 0.9f;
                initialFireCount = 1;
                fireSpreadPreset = "Moderate";
                startSmokeDensity = 0.9f;
                smokeAccumulationMultiplier = 2f;
                ventilationPreset = "Confined";
                occupantRiskPreset = "High";
                estimatedTrappedCountKnown = true;
                estimatedTrappedCountMin = 2;
                estimatedTrappedCountMax = 6;
                victimLocationIntelMode = CallPhaseVictimLocationIntelMode.None;
                visibleVictimIconCount = 0;
                estimatedVictimIconRevealDistance = 0f;
                reportAddress = "Westbridge Research Center, 100 Main Street";
                reportFireLocation = "Equipment Room";
                reportOccupantRisk = "Researchers trapped on upper floors";
                reportHazard = "High-voltage electrical panel";
                reportSpreadStatus = "Smoke in research wing stairwell";
                reportCallerSafety = "Outside research center";
                break;

            case DebugIncidentPreset.Map3Hallway:
                caseId = "CT-HW-01";
                scenarioId = "hallway_fire";
                fireOrigin = "Hallway";
                logicalFireLocation = "Hallway";
                hazardType = "OrdinaryCombustibles";
                isolationType = "None";
                requiresIsolation = false;
                severityBand = "High";
                initialFireIntensity = 0.85f;
                initialFireCount = 2;
                fireSpreadPreset = "Aggressive";
                startSmokeDensity = 0.6f;
                smokeAccumulationMultiplier = 1.5f;
                ventilationPreset = "Confined";
                occupantRiskPreset = "High";
                estimatedTrappedCountKnown = true;
                estimatedTrappedCountMin = 2;
                estimatedTrappedCountMax = 2;
                victimLocationIntelMode = CallPhaseVictimLocationIntelMode.Estimated;
                visibleVictimIconCount = 2;
                estimatedVictimIconRevealDistance = 12f;
                reportAddress = "Westbridge Research Center, 100 Main Street";
                reportFireLocation = "Hallway";
                reportOccupantRisk = "Researchers behind lab doors";
                reportHazard = "Burning corridor materials";
                reportSpreadStatus = "Smoke spreading into labs and stairwell";
                reportCallerSafety = "On stair landing";
                break;

            case DebugIncidentPreset.Map3HallwayEstimatedVictimIntel:
                caseId = "CT-HW-EST-01";
                scenarioId = "hallway_fire_estimated_victim_intel";
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
                victimLocationIntelMode = CallPhaseVictimLocationIntelMode.Estimated;
                visibleVictimIconCount = 2;
                estimatedVictimIconRevealDistance = 12f;
                reportAddress = "Westbridge Research Center, 100 Main Street";
                reportFireLocation = "Hallway";
                reportOccupantRisk = "Researchers reported somewhere near the corridor";
                reportHazard = "Heavy smoke in corridor";
                reportSpreadStatus = "Spreading";
                reportCallerSafety = "Caller outside";
                break;

            case DebugIncidentPreset.Map3HallwayConfirmedVictimIntel:
                caseId = "CT-HW-CONF-01";
                scenarioId = "hallway_fire_confirmed_victim_intel";
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
                victimLocationIntelMode = CallPhaseVictimLocationIntelMode.Confirmed;
                visibleVictimIconCount = 3;
                estimatedVictimIconRevealDistance = 0f;
                reportAddress = "Westbridge Research Center, 100 Main Street";
                reportFireLocation = "Hallway";
                reportOccupantRisk = "Victims confirmed behind lab doors off the main corridor";
                reportHazard = "Heavy smoke in corridor";
                reportSpreadStatus = "Spreading";
                reportCallerSafety = "Caller outside";
                break;

            case DebugIncidentPreset.Map3StorageRoom:
                caseId = "CT-ST-01";
                scenarioId = "storage_room_fire";
                fireOrigin = "StorageRoom";
                logicalFireLocation = "Storage Room";
                hazardType = "OrdinaryCombustibles";
                isolationType = "None";
                requiresIsolation = false;
                severityBand = "High";
                initialFireIntensity = 0.85f;
                initialFireCount = 2;
                fireSpreadPreset = "Aggressive";
                startSmokeDensity = 0.5f;
                smokeAccumulationMultiplier = 1.5f;
                ventilationPreset = "Confined";
                occupantRiskPreset = "High";
                estimatedTrappedCountKnown = true;
                estimatedTrappedCountMin = 2;
                estimatedTrappedCountMax = 4;
                victimLocationIntelMode = CallPhaseVictimLocationIntelMode.Confirmed;
                visibleVictimIconCount = 1;
                estimatedVictimIconRevealDistance = 0f;
                reportAddress = "Westbridge Research Center, 100 Main Street";
                reportFireLocation = "Storage Room";
                reportOccupantRisk = "Possible lab technician inside";
                reportHazard = "Combustible lab storage contents";
                reportSpreadStatus = "Smoke spreading into research corridor";
                reportCallerSafety = "Outside storage room";
                break;

            case DebugIncidentPreset.Map3StorageRoomConfirmedVictimIntel:
                caseId = "CT-ST-CONF-01";
                scenarioId = "storage_room_fire_confirmed_victim_intel";
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
                estimatedTrappedCountKnown = true;
                estimatedTrappedCountMin = 1;
                estimatedTrappedCountMax = 1;
                victimLocationIntelMode = CallPhaseVictimLocationIntelMode.Confirmed;
                visibleVictimIconCount = 1;
                estimatedVictimIconRevealDistance = 0f;
                reportAddress = "Westbridge Research Center, 100 Main Street";
                reportFireLocation = "Storage Room";
                reportOccupantRisk = "One staff member confirmed inside the storage room";
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
            $"Hazard: {hazardType}",
            $"VictimIntel: {victimLocationIntelMode}",
            $"VictimIconCount: {visibleVictimIconCount}",
            $"VictimRevealDistance: {estimatedVictimIconRevealDistance:0.##}"
        };
    }

    private static string ResolveText(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
