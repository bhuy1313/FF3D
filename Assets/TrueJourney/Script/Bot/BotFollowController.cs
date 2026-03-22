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
        string followTargetTag,
        float followDistance,
        float followRepathDistance,
        float followCatchupDistance)
    {
        Transform target = decisionService.ResolveFollowTarget(owner.CurrentFollowTarget, followTargetTag);
        if (target == null)
        {
            owner.CurrentFollowTarget = null;
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;
            return;
        }

        owner.CurrentFollowTarget = target;
        Vector3 targetPosition = target.position;
        float horizontalDistance = GetHorizontalDistance(owner.transform.position, targetPosition);

        if (horizontalDistance <= followDistance)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;
            owner.AimTowardsPoint(targetPosition);
            return;
        }

        Vector3 desiredPosition = targetPosition;
        float desiredStandDistance = horizontalDistance > followCatchupDistance ? followDistance * 0.5f : followDistance;
        Vector3 flatToBot = owner.transform.position - targetPosition;
        flatToBot.y = 0f;
        if (flatToBot.sqrMagnitude > 0.001f)
        {
            desiredPosition = targetPosition + flatToBot.normalized * desiredStandDistance;
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

        owner.AimTowardsPoint(targetPosition);
    }

    private static float GetHorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
