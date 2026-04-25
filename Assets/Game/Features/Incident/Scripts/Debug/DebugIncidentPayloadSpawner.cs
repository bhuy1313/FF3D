using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[DefaultExecutionOrder(-300)]
[DisallowMultipleComponent]
public class DebugIncidentPayloadSpawner : MonoBehaviour
{
    private enum DebugOriginPreset
    {
        Laundry_WasherOutlet = 0,
        Kitchen_StoveTop = 1,
        Garage_WorkbenchCorner = 2,
        Custom = 3
    }

    private enum DebugHazardPreset
    {
        Electrical = 0,
        OrdinaryCombustibles = 1,
        Gas = 2,
        FlammableLiquid = 3
    }

    private enum DebugSpreadPreset
    {
        Conservative = 0,
        Moderate = 1,
        Aggressive = 2
    }

    private enum DebugSeverityPreset
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }

    private enum DebugIntensityPreset
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Custom = 3
    }

    private enum DebugFireCountPreset
    {
        Single = 0,
        SmallCluster = 1,
        MediumCluster = 2,
        LargeCluster = 3,
        Custom = 4
    }

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugSpawning = true;
    [SerializeField] private DebugOriginPreset debugOriginPreset = DebugOriginPreset.Laundry_WasherOutlet;
    [SerializeField] private DebugHazardPreset debugHazardPreset = DebugHazardPreset.Electrical;
    [SerializeField] private DebugSpreadPreset debugSpreadPreset = DebugSpreadPreset.Moderate;
    [SerializeField] private DebugSeverityPreset debugSeverityPreset = DebugSeverityPreset.Medium;
    [SerializeField] private DebugIntensityPreset debugIntensityPreset = DebugIntensityPreset.Medium;
    [SerializeField] private DebugFireCountPreset debugFireCountPreset = DebugFireCountPreset.MediumCluster;
    [SerializeField] [Range(0.1f, 1f)] private float customInitialFireIntensity = 0.65f;
    [SerializeField] [Range(1, 5)] private int customInitialFireCount = 3;
    [SerializeField] private string customFireOrigin = "Laundry_WasherOutlet";
    [SerializeField] private string customLogicalFireLocation = "Laundry";

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
            Debug.Log("[DebugIncidentPayloadSpawner] Found pending payload from normal flow. Debug injection skipped.");
            return;
        }

        string resolvedFireOrigin = ResolveFireOrigin();
        string resolvedLogicalLocation = ResolveLogicalLocation();
        string resolvedHazardType = ResolveHazardType();
        float resolvedInitialIntensity = ResolveInitialFireIntensity();
        int resolvedInitialFireCount = ResolveInitialFireCount();
        string resolvedSeverityBand = ResolveSeverityBand();
        string resolvedSpreadPreset = ResolveSpreadPreset();

        pendingDebugPayload = new IncidentWorldSetupPayload
        {
            caseId = "debug_case",
            scenarioId = "debug_scenario",
            fireOrigin = resolvedFireOrigin,
            logicalFireLocation = resolvedLogicalLocation,
            hazardType = resolvedHazardType,
            isolationType = ResolveIsolationType(resolvedHazardType),
            requiresIsolation = true,
            initialFireIntensity = resolvedInitialIntensity,
            initialFireCount = resolvedInitialFireCount,
            fireSpreadPreset = resolvedSpreadPreset,
            startSmokeDensity = 0.2f,
            smokeAccumulationMultiplier = 1f,
            ventilationPreset = "Neutral",
            occupantRiskPreset = "Manageable",
            severityBand = resolvedSeverityBand,
            confidenceScore = 1f,
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

    private string ResolveFireOrigin()
    {
        return debugOriginPreset switch
        {
            DebugOriginPreset.Kitchen_StoveTop => "Kitchen_StoveTop",
            DebugOriginPreset.Garage_WorkbenchCorner => "Garage_WorkbenchCorner",
            DebugOriginPreset.Custom => string.IsNullOrWhiteSpace(customFireOrigin) ? legacyDebugFireOrigin : customFireOrigin.Trim(),
            _ => "Laundry_WasherOutlet",
        };
    }

    private string ResolveLogicalLocation()
    {
        return debugOriginPreset switch
        {
            DebugOriginPreset.Kitchen_StoveTop => "Kitchen",
            DebugOriginPreset.Garage_WorkbenchCorner => "Garage",
            DebugOriginPreset.Custom => string.IsNullOrWhiteSpace(customLogicalFireLocation) ? legacyDebugLogicalFireLocation : customLogicalFireLocation.Trim(),
            _ => "Laundry",
        };
    }

    private string ResolveHazardType()
    {
        return debugHazardPreset switch
        {
            DebugHazardPreset.OrdinaryCombustibles => "OrdinaryCombustibles",
            DebugHazardPreset.Gas => "Gas",
            DebugHazardPreset.FlammableLiquid => "FlammableLiquid",
            _ => "Electrical",
        };
    }

    private string ResolveSpreadPreset()
    {
        return debugSpreadPreset switch
        {
            DebugSpreadPreset.Conservative => "Conservative",
            DebugSpreadPreset.Aggressive => "Aggressive",
            _ => "Moderate",
        };
    }

    private string ResolveSeverityBand()
    {
        return debugSeverityPreset switch
        {
            DebugSeverityPreset.Low => "Low",
            DebugSeverityPreset.High => "High",
            DebugSeverityPreset.Critical => "Critical",
            _ => "Medium",
        };
    }

    private float ResolveInitialFireIntensity()
    {
        return debugIntensityPreset switch
        {
            DebugIntensityPreset.Low => 0.35f,
            DebugIntensityPreset.High => 0.9f,
            DebugIntensityPreset.Custom => Mathf.Clamp(customInitialFireIntensity, 0.1f, 1f),
            _ => 0.65f,
        };
    }

    private int ResolveInitialFireCount()
    {
        return debugFireCountPreset switch
        {
            DebugFireCountPreset.Single => 1,
            DebugFireCountPreset.SmallCluster => 2,
            DebugFireCountPreset.LargeCluster => 5,
            DebugFireCountPreset.Custom => Mathf.Clamp(customInitialFireCount, 1, 5),
            _ => 3,
        };
    }

    private void OnValidate()
    {
        customInitialFireIntensity = Mathf.Clamp(customInitialFireIntensity, 0.1f, 1f);
        customInitialFireCount = Mathf.Clamp(customInitialFireCount, 1, 5);

        if (string.IsNullOrWhiteSpace(customFireOrigin))
        {
            customFireOrigin = legacyDebugFireOrigin;
        }

        if (string.IsNullOrWhiteSpace(customLogicalFireLocation))
        {
            customLogicalFireLocation = legacyDebugLogicalFireLocation;
        }

        if (debugOriginPreset == DebugOriginPreset.Custom)
        {
            return;
        }

        customFireOrigin = ResolveFireOrigin();
        customLogicalFireLocation = ResolveLogicalLocation();
    }
}
