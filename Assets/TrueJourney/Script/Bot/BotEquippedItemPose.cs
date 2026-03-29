using System;
using UnityEngine;

namespace TrueJourney.BotBehavior
{
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
        public Vector3 rightHandIkHintLocalEulerAngles;
    }

    public interface IBotEquippedItemPoseSource
    {
        bool TryGetBotEquippedItemPose(out BotEquippedItemPose pose);
    }
}
