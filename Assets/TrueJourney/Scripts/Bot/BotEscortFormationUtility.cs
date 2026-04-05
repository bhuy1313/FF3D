using System;
using System.Collections.Generic;
using UnityEngine;

public static class BotEscortFormationUtility
{
    public static int ResolveFormationRank(EntityId ownerId, IReadOnlyList<EntityId> followerIds)
    {
        if (followerIds == null || followerIds.Count == 0)
        {
            return 0;
        }

        EntityId[] sortedIds = new EntityId[followerIds.Count];
        for (int i = 0; i < followerIds.Count; i++)
        {
            sortedIds[i] = followerIds[i];
        }

        Array.Sort(sortedIds);
        for (int i = 0; i < sortedIds.Length; i++)
        {
            if (sortedIds[i] == ownerId)
            {
                return i;
            }
        }

        return 0;
    }

    public static int FillLocalOffsets(Vector3 preferredOffset, float followDistance, Vector3[] buffer)
    {
        if (buffer == null || buffer.Length < 5)
        {
            return 0;
        }

        float absX = Mathf.Max(Mathf.Abs(preferredOffset.x), 0.75f);
        float backZ = preferredOffset.z <= -0.1f
            ? preferredOffset.z
            : -Mathf.Max(followDistance * 0.85f, 1f);
        float centerBackZ = Mathf.Min(backZ * 1.2f, -Mathf.Max(followDistance, 1.25f));
        float wideX = absX * 1.65f;
        float sideZ = backZ * 0.9f;
        float preferredSign = preferredOffset.x < 0f ? -1f : 1f;

        buffer[0] = new Vector3(absX * preferredSign, 0f, backZ);
        buffer[1] = new Vector3(-absX * preferredSign, 0f, backZ);
        buffer[2] = new Vector3(0f, 0f, centerBackZ);
        buffer[3] = new Vector3(wideX * preferredSign, 0f, sideZ);
        buffer[4] = new Vector3(-wideX * preferredSign, 0f, sideZ);
        return 5;
    }

    public static int ResolvePreferredSlotIndex(int formationRank, int slotCount)
    {
        if (slotCount <= 0)
        {
            return -1;
        }

        return Mathf.Abs(formationRank) % slotCount;
    }

    public static int ResolveSlotIndex(
        Vector3 ownerPosition,
        Vector3 targetPosition,
        Quaternion targetRotation,
        IReadOnlyList<Vector3> localOffsets,
        int slotCount,
        int preferredSlotIndex,
        int currentSlotIndex,
        float slotPreferenceBias,
        IReadOnlyList<int> occupiedSlotIndices,
        int occupiedSlotCount)
    {
        if (localOffsets == null || slotCount <= 0)
        {
            return -1;
        }

        slotCount = Mathf.Min(slotCount, localOffsets.Count);
        if (slotCount <= 0)
        {
            return -1;
        }

        preferredSlotIndex = Mathf.Clamp(preferredSlotIndex, 0, slotCount - 1);
        float preferenceBias = Mathf.Max(0f, slotPreferenceBias);
        float currentSlotBonus = preferenceBias * 1.25f;
        float occupiedSlotPenalty = Mathf.Max(2.5f, preferenceBias * 3.5f + 1.5f);
        float bestScore = float.MaxValue;
        int bestIndex = preferredSlotIndex;

        for (int i = 0; i < slotCount; i++)
        {
            Vector3 worldPosition = targetPosition + (targetRotation * localOffsets[i]);
            float score = GetHorizontalDistance(ownerPosition, worldPosition);
            score += GetPreferenceOrder(preferredSlotIndex, i, slotCount) * preferenceBias;
            if (i != currentSlotIndex)
            {
                score += GetOccupiedSlotCount(occupiedSlotIndices, occupiedSlotCount, i) * occupiedSlotPenalty;
            }

            if (i == currentSlotIndex)
            {
                score -= currentSlotBonus;
            }

            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestIndex = i;
        }

        return bestIndex;
    }

    public static int CountOccupiedSlot(IReadOnlyList<int> occupiedSlotIndices, int occupiedSlotCount, int slotIndex)
    {
        return GetOccupiedSlotCount(occupiedSlotIndices, occupiedSlotCount, slotIndex);
    }

    private static int GetPreferenceOrder(int preferredSlotIndex, int candidateIndex, int slotCount)
    {
        if (slotCount <= 0)
        {
            return 0;
        }

        int order = (candidateIndex - preferredSlotIndex) % slotCount;
        return order < 0 ? order + slotCount : order;
    }

    private static int GetOccupiedSlotCount(IReadOnlyList<int> occupiedSlotIndices, int occupiedSlotCount, int slotIndex)
    {
        if (occupiedSlotIndices == null || occupiedSlotCount <= 0)
        {
            return 0;
        }

        int count = 0;
        int limit = Mathf.Min(occupiedSlotCount, occupiedSlotIndices.Count);
        for (int i = 0; i < limit; i++)
        {
            if (occupiedSlotIndices[i] == slotIndex)
            {
                count++;
            }
        }

        return count;
    }

    private static float GetHorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
