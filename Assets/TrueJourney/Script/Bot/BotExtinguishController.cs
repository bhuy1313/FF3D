using System;

public sealed class BotExtinguishController
{
    private readonly Action tick;

    public BotExtinguishController(Action tick)
    {
        this.tick = tick;
    }

    public void Tick()
    {
        tick?.Invoke();
    }
}
