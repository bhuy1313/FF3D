using System.Collections.Generic;

public sealed class BotPlan
{
    private readonly List<IBotPlanTask> tasks = new List<IBotPlanTask>();

    public BotPlan(string name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Unnamed Plan" : name;
    }

    public string Name { get; }
    public IReadOnlyList<IBotPlanTask> Tasks => tasks;
    public int Count => tasks.Count;

    public BotPlan Add(IBotPlanTask task)
    {
        if (task != null)
        {
            tasks.Add(task);
        }

        return this;
    }
}
