using System.Collections.Generic;
using TrueJourney.BotBehavior;
using UnityEngine;

public sealed class BotRuntimeDecisionService
{
    private readonly List<int> escortFollowerIds = new List<int>(8);

    public Transform ResolveFollowTarget(Transform currentTarget, string followTargetTag)
    {
        if (currentTarget != null && currentTarget.gameObject.activeInHierarchy)
        {
            return currentTarget;
        }

        if (string.IsNullOrWhiteSpace(followTargetTag))
        {
            return null;
        }

        GameObject targetObject = GameObject.FindGameObjectWithTag(followTargetTag);
        return targetObject != null ? targetObject.transform : null;
    }

    public int ResolveEscortFormationRank(BotCommandAgent owner, Transform target)
    {
        if (owner == null || target == null)
        {
            return 0;
        }

        escortFollowerIds.Clear();
        foreach (BotCommandAgent candidate in BotRuntimeRegistry.ActiveCommandAgents)
        {
            if (candidate == null || !candidate.isActiveAndEnabled || !candidate.TryGetFollowOrderSnapshot(out BotFollowOrder followOrder))
            {
                continue;
            }

            if (followOrder.Mode != BotFollowMode.Escort)
            {
                continue;
            }

            Transform candidateTarget = ResolveFollowTarget(
                followOrder.Target != null ? followOrder.Target : candidate.CurrentFollowTarget,
                followOrder.TargetTag);
            if (candidateTarget != target)
            {
                continue;
            }

            escortFollowerIds.Add(candidate.GetInstanceID());
        }

        if (escortFollowerIds.Count == 0)
        {
            escortFollowerIds.Add(owner.GetInstanceID());
        }

        return BotEscortFormationUtility.ResolveFormationRank(owner.GetInstanceID(), escortFollowerIds);
    }

    public int FillOccupiedEscortSlots(BotCommandAgent owner, Transform target, int[] buffer, int slotCount)
    {
        if (owner == null || target == null || buffer == null || slotCount <= 0)
        {
            return 0;
        }

        int count = 0;
        int maxCount = Mathf.Min(slotCount, buffer.Length);
        foreach (BotCommandAgent candidate in BotRuntimeRegistry.ActiveCommandAgents)
        {
            if (candidate == null || candidate == owner || !candidate.isActiveAndEnabled || !candidate.TryGetFollowOrderSnapshot(out BotFollowOrder followOrder))
            {
                continue;
            }

            if (followOrder.Mode != BotFollowMode.Escort)
            {
                continue;
            }

            Transform candidateTarget = ResolveFollowTarget(
                followOrder.Target != null ? followOrder.Target : candidate.CurrentFollowTarget,
                followOrder.TargetTag);
            if (candidateTarget != target)
            {
                continue;
            }

            int occupiedSlotIndex = candidate.CurrentEscortSlotIndex;
            if (occupiedSlotIndex < 0 || occupiedSlotIndex >= slotCount || count >= maxCount)
            {
                continue;
            }

            buffer[count++] = occupiedSlotIndex;
        }

        return count;
    }

    public IRescuableTarget ResolveRescueTarget(Vector3 orderPoint, IRescuableTarget currentTarget, GameObject requester, float rescueSearchRadius)
    {
        IRescuableTarget committedTarget = GetCommittedRescueTarget(currentTarget, requester);
        if (committedTarget != null)
        {
            return committedTarget;
        }

        IRescuableTarget bestTarget = null;
        float bestDistance = float.MaxValue;
        float bestPriority = float.NegativeInfinity;

        if (IsEligibleRescueTarget(currentTarget, requester, orderPoint, rescueSearchRadius))
        {
            bestTarget = currentTarget;
            bestDistance = GetHorizontalDistance(orderPoint, currentTarget.GetWorldPosition());
            bestPriority = currentTarget.RescuePriority;
        }

        foreach (IRescuableTarget candidate in BotRuntimeRegistry.ActiveRescuableTargets)
        {
            if (!IsEligibleRescueTarget(candidate, requester, orderPoint, rescueSearchRadius))
            {
                continue;
            }

            float candidateDistance = GetHorizontalDistance(orderPoint, candidate.GetWorldPosition());
            float candidatePriority = candidate.RescuePriority;
            bool isBetterPriority = candidatePriority > bestPriority;
            bool isTieBreakerDistance = Mathf.Approximately(candidatePriority, bestPriority) && candidateDistance < bestDistance;
            if (!isBetterPriority && !isTieBreakerDistance)
            {
                continue;
            }

            bestDistance = candidateDistance;
            bestPriority = candidatePriority;
            bestTarget = candidate;
        }

        return bestTarget;
    }

    public ISafeZoneTarget ResolveNearestSafeZone(Vector3 fromPosition, ISafeZoneTarget currentTarget)
    {
        if (currentTarget != null)
        {
            return currentTarget;
        }

        ISafeZoneTarget bestTarget = null;
        float bestDistance = float.MaxValue;

        foreach (ISafeZoneTarget candidate in BotRuntimeRegistry.ActiveSafeZones)
        {
            if (candidate == null)
            {
                continue;
            }

            float distance = GetHorizontalDistance(fromPosition, candidate.GetWorldPosition());
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestTarget = candidate;
        }

        return bestTarget;
    }

    private static IRescuableTarget GetCommittedRescueTarget(IRescuableTarget currentTarget, GameObject requester)
    {
        if (currentTarget == null || !currentTarget.NeedsRescue)
        {
            return null;
        }

        if (currentTarget.ActiveRescuer != requester)
        {
            return null;
        }

        if (!currentTarget.IsCarried && !currentTarget.IsRescueInProgress)
        {
            return null;
        }

        return currentTarget;
    }

    private static bool IsEligibleRescueTarget(IRescuableTarget candidate, GameObject requester, Vector3 orderPoint, float rescueSearchRadius)
    {
        if (candidate == null || !candidate.NeedsRescue)
        {
            return false;
        }

        if (candidate.IsRescueInProgress && candidate.ActiveRescuer != requester)
        {
            return false;
        }

        return GetHorizontalDistance(orderPoint, candidate.GetWorldPosition()) <= rescueSearchRadius;
    }

    private static float GetHorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
