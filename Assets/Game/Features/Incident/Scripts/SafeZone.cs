using System.Collections.Generic;
using TrueJourney.BotBehavior;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class SafeZone : MonoBehaviour, ISafeZoneTarget, IInteractable
{
    private enum SlotState
    {
        Free,
        Claimed,
        Occupied
    }

    [Header("Zone")]
    [SerializeField] private Collider zoneCollider;
    [SerializeField] private float fallbackRadius = 2f;

    [Header("Drop Point")]
    [SerializeField] private Transform dropPoint;
    [FormerlySerializedAs("slotPoints")]
    [SerializeField, HideInInspector] private List<Transform> legacySlotPoints = new List<Transform>();
    [SerializeField] private bool occupiedSlotBlocksClaims = false;

    private SlotState slotState;
    private GameObject slotClaimer;

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

    public bool HasAvailableSlot()
    {
        if (dropPoint == null)
        {
            return true;
        }

        return IsSlotClaimable();
    }

    public bool TryClaimSlot(GameObject claimer, out Vector3 slotPosition)
    {
        slotPosition = GetWorldPosition();

        if (dropPoint == null)
        {
            return true;
        }

        ReleaseSlotInternal(claimer);
        if (!IsSlotClaimable())
        {
            return false;
        }

        slotState = SlotState.Claimed;
        slotClaimer = claimer;
        slotPosition = dropPoint.position;
        return true;
    }

    public void ReleaseSlot(GameObject claimer)
    {
        if (dropPoint == null)
        {
            return;
        }

        ReleaseSlotInternal(claimer);
    }

    public void OccupySlotAt(Vector3 position)
    {
        if (dropPoint == null || !IsDropPointMatch(position))
        {
            return;
        }

        slotState = SlotState.Occupied;
        slotClaimer = null;
    }

    public void ReleaseOccupiedSlotAt(Vector3 position)
    {
        if (dropPoint == null || slotState != SlotState.Occupied || !IsDropPointMatch(position))
        {
            return;
        }

        slotState = SlotState.Free;
        slotClaimer = null;
    }

    public Quaternion GetSlotRotation(Vector3 slotPosition)
    {
        if (dropPoint == null || !IsDropPointMatch(slotPosition))
        {
            return Quaternion.identity;
        }

        RescuedSlotPoseProfile profile = dropPoint.GetComponent<RescuedSlotPoseProfile>();
        return profile != null ? profile.Rotation : Quaternion.identity;
    }

    public void Interact(GameObject interactor)
    {
    }

    private void OnEnable()
    {
        ResolveZoneCollider();
        MigrateLegacySlotPointIfNeeded();
        BotRuntimeRegistry.RegisterSafeZone(this);
    }

    private void OnDisable()
    {
        BotRuntimeRegistry.UnregisterSafeZone(this);
    }

    private void OnValidate()
    {
        ResolveZoneCollider();
        MigrateLegacySlotPointIfNeeded();
    }

    private void ResolveZoneCollider()
    {
        if (zoneCollider == null)
        {
            zoneCollider = GetComponent<Collider>();
        }
    }

    private void MigrateLegacySlotPointIfNeeded()
    {
        if (dropPoint != null || legacySlotPoints == null || legacySlotPoints.Count == 0)
        {
            return;
        }

        for (int i = 0; i < legacySlotPoints.Count; i++)
        {
            if (legacySlotPoints[i] != null)
            {
                dropPoint = legacySlotPoints[i];
                return;
            }
        }
    }

    private void ReleaseSlotInternal(GameObject claimer)
    {
        if (claimer == null || slotClaimer != claimer)
        {
            return;
        }

        slotState = SlotState.Free;
        slotClaimer = null;
    }

    private bool IsSlotClaimable()
    {
        if (slotState == SlotState.Free)
        {
            return true;
        }

        return slotState == SlotState.Occupied && !occupiedSlotBlocksClaims;
    }

    private bool IsDropPointMatch(Vector3 position)
    {
        return (dropPoint.position - position).sqrMagnitude <= 0.25f;
    }
}
