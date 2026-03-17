using UnityEngine;

public enum BotCommandType
{
    None = 0,
    Move = 1
}

public interface ICommandable
{
    bool CanAcceptCommand(BotCommandType commandType);
    bool TryIssueCommand(BotCommandType commandType, Vector3 worldPoint);
}
