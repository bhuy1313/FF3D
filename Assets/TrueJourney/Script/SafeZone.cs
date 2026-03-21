using TrueJourney.BotBehavior;
using UnityEngine;

[DisallowMultipleComponent]
public class SafeZone : MonoBehaviour, ISafeZoneTarget
{
    [Header("Zone")]
    [SerializeField] private Transform dropPoint;

    public Vector3 GetWorldPosition()
    {
        return dropPoint != null ? dropPoint.position : transform.position;
    }

    private void OnEnable()
    {
        BotRuntimeRegistry.RegisterSafeZone(this);
    }

    private void OnDisable()
    {
        BotRuntimeRegistry.UnregisterSafeZone(this);
    }
}
