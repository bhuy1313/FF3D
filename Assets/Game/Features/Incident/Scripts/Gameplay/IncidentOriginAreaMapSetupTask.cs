using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class IncidentOriginAreaMapSetupTask : IncidentMapSetupTask
{
    [SerializeField] private IncidentOriginArea[] explicitAreas = Array.Empty<IncidentOriginArea>();
    [SerializeField] private bool collectAreasFromScene = true;
    [SerializeField] private bool includeInactiveAreas = true;
    [SerializeField] private bool warnWhenIsolationDeviceMissing = true;
    [SerializeField] private bool logResolvedIgnitionSource = true;

    protected override IEnumerator Execute(IncidentMapSetupContext context)
    {
        if (context == null || context.Payload == null || context.ResolvedAnchor != null)
        {
            yield break;
        }

        if (context.FireSimulationManager == null)
        {
            context.AddWarning(
                $"{nameof(IncidentOriginAreaMapSetupTask)}: Missing {nameof(FireSimulationManager)} for payload application.",
                this);
            yield break;
        }

        if (context.FireSpawnProfile == null)
        {
            context.AddWarning(
                $"{nameof(IncidentOriginAreaMapSetupTask)}: Missing {nameof(IncidentFireSpawnProfile)} for payload application.",
                this);
            yield break;
        }

        IncidentOriginArea area = IncidentIgnitionResolver.ResolveBestArea(context.Payload, ResolveAreas());
        if (area == null)
        {
            yield break;
        }

        if (!IncidentIgnitionResolver.TryResolveIgnitionSource(context.Payload, area, out ResolvedIgnitionSource source) ||
            source == null)
        {
            context.AddWarning(
                $"{nameof(IncidentOriginAreaMapSetupTask)}: Failed to resolve ignition source inside area '{area.name}'.",
                area);
            yield break;
        }

        if (!area.ApplyPayload(
                context.Payload,
                firePrefabLibrary: null,
                context.FireSpawnProfile,
                context.FireSimulationManager,
                source))
        {
            context.AddWarning(
                $"{nameof(IncidentOriginAreaMapSetupTask)}: Area '{area.name}' could not apply payload with resolved source '{source.DebugLabel}'.",
                area);
            yield break;
        }

        context.ResolvedOriginArea = area;
        context.ResolvedAnchor = area.LegacyAnchor;

        if (warnWhenIsolationDeviceMissing &&
            context.Payload.requiresIsolation &&
            !area.HasConfiguredHazardIsolationDevices)
        {
            context.AddWarning(
                $"{nameof(IncidentOriginAreaMapSetupTask)}: Payload requires isolation but area '{area.name}' has no linked " +
                $"{nameof(HazardIsolationDevice)} references.",
                area);
        }

        if (logResolvedIgnitionSource)
        {
            Debug.Log(
                $"{nameof(IncidentOriginAreaMapSetupTask)} resolved area '{area.name}' with source '{source.DebugLabel}' " +
                $"for payload origin '{context.Payload.fireOrigin}' at {source.Position}.",
                area);
        }
    }

    private IncidentOriginArea[] ResolveAreas()
    {
        List<IncidentOriginArea> results = new List<IncidentOriginArea>();
        HashSet<IncidentOriginArea> seen = new HashSet<IncidentOriginArea>();

        if (explicitAreas != null)
        {
            for (int i = 0; i < explicitAreas.Length; i++)
            {
                IncidentOriginArea area = explicitAreas[i];
                if (area == null || !seen.Add(area))
                {
                    continue;
                }

                results.Add(area);
            }
        }

        if (collectAreasFromScene)
        {
            IncidentOriginArea[] sceneAreas = FindObjectsByType<IncidentOriginArea>(
                includeInactiveAreas ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
            for (int i = 0; i < sceneAreas.Length; i++)
            {
                IncidentOriginArea area = sceneAreas[i];
                if (area == null || !seen.Add(area))
                {
                    continue;
                }

                results.Add(area);
            }
        }

        return results.ToArray();
    }
}
