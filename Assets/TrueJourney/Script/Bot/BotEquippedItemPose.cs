using System;
using UnityEngine;

namespace TrueJourney.BotBehavior
{
    public enum BotEquippedItemPoseKey
    {
        Default = 0,
        Move = 1,
        Aim = 2,
        Use = 3,
        Crouch = 4,
        Reload = 5
    }

    [Serializable]
    public struct BotEquippedItemPoseContext
    {
        public BotEquippedItemPoseKey key;

        public static BotEquippedItemPoseContext Default => new BotEquippedItemPoseContext
        {
            key = BotEquippedItemPoseKey.Default
        };
    }

    [Serializable]
    public struct BotEquippedItemPose
    {
        public Vector3 equippedLocalPosition;
        public Vector3 equippedLocalEulerAngles;
        public bool useRightHandIkTarget;
        [Range(0f, 1f)] public float rightHandIkWeight;
        public Vector3 rightHandIkLocalPosition;
        public Vector3 rightHandIkLocalEulerAngles;
        public bool useRightHandIkHint;
        public Vector3 rightHandIkHintLocalPosition;
        public bool useLeftHandIkTarget;
        [Range(0f, 1f)] public float leftHandIkWeight;
        public Vector3 leftHandIkLocalPosition;
        public bool useLeftHandIkHint;
        public Vector3 leftHandIkHintLocalPosition;
        public bool overrideSpineAimMaxWeight;
        [Range(0f, 1f)] public float spineAimMaxWeight;
    }

    public interface IBotContextualEquippedItemPoseSource
    {
        bool TryGetBotEquippedItemPose(BotEquippedItemPoseContext context, out BotEquippedItemPose pose);
    }
}
