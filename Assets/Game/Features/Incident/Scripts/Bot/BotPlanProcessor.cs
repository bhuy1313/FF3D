using System.Collections.Generic;

public sealed class BotPlanProcessor
{
    private readonly LinkedList<IBotPlanTask> pendingTasks = new LinkedList<IBotPlanTask>();
    private IBotPlanTask currentTask;

    public string ActivePlanName { get; private set; } = string.Empty;
    public string CurrentTaskName => currentTask != null ? currentTask.Name : string.Empty;
    public bool HasActivePlan => currentTask != null || pendingTasks.Count > 0;
    public int PendingTaskCount => pendingTasks.Count;

    public void SetPlan(BotPlan plan, BotCommandAgent agent)
    {
        Clear(agent);
        ActivePlanName = plan != null ? plan.Name : string.Empty;
        if (plan == null || plan.Count == 0)
        {
            return;
        }

        IReadOnlyList<IBotPlanTask> tasks = plan.Tasks;
        for (int i = 0; i < tasks.Count; i++)
        {
            pendingTasks.AddLast(tasks[i]);
        }
    }

    public void InjectFront(BotCommandAgent agent, params IBotPlanTask[] tasks)
    {
        if (tasks == null || tasks.Length == 0)
        {
            return;
        }

        for (int i = tasks.Length - 1; i >= 0; i--)
        {
            if (tasks[i] != null)
            {
                pendingTasks.AddFirst(tasks[i]);
            }
        }
    }

    public void InterruptWith(BotCommandAgent agent, params IBotPlanTask[] tasks)
    {
        if (tasks == null || tasks.Length == 0)
        {
            return;
        }

        IBotPlanTask interruptedTask = currentTask;
        if (interruptedTask != null)
        {
            interruptedTask.OnEnd(agent, true);
            currentTask = null;
            pendingTasks.AddFirst(interruptedTask);
        }

        InjectFront(agent, tasks);
    }

    public void Tick(BotCommandAgent agent)
    {
        if (agent == null)
        {
            return;
        }

        if (currentTask == null)
        {
            if (pendingTasks.Count == 0)
            {
                return;
            }

            currentTask = pendingTasks.First.Value;
            pendingTasks.RemoveFirst();
            currentTask.OnStart(agent);
        }

        IBotPlanTask executingTask = currentTask;
        BotPlanTaskStatus status = executingTask.OnUpdate(agent);
        switch (status)
        {
            case BotPlanTaskStatus.Success:
                executingTask?.OnEnd(agent, false);
                if (ReferenceEquals(currentTask, executingTask))
                {
                    currentTask = null;
                }

                if (pendingTasks.Count == 0)
                {
                    ActivePlanName = string.Empty;
                }

                break;
            case BotPlanTaskStatus.Failure:
                executingTask?.OnEnd(agent, false);
                if (ReferenceEquals(currentTask, executingTask))
                {
                    currentTask = null;
                }

                pendingTasks.Clear();
                ActivePlanName = string.Empty;
                break;
        }
    }

    public void Clear(BotCommandAgent agent)
    {
        if (currentTask != null)
        {
            currentTask.OnEnd(agent, true);
            currentTask = null;
        }

        pendingTasks.Clear();
        ActivePlanName = string.Empty;
    }

    public void CopyPendingTaskNames(List<string> results, int maxCount = int.MaxValue)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        if (maxCount <= 0)
        {
            return;
        }

        LinkedListNode<IBotPlanTask> node = pendingTasks.First;
        while (node != null && results.Count < maxCount)
        {
            IBotPlanTask task = node.Value;
            results.Add(task != null ? task.Name : "(null task)");
            node = node.Next;
        }
    }
}
