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
        [SerializeField] private bool logPoseResolution = true;
        [SerializeField] private BotEquippedItemPoseProfileEntry[] poseEntries;

        private bool hasLoggedPoseResolution;
        private bool lastLoggedUsedFallback;
        private BotEquippedItemPoseKey lastLoggedRequestedKey;
        private BotEquippedItemPoseKey lastLoggedResolvedKey;

        public bool TryGetBotEquippedItemPose(BotEquippedItemPoseContext context, out BotEquippedItemPose pose)
        {
            pose = default;
            if (!overrideBotEquippedPose)
            {
                return false;
            }

            if (TryGetPoseEntry(context.key, out pose, out BotEquippedItemPoseKey resolvedKey, out bool usedFallback))
            {
                LogPoseResolution(context.key, resolvedKey, usedFallback);
                return true;
            }

            LogPoseResolutionMiss(context.key);
            return false;
        }

        private bool HasPoseEntries()
        {
            return poseEntries != null && poseEntries.Length > 0;
        }

        private bool TryGetPoseEntry(
            BotEquippedItemPoseKey key,
            out BotEquippedItemPose pose,
            out BotEquippedItemPoseKey resolvedKey,
            out bool usedFallback)
        {
            pose = default;
            resolvedKey = key;
            usedFallback = false;

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
                    resolvedKey = entry.key;
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
            resolvedKey = BotEquippedItemPoseKey.Default;
            usedFallback = true;
            return true;
        }

        private void LogPoseResolution(BotEquippedItemPoseKey requestedKey, BotEquippedItemPoseKey resolvedKey, bool usedFallback)
        {
            if (!logPoseResolution)
            {
                return;
            }

            if (
                hasLoggedPoseResolution
                && lastLoggedRequestedKey == requestedKey
                && lastLoggedResolvedKey == resolvedKey
                && lastLoggedUsedFallback == usedFallback)
            {
                return;
            }

            hasLoggedPoseResolution = true;
            lastLoggedRequestedKey = requestedKey;
            lastLoggedResolvedKey = resolvedKey;
            lastLoggedUsedFallback = usedFallback;

            Debug.Log(
                $"{nameof(BotEquippedItemPoseProfile)} '{name}' resolved bot pose: requested={requestedKey}, " +
                $"resolved={resolvedKey}, fallback={usedFallback}.",
                this);
        }

        private void LogPoseResolutionMiss(BotEquippedItemPoseKey requestedKey)
        {
            if (!logPoseResolution)
            {
                return;
            }

            Debug.LogWarning(
                $"{nameof(BotEquippedItemPoseProfile)} '{name}' has no bot pose for requested={requestedKey} and no Default fallback.",
                this);
        }
    }
}
