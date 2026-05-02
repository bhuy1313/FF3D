using UnityEngine;

[DisallowMultipleComponent]
public class IncidentFirePrefabLibrary : MonoBehaviour
{
    [Header("Fire Prefabs")]
    [SerializeField] private GameObject ordinaryFirePrefab;
    [SerializeField] private GameObject electricalFirePrefab;
    [SerializeField] private GameObject gasFirePrefab;
    [SerializeField] private GameObject flammableLiquidFirePrefab;

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (ordinaryFirePrefab != null || electricalFirePrefab != null || gasFirePrefab != null || flammableLiquidFirePrefab != null)
        {
            Debug.LogWarning(
                $"{nameof(IncidentFirePrefabLibrary)} on '{name}' references legacy Fire prefabs. Prefer node-based fire simulation authoring for new content.",
                this);
        }
#endif
    }

    public GameObject ResolvePrefab(FireHazardType type)
    {
        return type switch
        {
            FireHazardType.Electrical => electricalFirePrefab != null ? electricalFirePrefab : ResolveOrdinaryPrefab(),
            FireHazardType.GasFed => gasFirePrefab != null ? gasFirePrefab : ResolveOrdinaryPrefab(),
            FireHazardType.FlammableLiquid => flammableLiquidFirePrefab != null ? flammableLiquidFirePrefab : ResolveOrdinaryPrefab(),
            _ => ResolveOrdinaryPrefab()
        };
    }

    public GameObject ResolveOrdinaryPrefab()
    {
        return ordinaryFirePrefab;
    }
}
