using UnityEngine;

public sealed class BotCommandState
{
    public ICommandable SelectedCommandable { get; private set; }
    public BotCommandType PendingCommand { get; private set; }

    public bool IsAwaitingTarget => SelectedCommandable != null && PendingCommand != BotCommandType.None;

    public bool TryBegin(ICommandable commandable, BotCommandType commandType)
    {
        if (commandable == null || commandType == BotCommandType.None)
        {
            return false;
        }

        if (!commandable.CanAcceptCommand(commandType))
        {
            return false;
        }

        SelectedCommandable = commandable;
        PendingCommand = commandType;
        return true;
    }

    public bool TryConfirm(Vector3 worldPoint)
    {
        if (!IsAwaitingTarget)
        {
            return false;
        }

        bool accepted = SelectedCommandable.TryIssueCommand(PendingCommand, worldPoint);
        if (accepted)
        {
            Clear();
        }

        return accepted;
    }

    public void Cancel()
    {
        Clear();
    }

    private void Clear()
    {
        SelectedCommandable = null;
        PendingCommand = BotCommandType.None;
    }
}
