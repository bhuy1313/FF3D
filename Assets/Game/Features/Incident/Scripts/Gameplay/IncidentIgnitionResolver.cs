using System;
using System.Collections.Generic;
using UnityEngine;

public static class IncidentIgnitionResolver
{
    public static IncidentOriginArea ResolveBestArea(IncidentWorldSetupPayload payload, IncidentOriginArea[] areas)
    {
        if (payload == null || areas == null || areas.Length <= 0)
        {
            return null;
        }

        IncidentOriginArea bestArea = null;
        int bestScore = int.MinValue;
        string areaHintFromOrigin = ExtractAreaHint(payload.fireOrigin);

        for (int i = 0; i < areas.Length; i++)
        {
            IncidentOriginArea area = areas[i];
            if (area == null)
            {
                continue;
            }

            int score = 0;
            if (area.MatchesFireOriginHint(payload.fireOrigin))
            {
                score += 100;
            }

            if (area.MatchesAreaKey(areaHintFromOrigin))
            {
                score += 50;
            }

            if (area.MatchesAreaKey(payload.logicalFireLocation))
            {
                score += 25;
            }

            if (area.IsDefaultArea)
            {
                score += 1;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestArea = area;
            }
        }

        return bestScore > 0 ? bestArea : null;
    }

    public static bool TryResolveIgnitionSource(
        IncidentWorldSetupPayload payload,
        IncidentOriginArea area,
        out ResolvedIgnitionSource source)
    {
        source = null;
        if (payload == null || area == null)
        {
            return false;
        }

        List<Candidate> candidates = BuildCandidates(payload, area);
        if (candidates.Count <= 0)
        {
            return false;
        }

        float totalWeight = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight += Mathf.Max(0.01f, candidates[i].Weight);
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            roll -= Mathf.Max(0.01f, candidate.Weight);
            if (roll > 0f)
            {
                continue;
            }

            source = new ResolvedIgnitionSource
            {
                Area = area,
                Cause = candidate.Cause,
                IsNormalRoomFire = candidate.IsNormalRoomFire,
                Position = candidate.Position,
                Rotation = candidate.Rotation,
                HazardType = IncidentPayloadStartupTask.ResolveFireHazardType(payload.hazardType),
                Weight = candidate.Weight,
                DebugLabel = candidate.Label
            };
            return true;
        }

        Candidate lastCandidate = candidates[candidates.Count - 1];
        source = new ResolvedIgnitionSource
        {
            Area = area,
            Cause = lastCandidate.Cause,
            IsNormalRoomFire = lastCandidate.IsNormalRoomFire,
            Position = lastCandidate.Position,
            Rotation = lastCandidate.Rotation,
            HazardType = IncidentPayloadStartupTask.ResolveFireHazardType(payload.hazardType),
            Weight = lastCandidate.Weight,
            DebugLabel = lastCandidate.Label
        };
        return true;
    }

    private static List<Candidate> BuildCandidates(IncidentWorldSetupPayload payload, IncidentOriginArea area)
    {
        List<Candidate> results = new List<Candidate>();

        IncidentPossibleFireCause[] causes = area.CollectPossibleCauses(includeInactive: true);
        for (int i = 0; i < causes.Length; i++)
        {
            IncidentPossibleFireCause cause = causes[i];
            if (cause == null)
            {
                continue;
            }

            float weight = cause.ComputeWeight(payload, area);
            if (weight <= 0f || !cause.TryResolveIgnitionPose(out Vector3 position, out Quaternion rotation))
            {
                continue;
            }

            results.Add(new Candidate
            {
                Cause = cause,
                Position = position,
                Rotation = rotation,
                Weight = weight,
                Label = $"Cause:{cause.CauseKey}"
            });
        }

        if (area.NormalRoomFireWeight > 0f &&
            IncidentNormalFirePlacementUtility.TryResolvePlacement(area, out Vector3 normalPosition, out Quaternion normalRotation))
        {
            results.Add(new Candidate
            {
                IsNormalRoomFire = true,
                Position = normalPosition,
                Rotation = normalRotation,
                Weight = Mathf.Max(0.01f, area.NormalRoomFireWeight),
                Label = "NormalRoomFire"
            });
        }

        return results;
    }

    private static string ExtractAreaHint(string fireOrigin)
    {
        if (string.IsNullOrWhiteSpace(fireOrigin))
        {
            return string.Empty;
        }

        string trimmed = fireOrigin.Trim();
        int separatorIndex = trimmed.IndexOf('_');
        return separatorIndex > 0 ? trimmed.Substring(0, separatorIndex) : trimmed;
    }

    private sealed class Candidate
    {
        public IncidentPossibleFireCause Cause { get; set; }
        public bool IsNormalRoomFire { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public float Weight { get; set; }
        public string Label { get; set; }
    }
}
