using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class IncidentPayloadStartupTask : SceneStartupTask
{
    [Header("Payload")]
    [SerializeField] private bool clearPendingPayloadAfterApply = true;
    [SerializeField] private bool logResolvedPayload = true;

    [Header("Scene Bindings")]
    [SerializeField] private Fire defaultFirePrefab;
    [SerializeField] private IncidentPayloadAnchor[] explicitAnchors = Array.Empty<IncidentPayloadAnchor>();

    protected override IEnumerator Execute(SceneStartupFlow startupFlow)
    {
        if (!LoadingFlowState.TryGetPendingIncidentPayload(out IncidentWorldSetupPayload payload) || payload == null)
        {
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

        anchor.ApplyPayload(payload, defaultFirePrefab);

        if (logResolvedPayload)
        {
            Debug.Log(
                $"{nameof(IncidentPayloadStartupTask)} applied payload " +
                $"caseId='{payload.caseId}', scenarioId='{payload.scenarioId}', fireOrigin='{payload.fireOrigin}', " +
                $"logicalFireLocation='{payload.logicalFireLocation}', hazardType='{payload.hazardType}', " +
                $"fireCount={payload.initialFireCount}, intensity={payload.initialFireIntensity:0.00}, " +
                $"smoke={payload.startSmokeDensity:0.00}, smokeMul={payload.smokeAccumulationMultiplier:0.00}.",
                anchor);
        }

        if (clearPendingPayloadAfterApply)
        {
            LoadingFlowState.ClearPendingIncidentPayload();
        }

        yield break;
    }

    private IncidentPayloadAnchor ResolveAnchor(IncidentWorldSetupPayload payload)
    {
        IncidentPayloadAnchor[] anchors = ResolveAnchors();
        IncidentPayloadAnchor locationFallback = null;
        IncidentPayloadAnchor defaultAnchor = null;

        for (int i = 0; i < anchors.Length; i++)
        {
            IncidentPayloadAnchor anchor = anchors[i];
            if (anchor == null)
            {
                continue;
            }

            if (anchor.MatchesFireOrigin(payload.fireOrigin))
            {
                return anchor;
            }

            if (locationFallback == null && anchor.MatchesLogicalLocation(payload.logicalFireLocation))
            {
                locationFallback = anchor;
            }

            if (defaultAnchor == null && anchor.IsDefaultAnchor)
            {
                defaultAnchor = anchor;
            }
        }

        return locationFallback ?? defaultAnchor;
    }

    private IncidentPayloadAnchor[] ResolveAnchors()
    {
        if (explicitAnchors != null && explicitAnchors.Length > 0)
        {
            return explicitAnchors;
        }

        return FindObjectsByType<IncidentPayloadAnchor>(FindObjectsInactive.Include);
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
