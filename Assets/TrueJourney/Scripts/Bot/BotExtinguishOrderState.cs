using UnityEngine;

public enum BotExtinguishCommandMode
{
    Auto = 0,
    FireGroup = 1,
    PointFire = 2
}

public sealed class BotExtinguishOrderState
{
    public bool HasExtinguishOrder { get; private set; }
    public Vector3 Destination { get; private set; }
    public Vector3 ScanOrigin { get; private set; }
    public BotExtinguishCommandMode Mode { get; private set; }

    public void SetDestination(Vector3 destination)
    {
        SetDestination(destination, destination, BotExtinguishCommandMode.Auto);
    }

    public void SetDestination(Vector3 destination, Vector3 scanOrigin, BotExtinguishCommandMode mode)
    {
        Destination = destination;
        ScanOrigin = scanOrigin;
        Mode = mode;
        HasExtinguishOrder = true;
    }

    public bool TryGetDestination(out Vector3 destination)
    {
        destination = Destination;
        return HasExtinguishOrder;
    }

    public bool TryGetDestination(out Vector3 destination, out Vector3 scanOrigin, out BotExtinguishCommandMode mode)
    {
        destination = Destination;
        scanOrigin = ScanOrigin;
        mode = Mode;
        return HasExtinguishOrder;
    }

    public void Clear()
    {
        HasExtinguishOrder = false;
        Destination = default;
        ScanOrigin = default;
        Mode = BotExtinguishCommandMode.Auto;
    }
}
