public sealed class BotFollowOrderState
{
    public bool HasFollowOrder { get; private set; }

    public void SetActive()
    {
        HasFollowOrder = true;
    }

    public void Clear()
    {
        HasFollowOrder = false;
    }
}
