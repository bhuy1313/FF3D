using UnityEngine;

public interface IInteractable
{
    void Interact(GameObject interactor);
}

public interface IUsable
{
    void Use(GameObject user);
}

public interface IPickupable
{
    Rigidbody Rigidbody { get; }
    void OnPickup(GameObject picker);
    void OnDrop(GameObject dropper);
}
