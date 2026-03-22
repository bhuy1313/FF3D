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

        if (currentTarget != null &&
            currentTarget.NeedsRescue &&
            (!currentTarget.IsRescueInProgress || currentTarget.ActiveRescuer == requester) &&
            GetHorizontalDistance(orderPoint, currentTarget.GetWorldPosition()) <= rescueSearchRadius)
        {
            return currentTarget;
        }

        IRescuableTarget bestTarget = null;
        float bestDistance = float.MaxValue;

        foreach (IRescuableTarget candidate in BotRuntimeRegistry.ActiveRescuableTargets)
        {
            if (candidate == null || !candidate.NeedsRescue)
            {
                continue;
            }

            if (candidate.IsRescueInProgress && candidate.ActiveRescuer != requester)
            {
                continue;
            }

            float distance = GetHorizontalDistance(orderPoint, candidate.GetWorldPosition());
            if (distance > rescueSearchRadius || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
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

    private static float GetHorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
