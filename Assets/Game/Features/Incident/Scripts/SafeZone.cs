using System.Collections.Generic;
using TrueJourney.BotBehavior;
using UnityEngine;

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
    [SerializeField] private Transform dropPoint;
    [SerializeField] private Collider zoneCollider;
    [SerializeField] private float fallbackRadius = 2f;

    [Header("Slots")]
    [SerializeField] private List<Transform> slotPoints = new List<Transform>();

    private SlotState[] slotStates;
    private GameObject[] slotClaimers;

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
        if (slotPoints == null || slotPoints.Count == 0)
        {
            return true;
        }

        EnsureSlotArrays();
        for (int i = 0; i < slotStates.Length; i++)
        {
            if (slotStates[i] == SlotState.Free)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryClaimSlot(GameObject claimer, out Vector3 slotPosition)
    {
        slotPosition = GetWorldPosition();

        if (slotPoints == null || slotPoints.Count == 0)
        {
            return true;
        }

        EnsureSlotArrays();

        // Release any previous claim by this claimer first.
        ReleaseSlotInternal(claimer);

        int bestIndex = -1;
        float bestDistance = float.MaxValue;
        Vector3 claimerPosition = claimer != null ? claimer.transform.position : GetWorldPosition();

        for (int i = 0; i < slotStates.Length; i++)
        {
            if (slotStates[i] != SlotState.Free)
            {
                continue;
            }

            Transform slotTransform = slotPoints[i];
            if (slotTransform == null)
            {
                continue;
            }

            float distance = Vector3.Distance(claimerPosition, slotTransform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            return false;
        }

        slotStates[bestIndex] = SlotState.Claimed;
        slotClaimers[bestIndex] = claimer;
        slotPosition = slotPoints[bestIndex].position;
        return true;
    }

    public void ReleaseSlot(GameObject claimer)
    {
        if (slotPoints == null || slotPoints.Count == 0)
        {
            return;
        }

        EnsureSlotArrays();
        ReleaseSlotInternal(claimer);
    }

    public void OccupySlotAt(Vector3 position)
    {
        if (slotPoints == null || slotPoints.Count == 0)
        {
            return;
        }

        EnsureSlotArrays();

        int bestIndex = -1;
        float bestDistanceSq = float.MaxValue;

        for (int i = 0; i < slotStates.Length; i++)
        {
            Transform slotTransform = slotPoints[i];
            if (slotTransform == null)
            {
                continue;
            }

            float distanceSq = (slotTransform.position - position).sqrMagnitude;
            if (distanceSq < bestDistanceSq)
            {
                bestDistanceSq = distanceSq;
                bestIndex = i;
            }
        }

        if (bestIndex >= 0)
        {
            slotStates[bestIndex] = SlotState.Occupied;
            slotClaimers[bestIndex] = null;
        }
    }

    public Quaternion GetSlotRotation(Vector3 slotPosition)
    {
        if (slotPoints == null || slotPoints.Count == 0)
        {
            return Quaternion.identity;
        }

        EnsureSlotArrays();

        int bestIndex = -1;
        float bestDistanceSq = float.MaxValue;

        for (int i = 0; i < slotStates.Length; i++)
        {
            Transform slotTransform = slotPoints[i];
            if (slotTransform == null)
            {
                continue;
            }

            float distanceSq = (slotTransform.position - slotPosition).sqrMagnitude;
            if (distanceSq < bestDistanceSq)
            {
                bestDistanceSq = distanceSq;
                bestIndex = i;
            }
        }

        if (bestIndex < 0 || slotPoints[bestIndex] == null)
        {
            return Quaternion.identity;
        }

        RescuedSlotPoseProfile profile = slotPoints[bestIndex].GetComponent<RescuedSlotPoseProfile>();
        return profile != null ? profile.Rotation : Quaternion.identity;
    }

    public void Interact(GameObject interactor)
    {
    }

    private void OnEnable()
    {
        ResolveZoneCollider();
        EnsureSlotArrays();
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

    private void EnsureSlotArrays()
    {
        int count = slotPoints != null ? slotPoints.Count : 0;
        if (count == 0)
        {
            slotStates = null;
            slotClaimers = null;
            return;
        }

        if (slotStates != null && slotStates.Length == count)
        {
            return;
        }

        slotStates = new SlotState[count];
        slotClaimers = new GameObject[count];
    }

    private void ReleaseSlotInternal(GameObject claimer)
    {
        if (claimer == null || slotClaimers == null)
        {
            return;
        }

        for (int i = 0; i < slotClaimers.Length; i++)
        {
            if (slotClaimers[i] == claimer)
            {
                slotStates[i] = SlotState.Free;
                slotClaimers[i] = null;
            }
        }
    }
}
