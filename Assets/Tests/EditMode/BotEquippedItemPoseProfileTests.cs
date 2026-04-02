using System.Reflection;
using NUnit.Framework;
using TrueJourney.BotBehavior;
using UnityEngine;

public class BotEquippedItemPoseProfileTests
{
    private GameObject testObject;
    private BotEquippedItemPoseProfile profile;

    [SetUp]
    public void SetUp()
    {
        testObject = new GameObject("BotEquippedItemPoseProfileTests");
        profile = testObject.AddComponent<BotEquippedItemPoseProfile>();
    }

    [TearDown]
    public void TearDown()
    {
        if (testObject != null)
        {
            Object.DestroyImmediate(testObject);
        }
    }

    [Test]
    public void TryGetBotEquippedItemPose_ReturnsFalseWhenProfileHasNoEntries()
    {
        bool found = profile.TryGetBotEquippedItemPose(BotEquippedItemPoseContext.Default, out BotEquippedItemPose pose);

        Assert.That(found, Is.False);
        Assert.That(pose, Is.EqualTo(default(BotEquippedItemPose)));
    }

    [Test]
    public void TryGetBotEquippedItemPose_ReturnsExactMatchingEntry()
    {
        SetPrivateField("poseEntries", new[]
        {
            new BotEquippedItemPoseProfileEntry
            {
                key = BotEquippedItemPoseKey.Default,
                pose = CreatePose(new Vector3(1f, 0f, 0f), 0.25f)
            },
            new BotEquippedItemPoseProfileEntry
            {
                key = BotEquippedItemPoseKey.Aim,
                pose = CreatePose(new Vector3(2f, 0f, 0f), 0.75f)
            }
        });

        bool found = profile.TryGetBotEquippedItemPose(
            new BotEquippedItemPoseContext { key = BotEquippedItemPoseKey.Aim },
            out BotEquippedItemPose pose);

        Assert.That(found, Is.True);
        Assert.That(pose.equippedLocalPosition, Is.EqualTo(new Vector3(2f, 0f, 0f)));
        Assert.That(pose.rightHandIkWeight, Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(pose.rightHandIkLocalEulerAngles, Is.EqualTo(new Vector3(40f, 50f, 60f)));
        Assert.That(pose.overrideSpineAimMaxWeight, Is.True);
        Assert.That(pose.spineAimMaxWeight, Is.EqualTo(0.3f).Within(0.0001f));
    }

    [Test]
    public void TryGetBotEquippedItemPose_FallsBackToDefaultEntryWhenKeyIsMissing()
    {
        SetPrivateField("poseEntries", new[]
        {
            new BotEquippedItemPoseProfileEntry
            {
                key = BotEquippedItemPoseKey.Default,
                pose = CreatePose(new Vector3(3f, 0f, 0f), 0.6f)
            }
        });

        bool found = profile.TryGetBotEquippedItemPose(
            new BotEquippedItemPoseContext { key = BotEquippedItemPoseKey.Crouch },
            out BotEquippedItemPose pose);

        Assert.That(found, Is.True);
        Assert.That(pose.equippedLocalPosition, Is.EqualTo(new Vector3(3f, 0f, 0f)));
        Assert.That(pose.rightHandIkWeight, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(pose.rightHandIkLocalEulerAngles, Is.EqualTo(new Vector3(40f, 50f, 60f)));
        Assert.That(pose.overrideSpineAimMaxWeight, Is.True);
        Assert.That(pose.spineAimMaxWeight, Is.EqualTo(0.3f).Within(0.0001f));
    }

    private static BotEquippedItemPose CreatePose(Vector3 equippedLocalPosition, float rightHandIkWeight)
    {
        return new BotEquippedItemPose
        {
            equippedLocalPosition = equippedLocalPosition,
            equippedLocalEulerAngles = new Vector3(10f, 20f, 30f),
            useRightHandIkTarget = true,
            rightHandIkWeight = rightHandIkWeight,
            rightHandIkLocalPosition = new Vector3(0.5f, 0.6f, 0.7f),
            rightHandIkLocalEulerAngles = new Vector3(40f, 50f, 60f),
            useRightHandIkHint = true,
            rightHandIkHintLocalPosition = new Vector3(0.8f, 0.9f, 1f),
            useLeftHandIkTarget = true,
            leftHandIkWeight = rightHandIkWeight * 0.5f,
            leftHandIkLocalPosition = new Vector3(1.5f, 1.6f, 1.7f),
            useLeftHandIkHint = true,
            leftHandIkHintLocalPosition = new Vector3(1.8f, 1.9f, 2f),
            overrideSpineAimMaxWeight = true,
            spineAimMaxWeight = 0.3f
        };
    }

    private void SetPrivateField(string fieldName, object value)
    {
        FieldInfo field = typeof(BotEquippedItemPoseProfile).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found.");
        field.SetValue(profile, value);
    }
}
