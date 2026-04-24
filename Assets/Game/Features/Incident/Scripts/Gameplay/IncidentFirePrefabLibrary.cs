using UnityEngine;

[DisallowMultipleComponent]
public class IncidentFirePrefabLibrary : MonoBehaviour
{
    [Header("Fire Prefabs")]
    [SerializeField] private Fire ordinaryFirePrefab;
    [SerializeField] private Fire electricalFirePrefab;
    [SerializeField] private Fire gasFirePrefab;
    [SerializeField] private Fire flammableLiquidFirePrefab;

    public Fire ResolvePrefab(FireHazardType type)
    {
        return type switch
        {
            FireHazardType.Electrical => electricalFirePrefab != null ? electricalFirePrefab : ResolveOrdinaryPrefab(),
            FireHazardType.GasFed => gasFirePrefab != null ? gasFirePrefab : ResolveOrdinaryPrefab(),
            FireHazardType.FlammableLiquid => flammableLiquidFirePrefab != null ? flammableLiquidFirePrefab : ResolveOrdinaryPrefab(),
            _ => ResolveOrdinaryPrefab()
        };
    }

    public Fire ResolveOrdinaryPrefab()
    {
        return ordinaryFirePrefab;
    }
}
