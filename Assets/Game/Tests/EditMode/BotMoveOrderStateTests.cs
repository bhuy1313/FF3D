using NUnit.Framework;
using UnityEngine;

public class BotMoveOrderStateTests
{
    [Test]
    public void SetDestination_StoresMoveOrder()
    {
        BotMoveOrderState state = new BotMoveOrderState();
        Vector3 destination = new Vector3(3f, 0f, 7f);

        state.SetDestination(destination);

        Assert.That(state.HasMoveOrder, Is.True);
        Assert.That(state.TryGetDestination(out Vector3 received), Is.True);
        Assert.That(received, Is.EqualTo(destination));
    }

    [Test]
    public void Clear_RemovesStoredMoveOrder()
    {
        BotMoveOrderState state = new BotMoveOrderState();
        state.SetDestination(Vector3.one);

        state.Clear();

        Assert.That(state.HasMoveOrder, Is.False);
        Assert.That(state.TryGetDestination(out Vector3 received), Is.False);
        Assert.That(received, Is.EqualTo(Vector3.zero));
    }
}
