using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class IncidentOriginArea : MonoBehaviour
{
    [Header("Matching")]
    [SerializeField] private string areaKey = string.Empty;
    [SerializeField] private string[] fireOriginHintKeys = Array.Empty<string>();
    [SerializeField] private bool isDefaultArea;

    [Header("Area")]
    [FormerlySerializedAs("areaVolume")]
    [SerializeField] private Collider areaVolume;
    [SerializeField] private Vector3 fallbackAreaSize = new Vector3(4f, 2.5f, 4f);

    [Header("Candidates")]
    [SerializeField] private IncidentPossibleFireCause[] explicitCauses = Array.Empty<IncidentPossibleFireCause>();
    [SerializeField] private bool collectCausesFromChildren = true;
    [SerializeField] [Min(0f)] private float normalRoomFireWeight = 1f;

    [Header("Compatibility")]
    [SerializeField] private IncidentPayloadAnchor legacyAnchor;

    public string EffectiveAreaKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(areaKey))
            {
                return areaKey.Trim();
            }

            ResolveLegacyAnchor();
            if (legacyAnchor != null && !string.IsNullOrWhiteSpace(legacyAnchor.LogicalLocationKey))
            {
                return legacyAnchor.LogicalLocationKey;
            }

            return name;
        }
    }

    public bool IsDefaultArea => isDefaultArea;
    public IncidentPayloadAnchor LegacyAnchor => legacyAnchor;
    public float NormalRoomFireWeight => Mathf.Max(0f, normalRoomFireWeight);
    public LayerMask SurfacePlacementMask => legacyAnchor != null ? legacyAnchor.FirePlacementMask : ~0;
    public QueryTriggerInteraction SurfacePlacementTriggerInteraction =>
        legacyAnchor != null ? legacyAnchor.FirePlacementTriggerInteraction : QueryTriggerInteraction.Ignore;
    public float SurfaceOffset => legacyAnchor != null ? legacyAnchor.SurfaceOffset : 0.03f;
    public bool HasConfiguredHazardIsolationDevices => legacyAnchor != null && legacyAnchor.HasConfiguredHazardIsolationDevices;
    public float FloorPlacementWeight => ResolvePlacementSurfaceWeight(profile => profile.FloorPlacementWeight, 0.65f);
    public float WallPlacementWeight => ResolvePlacementSurfaceWeight(profile => profile.WallPlacementWeight, 1.35f);
    public float CeilingPlacementWeight => ResolvePlacementSurfaceWeight(profile => profile.CeilingPlacementWeight, 0.2f);
    public Collider AreaCollider
    {
        get
        {
            ResolveAreaVolume();
            return areaVolume;
        }
    }

    private void Reset()
    {
        ResolveLegacyAnchor();
        ResolveAreaVolume();
    }

    private void OnValidate()
    {
        ResolveLegacyAnchor();
        ResolveAreaVolume();
    }

    public bool MatchesFireOriginHint(string fireOrigin)
    {
        if (string.IsNullOrWhiteSpace(fireOrigin))
        {
            return false;
        }

        ResolveLegacyAnchor();
        string normalizedOrigin = Normalize(fireOrigin);
        if (legacyAnchor != null && normalizedOrigin == Normalize(legacyAnchor.FireOriginKey))
        {
            return true;
        }

        for (int i = 0; i < fireOriginHintKeys.Length; i++)
        {
            if (normalizedOrigin == Normalize(fireOriginHintKeys[i]))
            {
                return true;
            }
        }

        return false;
    }

    public bool MatchesAreaKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return Normalize(EffectiveAreaKey) == Normalize(key);
    }

    public Bounds GetAreaBounds()
    {
        ResolveAreaVolume();
        if (areaVolume != null)
        {
            return areaVolume.bounds;
        }

        ResolveLegacyAnchor();
        Vector3 size = fallbackAreaSize;
        if (legacyAnchor != null)
        {
            size = Vector3.Max(size, legacyAnchor.RuntimeZoneSize);
        }

        return new Bounds(transform.position, Vector3.Max(size, Vector3.one));
    }

    public Vector3 GetAreaCenter()
    {
        return GetAreaBounds().center;
    }

    public bool ContainsWorldPosition(Vector3 worldPosition)
    {
        ResolveAreaVolume();
        if (areaVolume == null)
        {
            return GetAreaBounds().Contains(worldPosition);
        }

        Vector3 closestPoint = areaVolume.ClosestPoint(worldPosition);
        return (closestPoint - worldPosition).sqrMagnitude <= 0.0001f;
    }

    public IncidentPossibleFireCause[] CollectPossibleCauses(bool includeInactive)
    {
        List<IncidentPossibleFireCause> results = new List<IncidentPossibleFireCause>();
        HashSet<IncidentPossibleFireCause> seen = new HashSet<IncidentPossibleFireCause>();

        if (explicitCauses != null)
        {
            for (int i = 0; i < explicitCauses.Length; i++)
            {
                IncidentPossibleFireCause cause = explicitCauses[i];
                if (cause == null || !seen.Add(cause))
                {
                    continue;
                }

                results.Add(cause);
            }
        }

        if (collectCausesFromChildren)
        {
            IncidentPossibleFireCause[] childCauses = GetComponentsInChildren<IncidentPossibleFireCause>(includeInactive);
            for (int i = 0; i < childCauses.Length; i++)
            {
                IncidentPossibleFireCause cause = childCauses[i];
                if (cause == null || !seen.Add(cause))
                {
                    continue;
                }

                results.Add(cause);
            }
        }

        return results.ToArray();
    }

    public bool ApplyPayload(
        IncidentWorldSetupPayload payload,
        IncidentFirePrefabLibrary firePrefabLibrary,
        IncidentFireSpawnProfile fireSpawnProfile,
        FireSimulationManager fireSimulationManager,
        ResolvedIgnitionSource source)
    {
        ResolveLegacyAnchor();
        if (legacyAnchor == null || payload == null || source == null)
        {
            return false;
        }

        return legacyAnchor.ApplyPayloadFromResolvedSource(
            payload,
            firePrefabLibrary,
            fireSpawnProfile,
            fireSimulationManager,
            source.Position,
            source.Rotation);
    }

    public void SetAreaVolume(Collider volume)
    {
        areaVolume = volume;
    }

    private void ResolveLegacyAnchor()
    {
        if (legacyAnchor == null)
        {
            legacyAnchor = GetComponent<IncidentPayloadAnchor>();
        }
    }

    private void ResolveAreaVolume()
    {
        if (areaVolume == null)
        {
            areaVolume = GetComponent<Collider>();
        }
    }

    private float ResolvePlacementSurfaceWeight(Func<IncidentFireSpawnProfile, float> selector, float fallback)
    {
        if (selector == null)
        {
            return Mathf.Max(0.01f, fallback);
        }

        IncidentMapSetupRoot setupRoot = GetComponentInParent<IncidentMapSetupRoot>(true);
        IncidentFireSpawnProfile profile = setupRoot != null ? setupRoot.FireSpawnProfile : null;
        return profile != null
            ? Mathf.Max(0.01f, selector(profile))
            : Mathf.Max(0.01f, fallback);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }
}
