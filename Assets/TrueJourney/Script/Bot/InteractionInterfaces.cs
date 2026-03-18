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

public interface IGrabbable
{
    //None
}

public interface IDamageable
{
    void TakeDamage(float amount, GameObject source, Vector3 hitPoint, Vector3 hitNormal);
}

public interface IEventListener
{
    void OnEventTriggered(GameObject eventSource, GameObject instigator);
}

public interface IOpenable
{
    bool IsOpen { get; }
}
