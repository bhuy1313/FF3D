using System;
using UnityEngine;

[DisallowMultipleComponent]
public class IncidentPossibleFireCause : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string causeKey = "GenericCause";
    [SerializeField] private Transform ignitionPoint;
    [SerializeField] private bool isEnabled = true;

    [Header("Weight")]
    [SerializeField] [Min(0f)] private float baseWeight = 1f;

    [Header("Matching")]
    [SerializeField] private FireHazardType[] supportedHazards = Array.Empty<FireHazardType>();
    [SerializeField] private string[] supportedAreaKeys = Array.Empty<string>();
    [SerializeField] private string[] supportedOriginHints = Array.Empty<string>();

    public string CauseKey => string.IsNullOrWhiteSpace(causeKey) ? name : causeKey.Trim();
    public bool IsEnabled => isEnabled;
    public float BaseWeight => Mathf.Max(0f, baseWeight);

    public bool IsCompatible(IncidentWorldSetupPayload payload, IncidentOriginArea area)
    {
        if (!isEnabled)
        {
            return false;
        }

        if (payload == null || area == null)
        {
            return false;
        }

        if (!MatchesHazard(payload.hazardType))
        {
            return false;
        }

        if (!MatchesAny(area.EffectiveAreaKey, supportedAreaKeys))
        {
            return false;
        }

        if (!MatchesOriginHint(payload.fireOrigin))
        {
            return false;
        }

        return true;
    }

    public float ComputeWeight(IncidentWorldSetupPayload payload, IncidentOriginArea area)
    {
        if (!IsCompatible(payload, area))
        {
            return 0f;
        }

        float weight = Mathf.Max(0.01f, BaseWeight);
        if (MatchesHazard(payload.hazardType) && supportedHazards != null && supportedHazards.Length > 0)
        {
            weight += 3f;
        }

        if (MatchesAny(payload.fireOrigin, supportedOriginHints))
        {
            weight += 2f;
        }

        if (MatchesAny(area.EffectiveAreaKey, supportedAreaKeys) && supportedAreaKeys != null && supportedAreaKeys.Length > 0)
        {
            weight += 1f;
        }

        return weight;
    }

    public bool TryResolveIgnitionPose(out Vector3 position, out Quaternion rotation)
    {
        Transform point = ignitionPoint != null ? ignitionPoint : transform;
        position = point.position;
        rotation = point.rotation;
        return true;
    }

    private bool MatchesHazard(string payloadHazardType)
    {
        if (supportedHazards == null || supportedHazards.Length <= 0)
        {
            return true;
        }

        FireHazardType resolvedHazard = IncidentPayloadStartupTask.ResolveFireHazardType(payloadHazardType);
        for (int i = 0; i < supportedHazards.Length; i++)
        {
            if (supportedHazards[i] == resolvedHazard)
            {
                return true;
            }
        }

        return false;
    }

    private bool MatchesOriginHint(string fireOrigin)
    {
        if (supportedOriginHints == null || supportedOriginHints.Length <= 0)
        {
            return true;
        }

        return MatchesAny(fireOrigin, supportedOriginHints);
    }

    private static bool MatchesAny(string value, string[] candidates)
    {
        if (candidates == null || candidates.Length <= 0)
        {
            return true;
        }

        string normalizedValue = Normalize(value);
        if (normalizedValue.Length <= 0)
        {
            return false;
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            if (normalizedValue == Normalize(candidates[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }
}
