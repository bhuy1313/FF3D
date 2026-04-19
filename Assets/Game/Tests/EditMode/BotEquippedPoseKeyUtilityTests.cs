using NUnit.Framework;
using TrueJourney.BotBehavior;

public class BotEquippedPoseKeyUtilityTests
{
    [Test]
    public void Resolve_ReturnsDefaultWhenNoEquippedItem()
    {
        BotEquippedItemPoseKey key = BotEquippedPoseKeyUtility.Resolve(
            hasEquippedItem: false,
            isUsingTool: true,
            isAimingTool: true,
            isCrouching: true,
            isMoving: true,
            extinguishStance: -1f);

        Assert.That(key, Is.EqualTo(BotEquippedItemPoseKey.Default));
    }

    [Test]
    public void Resolve_PrioritizesCrouchOverUseAimAndMove()
    {
        BotEquippedItemPoseKey key = BotEquippedPoseKeyUtility.Resolve(
            hasEquippedItem: true,
            isUsingTool: true,
            isAimingTool: true,
            isCrouching: true,
            isMoving: true,
            extinguishStance: -1f);

        Assert.That(key, Is.EqualTo(BotEquippedItemPoseKey.Crouch));
    }

    [Test]
    public void Resolve_ReturnsCrouchStance0()
    {
        BotEquippedItemPoseKey key = BotEquippedPoseKeyUtility.Resolve(
            hasEquippedItem: true,
            isUsingTool: true,
            isAimingTool: true,
            isCrouching: true,
            isMoving: true,
            extinguishStance: 0f);

        Assert.That(key, Is.EqualTo(BotEquippedItemPoseKey.CrouchStance0));
    }

    [Test]
    public void Resolve_ReturnsCrouchStance1()
    {
        BotEquippedItemPoseKey key = BotEquippedPoseKeyUtility.Resolve(
            hasEquippedItem: true,
            isUsingTool: true,
            isAimingTool: true,
            isCrouching: true,
            isMoving: true,
            extinguishStance: 1f);

        Assert.That(key, Is.EqualTo(BotEquippedItemPoseKey.CrouchStance1));
    }

    [Test]
    public void Resolve_PrioritizesUseOverAimAndMove()
    {
        BotEquippedItemPoseKey key = BotEquippedPoseKeyUtility.Resolve(
            hasEquippedItem: true,
            isUsingTool: true,
            isAimingTool: true,
            isCrouching: false,
            isMoving: true,
            extinguishStance: -1f);

        Assert.That(key, Is.EqualTo(BotEquippedItemPoseKey.Use));
    }

    [Test]
    public void Resolve_PrioritizesAimOverMove()
    {
        BotEquippedItemPoseKey key = BotEquippedPoseKeyUtility.Resolve(
            hasEquippedItem: true,
            isUsingTool: false,
            isAimingTool: true,
            isCrouching: false,
            isMoving: true,
            extinguishStance: -1f);

        Assert.That(key, Is.EqualTo(BotEquippedItemPoseKey.Aim));
    }
}
