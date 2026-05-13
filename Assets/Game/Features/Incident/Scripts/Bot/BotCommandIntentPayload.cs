using System;
using UnityEngine;

[Serializable]
public struct BotCommandIntentPayload
{
    [SerializeField] private BotCommandType commandType;
    [SerializeField] private BotCommandIntent intent;
    [SerializeField] private Vector3 worldPoint;
    [SerializeField] private bool hasWorldPoint;
    [SerializeField] private Transform targetTransform;
    [SerializeField] private string targetTag;
    [SerializeField] private bool allowAssist;
    [SerializeField] private BotExtinguishCommandMode extinguishMode;
    [SerializeField] private BotExtinguishEngagementMode extinguishEngagementMode;

    public BotCommandType CommandType => commandType;
    public BotCommandIntent Intent => intent;
    public Vector3 WorldPoint => worldPoint;
    public bool HasWorldPoint => hasWorldPoint;
    public Transform TargetTransform => targetTransform;
    public string TargetTag => targetTag;
    public bool AllowAssist => allowAssist;
    public BotExtinguishCommandMode ExtinguishMode => extinguishMode;
    public BotExtinguishEngagementMode ExtinguishEngagementMode => extinguishEngagementMode;
    public bool IsValid => commandType != BotCommandType.None && intent != BotCommandIntent.None;

    public static BotCommandIntentPayload None => default;

    public static BotCommandIntentPayload Create(BotCommandType commandType, Vector3 worldPoint)
    {
        return new BotCommandIntentPayload
        {
            commandType = commandType,
            intent = BotCommandTypeUtility.ResolveIntent(commandType),
            worldPoint = worldPoint,
            hasWorldPoint = true,
            extinguishMode = BotExtinguishCommandMode.Auto,
            extinguishEngagementMode = BotExtinguishEngagementMode.DirectBestTool
        };
    }

    public static BotCommandIntentPayload CreateExtinguish(Vector3 destination, Vector3 scanOrigin, BotExtinguishCommandMode mode)
    {
        return CreateExtinguish(destination, scanOrigin, mode, BotExtinguishEngagementMode.DirectBestTool);
    }

    public static BotCommandIntentPayload CreateExtinguish(
        Vector3 destination,
        Vector3 scanOrigin,
        BotExtinguishCommandMode mode,
        BotExtinguishEngagementMode engagementMode)
    {
        return new BotCommandIntentPayload
        {
            commandType = BotCommandType.Extinguish,
            intent = BotCommandIntent.Extinguish,
            worldPoint = scanOrigin,
            hasWorldPoint = true,
            extinguishMode = mode,
            extinguishEngagementMode = engagementMode
        };
    }

    public static BotCommandIntentPayload CreateFollow(BotCommandType commandType, BotFollowOrder order)
    {
        return new BotCommandIntentPayload
        {
            commandType = commandType,
            intent = BotCommandTypeUtility.ResolveIntent(commandType),
            worldPoint = order.Target != null ? order.Target.position : Vector3.zero,
            hasWorldPoint = order.Target != null,
            targetTransform = order.Target,
            targetTag = order.TargetTag,
            allowAssist = order.AllowAssist,
            extinguishMode = BotExtinguishCommandMode.Auto,
            extinguishEngagementMode = BotExtinguishEngagementMode.DirectBestTool
        };
    }
}
