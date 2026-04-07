using NUnit.Framework;
using UnityEngine;

public class BotFollowOrderStateTests
{
    [Test]
    public void SetActive_StoresConfiguredFollowOrder()
    {
        GameObject targetObject = new GameObject("FollowTarget");
        try
        {
            BotFollowOrderState state = new BotFollowOrderState();
            BotFollowOrder order = new BotFollowOrder(
                targetObject.transform,
                "Player",
                BotFollowMode.Escort,
                3.5f,
                new Vector3(1f, 0f, -2f),
                true);

            state.SetActive(order);

            Assert.That(state.HasFollowOrder, Is.True);
            Assert.That(state.TryGetOrder(out BotFollowOrder storedOrder), Is.True);
            Assert.That(storedOrder.Target, Is.SameAs(targetObject.transform));
            Assert.That(storedOrder.TargetTag, Is.EqualTo("Player"));
            Assert.That(storedOrder.Mode, Is.EqualTo(BotFollowMode.Escort));
            Assert.That(storedOrder.FollowDistance, Is.EqualTo(3.5f));
            Assert.That(storedOrder.LocalOffset, Is.EqualTo(new Vector3(1f, 0f, -2f)));
            Assert.That(storedOrder.AllowAssist, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(targetObject);
        }
    }

    [Test]
    public void Clear_RemovesStoredFollowOrder()
    {
        GameObject targetObject = new GameObject("FollowTarget");
        try
        {
            BotFollowOrderState state = new BotFollowOrderState();
            state.SetActive(new BotFollowOrder(
                targetObject.transform,
                "Player",
                BotFollowMode.Passive,
                2.5f,
                Vector3.zero,
                false));

            state.Clear();

            Assert.That(state.HasFollowOrder, Is.False);
            Assert.That(state.TryGetOrder(out BotFollowOrder storedOrder), Is.False);
            Assert.That(storedOrder.Target, Is.Null);
            Assert.That(storedOrder.TargetTag, Is.Null.Or.Empty);
            Assert.That(storedOrder.Mode, Is.EqualTo(BotFollowMode.Passive));
            Assert.That(storedOrder.FollowDistance, Is.Zero);
            Assert.That(storedOrder.LocalOffset, Is.EqualTo(Vector3.zero));
            Assert.That(storedOrder.AllowAssist, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(targetObject);
        }
    }
}
