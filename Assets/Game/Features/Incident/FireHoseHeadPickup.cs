using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class FireHoseHeadPickup : MonoBehaviour,
    IInteractable,
    IPickupable,
    IUsable,
    IMovementWeightSource,
    IHandOccupyingObject,
    IInventoryStowBlocker,
    IInventorySelectionBlocker,
    IJumpActionBlocker
{
    [SerializeField] private FireHoseAssembly assembly;
    [SerializeField] private float movementWeightKg = 12f;

    private Rigidbody cachedRigidbody;

    public Rigidbody Rigidbody => cachedRigidbody;
    public float MovementWeightKg => Mathf.Max(0f, movementWeightKg);
    public bool IsHeld => assembly != null && assembly.IsHeadHeld;
    public GameObject CurrentHolder => assembly != null ? assembly.CurrentHolder : null;

    void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        if (assembly == null)
        {
            assembly = GetComponentInParent<FireHoseAssembly>() ?? FindAnyObjectByType<FireHoseAssembly>();
        }
    }

    void Reset()
    {
        assembly = GetComponentInParent<FireHoseAssembly>() ?? FindAnyObjectByType<FireHoseAssembly>();
        cachedRigidbody = GetComponent<Rigidbody>();
    }

    void OnValidate()
    {
        movementWeightKg = Mathf.Max(0f, movementWeightKg);
    }

    public void Interact(GameObject interactor)
    {
    }

    public void Use(GameObject user)
    {
        FireHose shooter = assembly != null ? assembly.Shooter : null;
        if (shooter == null)
        {
            return;
        }

        shooter.Use(user);
    }

    public void OnPickup(GameObject picker)
    {
        assembly?.HandleHeadPickedUp(picker);
    }

    public void OnDrop(GameObject dropper)
    {
        assembly?.HandleHeadDropped(dropper);
    }

    public void ConfigureAssembly(FireHoseAssembly owner)
    {
        assembly = owner;
    }

    public FireHoseAssembly Assembly => assembly;

    public bool BlocksInventoryStow(GameObject owner)
    {
        return owner != null && CurrentHolder == owner;
    }

    public bool BlocksInventorySelectionChange(GameObject owner)
    {
        return BlocksInventoryStow(owner);
    }

    public bool BlocksJumpAction(GameObject owner)
    {
        return BlocksInventoryStow(owner);
    }
}
