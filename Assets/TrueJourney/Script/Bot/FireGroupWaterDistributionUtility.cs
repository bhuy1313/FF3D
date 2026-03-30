using UnityEngine;

namespace TrueJourney.BotBehavior
{
    public enum FireGroupWaterDistributionMode
    {
        EvenSplit = 0,
        WeightedByCurrentHp = 1
    }

    public static class FireGroupWaterDistributionUtility
    {
        public static float GetDistributedAmount(
            float totalAmount,
            FireGroupWaterDistributionMode distributionMode,
            int activeFireCount,
            float currentFireWeight,
            float totalWeight)
        {
            if (totalAmount <= 0f || activeFireCount <= 0)
            {
                return 0f;
            }

            switch (distributionMode)
            {
                case FireGroupWaterDistributionMode.WeightedByCurrentHp:
                    if (totalWeight > 0.0001f && currentFireWeight > 0f)
                    {
                        return totalAmount * (currentFireWeight / totalWeight);
                    }

                    break;
            }

            return totalAmount / activeFireCount;
        }
    }
}
