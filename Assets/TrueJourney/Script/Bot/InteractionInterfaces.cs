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

public interface ICustomGrabPlacement
{
    bool TryGetGrabPlacementPose(Transform aimTransform, LayerMask placementMask, float maxDistance, out Vector3 position, out Quaternion rotation);
    void OnGrabStarted();
    void OnGrabCancelled();
    void OnGrabPlaced(Vector3 position, Quaternion rotation);
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

public interface ISmokeVentPoint : IOpenable
{
    float SmokeVentilationRelief { get; }
    float FireDraftRisk { get; }
}
