using System.Collections.Generic;
using TrueJourney.BotBehavior;
using UnityEngine;

public sealed class BotRuntimeDecisionService
{
    private sealed class FollowTargetLookupRecord
    {
        public int Frame;
        public Transform Target;
    }

    private readonly List<EntityId> escortFollowerIds = new List<EntityId>(8);
    private readonly Dictionary<string, FollowTargetLookupRecord> followTargetLookupCache = new Dictionary<string, FollowTargetLookupRecord>();

    public Transform ResolveFollowTarget(Transform currentTarget, string followTargetTag, BotPerceptionMemory memory = null)
    {
        if (currentTarget != null && currentTarget.gameObject.activeInHierarchy)
        {
            RememberFollowTarget(followTargetTag, currentTarget, memory);
            return currentTarget;
        }

        if (string.IsNullOrWhiteSpace(followTargetTag))
        {
            return null;
        }

        if (memory != null && memory.TryGetRecentFollowTarget(followTargetTag, out Transform rememberedTarget))
        {
            RememberFollowTarget(followTargetTag, rememberedTarget, memory);
            return rememberedTarget;
        }

        if (BotRuntimeRegistry.SharedIncidentBlackboard.TryGetRecentFollowTarget(followTargetTag, out Transform sharedTarget))
        {
            RememberFollowTarget(followTargetTag, sharedTarget, memory);
            return sharedTarget;
        }

        Transform resolvedTarget = ResolveSceneFollowTarget(followTargetTag);
        RememberFollowTarget(followTargetTag, resolvedTarget, memory);
        return resolvedTarget;
    }

    private Transform ResolveSceneFollowTarget(string followTargetTag)
    {
        if (string.IsNullOrWhiteSpace(followTargetTag))
        {
            return null;
        }

        if (followTargetLookupCache.TryGetValue(followTargetTag, out FollowTargetLookupRecord cachedRecord) &&
            cachedRecord != null &&
            cachedRecord.Frame == Time.frameCount)
        {
            return cachedRecord.Target != null && cachedRecord.Target.gameObject.activeInHierarchy
                ? cachedRecord.Target
                : null;
        }

        Transform resolvedTarget = null;
        try
        {
            GameObject targetObject = GameObject.FindGameObjectWithTag(followTargetTag);
            resolvedTarget = targetObject != null && targetObject.activeInHierarchy
                ? targetObject.transform
                : null;
        }
        catch (UnityException)
        {
            resolvedTarget = null;
        }

        followTargetLookupCache[followTargetTag] = new FollowTargetLookupRecord
        {
            Frame = Time.frameCount,
            Target = resolvedTarget
        };

        return resolvedTarget;
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

            escortFollowerIds.Add(candidate.GetEntityId());
        }

        if (escortFollowerIds.Count == 0)
        {
            escortFollowerIds.Add(owner.GetEntityId());
        }

        return BotEscortFormationUtility.ResolveFormationRank(owner.GetEntityId(), escortFollowerIds);
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
            RememberRescueTarget(requester, committedTarget);
            return committedTarget;
        }

        BotPerceptionMemory requesterMemory = requester != null ? requester.GetComponent<BotPerceptionMemory>() : null;
        IRescuableTarget bestTarget = null;
        float bestDistance = float.MaxValue;
        float bestPriority = float.NegativeInfinity;

        if (requesterMemory != null &&
            requesterMemory.TryGetBestRecentRescuable(orderPoint, rescueSearchRadius, requester, out IRescuableTarget rememberedTarget))
        {
            bestTarget = rememberedTarget;
            bestDistance = GetHorizontalDistance(orderPoint, rememberedTarget.GetWorldPosition());
            bestPriority = rememberedTarget.RescuePriority;
        }

        if (bestTarget == null &&
            BotRuntimeRegistry.SharedIncidentBlackboard.TryGetBestRecentRescueTarget(orderPoint, rescueSearchRadius, requester, out IRescuableTarget sharedTarget))
        {
            bestTarget = sharedTarget;
            bestDistance = GetHorizontalDistance(orderPoint, sharedTarget.GetWorldPosition());
            bestPriority = sharedTarget.RescuePriority;
        }

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

        RememberRescueTarget(requester, bestTarget);
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

    public IBotHazardIsolationTarget ResolveNearestHazardIsolationTarget(Vector3 fromPosition, GameObject requester, FireHazardType hazardType, float searchRadius)
    {
        BotPerceptionMemory requesterMemory = requester != null ? requester.GetComponent<BotPerceptionMemory>() : null;
        if (requesterMemory != null &&
            requesterMemory.TryGetNearestRecentHazardIsolationTarget(fromPosition, searchRadius, hazardType, out IBotHazardIsolationTarget rememberedTarget))
        {
            RememberHazardIsolationTarget(requesterMemory, rememberedTarget);
            return rememberedTarget;
        }

        if (BotRuntimeRegistry.SharedIncidentBlackboard.TryGetNearestRecentHazardIsolationTarget(fromPosition, searchRadius, hazardType, out IBotHazardIsolationTarget sharedTarget))
        {
            RememberHazardIsolationTarget(requesterMemory, sharedTarget);
            return sharedTarget;
        }

        IBotHazardIsolationTarget bestTarget = null;
        float bestDistanceSq = float.PositiveInfinity;
        float searchRadiusSq = Mathf.Max(0.05f, searchRadius) * Mathf.Max(0.05f, searchRadius);

        foreach (IBotHazardIsolationTarget candidate in BotRuntimeRegistry.ActiveHazardIsolationTargets)
        {
            if (!IsEligibleHazardIsolationTarget(candidate, requester, hazardType))
            {
                continue;
            }

            float distanceSq = (candidate.GetWorldPosition() - fromPosition).sqrMagnitude;
            if (distanceSq > searchRadiusSq || distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            bestTarget = candidate;
        }

        RememberHazardIsolationTarget(requesterMemory, bestTarget);
        return bestTarget;
    }

    public IBotHazardIsolationTarget ResolveNearestHazardIsolationTarget(Vector3 fromPosition, GameObject requester, float searchRadius)
    {
        BotPerceptionMemory requesterMemory = requester != null ? requester.GetComponent<BotPerceptionMemory>() : null;
        if (requesterMemory != null &&
            requesterMemory.TryGetNearestRecentHazardIsolationTarget(fromPosition, searchRadius, out IBotHazardIsolationTarget rememberedTarget))
        {
            RememberHazardIsolationTarget(requesterMemory, rememberedTarget);
            return rememberedTarget;
        }

        if (BotRuntimeRegistry.SharedIncidentBlackboard.TryGetNearestRecentHazardIsolationTarget(fromPosition, searchRadius, out IBotHazardIsolationTarget sharedTarget))
        {
            RememberHazardIsolationTarget(requesterMemory, sharedTarget);
            return sharedTarget;
        }

        IBotHazardIsolationTarget bestTarget = null;
        float bestDistanceSq = float.PositiveInfinity;
        float searchRadiusSq = Mathf.Max(0.05f, searchRadius) * Mathf.Max(0.05f, searchRadius);

        foreach (IBotHazardIsolationTarget candidate in BotRuntimeRegistry.ActiveHazardIsolationTargets)
        {
            if (!IsEligibleHazardIsolationTarget(candidate, requester))
            {
                continue;
            }

            float distanceSq = (candidate.GetWorldPosition() - fromPosition).sqrMagnitude;
            if (distanceSq > searchRadiusSq || distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            bestTarget = candidate;
        }

        RememberHazardIsolationTarget(requesterMemory, bestTarget);
        return bestTarget;
    }

    public IBotPryTarget ResolveNearestPryTarget(Vector3 fromPosition, GameObject requester, float searchRadius)
    {
        IBotPryTarget bestTarget = null;
        float bestDistanceSq = float.PositiveInfinity;
        float searchRadiusSq = Mathf.Max(0.05f, searchRadius) * Mathf.Max(0.05f, searchRadius);

        foreach (IBotPryTarget candidate in BotRuntimeRegistry.ActivePryTargets)
        {
            if (!IsEligiblePryTarget(candidate, requester))
            {
                continue;
            }

            float distanceSq = (candidate.GetWorldPosition() - fromPosition).sqrMagnitude;
            if (distanceSq > searchRadiusSq || distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
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

        if (BotRuntimeRegistry.Reservations.IsReservedByOther(candidate, requester))
        {
            return false;
        }

        return GetHorizontalDistance(orderPoint, candidate.GetWorldPosition()) <= rescueSearchRadius;
    }

    private static void RememberFollowTarget(string followTargetTag, Transform target, BotPerceptionMemory memory)
    {
        if (target == null)
        {
            return;
        }

        memory?.RememberFollowTarget(followTargetTag, target);
        BotRuntimeRegistry.SharedIncidentBlackboard.RememberFollowTarget(followTargetTag, target);
    }

    private static void RememberRescueTarget(GameObject requester, IRescuableTarget target)
    {
        if (target == null)
        {
            return;
        }

        if (requester != null && requester.TryGetComponent(out BotPerceptionMemory memory))
        {
            memory.RememberRescuable(target);
        }

        BotRuntimeRegistry.SharedIncidentBlackboard.RememberRescuable(target);
    }

    private static void RememberHazardIsolationTarget(BotPerceptionMemory memory, IBotHazardIsolationTarget target)
    {
        if (target == null)
        {
            return;
        }

        memory?.RememberHazardIsolationTarget(target);
        BotRuntimeRegistry.SharedIncidentBlackboard.RememberHazardIsolationTarget(target);
    }

    private static bool IsEligibleHazardIsolationTarget(
        IBotHazardIsolationTarget candidate,
        GameObject requester,
        FireHazardType? hazardType = null)
    {
        if (candidate == null || !candidate.IsHazardActive || !candidate.IsInteractionAvailable)
        {
            return false;
        }

        if (hazardType.HasValue && candidate.HazardType != hazardType.Value)
        {
            return false;
        }

        return !BotRuntimeRegistry.Reservations.IsReservedByOther(candidate, requester);
    }

    private static bool IsEligiblePryTarget(IBotPryTarget candidate, GameObject requester)
    {
        if (candidate == null || candidate.IsBreached)
        {
            return false;
        }

        if (!candidate.CanBePriedOpen && !candidate.IsPryInProgress)
        {
            return false;
        }

        return !BotRuntimeRegistry.Reservations.IsReservedByOther(candidate, requester);
    }

    private static float GetHorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
