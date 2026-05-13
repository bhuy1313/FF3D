using UnityEngine;

public enum BotExtinguishCommandMode
{
    Auto = 0,
    FireGroup = 1,
    PointFire = 2
}

public enum BotExtinguishEngagementMode
{
    DirectBestTool = 0,
    PrecisionFireHose = 1
}

public sealed class BotExtinguishOrderState
{
    public bool HasExtinguishOrder { get; private set; }
    public Vector3 Destination { get; private set; }
    public Vector3 ScanOrigin { get; private set; }
    public BotExtinguishCommandMode Mode { get; private set; }
    public BotExtinguishEngagementMode EngagementMode { get; private set; }

    public void SetDestination(Vector3 destination)
    {
        SetDestination(destination, destination, BotExtinguishCommandMode.Auto, BotExtinguishEngagementMode.DirectBestTool);
    }

    public void SetDestination(Vector3 destination, Vector3 scanOrigin, BotExtinguishCommandMode mode)
    {
        SetDestination(destination, scanOrigin, mode, BotExtinguishEngagementMode.DirectBestTool);
    }

    public void SetDestination(Vector3 destination, Vector3 scanOrigin, BotExtinguishCommandMode mode, BotExtinguishEngagementMode engagementMode)
    {
        Destination = destination;
        ScanOrigin = scanOrigin;
        Mode = mode;
        EngagementMode = engagementMode;
        HasExtinguishOrder = true;
    }

    public bool TryGetDestination(out Vector3 destination)
    {
        destination = Destination;
        return HasExtinguishOrder;
    }

    public bool TryGetDestination(out Vector3 destination, out Vector3 scanOrigin, out BotExtinguishCommandMode mode)
    {
        return TryGetDestination(out destination, out scanOrigin, out mode, out _);
    }

    public bool TryGetDestination(out Vector3 destination, out Vector3 scanOrigin, out BotExtinguishCommandMode mode, out BotExtinguishEngagementMode engagementMode)
    {
        destination = Destination;
        scanOrigin = ScanOrigin;
        mode = Mode;
        engagementMode = EngagementMode;
        return HasExtinguishOrder;
    }

    public void Clear()
    {
        HasExtinguishOrder = false;
        Destination = default;
        ScanOrigin = default;
        Mode = BotExtinguishCommandMode.Auto;
        EngagementMode = BotExtinguishEngagementMode.DirectBestTool;
    }
}
