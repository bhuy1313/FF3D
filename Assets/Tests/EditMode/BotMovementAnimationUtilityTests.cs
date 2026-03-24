using NUnit.Framework;
using UnityEngine;

public class BotMovementAnimationUtilityTests
{
    [Test]
    public void ReturnsFalseWhenAgentIsStopped()
    {
        bool result = BotMovementAnimationUtility.ShouldUseMoveAnimation(
            agentEnabled: true,
            isOnNavMesh: true,
            isStopped: true,
            pathPending: false,
            hasPath: true,
            remainingDistance: 2f,
            stoppingDistance: 0.25f,
            velocity: new Vector3(0f, 0f, 1f),
            desiredVelocity: new Vector3(0f, 0f, 1f),
            movementThreshold: 0.1f);

        Assert.That(result, Is.False);
    }

    [Test]
    public void ReturnsTrueWhenPathIsPending()
    {
        bool result = BotMovementAnimationUtility.ShouldUseMoveAnimation(
            agentEnabled: true,
            isOnNavMesh: true,
            isStopped: false,
            pathPending: true,
            hasPath: false,
            remainingDistance: 0f,
            stoppingDistance: 0.25f,
            velocity: Vector3.zero,
            desiredVelocity: Vector3.zero,
            movementThreshold: 0.1f);

        Assert.That(result, Is.True);
    }

    [Test]
    public void ReturnsTrueWhenBotIsTravellingAlongPath()
    {
        bool result = BotMovementAnimationUtility.ShouldUseMoveAnimation(
            agentEnabled: true,
            isOnNavMesh: true,
            isStopped: false,
            pathPending: false,
            hasPath: true,
            remainingDistance: 3f,
            stoppingDistance: 0.25f,
            velocity: new Vector3(0.5f, 0f, 0f),
            desiredVelocity: new Vector3(1f, 0f, 0f),
            movementThreshold: 0.1f);

        Assert.That(result, Is.True);
    }

    [Test]
    public void ReturnsFalseWhenBotHasReachedDestination()
    {
        bool result = BotMovementAnimationUtility.ShouldUseMoveAnimation(
            agentEnabled: true,
            isOnNavMesh: true,
            isStopped: false,
            pathPending: false,
            hasPath: true,
            remainingDistance: 0.2f,
            stoppingDistance: 0.25f,
            velocity: Vector3.zero,
            desiredVelocity: Vector3.zero,
            movementThreshold: 0.1f);

        Assert.That(result, Is.False);
    }
}
