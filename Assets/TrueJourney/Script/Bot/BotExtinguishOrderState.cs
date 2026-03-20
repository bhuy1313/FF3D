using UnityEngine;

public sealed class BotExtinguishOrderState
{
    public bool HasExtinguishOrder { get; private set; }
    public Vector3 Destination { get; private set; }

    public void SetDestination(Vector3 destination)
    {
        Destination = destination;
        HasExtinguishOrder = true;
    }

    public bool TryGetDestination(out Vector3 destination)
    {
        destination = Destination;
        return HasExtinguishOrder;
    }

    public void Clear()
    {
        HasExtinguishOrder = false;
        Destination = default;
    }
}
