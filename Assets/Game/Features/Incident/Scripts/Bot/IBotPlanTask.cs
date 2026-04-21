public interface IBotPlanTask
{
    string Name { get; }
    void OnStart(BotCommandAgent agent);
    BotPlanTaskStatus OnUpdate(BotCommandAgent agent);
    void OnEnd(BotCommandAgent agent, bool interrupted);
}
