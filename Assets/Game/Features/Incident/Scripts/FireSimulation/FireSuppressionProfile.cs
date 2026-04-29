using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "FireSuppressionProfile",
    menuName = "FF3D/Incident/Fire Suppression Profile")]
public sealed class FireSuppressionProfile : ScriptableObject
{
    [Serializable]
    public struct AgentEffectiveness
    {
        [Tooltip("Multiplier applied to suppression amount when using Water (0 = ineffective, >1 = bonus).")]
        [Min(0f)] public float water;
        [Tooltip("Multiplier applied to suppression amount when using Dry Chemical / Foam.")]
        [Min(0f)] public float dryChemical;
        [Tooltip("Multiplier applied to suppression amount when using CO2.")]
        [Min(0f)] public float co2;
        [Tooltip("If true, applying Water on this hazard adds heat instead of removing (e.g. flammable liquid backfire).")]
        public bool waterWorsens;

        public float GetEffectiveness(FireSuppressionAgent agent)
        {
            switch (agent)
            {
                case FireSuppressionAgent.Water: return water;
                case FireSuppressionAgent.CO2: return co2;
                default: return dryChemical;
            }
        }
    }

    [Serializable]
    public struct HazardEntry
    {
        public FireHazardType hazardType;
        [Tooltip("Effectiveness when the hazard source is NOT yet isolated.")]
        public AgentEffectiveness notIsolated;
        [Tooltip("Effectiveness when the hazard source has been isolated by a HazardIsolationDevice.")]
        public AgentEffectiveness isolated;
    }

    [SerializeField] private HazardEntry[] entries = CreateDefaultEntries();

    public float GetEffectiveness(FireHazardType hazardType, FireSuppressionAgent agent, bool isolated)
    {
        AgentEffectiveness eff = ResolveAgentEffectiveness(hazardType, isolated);
        return eff.GetEffectiveness(agent);
    }

    public bool GetWorsens(FireHazardType hazardType, FireSuppressionAgent agent, bool isolated)
    {
        if (agent != FireSuppressionAgent.Water)
        {
            return false;
        }

        AgentEffectiveness eff = ResolveAgentEffectiveness(hazardType, isolated);
        return eff.waterWorsens;
    }

    private AgentEffectiveness ResolveAgentEffectiveness(FireHazardType hazardType, bool isolated)
    {
        if (entries == null)
        {
            return DefaultFor(hazardType, isolated);
        }

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].hazardType == hazardType)
            {
                return isolated ? entries[i].isolated : entries[i].notIsolated;
            }
        }

        return DefaultFor(hazardType, isolated);
    }

    public static FireSuppressionProfile CreateDefault()
    {
        FireSuppressionProfile profile = CreateInstance<FireSuppressionProfile>();
        profile.entries = CreateDefaultEntries();
        return profile;
    }

    private static HazardEntry[] CreateDefaultEntries()
    {
        return new[]
        {
            new HazardEntry
            {
                hazardType = FireHazardType.OrdinaryCombustibles,
                notIsolated = new AgentEffectiveness { water = 1f, dryChemical = 0.8f, co2 = 0.55f, waterWorsens = false },
                isolated    = new AgentEffectiveness { water = 1f, dryChemical = 0.8f, co2 = 0.55f, waterWorsens = false }
            },
            new HazardEntry
            {
                hazardType = FireHazardType.Electrical,
                notIsolated = new AgentEffectiveness { water = 0f, dryChemical = 1.25f, co2 = 1.35f, waterWorsens = false },
                isolated    = new AgentEffectiveness { water = 0.8f, dryChemical = 1.05f, co2 = 1f, waterWorsens = false }
            },
            new HazardEntry
            {
                hazardType = FireHazardType.FlammableLiquid,
                notIsolated = new AgentEffectiveness { water = 0f, dryChemical = 1.2f, co2 = 1f, waterWorsens = true },
                isolated    = new AgentEffectiveness { water = 0f, dryChemical = 1.2f, co2 = 1f, waterWorsens = true }
            },
            new HazardEntry
            {
                hazardType = FireHazardType.GasFed,
                notIsolated = new AgentEffectiveness { water = 0.3f, dryChemical = 0.45f, co2 = 0.4f, waterWorsens = false },
                isolated    = new AgentEffectiveness { water = 0.85f, dryChemical = 1.1f, co2 = 1f, waterWorsens = false }
            }
        };
    }

    private static AgentEffectiveness DefaultFor(FireHazardType hazardType, bool isolated)
    {
        HazardEntry[] defaults = CreateDefaultEntries();
        for (int i = 0; i < defaults.Length; i++)
        {
            if (defaults[i].hazardType == hazardType)
            {
                return isolated ? defaults[i].isolated : defaults[i].notIsolated;
            }
        }

        return defaults[0].notIsolated;
    }
}
