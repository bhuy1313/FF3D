using TrueJourney.BotBehavior;
using UnityEngine;

[DisallowMultipleComponent]
public class SafeZone : MonoBehaviour, ISafeZoneTarget
{
    [Header("Zone")]
    [SerializeField] private Transform dropPoint;
    [SerializeField] private Collider zoneCollider;
    [SerializeField] private float fallbackRadius = 2f;

    public Vector3 GetWorldPosition()
    {
        return dropPoint != null ? dropPoint.position : transform.position;
    }

    public Vector3 GetDropPoint(Vector3 fallbackPosition)
    {
        return dropPoint != null ? dropPoint.position : fallbackPosition;
    }

    public bool ContainsPoint(Vector3 worldPosition)
    {
        if (zoneCollider != null)
        {
            Vector3 closestPoint = zoneCollider.bounds.ClosestPoint(worldPosition);
            Vector2 delta = new Vector2(closestPoint.x - worldPosition.x, closestPoint.z - worldPosition.z);
            return delta.sqrMagnitude <= 0.01f;
        }

        Vector3 zonePosition = GetWorldPosition();
        return Vector3.Distance(
            new Vector3(zonePosition.x, 0f, zonePosition.z),
            new Vector3(worldPosition.x, 0f, worldPosition.z)) <= Mathf.Max(0.1f, fallbackRadius);
    }

    private void OnEnable()
    {
        ResolveZoneCollider();
        BotRuntimeRegistry.RegisterSafeZone(this);
    }

    private void OnDisable()
    {
        BotRuntimeRegistry.UnregisterSafeZone(this);
    }

    private void OnValidate()
    {
        ResolveZoneCollider();
    }

    private void ResolveZoneCollider()
    {
        if (zoneCollider == null)
        {
            zoneCollider = GetComponent<Collider>();
        }
    }
}
