using UnityEngine.Serialization;
using UnityEngine;

namespace TrueJourney.BotBehavior
{
    [System.Serializable]
    public struct BotEquippedItemPoseProfileEntry
    {
        public BotEquippedItemPoseKey key;
        public BotEquippedItemPose pose;
    }

    public class BotEquippedItemPoseProfile : MonoBehaviour, IBotEquippedItemPoseSource, IBotContextualEquippedItemPoseSource
    {
        [SerializeField] private bool overrideBotEquippedPose = true;
        [SerializeField] private BotEquippedItemPoseProfileEntry[] poseEntries;

        [Header("Legacy Fallback")]
        [SerializeField] private Vector3 equippedLocalPosition;
        [SerializeField] private Vector3 equippedLocalEulerAngles;
        [FormerlySerializedAs("useRightHandIk")]
        [SerializeField] private bool useRightHandIkTarget = true;
        [SerializeField, Range(0f, 1f)] private float rightHandIkWeight = 1f;
        [SerializeField] private Vector3 rightHandIkLocalPosition;
        [SerializeField] private Vector3 rightHandIkLocalEulerAngles;
        [SerializeField] private bool useRightHandIkHint = true;
        [SerializeField] private Vector3 rightHandIkHintLocalPosition;
        [SerializeField] private Vector3 rightHandIkHintLocalEulerAngles;
        [SerializeField] private bool useLeftHandIkTarget;
        [SerializeField, Range(0f, 1f)] private float leftHandIkWeight = 1f;
        [SerializeField] private Vector3 leftHandIkLocalPosition;
        [SerializeField] private Vector3 leftHandIkLocalEulerAngles;
        [SerializeField] private bool useLeftHandIkHint;
        [SerializeField] private Vector3 leftHandIkHintLocalPosition;
        [SerializeField] private Vector3 leftHandIkHintLocalEulerAngles;

        public bool TryGetBotEquippedItemPose(out BotEquippedItemPose pose)
        {
            return TryGetBotEquippedItemPose(BotEquippedItemPoseContext.Default, out pose);
        }

        public bool TryGetBotEquippedItemPose(BotEquippedItemPoseContext context, out BotEquippedItemPose pose)
        {
            pose = default;
            if (!overrideBotEquippedPose)
            {
                return false;
            }

            if (TryGetPoseEntry(context.key, out pose))
            {
                return true;
            }

            if (!HasPoseEntries())
            {
                pose = CreateLegacyPose();
                return true;
            }

            return false;
        }

        private bool HasPoseEntries()
        {
            return poseEntries != null && poseEntries.Length > 0;
        }

        private bool TryGetPoseEntry(BotEquippedItemPoseKey key, out BotEquippedItemPose pose)
        {
            pose = default;
            if (!HasPoseEntries())
            {
                return false;
            }

            bool hasFallback = false;
            BotEquippedItemPose fallbackPose = default;
            for (int i = 0; i < poseEntries.Length; i++)
            {
                BotEquippedItemPoseProfileEntry entry = poseEntries[i];
                if (entry.key == key)
                {
                    pose = entry.pose;
                    return true;
                }

                if (!hasFallback && entry.key == BotEquippedItemPoseKey.Default)
                {
                    fallbackPose = entry.pose;
                    hasFallback = true;
                }
            }

            if (!hasFallback)
            {
                return false;
            }

            pose = fallbackPose;
            return true;
        }

        private BotEquippedItemPose CreateLegacyPose()
        {
            BotEquippedItemPose pose = default;
            pose.equippedLocalPosition = equippedLocalPosition;
            pose.equippedLocalEulerAngles = equippedLocalEulerAngles;
            pose.useRightHandIkTarget = useRightHandIkTarget;
            pose.rightHandIkWeight = rightHandIkWeight;
            pose.rightHandIkLocalPosition = rightHandIkLocalPosition;
            pose.rightHandIkLocalEulerAngles = rightHandIkLocalEulerAngles;
            pose.useRightHandIkHint = useRightHandIkHint;
            pose.rightHandIkHintLocalPosition = rightHandIkHintLocalPosition;
            pose.rightHandIkHintLocalEulerAngles = rightHandIkHintLocalEulerAngles;
            pose.useLeftHandIkTarget = useLeftHandIkTarget;
            pose.leftHandIkWeight = leftHandIkWeight;
            pose.leftHandIkLocalPosition = leftHandIkLocalPosition;
            pose.leftHandIkLocalEulerAngles = leftHandIkLocalEulerAngles;
            pose.useLeftHandIkHint = useLeftHandIkHint;
            pose.leftHandIkHintLocalPosition = leftHandIkHintLocalPosition;
            pose.leftHandIkHintLocalEulerAngles = leftHandIkHintLocalEulerAngles;
            return pose;
        }
    }
}
