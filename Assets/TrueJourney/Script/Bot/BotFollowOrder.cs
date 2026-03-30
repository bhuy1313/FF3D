using UnityEngine;

public readonly struct BotFollowOrder
{
    public BotFollowOrder(
        Transform target,
        string targetTag,
        BotFollowMode mode,
        float followDistance,
        Vector3 localOffset,
        bool allowAssist)
    {
        Target = target;
        TargetTag = string.IsNullOrWhiteSpace(targetTag) ? string.Empty : targetTag;
        Mode = mode;
        FollowDistance = Mathf.Max(0f, followDistance);
        LocalOffset = localOffset;
        AllowAssist = allowAssist;
    }

    public Transform Target { get; }
    public string TargetTag { get; }
    public BotFollowMode Mode { get; }
    public float FollowDistance { get; }
    public Vector3 LocalOffset { get; }
    public bool AllowAssist { get; }
}
