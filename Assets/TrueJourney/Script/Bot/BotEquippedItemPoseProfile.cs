using UnityEngine;

namespace TrueJourney.BotBehavior
{
    [System.Serializable]
    public struct BotEquippedItemPoseProfileEntry
    {
        public BotEquippedItemPoseKey key;
        public BotEquippedItemPose pose;
    }

    public class BotEquippedItemPoseProfile : MonoBehaviour, IBotContextualEquippedItemPoseSource
    {
        [SerializeField] private bool overrideBotEquippedPose = true;
        [SerializeField] private BotEquippedItemPoseProfileEntry[] poseEntries;

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
    }
}
