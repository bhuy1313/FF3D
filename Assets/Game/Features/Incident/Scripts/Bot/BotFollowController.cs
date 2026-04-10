using UnityEngine;
using UnityEngine.AI;

public sealed class BotFollowController
{
    private readonly BotRuntimeDecisionService decisionService;

    public BotFollowController(BotRuntimeDecisionService decisionService)
    {
        this.decisionService = decisionService;
    }

    public void Tick(
        BotCommandAgent owner,
        NavMeshAgent navMeshAgent,
        float navMeshSampleDistance,
        BotFollowOrder followOrder,
        float followRepathDistance,
        float followCatchupDistance,
        float followResumeDistanceBuffer,
        float escortSlotPreferenceBias)
    {
        Transform seededTarget = followOrder.Target != null ? followOrder.Target : owner.CurrentFollowTarget;
        Transform target = decisionService.ResolveFollowTarget(seededTarget, followOrder.TargetTag, owner.PerceptionMemory);
        if (target == null)
        {
            owner.CurrentFollowTarget = null;
            owner.CurrentEscortSlotIndex = -1;
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;

            if (owner.ShouldCancelFollowAfterTargetLoss())
            {
                owner.TryCancelFollowCommand();
            }

            return;
        }

        owner.ClearFollowTargetLossState();
        owner.CurrentFollowTarget = target;
        Vector3 targetPosition = target.position;
        float followDistance = Mathf.Max(0.1f, followOrder.FollowDistance);
        float targetHorizontalDistance = GetHorizontalDistance(owner.transform.position, targetPosition);
        if (followOrder.Mode != BotFollowMode.Escort)
        {
            owner.CurrentEscortSlotIndex = -1;
        }

        Vector3 desiredPosition = followOrder.Mode == BotFollowMode.Escort
            ? ResolveEscortDesiredPosition(
                owner,
                target,
                targetHorizontalDistance,
                followDistance,
                followCatchupDistance,
                followOrder,
                escortSlotPreferenceBias)
            : ResolvePassiveDesiredPosition(
                owner.transform.position,
                target.position,
                targetHorizontalDistance,
                followDistance,
                followCatchupDistance);
        float desiredHorizontalDistance = GetHorizontalDistance(owner.transform.position, desiredPosition);
        bool isStopped = navMeshAgent.isStopped || !navMeshAgent.hasPath;

        if (ShouldHoldPosition(
            followOrder,
            isStopped,
            targetHorizontalDistance,
            desiredHorizontalDistance,
            followDistance,
            followRepathDistance,
            followResumeDistanceBuffer))
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;
            owner.AimTowardsPoint(targetPosition);
            return;
        }

        desiredPosition.y = owner.transform.position.y;
        if (navMeshSampleDistance > 0f &&
            NavMesh.SamplePosition(desiredPosition, out NavMeshHit navMeshHit, navMeshSampleDistance, navMeshAgent.areaMask))
        {
            desiredPosition = navMeshHit.position;
        }

        if ((desiredPosition - owner.LastFollowDestination).sqrMagnitude >= followRepathDistance * followRepathDistance ||
            navMeshAgent.isStopped ||
            !navMeshAgent.hasPath)
        {
            owner.LastFollowDestination = desiredPosition;
            owner.MoveToCommand(desiredPosition);
        }
        else if (owner.ShouldRefreshPathClearingCheckCommand())
        {
            owner.MoveToCommand(owner.LastFollowDestination);
        }

        Vector3 facingDirection = BotMovementFacingUtility.ResolveHorizontalFacingDirection(
            navMeshAgent.velocity,
            navMeshAgent.desiredVelocity,
            navMeshAgent.hasPath,
            navMeshAgent.steeringTarget,
            owner.transform.position,
            owner.LastFollowDestination,
            owner.transform.forward);
        if (facingDirection.sqrMagnitude > 0.001f)
        {
            owner.AimTowardsPoint(owner.transform.position + facingDirection);
        }
    }

    private static float GetHorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private static bool ShouldHoldPosition(
        BotFollowOrder followOrder,
        bool isStopped,
        float targetHorizontalDistance,
        float desiredHorizontalDistance,
        float followDistance,
        float followRepathDistance,
        float followResumeDistanceBuffer)
    {
        float resumeBuffer = Mathf.Max(0f, followResumeDistanceBuffer);
        if (followOrder.Mode == BotFollowMode.Escort && followOrder.LocalOffset.sqrMagnitude > 0.001f)
        {
            float slotHoldDistance = Mathf.Max(0.35f, Mathf.Min(followDistance, followRepathDistance));
            float slotResumeDistance = slotHoldDistance + resumeBuffer;
            bool withinTargetLeash = targetHorizontalDistance <= followDistance + resumeBuffer;
            float comparisonDistance = isStopped ? slotResumeDistance : slotHoldDistance;
            return withinTargetLeash && desiredHorizontalDistance <= comparisonDistance;
        }

        float comparisonTargetDistance = isStopped
            ? followDistance + resumeBuffer
            : followDistance;
        return targetHorizontalDistance <= comparisonTargetDistance;
    }

    private Vector3 ResolveEscortDesiredPosition(
        BotCommandAgent owner,
        Transform target,
        float targetHorizontalDistance,
        float followDistance,
        float followCatchupDistance,
        BotFollowOrder followOrder,
        float escortSlotPreferenceBias)
    {
        Vector3 localOffset = followOrder.LocalOffset;
        localOffset.y = 0f;
        if (localOffset.sqrMagnitude <= 0.001f)
        {
            owner.CurrentEscortSlotIndex = -1;
            return ResolvePassiveDesiredPosition(
                owner.transform.position,
                target.position,
                targetHorizontalDistance,
                followDistance,
                followCatchupDistance);
        }

        float catchupScale = targetHorizontalDistance > followCatchupDistance ? 0.35f : 1f;
        int slotCount = BotEscortFormationUtility.FillLocalOffsets(
            localOffset * catchupScale,
            Mathf.Max(followDistance * catchupScale, followDistance * 0.5f),
            owner.EscortSlotOffsets);
        if (slotCount <= 0)
        {
            owner.CurrentEscortSlotIndex = -1;
            return ResolvePassiveDesiredPosition(
                owner.transform.position,
                target.position,
                targetHorizontalDistance,
                followDistance,
                followCatchupDistance);
        }

        int formationRank = decisionService.ResolveEscortFormationRank(owner, target);
        int preferredSlotIndex = BotEscortFormationUtility.ResolvePreferredSlotIndex(formationRank, slotCount);
        int occupiedSlotCount = decisionService.FillOccupiedEscortSlots(
            owner,
            target,
            owner.OccupiedEscortSlotIndices,
            slotCount);
        int selectedSlotIndex = BotEscortFormationUtility.ResolveSlotIndex(
            owner.transform.position,
            target.position,
            target.rotation,
            owner.EscortSlotOffsets,
            slotCount,
            preferredSlotIndex,
            owner.CurrentEscortSlotIndex,
            escortSlotPreferenceBias,
            owner.OccupiedEscortSlotIndices,
            occupiedSlotCount);
        owner.CurrentEscortSlotIndex = selectedSlotIndex;
        return selectedSlotIndex >= 0
            ? target.position + (target.rotation * owner.EscortSlotOffsets[selectedSlotIndex])
            : ResolvePassiveDesiredPosition(
                owner.transform.position,
                target.position,
                targetHorizontalDistance,
                followDistance,
                followCatchupDistance);
    }

    private static Vector3 ResolvePassiveDesiredPosition(
        Vector3 ownerPosition,
        Vector3 targetPosition,
        float targetHorizontalDistance,
        float followDistance,
        float followCatchupDistance)
    {
        Vector3 desiredPosition = targetPosition;
        float desiredStandDistance = targetHorizontalDistance > followCatchupDistance ? followDistance * 0.5f : followDistance;
        Vector3 flatToBot = ownerPosition - targetPosition;
        flatToBot.y = 0f;
        if (flatToBot.sqrMagnitude > 0.001f)
        {
            desiredPosition = targetPosition + flatToBot.normalized * desiredStandDistance;
        }

        return desiredPosition;
    }
}
