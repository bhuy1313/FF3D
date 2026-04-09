using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public abstract class WornGearItem : Item, IInteractable, IPickupable, IUsable, IMovementWeightSource, IInventoryEquippable, IInventoryRuntimeTickable
{
    [Header("Worn Gear")]
    [SerializeField] private float movementWeightKg = 1f;
    [SerializeField] private bool autoEnableWhenEquipped;
    [SerializeField] private bool remainActiveWhenStowed = true;

    [Header("Runtime")]
    [SerializeField] private bool gearEnabled;
    [SerializeField] private bool isEquipped;
    [SerializeField] private GameObject currentHolder;

    private Rigidbody cachedRigidbody;

    public Rigidbody Rigidbody => cachedRigidbody;
    public float MovementWeightKg => Mathf.Max(0f, movementWeightKg);
    protected GameObject CurrentHolder => currentHolder;
    protected bool IsGearEnabled => gearEnabled;
    protected bool IsGearEquipped => isEquipped;
    protected bool AutoEnableWhenEquipped => autoEnableWhenEquipped;
    protected bool RemainActiveWhenStowed => remainActiveWhenStowed;

    protected virtual void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        OnGearAwake();
    }

    protected virtual void OnValidate()
    {
        movementWeightKg = Mathf.Max(0f, movementWeightKg);
        OnGearValidate();
    }

    public virtual void Interact(GameObject interactor)
    {
    }

    public virtual void OnPickup(GameObject picker)
    {
        UpdateHolder(picker);
    }

    public virtual void OnDrop(GameObject dropper)
    {
        GameObject resolvedHolder = dropper != null ? dropper : currentHolder;
        SetGearEnabled(false, resolvedHolder);
        isEquipped = false;
        UpdateHolder(null);
    }

    public virtual void Use(GameObject user)
    {
        GameObject resolvedHolder = user != null ? user : currentHolder;
        UpdateHolder(resolvedHolder);

        if (gearEnabled)
        {
            SetGearEnabled(false, resolvedHolder);
            return;
        }

        SetGearEnabled(true, resolvedHolder);
    }

    public virtual void OnEquipped(GameObject owner)
    {
        UpdateHolder(owner);
        isEquipped = true;
        OnWearEquipStateChanged(owner, true);

        if (autoEnableWhenEquipped)
        {
            SetGearEnabled(true, owner);
        }
    }

    public virtual void OnStowed(GameObject owner)
    {
        UpdateHolder(owner != null ? owner : currentHolder);
        isEquipped = false;
        OnWearEquipStateChanged(currentHolder, false);

        if (gearEnabled && !remainActiveWhenStowed)
        {
            SetGearEnabled(false, currentHolder);
        }
    }

    public virtual void OnInventoryTick(GameObject owner, bool isEquipped, float deltaTime)
    {
        UpdateHolder(owner != null ? owner : currentHolder);
        this.isEquipped = isEquipped;

        if (gearEnabled && !CanRemainEnabled(currentHolder, isEquipped))
        {
            SetGearEnabled(false, currentHolder);
        }

        OnWearTick(currentHolder, isEquipped, Mathf.Max(0f, deltaTime));
    }

    protected bool SetGearEnabled(bool enabled, GameObject owner)
    {
        if (enabled)
        {
            if (!CanEnableGear(owner))
            {
                gearEnabled = false;
                return false;
            }
        }

        if (gearEnabled == enabled)
        {
            return gearEnabled;
        }

        gearEnabled = enabled;
        if (enabled)
        {
            OnGearEnabled(owner);
        }
        else
        {
            OnGearDisabled(owner);
        }

        return gearEnabled;
    }

    protected virtual bool CanEnableGear(GameObject owner)
    {
        return owner != null;
    }

    protected virtual bool CanRemainEnabled(GameObject owner, bool currentlyEquipped)
    {
        return CanEnableGear(owner) && (remainActiveWhenStowed || currentlyEquipped);
    }

    protected virtual void OnGearAwake()
    {
    }

    protected virtual void OnGearValidate()
    {
    }

    protected virtual void OnGearEnabled(GameObject owner)
    {
    }

    protected virtual void OnGearDisabled(GameObject owner)
    {
    }

    protected virtual void OnWearEquipStateChanged(GameObject owner, bool equipped)
    {
    }

    protected virtual void OnWearTick(GameObject owner, bool equipped, float deltaTime)
    {
    }

    protected virtual void OnWearHolderChanged(GameObject previousHolder, GameObject newHolder, bool gearWasEnabled)
    {
    }

    private void UpdateHolder(GameObject newHolder)
    {
        if (ReferenceEquals(currentHolder, newHolder))
        {
            return;
        }

        GameObject previousHolder = currentHolder;
        bool wasEnabled = gearEnabled;
        currentHolder = newHolder;
        OnWearHolderChanged(previousHolder, newHolder, wasEnabled);
    }
}
