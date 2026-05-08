using UnityEngine;

[DisallowMultipleComponent]
public class FireHoseHeadSocket : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform attachPoint;
    [SerializeField] private bool snapPosition = true;
    [SerializeField] private bool snapRotation = true;
    [SerializeField] private bool autoAssignFromSelf = true;

    public Transform AttachPoint => attachPoint != null ? attachPoint : transform;

    void Reset()
    {
        if (autoAssignFromSelf)
        {
            attachPoint = transform;
        }
    }

    public void Interact(GameObject interactor)
    {
        if (interactor == null || !interactor.TryGetComponent(out FPSInventorySystem inventory))
        {
            return;
        }

        GameObject heldObject = inventory.HeldObject;
        if (heldObject == null)
        {
            return;
        }

        FireHoseHeadPickup headPickup =
            heldObject.GetComponent<FireHoseHeadPickup>() ??
            heldObject.GetComponentInParent<FireHoseHeadPickup>();

        if (headPickup == null)
        {
            return;
        }

        FireHoseAssembly assembly = headPickup.GetComponentInParent<FireHoseAssembly>();
        if (assembly == null)
        {
            return;
        }

        assembly.TryAttachHeadToSocket(this, interactor, inventory, snapPosition, snapRotation);
    }
}
