using UnityEngine;

public static class BotMovementFacingUtility
{
    public static Vector3 ResolveHorizontalFacingDirection(
        Vector3 velocity,
        Vector3 desiredVelocity,
        bool hasPath,
        Vector3 steeringTarget,
        Vector3 currentPosition,
        Vector3 fallbackDestination,
        Vector3 fallbackForward)
    {
        Vector3 direction = velocity;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
        {
            direction = desiredVelocity;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.01f && hasPath)
        {
            direction = steeringTarget - currentPosition;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.01f)
        {
            direction = fallbackDestination - currentPosition;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.01f)
        {
            direction = fallbackForward;
            direction.y = 0f;
        }

        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero;
    }
}
