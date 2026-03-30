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
    public void TryGetBotEquippedItemPose_UsesLegacyPoseWhenProfileHasNoEntries()
    {
        SetPrivateField("equippedLocalPosition", new Vector3(0.1f, 0.2f, 0.3f));
        SetPrivateField("equippedLocalEulerAngles", new Vector3(4f, 5f, 6f));
        SetPrivateField("useRightHandIkTarget", false);
        SetPrivateField("rightHandIkWeight", 0.4f);
        SetPrivateField("useRightHandIkHint", false);
        SetPrivateField("useLeftHandIkTarget", true);
        SetPrivateField("leftHandIkWeight", 0.65f);
        SetPrivateField("leftHandIkLocalPosition", new Vector3(1.1f, 1.2f, 1.3f));
        SetPrivateField("leftHandIkLocalEulerAngles", new Vector3(11f, 12f, 13f));
        SetPrivateField("useLeftHandIkHint", true);
        SetPrivateField("leftHandIkHintLocalPosition", new Vector3(2.1f, 2.2f, 2.3f));
        SetPrivateField("leftHandIkHintLocalEulerAngles", new Vector3(21f, 22f, 23f));

        bool found = profile.TryGetBotEquippedItemPose(BotEquippedItemPoseContext.Default, out BotEquippedItemPose pose);

        Assert.That(found, Is.True);
        AssertPoseEquals(
            pose,
            new Vector3(0.1f, 0.2f, 0.3f),
            new Vector3(4f, 5f, 6f),
            false,
            0.4f,
            false,
            true,
            0.65f,
            true);
        Assert.That(pose.leftHandIkLocalPosition, Is.EqualTo(new Vector3(1.1f, 1.2f, 1.3f)));
        Assert.That(pose.leftHandIkLocalEulerAngles, Is.EqualTo(new Vector3(11f, 12f, 13f)));
        Assert.That(pose.leftHandIkHintLocalPosition, Is.EqualTo(new Vector3(2.1f, 2.2f, 2.3f)));
        Assert.That(pose.leftHandIkHintLocalEulerAngles, Is.EqualTo(new Vector3(21f, 22f, 23f)));
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
            rightHandIkHintLocalEulerAngles = new Vector3(70f, 80f, 90f),
            useLeftHandIkTarget = true,
            leftHandIkWeight = rightHandIkWeight * 0.5f,
            leftHandIkLocalPosition = new Vector3(1.5f, 1.6f, 1.7f),
            leftHandIkLocalEulerAngles = new Vector3(14f, 15f, 16f),
            useLeftHandIkHint = true,
            leftHandIkHintLocalPosition = new Vector3(1.8f, 1.9f, 2f),
            leftHandIkHintLocalEulerAngles = new Vector3(17f, 18f, 19f)
        };
    }

    private void SetPrivateField(string fieldName, object value)
    {
        FieldInfo field = typeof(BotEquippedItemPoseProfile).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found.");
        field.SetValue(profile, value);
    }

    private static void AssertPoseEquals(
        BotEquippedItemPose pose,
        Vector3 expectedEquippedPosition,
        Vector3 expectedEquippedEulerAngles,
        bool expectedUseRightHandIkTarget,
        float expectedRightHandIkWeight,
        bool expectedUseRightHandIkHint,
        bool expectedUseLeftHandIkTarget,
        float expectedLeftHandIkWeight,
        bool expectedUseLeftHandIkHint)
    {
        Assert.That(pose.equippedLocalPosition, Is.EqualTo(expectedEquippedPosition));
        Assert.That(pose.equippedLocalEulerAngles, Is.EqualTo(expectedEquippedEulerAngles));
        Assert.That(pose.useRightHandIkTarget, Is.EqualTo(expectedUseRightHandIkTarget));
        Assert.That(pose.rightHandIkWeight, Is.EqualTo(expectedRightHandIkWeight).Within(0.0001f));
        Assert.That(pose.useRightHandIkHint, Is.EqualTo(expectedUseRightHandIkHint));
        Assert.That(pose.useLeftHandIkTarget, Is.EqualTo(expectedUseLeftHandIkTarget));
        Assert.That(pose.leftHandIkWeight, Is.EqualTo(expectedLeftHandIkWeight).Within(0.0001f));
        Assert.That(pose.useLeftHandIkHint, Is.EqualTo(expectedUseLeftHandIkHint));
    }
}
