using UnityEngine;

[DisallowMultipleComponent]
public class ObiRodPickup : MonoBehaviour,
    IInteractable,
    IPickupable,
    IUsable,
    IMovementWeightSource,
    IHandOccupyingObject,
    IInventoryStowBlocker,
    IInventorySelectionBlocker,
    IJumpActionBlocker
{
    [SerializeField] private Rigidbody pickupRigidbody;
    [SerializeField] private float movementWeightKg = 12f;

    private GameObject currentHolder;

    public Rigidbody Rigidbody => pickupRigidbody;
    public float MovementWeightKg => Mathf.Max(0f, movementWeightKg);

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        movementWeightKg = Mathf.Max(0f, movementWeightKg);
        ResolveReferences();
    }

    public void Interact(GameObject interactor)
    {
    }

    public void Use(GameObject user)
    {
    }

    public void OnPickup(GameObject picker)
    {
        currentHolder = picker;
    }

    public void OnDrop(GameObject dropper)
    {
        if (currentHolder == dropper)
        {
            currentHolder = null;
        }
    }

    public bool BlocksInventoryStow(GameObject owner)
    {
        return owner != null && currentHolder == owner;
    }

    public bool BlocksInventorySelectionChange(GameObject owner)
    {
        return BlocksInventoryStow(owner);
    }

    public bool BlocksJumpAction(GameObject owner)
    {
        return BlocksInventoryStow(owner);
    }

    public void SetPickupRigidbody(Rigidbody targetRigidbody)
    {
        pickupRigidbody = targetRigidbody;
    }

    private void ResolveReferences()
    {
        if (pickupRigidbody == null)
        {
            pickupRigidbody = GetComponent<Rigidbody>();
        }
    }
}
