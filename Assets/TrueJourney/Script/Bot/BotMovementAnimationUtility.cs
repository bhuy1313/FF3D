using UnityEngine;

public static class BotMovementAnimationUtility
{
    public static bool ShouldUseMoveAnimation(
        bool agentEnabled,
        bool isOnNavMesh,
        bool isStopped,
        bool pathPending,
        bool hasPath,
        float remainingDistance,
        float stoppingDistance,
        Vector3 velocity,
        Vector3 desiredVelocity,
        float movementThreshold)
    {
        if (!agentEnabled || !isOnNavMesh || isStopped)
        {
            return false;
        }

        float threshold = Mathf.Max(0.01f, movementThreshold);
        float thresholdSq = threshold * threshold;
        float horizontalVelocitySq = GetHorizontalMagnitudeSquared(velocity);
        float horizontalDesiredVelocitySq = GetHorizontalMagnitudeSquared(desiredVelocity);

        if (pathPending)
        {
            return true;
        }

        bool hasMovementSignal = horizontalVelocitySq > thresholdSq || horizontalDesiredVelocitySq > thresholdSq;
        if (!hasMovementSignal)
        {
            return false;
        }

        if (!hasPath)
        {
            return horizontalVelocitySq > thresholdSq;
        }

        float bufferedStoppingDistance = Mathf.Max(0f, stoppingDistance) + 0.05f;
        return remainingDistance > bufferedStoppingDistance || horizontalVelocitySq > thresholdSq;
    }

    private static float GetHorizontalMagnitudeSquared(Vector3 vector)
    {
        vector.y = 0f;
        return vector.sqrMagnitude;
    }
}
