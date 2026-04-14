using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class IncidentVentilationPresetSetupStep : IncidentMapSetupStep
{
    [SerializeField] private IncidentVentilationPresetBinding[] explicitBindings = Array.Empty<IncidentVentilationPresetBinding>();
    [SerializeField] private bool collectBindingsFromScene = true;
    [SerializeField] private bool includeInactiveBindings = true;
    [SerializeField] private bool stopAfterFirstAppliedBinding;
    [SerializeField] private bool warnWhenPresetUnmapped;

    protected override IEnumerator Execute(IncidentMapSetupContext context)
    {
        if (context == null || context.Payload == null || string.IsNullOrWhiteSpace(context.Payload.ventilationPreset))
        {
            yield break;
        }

        IncidentVentilationPresetBinding[] bindings = ResolveBindings();
        if (bindings.Length <= 0)
        {
            yield break;
        }

        int appliedCount = ApplyMatchingBindings(context.Payload.ventilationPreset, bindings);
        if (appliedCount <= 0)
        {
            appliedCount = ApplyFallbackBindings(bindings);
        }

        if (appliedCount <= 0 && warnWhenPresetUnmapped)
        {
            context.AddWarning(
                $"{nameof(IncidentVentilationPresetSetupStep)}: No ventilation binding matched preset '{context.Payload.ventilationPreset}'.",
                this);
        }
    }

    private int ApplyMatchingBindings(string preset, IncidentVentilationPresetBinding[] bindings)
    {
        int appliedCount = 0;
        for (int i = 0; i < bindings.Length; i++)
        {
            IncidentVentilationPresetBinding binding = bindings[i];
            if (binding == null || !binding.MatchesPreset(preset))
            {
                continue;
            }

            binding.ApplyBinding();
            appliedCount++;

            if (stopAfterFirstAppliedBinding)
            {
                break;
            }
        }

        return appliedCount;
    }

    private int ApplyFallbackBindings(IncidentVentilationPresetBinding[] bindings)
    {
        int appliedCount = 0;
        for (int i = 0; i < bindings.Length; i++)
        {
            IncidentVentilationPresetBinding binding = bindings[i];
            if (binding == null || !binding.IsFallbackBinding)
            {
                continue;
            }

            binding.ApplyBinding();
            appliedCount++;

            if (stopAfterFirstAppliedBinding)
            {
                break;
            }
        }

        return appliedCount;
    }

    private IncidentVentilationPresetBinding[] ResolveBindings()
    {
        List<IncidentVentilationPresetBinding> results = new List<IncidentVentilationPresetBinding>();
        HashSet<IncidentVentilationPresetBinding> seen = new HashSet<IncidentVentilationPresetBinding>();

        if (explicitBindings != null)
        {
            for (int i = 0; i < explicitBindings.Length; i++)
            {
                IncidentVentilationPresetBinding binding = explicitBindings[i];
                if (binding == null || !seen.Add(binding))
                {
                    continue;
                }

                results.Add(binding);
            }
        }

        if (collectBindingsFromScene)
        {
            IncidentVentilationPresetBinding[] sceneBindings = FindObjectsByType<IncidentVentilationPresetBinding>(
                includeInactiveBindings ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
            for (int i = 0; i < sceneBindings.Length; i++)
            {
                IncidentVentilationPresetBinding binding = sceneBindings[i];
                if (binding == null || !seen.Add(binding))
                {
                    continue;
                }

                results.Add(binding);
            }
        }

        return results.ToArray();
    }
}
