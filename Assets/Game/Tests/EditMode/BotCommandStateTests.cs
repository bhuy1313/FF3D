using NUnit.Framework;
using UnityEngine;

public class BotCommandStateTests
{
    [Test]
    public void TryBegin_RejectsNullAndUnsupportedTargets()
    {
        BotCommandState state = new BotCommandState();
        FakeCommandable unsupported = new FakeCommandable(false);

        Assert.That(state.TryBegin(null, BotCommandType.Move), Is.False);
        Assert.That(state.TryBegin(unsupported, BotCommandType.Move), Is.False);
        Assert.That(state.IsAwaitingTarget, Is.False);
    }

    [Test]
    public void TryConfirm_IssuesCommandAndClearsState()
    {
        BotCommandState state = new BotCommandState();
        FakeCommandable commandable = new FakeCommandable(true);
        Vector3 destination = new Vector3(4f, 0f, 8f);

        Assert.That(state.TryBegin(commandable, BotCommandType.Move), Is.True);
        Assert.That(state.TryConfirm(destination), Is.True);

        Assert.That(commandable.ReceivedCommandType, Is.EqualTo(BotCommandType.Move));
        Assert.That(commandable.ReceivedDestination, Is.EqualTo(destination));
        Assert.That(state.IsAwaitingTarget, Is.False);
        Assert.That(state.SelectedCommandable, Is.Null);
    }

    [Test]
    public void Cancel_ClearsPendingSelection()
    {
        BotCommandState state = new BotCommandState();
        FakeCommandable commandable = new FakeCommandable(true);

        Assert.That(state.TryBegin(commandable, BotCommandType.Move), Is.True);

        state.Cancel();

        Assert.That(state.IsAwaitingTarget, Is.False);
        Assert.That(state.PendingCommand, Is.EqualTo(BotCommandType.None));
        Assert.That(state.SelectedCommandable, Is.Null);
    }

    [Test]
    public void TryConfirm_KeepsSelectionWhenCommandFails()
    {
        BotCommandState state = new BotCommandState();
        FakeCommandable commandable = new FakeCommandable(true, shouldAcceptIssue: false);

        Assert.That(state.TryBegin(commandable, BotCommandType.Move), Is.True);
        Assert.That(state.TryConfirm(Vector3.one), Is.False);
        Assert.That(state.IsAwaitingTarget, Is.True);
        Assert.That(state.SelectedCommandable, Is.SameAs(commandable));
    }

    private sealed class FakeCommandable : ICommandable
    {
        private readonly bool canAccept;
        private readonly bool shouldAcceptIssue;

        public FakeCommandable(bool canAccept, bool shouldAcceptIssue = true)
        {
            this.canAccept = canAccept;
            this.shouldAcceptIssue = shouldAcceptIssue;
        }

        public BotCommandType ReceivedCommandType { get; private set; }
        public Vector3 ReceivedDestination { get; private set; }

        public bool CanAcceptCommand(BotCommandType commandType)
        {
            return canAccept && commandType == BotCommandType.Move;
        }

        public bool TryIssueCommand(BotCommandType commandType, Vector3 worldPoint)
        {
            ReceivedCommandType = commandType;
            ReceivedDestination = worldPoint;
            return shouldAcceptIssue;
        }
    }
}
