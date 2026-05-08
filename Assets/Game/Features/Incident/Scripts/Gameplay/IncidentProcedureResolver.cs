using UnityEngine;

public static class IncidentProcedureResolver
{
    public static IncidentProcedureDefinition Resolve(IncidentWorldSetupPayload payload)
    {
        IncidentProcedureDefinition[] definitions = Resources.LoadAll<IncidentProcedureDefinition>(string.Empty);
        if (definitions == null || definitions.Length == 0)
        {
            return null;
        }

        IncidentProcedureDefinition bestMatch = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < definitions.Length; i++)
        {
            IncidentProcedureDefinition candidate = definitions[i];
            if (candidate == null || !candidate.Matches(payload))
            {
                continue;
            }

            int score = 0;
            if (!string.IsNullOrWhiteSpace(candidate.ScenarioId))
            {
                score += 4;
            }

            if (!string.IsNullOrWhiteSpace(candidate.HazardType))
            {
                score += 2;
            }

            if (!string.IsNullOrWhiteSpace(candidate.FireLocation))
            {
                score += 2;
            }

            if (!string.IsNullOrWhiteSpace(candidate.SeverityBand))
            {
                score += 1;
            }

            if (candidate.RequiresKnownVictimRisk)
            {
                score += 1;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = candidate;
            }
        }

        return bestMatch;
    }
}
