using NUnit.Framework;
using UnityEngine;

public class BotMovementFacingUtilityTests
{
    [Test]
    public void ResolveHorizontalFacingDirection_PrefersVelocity()
    {
        Vector3 direction = BotMovementFacingUtility.ResolveHorizontalFacingDirection(
            velocity: new Vector3(2f, 0f, 0f),
            desiredVelocity: new Vector3(0f, 0f, 1f),
            hasPath: true,
            steeringTarget: new Vector3(0f, 0f, 3f),
            currentPosition: Vector3.zero,
            fallbackDestination: new Vector3(0f, 0f, 5f),
            fallbackForward: Vector3.forward);

        Assert.That(direction, Is.EqualTo(Vector3.right));
    }

    [Test]
    public void ResolveHorizontalFacingDirection_FallsBackToSteeringTarget()
    {
        Vector3 direction = BotMovementFacingUtility.ResolveHorizontalFacingDirection(
            velocity: Vector3.zero,
            desiredVelocity: Vector3.zero,
            hasPath: true,
            steeringTarget: new Vector3(0f, 0f, 4f),
            currentPosition: Vector3.zero,
            fallbackDestination: new Vector3(4f, 0f, 0f),
            fallbackForward: Vector3.left);

        Assert.That(direction, Is.EqualTo(Vector3.forward));
    }

    [Test]
    public void ResolveHorizontalFacingDirection_FallsBackToDestinationThenForward()
    {
        Vector3 towardDestination = BotMovementFacingUtility.ResolveHorizontalFacingDirection(
            velocity: Vector3.zero,
            desiredVelocity: Vector3.zero,
            hasPath: false,
            steeringTarget: Vector3.zero,
            currentPosition: Vector3.zero,
            fallbackDestination: new Vector3(0f, 0f, -2f),
            fallbackForward: Vector3.right);

        Assert.That(towardDestination, Is.EqualTo(Vector3.back));

        Vector3 towardForward = BotMovementFacingUtility.ResolveHorizontalFacingDirection(
            velocity: Vector3.zero,
            desiredVelocity: Vector3.zero,
            hasPath: false,
            steeringTarget: Vector3.zero,
            currentPosition: Vector3.zero,
            fallbackDestination: Vector3.zero,
            fallbackForward: Vector3.right);

        Assert.That(towardForward, Is.EqualTo(Vector3.right));
    }
}
