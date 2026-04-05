using UnityEngine;

public sealed class BotFollowOrderState
{
    public bool HasFollowOrder { get; private set; }
    public Transform Target { get; private set; }
    public string TargetTag { get; private set; } = string.Empty;
    public BotFollowMode Mode { get; private set; }
    public float FollowDistance { get; private set; }
    public Vector3 LocalOffset { get; private set; }
    public bool AllowAssist { get; private set; }

    public void SetActive(BotFollowOrder order)
    {
        HasFollowOrder = true;
        Target = order.Target;
        TargetTag = order.TargetTag;
        Mode = order.Mode;
        FollowDistance = order.FollowDistance;
        LocalOffset = order.LocalOffset;
        AllowAssist = order.AllowAssist;
    }

    public bool TryGetOrder(out BotFollowOrder order)
    {
        if (!HasFollowOrder)
        {
            order = default;
            return false;
        }

        order = new BotFollowOrder(Target, TargetTag, Mode, FollowDistance, LocalOffset, AllowAssist);
        return true;
    }

    public void Clear()
    {
        HasFollowOrder = false;
        Target = null;
        TargetTag = string.Empty;
        Mode = BotFollowMode.Passive;
        FollowDistance = 0f;
        LocalOffset = Vector3.zero;
        AllowAssist = false;
    }
}
