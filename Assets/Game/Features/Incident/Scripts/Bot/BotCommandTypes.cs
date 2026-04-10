using UnityEngine;

public enum BotCommandType
{
    None = 0,
    Move = 1,
    Extinguish = 2,
    Follow = 3,
    Rescue = 4,
    Hold = 5,
    Breach = 6,
    Isolate = 7,
    Search = 8,
    Assist = 9,
    Regroup = 10
}

public interface ICommandable
{
    bool CanAcceptCommand(BotCommandType commandType);
    bool TryIssueCommand(BotCommandType commandType, Vector3 worldPoint);
}

public interface IIntentCommandable : ICommandable
{
    bool CanAcceptCommandIntent(BotCommandIntentPayload payload);
    bool TryIssueCommandIntent(BotCommandIntentPayload payload);
}

public static class BotCommandTypeUtility
{
    public static bool UsesMoveOrder(BotCommandType commandType)
    {
        switch (commandType)
        {
            case BotCommandType.Move:
            case BotCommandType.Breach:
            case BotCommandType.Isolate:
            case BotCommandType.Search:
                return true;
            default:
                return false;
        }
    }

    public static bool UsesFollowOrder(BotCommandType commandType)
    {
        switch (commandType)
        {
            case BotCommandType.Follow:
            case BotCommandType.Regroup:
                return true;
            default:
                return false;
        }
    }

    public static bool UsesImmediateConfirmation(BotCommandType commandType)
    {
        return UsesFollowOrder(commandType) || commandType == BotCommandType.Hold;
    }

    public static BotCommandIntent ResolveIntent(BotCommandType commandType)
    {
        switch (commandType)
        {
            case BotCommandType.Move:
                return BotCommandIntent.Move;
            case BotCommandType.Extinguish:
                return BotCommandIntent.Extinguish;
            case BotCommandType.Follow:
                return BotCommandIntent.Follow;
            case BotCommandType.Rescue:
                return BotCommandIntent.Rescue;
            case BotCommandType.Hold:
                return BotCommandIntent.Hold;
            case BotCommandType.Breach:
                return BotCommandIntent.Breach;
            case BotCommandType.Isolate:
                return BotCommandIntent.Isolate;
            case BotCommandType.Search:
                return BotCommandIntent.Search;
            case BotCommandType.Assist:
                return BotCommandIntent.Follow;
            case BotCommandType.Regroup:
                return BotCommandIntent.Regroup;
            default:
                return BotCommandIntent.None;
        }
    }
}
