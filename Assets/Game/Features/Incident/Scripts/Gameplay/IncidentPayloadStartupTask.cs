using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class IncidentPayloadStartupTask : SceneStartupTask
{
    [Header("Payload")]
    [SerializeField] private bool logResolvedPayload = true;

    [Header("Scene Bindings")]
    [SerializeField] private IncidentMapSetupRoot explicitMapSetupRoot;
    [SerializeField] private IncidentPayloadAnchor[] explicitAnchors = Array.Empty<IncidentPayloadAnchor>();

    protected override IEnumerator Execute(SceneStartupFlow startupFlow)
    {
        if (!LoadingFlowState.TryGetPendingIncidentPayload(out IncidentWorldSetupPayload payload) || payload == null)
        {
            yield break;
        }

        IncidentPayloadAnchor resolvedAnchor = null;
        IncidentMapSetupRoot setupRoot = ResolveMapSetupRoot(startupFlow);
        if (setupRoot != null)
        {
            yield return setupRoot.ApplyPayload(startupFlow, payload, firePrefabLibrary: null);
            resolvedAnchor = setupRoot.LastResolvedAnchor;
        }
        else
        {
            FireSimulationManager resolvedSimulationManager = setupRoot != null ? setupRoot.FireSimulationManager : GetComponentInChildren<FireSimulationManager>(true);
            if (resolvedSimulationManager == null)
            {
                Debug.LogWarning(
                    $"{nameof(IncidentPayloadStartupTask)}: Missing {nameof(FireSimulationManager)} for payload application.",
                    this);
                yield break;
            }

            IncidentFireSpawnProfile resolvedProfile = setupRoot != null ? setupRoot.FireSpawnProfile : null;
            if (resolvedProfile == null)
            {
                Debug.LogWarning(
                    $"{nameof(IncidentPayloadStartupTask)}: Missing {nameof(IncidentFireSpawnProfile)} for payload application.",
                    this);
                yield break;
            }

            IncidentPayloadAnchor anchor = ResolveAnchor(payload);
            if (anchor == null)
            {
                Debug.LogWarning(
                    $"{nameof(IncidentPayloadStartupTask)}: No scene anchor matched payload origin '{payload.fireOrigin}' " +
                    $"or location '{payload.logicalFireLocation}'.",
                    this);
                yield break;
            }

            if (!anchor.ApplyPayload(payload, firePrefabLibrary: null, resolvedProfile, resolvedSimulationManager))
            {
                Debug.LogWarning(
                    $"{nameof(IncidentPayloadStartupTask)}: Anchor '{anchor.name}' failed to apply payload.",
                    anchor);
                yield break;
            }

            resolvedAnchor = anchor;
        }

        if (resolvedAnchor == null)
        {
            yield break;
        }

        if (logResolvedPayload)
        {
            Debug.Log(
                $"{nameof(IncidentPayloadStartupTask)} applied payload " +
                $"caseId='{payload.caseId}', scenarioId='{payload.scenarioId}', fireOrigin='{payload.fireOrigin}', " +
                $"logicalFireLocation='{payload.logicalFireLocation}', hazardType='{payload.hazardType}', " +
                $"fireCount={payload.initialFireCount}, intensity={payload.initialFireIntensity:0.00}, " +
                $"smoke={payload.startSmokeDensity:0.00}, smokeMul={payload.smokeAccumulationMultiplier:0.00}.",
                resolvedAnchor);
        }

        yield break;
    }

    private IncidentPayloadAnchor ResolveAnchor(IncidentWorldSetupPayload payload)
    {
        return IncidentAnchorHazardMapSetupTask.ResolveBestAnchor(payload, ResolveAnchors());
    }

    private IncidentPayloadAnchor[] ResolveAnchors()
    {
        if (explicitAnchors != null && explicitAnchors.Length > 0)
        {
            return explicitAnchors;
        }

        return FindObjectsByType<IncidentPayloadAnchor>(FindObjectsInactive.Include);
    }

    private IncidentMapSetupRoot ResolveMapSetupRoot(SceneStartupFlow startupFlow)
    {
        if (explicitMapSetupRoot != null)
        {
            return explicitMapSetupRoot;
        }

        IncidentMapSetupRoot localRoot = GetComponent<IncidentMapSetupRoot>();
        if (localRoot != null)
        {
            return localRoot;
        }

        if (startupFlow != null)
        {
            localRoot = startupFlow.GetComponentInChildren<IncidentMapSetupRoot>(true);
            if (localRoot != null)
            {
                return localRoot;
            }
        }

        return FindAnyObjectByType<IncidentMapSetupRoot>(FindObjectsInactive.Include);
    }
    public static FireHazardType ResolveFireHazardType(string hazardType)
    {
        if (string.Equals(hazardType, "Electrical", StringComparison.OrdinalIgnoreCase))
        {
            return FireHazardType.Electrical;
        }

        if (string.Equals(hazardType, "Gas", StringComparison.OrdinalIgnoreCase))
        {
            return FireHazardType.GasFed;
        }

        if (string.Equals(hazardType, "FlammableLiquid", StringComparison.OrdinalIgnoreCase))
        {
            return FireHazardType.FlammableLiquid;
        }

        return FireHazardType.OrdinaryCombustibles;
    }

    public static float ResolveSpreadInterval(string fireSpreadPreset)
    {
        switch (NormalizePreset(fireSpreadPreset))
        {
            case "conservative":
                return 1.5f;
            case "aggressive":
                return 0.7f;
            default:
                return 1f;
        }
    }

    public static float ResolveSpreadIgniteAmount(string fireSpreadPreset)
    {
        switch (NormalizePreset(fireSpreadPreset))
        {
            case "conservative":
                return 0.12f;
            case "aggressive":
                return 0.35f;
            default:
                return 0.2f;
        }
    }

    public static float ResolveSpreadThreshold(string fireSpreadPreset)
    {
        switch (NormalizePreset(fireSpreadPreset))
        {
            case "conservative":
                return 0.55f;
            case "aggressive":
                return 0.2f;
            default:
                return 0.35f;
        }
    }

    private static string NormalizePreset(string preset)
    {
        return string.IsNullOrWhiteSpace(preset)
            ? string.Empty
            : preset.Trim().ToLowerInvariant();
    }
}
