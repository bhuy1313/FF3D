using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

[DefaultExecutionOrder(-300)]
[DisallowMultipleComponent]
public class DebugIncidentPayloadSpawner : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugSpawning = true;

    [Header("Payload")]
    [SerializeField] private string caseId = "debug_case";
    [SerializeField] private string scenarioId = "debug_scenario";
    [FormerlySerializedAs("customFireOrigin")]
    [SerializeField] private string fireOrigin = "Laundry_WasherOutlet";
    [FormerlySerializedAs("customLogicalFireLocation")]
    [SerializeField] private string logicalFireLocation = "Laundry";
    [SerializeField] private string hazardType = "Electrical";
    [SerializeField] private string isolationType = "Electrical";
    [SerializeField] private bool requiresIsolation = true;
    [FormerlySerializedAs("customInitialFireIntensity")]
    [SerializeField, Range(0.1f, 1f)] private float initialFireIntensity = 0.65f;
    [FormerlySerializedAs("customInitialFireCount")]
    [SerializeField, Min(1)] private int initialFireCount = 3;
    [SerializeField] private string fireSpreadPreset = "Moderate";
    [SerializeField, Min(0f)] private float startSmokeDensity = 0.2f;
    [SerializeField, Min(0f)] private float smokeAccumulationMultiplier = 1f;
    [SerializeField] private string ventilationPreset = "Neutral";
    [SerializeField] private string occupantRiskPreset = "Manageable";
    [SerializeField] private string severityBand = "Medium";
    [SerializeField] private bool estimatedTrappedCountKnown;
    [SerializeField, Min(0)] private int estimatedTrappedCountMin;
    [SerializeField, Min(0)] private int estimatedTrappedCountMax;
    [SerializeField, Range(0f, 1f)] private float confidenceScore = 1f;
    [SerializeField] private int placementRandomSeed;

    [Header("Report Snapshot")]
    [SerializeField] private string reportAddress = "123 Debug St";
    [SerializeField] private string reportFireLocation = "Laundry room";
    [SerializeField] private string reportOccupantRisk = "Unknown";
    [SerializeField] private string reportHazard = "Electrical appliance";
    [SerializeField] private string reportSpreadStatus = "Contained";
    [SerializeField] private string reportCallerSafety = "Caller outside";
    [SerializeField] private string reportSeverity = "Medium";

    [Header("Applied Signals")]
    [SerializeField] private string[] appliedSignals =
    {
        "Debug payload injected manually."
    };

    [FormerlySerializedAs("debugFireOrigin")]
    [SerializeField, HideInInspector] private string legacyDebugFireOrigin = "Laundry_WasherOutlet";
    [FormerlySerializedAs("debugLogicalFireLocation")]
    [SerializeField, HideInInspector] private string legacyDebugLogicalFireLocation = "Laundry";
    [FormerlySerializedAs("debugHazardType")]
    [SerializeField, HideInInspector] private string legacyDebugHazardType = "Electrical";
    [FormerlySerializedAs("debugInitialFireIntensity")]
    [SerializeField, HideInInspector] private float legacyDebugInitialFireIntensity = 0.65f;
    [FormerlySerializedAs("debugInitialFireCount")]
    [SerializeField, HideInInspector] private int legacyDebugInitialFireCount = 3;
    [FormerlySerializedAs("debugSeverityBand")]
    [SerializeField, HideInInspector] private string legacyDebugSeverityBand = "Medium";

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
            string activeSceneName = SceneManager.GetActiveScene().name;
            if (LoadingFlowState.WasSceneActivatedFromLoadingFlow(activeSceneName))
            {
                Debug.Log("[DebugIncidentPayloadSpawner] Found pending payload from normal flow. Debug injection skipped.");
                return;
            }

            LoadingFlowState.ClearPendingIncidentPayload();
            Debug.Log(
                $"[DebugIncidentPayloadSpawner] Cleared stale pending payload before direct scene debug play in '{activeSceneName}'.");
        }

        string resolvedCaseId = ResolveText(caseId, "debug_case");
        string resolvedScenarioId = ResolveText(scenarioId, "debug_scenario");
        string resolvedFireOrigin = ResolveText(fireOrigin, ResolveText(legacyDebugFireOrigin, "Laundry_WasherOutlet"));
        string resolvedLogicalLocation = ResolveText(logicalFireLocation, ResolveText(legacyDebugLogicalFireLocation, "Laundry"));
        string resolvedHazardType = ResolveText(hazardType, ResolveText(legacyDebugHazardType, "Electrical"));
        float resolvedInitialFireIntensity = initialFireIntensity > 0f ? initialFireIntensity : legacyDebugInitialFireIntensity;
        int resolvedInitialFireCount = initialFireCount > 0 ? initialFireCount : legacyDebugInitialFireCount;
        IncidentWorldSetupReportSnapshot resolvedReportSnapshot = BuildReportSnapshot();
        List<string> resolvedAppliedSignals = BuildAppliedSignals();

        pendingDebugPayload = new IncidentWorldSetupPayload
        {
            caseId = resolvedCaseId,
            scenarioId = resolvedScenarioId,
            fireOrigin = resolvedFireOrigin,
            logicalFireLocation = resolvedLogicalLocation,
            hazardType = resolvedHazardType,
            isolationType = ResolveText(isolationType, "None"),
            requiresIsolation = requiresIsolation,
            initialFireIntensity = Mathf.Clamp(resolvedInitialFireIntensity, 0.1f, 1f),
            initialFireCount = Mathf.Max(1, resolvedInitialFireCount),
            fireSpreadPreset = ResolveText(fireSpreadPreset, "Moderate"),
            startSmokeDensity = Mathf.Max(0f, startSmokeDensity),
            smokeAccumulationMultiplier = Mathf.Max(0f, smokeAccumulationMultiplier),
            ventilationPreset = ResolveText(ventilationPreset, "Neutral"),
            occupantRiskPreset = ResolveText(occupantRiskPreset, "Manageable"),
            severityBand = ResolveText(severityBand, ResolveText(legacyDebugSeverityBand, "Medium")),
            estimatedTrappedCountKnown = estimatedTrappedCountKnown,
            estimatedTrappedCountMin = estimatedTrappedCountKnown ? Mathf.Max(0, estimatedTrappedCountMin) : 0,
            estimatedTrappedCountMax = estimatedTrappedCountKnown
                ? Mathf.Max(Mathf.Max(0, estimatedTrappedCountMin), estimatedTrappedCountMax)
                : 0,
            confidenceScore = Mathf.Clamp01(confidenceScore),
            placementRandomSeed = placementRandomSeed,
            reportSnapshot = resolvedReportSnapshot,
            appliedSignals = resolvedAppliedSignals,
        };

        LoadingFlowState.SetPendingIncidentPayload(pendingDebugPayload);
        shouldDirectApplyPayload =
            FindAnyObjectByType<SceneStartupFlow>(FindObjectsInactive.Include) == null &&
            FindAnyObjectByType<IncidentPayloadStartupTask>(FindObjectsInactive.Include) == null;

        Debug.Log(
            $"[DebugIncidentPayloadSpawner] Injected debug payload for origin '{resolvedFireOrigin}'. " +
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
            Debug.LogWarning("[DebugIncidentPayloadSpawner] No IncidentMapSetupRoot found for direct payload apply.", this);
            yield break;
        }

        Debug.Log("[DebugIncidentPayloadSpawner] Applying debug payload directly through IncidentMapSetupRoot.", this);
        yield return setupRoot.ApplyPayload(null, payload, firePrefabLibrary: null);

        if (setupRoot.LastResolvedAnchor != null)
        {
            LoadingFlowState.ClearPendingIncidentPayload();
        }
        else
        {
            Debug.LogWarning("[DebugIncidentPayloadSpawner] Debug payload apply did not resolve any anchor.", this);
        }
    }

    private static string ResolveText(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private IncidentWorldSetupReportSnapshot BuildReportSnapshot()
    {
        return new IncidentWorldSetupReportSnapshot
        {
            address = ResolveText(reportAddress, "123 Debug St"),
            fireLocation = ResolveText(reportFireLocation, logicalFireLocation),
            occupantRisk = ResolveText(reportOccupantRisk, occupantRiskPreset),
            hazard = ResolveText(reportHazard, hazardType),
            spreadStatus = ResolveText(reportSpreadStatus, fireSpreadPreset),
            callerSafety = ResolveText(reportCallerSafety, "Unknown"),
            severity = ResolveText(reportSeverity, severityBand)
        };
    }

    private List<string> BuildAppliedSignals()
    {
        List<string> results = new List<string>();
        if (appliedSignals == null || appliedSignals.Length == 0)
        {
            results.Add("Debug payload injected manually.");
            return results;
        }

        for (int i = 0; i < appliedSignals.Length; i++)
        {
            string value = appliedSignals[i];
            if (!string.IsNullOrWhiteSpace(value))
            {
                results.Add(value.Trim());
            }
        }

        if (results.Count == 0)
        {
            results.Add("Debug payload injected manually.");
        }

        return results;
    }

    private void OnValidate()
    {
        initialFireIntensity = Mathf.Clamp(initialFireIntensity, 0.1f, 1f);
        initialFireCount = Mathf.Max(1, initialFireCount);
        estimatedTrappedCountMin = Mathf.Max(0, estimatedTrappedCountMin);
        estimatedTrappedCountMax = Mathf.Max(estimatedTrappedCountMin, estimatedTrappedCountMax);
        startSmokeDensity = Mathf.Max(0f, startSmokeDensity);
        smokeAccumulationMultiplier = Mathf.Max(0f, smokeAccumulationMultiplier);
        confidenceScore = Mathf.Clamp01(confidenceScore);

        if (string.IsNullOrWhiteSpace(caseId))
        {
            caseId = "debug_case";
        }

        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            scenarioId = "debug_scenario";
        }

        if (string.IsNullOrWhiteSpace(fireOrigin))
        {
            fireOrigin = ResolveText(legacyDebugFireOrigin, "Laundry_WasherOutlet");
        }

        if (string.IsNullOrWhiteSpace(logicalFireLocation))
        {
            logicalFireLocation = ResolveText(legacyDebugLogicalFireLocation, "Laundry");
        }

        if (string.IsNullOrWhiteSpace(hazardType))
        {
            hazardType = ResolveText(legacyDebugHazardType, "Electrical");
        }

        if (string.IsNullOrWhiteSpace(isolationType))
        {
            isolationType = "None";
        }

        if (string.IsNullOrWhiteSpace(fireSpreadPreset))
        {
            fireSpreadPreset = "Moderate";
        }

        if (string.IsNullOrWhiteSpace(ventilationPreset))
        {
            ventilationPreset = "Neutral";
        }

        if (string.IsNullOrWhiteSpace(occupantRiskPreset))
        {
            occupantRiskPreset = "Manageable";
        }

        if (string.IsNullOrWhiteSpace(severityBand))
        {
            severityBand = ResolveText(legacyDebugSeverityBand, "Medium");
        }

        if (string.IsNullOrWhiteSpace(reportAddress))
        {
            reportAddress = "123 Debug St";
        }

        if (string.IsNullOrWhiteSpace(reportFireLocation))
        {
            reportFireLocation = logicalFireLocation;
        }

        if (string.IsNullOrWhiteSpace(reportOccupantRisk))
        {
            reportOccupantRisk = occupantRiskPreset;
        }

        if (string.IsNullOrWhiteSpace(reportHazard))
        {
            reportHazard = hazardType;
        }

        if (string.IsNullOrWhiteSpace(reportSpreadStatus))
        {
            reportSpreadStatus = fireSpreadPreset;
        }

        if (string.IsNullOrWhiteSpace(reportCallerSafety))
        {
            reportCallerSafety = "Unknown";
        }

        if (string.IsNullOrWhiteSpace(reportSeverity))
        {
            reportSeverity = severityBand;
        }

        if (appliedSignals == null || appliedSignals.Length == 0)
        {
            appliedSignals = new[] { "Debug payload injected manually." };
        }
    }
}
