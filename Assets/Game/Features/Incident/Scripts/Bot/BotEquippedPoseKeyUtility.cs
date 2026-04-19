using UnityEngine;

namespace TrueJourney.BotBehavior
{
    public static class BotEquippedPoseKeyUtility
    {
        public static BotEquippedItemPoseKey Resolve(
            bool hasEquippedItem,
            bool isUsingTool,
            bool isAimingTool,
            bool isCrouching,
            bool isMoving,
            float extinguishStance)
        {
            if (!hasEquippedItem)
            {
                return BotEquippedItemPoseKey.Default;
            }

            if (isCrouching)
            {
                if (Mathf.Approximately(extinguishStance, 0f))
                {
                    return BotEquippedItemPoseKey.CrouchStance0;
                }
                else if (Mathf.Approximately(extinguishStance, 1f))
                {
                    return BotEquippedItemPoseKey.CrouchStance1;
                }

                return BotEquippedItemPoseKey.Crouch;
            }

            if (isUsingTool)
            {
                return BotEquippedItemPoseKey.Use;
            }

            if (isAimingTool)
            {
                return BotEquippedItemPoseKey.Aim;
            }

            if (isMoving)
            {
                return BotEquippedItemPoseKey.Move;
            }

            return BotEquippedItemPoseKey.Default;
        }
    }
}
