using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class IncidentAnchorHazardSetupStep : IncidentMapSetupStep
{
    [SerializeField] private IncidentPayloadAnchor[] explicitAnchors = Array.Empty<IncidentPayloadAnchor>();
    [SerializeField] private bool includeInactiveAnchors = true;
    [SerializeField] private bool warnWhenIsolationDeviceMissing = true;

    protected override IEnumerator Execute(IncidentMapSetupContext context)
    {
        if (context == null || context.Payload == null)
        {
            yield break;
        }

        if (context.DefaultFirePrefab == null)
        {
            context.AddWarning(
                $"{nameof(IncidentAnchorHazardSetupStep)}: Missing default fire prefab for payload application.",
                this);
            yield break;
        }

        IncidentPayloadAnchor anchor = ResolveBestAnchor(context.Payload, ResolveAnchors());
        if (anchor == null)
        {
            context.AddWarning(
                $"{nameof(IncidentAnchorHazardSetupStep)}: No scene anchor matched payload origin '{context.Payload.fireOrigin}' " +
                $"or location '{context.Payload.logicalFireLocation}'.",
                this);
            yield break;
        }

        anchor.ApplyPayload(context.Payload, context.DefaultFirePrefab);
        context.ResolvedAnchor = anchor;

        if (warnWhenIsolationDeviceMissing &&
            context.Payload.requiresIsolation &&
            !anchor.HasConfiguredHazardIsolationDevices)
        {
            context.AddWarning(
                $"{nameof(IncidentAnchorHazardSetupStep)}: Payload requires isolation but anchor '{anchor.name}' " +
                $"has no linked {nameof(HazardIsolationDevice)} references.",
                anchor);
        }
    }

    public static IncidentPayloadAnchor ResolveBestAnchor(IncidentWorldSetupPayload payload, IncidentPayloadAnchor[] anchors)
    {
        if (payload == null || anchors == null || anchors.Length <= 0)
        {
            return null;
        }

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

        return FindObjectsByType<IncidentPayloadAnchor>(
            includeInactiveAnchors ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
    }
}
