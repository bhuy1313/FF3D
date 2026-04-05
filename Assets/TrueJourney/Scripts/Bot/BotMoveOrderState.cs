using UnityEngine;

public sealed class BotMoveOrderState
{
    public bool HasMoveOrder { get; private set; }
    public Vector3 Destination { get; private set; }

    public void SetDestination(Vector3 destination)
    {
        Destination = destination;
        HasMoveOrder = true;
    }

    public bool TryGetDestination(out Vector3 destination)
    {
        destination = Destination;
        return HasMoveOrder;
    }

    public void Clear()
    {
        HasMoveOrder = false;
        Destination = default;
    }
}
