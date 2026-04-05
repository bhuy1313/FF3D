namespace TrueJourney.BotBehavior
{
    public static class BotEquippedPoseKeyUtility
    {
        public static BotEquippedItemPoseKey Resolve(
            bool hasEquippedItem,
            bool isUsingTool,
            bool isAimingTool,
            bool isCrouching,
            bool isMoving)
        {
            if (!hasEquippedItem)
            {
                return BotEquippedItemPoseKey.Default;
            }

            if (isCrouching)
            {
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
