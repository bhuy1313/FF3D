using UnityEngine;

public sealed class BotRescueOrderState
{
    public bool HasRescueOrder { get; private set; }
    public Vector3 Destination { get; private set; }

    public void SetDestination(Vector3 destination)
    {
        Destination = destination;
        HasRescueOrder = true;
    }

    public bool TryGetDestination(out Vector3 destination)
    {
        destination = Destination;
        return HasRescueOrder;
    }

    public void Clear()
    {
        HasRescueOrder = false;
        Destination = default;
    }
}
