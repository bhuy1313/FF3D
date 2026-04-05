using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public abstract class PlaceableItem : MonoBehaviour, IInteractable, IPickupable, IUsable, IMovementWeightSource
{
    [Header("Placeable")]
    [SerializeField] private bool consumeOnPlace = true;
    [SerializeField] private float movementWeightKg = 8f;

    [Header("Runtime")]
    [SerializeField] private GameObject currentHolder;
    [SerializeField] private GameObject claimOwner;

    private Rigidbody cachedRigidbody;

    public Rigidbody Rigidbody => cachedRigidbody;
    public float MovementWeightKg => Mathf.Max(0f, movementWeightKg);
    public bool IsHeld => currentHolder != null;
    protected GameObject CurrentHolder => currentHolder;

    protected virtual void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
    }

    protected virtual void OnValidate()
    {
        movementWeightKg = Mathf.Max(0f, movementWeightKg);
    }

    public virtual void Interact(GameObject interactor)
    {
    }

    public virtual void OnPickup(GameObject picker)
    {
        currentHolder = picker;
        claimOwner = picker;
    }

    public virtual void OnDrop(GameObject dropper)
    {
        currentHolder = null;
        if (claimOwner == dropper)
        {
            claimOwner = null;
        }
    }

    public void Use(GameObject user)
    {
        if (!TryPlace(user))
        {
            return;
        }

        if (consumeOnPlace)
        {
            ConsumePlacedItem(user);
        }
    }

    protected abstract bool TryPlace(GameObject user);

    protected virtual void ConsumePlacedItem(GameObject user)
    {
        if (user != null &&
            user.TryGetComponent(out FPSInventorySystem inventory) &&
            inventory.HeldObject == gameObject)
        {
            inventory.RemoveHeld(user, destroyItem: true);
            return;
        }

        if (currentHolder == user)
        {
            OnDrop(user);
        }

        Destroy(gameObject);
    }
}
