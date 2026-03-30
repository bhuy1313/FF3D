using NUnit.Framework;
using TrueJourney.BotBehavior;

public class FireGroupWaterDistributionUtilityTests
{
    [Test]
    public void GetDistributedAmount_SplitsEvenlyByDefault()
    {
        float first = FireGroupWaterDistributionUtility.GetDistributedAmount(
            totalAmount: 6f,
            distributionMode: FireGroupWaterDistributionMode.EvenSplit,
            activeFireCount: 3,
            currentFireWeight: 5f,
            totalWeight: 10f);

        float second = FireGroupWaterDistributionUtility.GetDistributedAmount(
            totalAmount: 6f,
            distributionMode: FireGroupWaterDistributionMode.EvenSplit,
            activeFireCount: 3,
            currentFireWeight: 1f,
            totalWeight: 10f);

        Assert.That(first, Is.EqualTo(2f));
        Assert.That(second, Is.EqualTo(2f));
    }

    [Test]
    public void GetDistributedAmount_UsesWeightWhenConfigured()
    {
        float first = FireGroupWaterDistributionUtility.GetDistributedAmount(
            totalAmount: 12f,
            distributionMode: FireGroupWaterDistributionMode.WeightedByCurrentHp,
            activeFireCount: 3,
            currentFireWeight: 3f,
            totalWeight: 6f);

        float second = FireGroupWaterDistributionUtility.GetDistributedAmount(
            totalAmount: 12f,
            distributionMode: FireGroupWaterDistributionMode.WeightedByCurrentHp,
            activeFireCount: 3,
            currentFireWeight: 1f,
            totalWeight: 6f);

        Assert.That(first, Is.EqualTo(6f));
        Assert.That(second, Is.EqualTo(2f));
    }

    [Test]
    public void GetDistributedAmount_FallsBackToEvenSplitWhenWeightsAreInvalid()
    {
        float amount = FireGroupWaterDistributionUtility.GetDistributedAmount(
            totalAmount: 9f,
            distributionMode: FireGroupWaterDistributionMode.WeightedByCurrentHp,
            activeFireCount: 3,
            currentFireWeight: 0f,
            totalWeight: 0f);

        Assert.That(amount, Is.EqualTo(3f));
    }
}
