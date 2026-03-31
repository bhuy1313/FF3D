using NUnit.Framework;
using UnityEngine;

public class BotEscortFormationUtilityTests
{
    [Test]
    public void FillLocalOffsets_BuildsEscortCandidatesFromPreferredSide()
    {
        Vector3[] buffer = new Vector3[5];

        int count = BotEscortFormationUtility.FillLocalOffsets(new Vector3(1.25f, 0f, -1.5f), 2.5f, buffer);

        Assert.That(count, Is.EqualTo(5));
        Assert.That(buffer[0], Is.EqualTo(new Vector3(1.25f, 0f, -1.5f)));
        Assert.That(buffer[1], Is.EqualTo(new Vector3(-1.25f, 0f, -1.5f)));
        Assert.That(buffer[2].x, Is.Zero);
        Assert.That(buffer[2].z, Is.LessThan(-2.4f));
        Assert.That(buffer[3].x, Is.GreaterThan(buffer[0].x));
        Assert.That(buffer[4].x, Is.LessThan(buffer[1].x));
    }

    [Test]
    public void ResolveFormationRank_SortsFollowerIdsDeterministically()
    {
        GameObject ownerObject = new GameObject("EscortOwner");
        GameObject followerA = new GameObject("EscortFollowerA");
        GameObject followerB = new GameObject("EscortFollowerB");
        GameObject followerC = new GameObject("EscortFollowerC");

        try
        {
            EntityId ownerId = ownerObject.GetEntityId();
            EntityId[] followerIds =
            {
                followerC.GetEntityId(),
                ownerId,
                followerB.GetEntityId(),
                followerA.GetEntityId()
            };

            EntityId[] sortedIds = (EntityId[])followerIds.Clone();
            System.Array.Sort(sortedIds);

            int expectedRank = 0;
            for (int i = 0; i < sortedIds.Length; i++)
            {
                if (sortedIds[i] == ownerId)
                {
                    expectedRank = i;
                    break;
                }
            }

            int rank = BotEscortFormationUtility.ResolveFormationRank(ownerId, followerIds);
            Assert.That(rank, Is.EqualTo(expectedRank));
        }
        finally
        {
            Object.DestroyImmediate(ownerObject);
            Object.DestroyImmediate(followerA);
            Object.DestroyImmediate(followerB);
            Object.DestroyImmediate(followerC);
        }
    }

    [Test]
    public void ResolveSlotIndex_FallsBackToCloserSlotWhenPreferredSlotIsFar()
    {
        Vector3[] offsets =
        {
            new Vector3(2f, 0f, -2f),
            new Vector3(-2f, 0f, -2f),
            new Vector3(0f, 0f, -3f)
        };

        int slotIndex = BotEscortFormationUtility.ResolveSlotIndex(
            new Vector3(-2.1f, 0f, -1.9f),
            Vector3.zero,
            Quaternion.identity,
            offsets,
            offsets.Length,
            preferredSlotIndex: 0,
            currentSlotIndex: -1,
            slotPreferenceBias: 0.9f,
            occupiedSlotIndices: null,
            occupiedSlotCount: 0);

        Assert.That(slotIndex, Is.EqualTo(1));
    }

    [Test]
    public void ResolveSlotIndex_KeepsCurrentSlotWhenSwitchAdvantageIsSmall()
    {
        Vector3[] offsets =
        {
            new Vector3(2f, 0f, -2f),
            new Vector3(-2f, 0f, -2f),
            new Vector3(0f, 0f, -3f)
        };

        int slotIndex = BotEscortFormationUtility.ResolveSlotIndex(
            new Vector3(-1.55f, 0f, -2.05f),
            Vector3.zero,
            Quaternion.identity,
            offsets,
            offsets.Length,
            preferredSlotIndex: 0,
            currentSlotIndex: 1,
            slotPreferenceBias: 1f,
            occupiedSlotIndices: null,
            occupiedSlotCount: 0);

        Assert.That(slotIndex, Is.EqualTo(1));
    }

    [Test]
    public void ResolveSlotIndex_AvoidsOccupiedSlotClaimedByAnotherEscortBot()
    {
        Vector3[] offsets =
        {
            new Vector3(1.4f, 0f, -2.2f),
            new Vector3(-1.4f, 0f, -2.2f),
            new Vector3(0f, 0f, -3f)
        };
        int[] occupiedSlots = { 0 };

        int slotIndex = BotEscortFormationUtility.ResolveSlotIndex(
            new Vector3(1.35f, 0f, -2.15f),
            Vector3.zero,
            Quaternion.identity,
            offsets,
            offsets.Length,
            preferredSlotIndex: 0,
            currentSlotIndex: -1,
            slotPreferenceBias: 1f,
            occupiedSlotIndices: occupiedSlots,
            occupiedSlotCount: 1);

        Assert.That(slotIndex, Is.Not.EqualTo(0));
    }
}
