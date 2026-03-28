using TrueJourney.BotBehavior;
using UnityEngine;

public sealed class BotRuntimeDecisionService
{
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
