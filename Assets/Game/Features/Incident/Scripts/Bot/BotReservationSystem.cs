using System.Collections.Generic;
using UnityEngine;

public sealed class BotReservationSystem
{
    private sealed class ReservationRecord
    {
        public EntityId OwnerEntityId;
        public string OwnerName;
        public BotTaskType TaskType;
        public float ExpiresAtTime;
    }

    private readonly Dictionary<object, ReservationRecord> reservations = new Dictionary<object, ReservationRecord>();

    public bool TryReserve(object target, GameObject owner, BotTaskType taskType, float durationSeconds)
    {
        if (target == null || owner == null)
        {
            return false;
        }

        CleanupExpiredReservations();

        EntityId ownerEntityId = owner.GetEntityId();
        float expiresAt = ResolveExpirationTime(durationSeconds);
        if (reservations.TryGetValue(target, out ReservationRecord existingRecord))
        {
            if (existingRecord.OwnerEntityId != ownerEntityId && !IsExpired(existingRecord))
            {
                return false;
            }

            existingRecord.OwnerEntityId = ownerEntityId;
            existingRecord.OwnerName = owner.name;
            existingRecord.TaskType = taskType;
            existingRecord.ExpiresAtTime = expiresAt;
            return true;
        }

        reservations[target] = new ReservationRecord
        {
            OwnerEntityId = ownerEntityId,
            OwnerName = owner.name,
            TaskType = taskType,
            ExpiresAtTime = expiresAt
        };
        return true;
    }

    public void RefreshReservation(object target, GameObject owner, float durationSeconds)
    {
        if (target == null || owner == null)
        {
            return;
        }

        CleanupExpiredReservations();
        if (!reservations.TryGetValue(target, out ReservationRecord existingRecord))
        {
            return;
        }

        if (existingRecord.OwnerEntityId != owner.GetEntityId())
        {
            return;
        }

        existingRecord.ExpiresAtTime = ResolveExpirationTime(durationSeconds);
    }

    public void Release(object target, GameObject owner)
    {
        if (target == null || owner == null)
        {
            return;
        }

        if (!reservations.TryGetValue(target, out ReservationRecord existingRecord))
        {
            return;
        }

        if (existingRecord.OwnerEntityId == owner.GetEntityId())
        {
            reservations.Remove(target);
        }
    }

    public void ReleaseAllOwnedBy(GameObject owner)
    {
        if (owner == null || reservations.Count == 0)
        {
            return;
        }

        EntityId ownerEntityId = owner.GetEntityId();
        List<object> toRemove = null;
        foreach (KeyValuePair<object, ReservationRecord> pair in reservations)
        {
            if (pair.Value == null || pair.Value.OwnerEntityId != ownerEntityId)
            {
                continue;
            }

            toRemove ??= new List<object>();
            toRemove.Add(pair.Key);
        }

        if (toRemove == null)
        {
            return;
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            reservations.Remove(toRemove[i]);
        }
    }

    public bool IsReservedByOther(object target, GameObject requester)
    {
        if (target == null || requester == null)
        {
            return false;
        }

        CleanupExpiredReservations();
        if (!reservations.TryGetValue(target, out ReservationRecord record) || record == null)
        {
            return false;
        }

        return record.OwnerEntityId != requester.GetEntityId();
    }

    private void CleanupExpiredReservations()
    {
        if (reservations.Count == 0 || !Application.isPlaying)
        {
            return;
        }

        List<object> toRemove = null;
        foreach (KeyValuePair<object, ReservationRecord> pair in reservations)
        {
            if (!IsExpired(pair.Value))
            {
                continue;
            }

            toRemove ??= new List<object>();
            toRemove.Add(pair.Key);
        }

        if (toRemove == null)
        {
            return;
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            reservations.Remove(toRemove[i]);
        }
    }

    private static bool IsExpired(ReservationRecord record)
    {
        return record == null || (Application.isPlaying && record.ExpiresAtTime > 0f && Time.time > record.ExpiresAtTime);
    }

    private static float ResolveExpirationTime(float durationSeconds)
    {
        if (!Application.isPlaying)
        {
            return 0f;
        }

        return Time.time + Mathf.Max(0.05f, durationSeconds);
    }
}
