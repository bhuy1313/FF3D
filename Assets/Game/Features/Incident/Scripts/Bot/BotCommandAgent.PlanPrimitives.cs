using UnityEngine;

public partial class BotCommandAgent
{
    private bool TryResolveIntentOrderPoint(BotCommandIntentPayload intent, out Vector3 orderPoint)
    {
        orderPoint = intent.HasWorldPoint
            ? intent.WorldPoint
            : hasIssuedDestination ? lastIssuedDestination : transform.position;
        return true;
    }

    private bool TryMoveToOrFail(Vector3 destination, System.Action<string> failAction, string failReason)
    {
        if (MoveToCommand(destination))
        {
            return true;
        }

        failAction?.Invoke(failReason);
        return false;
    }

    private void StopNavMeshMovement()
    {
        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;
        }
    }

    private bool IsWithinHorizontalDistance(Vector3 targetPosition, float distance)
    {
        return BotCommandAgent.GetHorizontalDistance(transform.position, targetPosition) <= Mathf.Max(0.01f, distance);
    }

    private bool TryMoveIntoHorizontalRangeOrFail(
        Vector3 targetPosition,
        float interactionDistance,
        System.Action<string> failAction,
        string failReason)
    {
        if (IsWithinHorizontalDistance(targetPosition, interactionDistance))
        {
            StopNavMeshMovement();
            return true;
        }

        return TryMoveToOrFail(targetPosition, failAction, failReason);
    }

    private void StopAndAimTowards(Vector3 targetPosition)
    {
        StopNavMeshMovement();
        AimTowards(targetPosition);
    }
}
