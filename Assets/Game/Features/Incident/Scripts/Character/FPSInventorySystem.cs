using UnityEngine;

public class FPSInventorySystem : MonoBehaviour
{
    [Header("Pickup")]
    [SerializeField] private bool allowPickup = true;
    [SerializeField] private int maxSlots = 6;
    [SerializeField] private Transform viewPointTransform;
    [SerializeField] private Transform inventoryRoot;
    [SerializeField] private Vector3 viewPoint;
    [SerializeField] private bool hideStoredItems = true;
    [Header("ViewPoint Rotation Lag")]
    [SerializeField] private bool useViewPointRotationLag = true;
    [SerializeField] private float viewPointRotationFollowSpeed = 12f;
    [SerializeField] private float viewPointRotationMaxAngle = 8f;
    [SerializeField] private bool createRuntimeLagPivot = true;

    private class InventorySlot
    {
        public IPickupable Item;
        public Transform OriginalParent;
        public bool WasKinematic;
        public bool DetectCollisions;
        public bool WasActive;
    }

    private readonly System.Collections.Generic.List<InventorySlot> slots = new System.Collections.Generic.List<InventorySlot>();
    private int activeIndex = -1;
    private ChildRotationLag rotationLag;
    private Transform runtimeLagPivot;

    public bool HasItem => activeIndex >= 0 && activeIndex < slots.Count;
    public int ItemCount => slots.Count;
    public int MaxSlots => maxSlots;
    public GameObject HeldObject => HasItem ? slots[activeIndex].Item.Rigidbody.gameObject : null;

    private void Awake()
    {
        if (viewPointTransform == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                GameObject hold = new GameObject("ViewPointTransform");
                hold.transform.SetParent(cam.transform, false);
                hold.transform.localPosition = viewPoint;
                viewPointTransform = hold.transform;
            }
        }
        else
        {
            viewPointTransform.localPosition = viewPoint;
        }

        ConfigureViewPointRotationLag();
        ApplyRotationLagSettings();

        if (inventoryRoot == null)
        {
            GameObject root = new GameObject("InventoryRoot");
            root.transform.SetParent(transform, false);
            inventoryRoot = root.transform;
        }
    }

    private void LateUpdate()
    {
        ApplyRotationLagSettings();
        TickRuntimeInventoryItems(Time.deltaTime);
    }

    public bool TryPickup(GameObject target, GameObject picker)
    {
        if (!allowPickup || target == null || viewPointTransform == null || maxSlots <= 0)
        {
            return false;
        }

        if (slots.Count >= maxSlots)
        {
            return false;
        }

        IPickupable pickupable = FindPickupable(target);
        if (pickupable == null || pickupable.Rigidbody == null)
        {
            return false;
        }

        if (ContainsItem(pickupable))
        {
            return false;
        }

        Rigidbody rb = pickupable.Rigidbody;
        InventorySlot slot = new InventorySlot
        {
            Item = pickupable,
            OriginalParent = rb.transform.parent,
            WasKinematic = rb.isKinematic,
            DetectCollisions = rb.detectCollisions,
            WasActive = rb.gameObject.activeSelf
        };

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.detectCollisions = false;

        pickupable.OnPickup(picker);

        slots.Add(slot);
        if (!HasItem)
        {
            activeIndex = slots.Count - 1;
            EquipSlot(slot);
        }
        else
        {
            StowSlot(slot);
        }

        return true;
    }

    public void Drop(GameObject dropper)
    {
        if (!HasItem)
        {
            return;
        }

        RemoveSlotAt(activeIndex, dropper, destroyItem: false);
    }

    public bool RemoveHeld(GameObject owner, bool destroyItem = true)
    {
        if (!HasItem)
        {
            return false;
        }

        RemoveSlotAt(activeIndex, owner, destroyItem);
        return true;
    }

    public void UseHeld(GameObject user)
    {
        if (!HasItem)
        {
            return;
        }

        if (slots[activeIndex].Item is IUsable usable)
        {
            usable.Use(user);
        }
    }

    public bool TrySelectSlot(int index)
    {
        if (index < 0 || index >= slots.Count)
        {
            return false;
        }

        if (index == activeIndex)
        {
            StowSlot(slots[activeIndex]);
            activeIndex = -1;
            return true;
        }

        if (HasItem)
        {
            StowSlot(slots[activeIndex]);
        }

        activeIndex = index;
        EquipSlot(slots[activeIndex]);
        return true;
    }

    private bool ContainsItem(IPickupable pickupable)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].Item == pickupable)
            {
                return true;
            }
        }

        return false;
    }

    private void EquipSlot(InventorySlot slot)
    {
        Transform itemTransform = slot.Item.Rigidbody.transform;
        itemTransform.SetParent(viewPointTransform, false);
        itemTransform.localPosition = Vector3.zero;
        itemTransform.localRotation = Quaternion.identity;
        if (hideStoredItems && slot.WasActive)
        {
            slot.Item.Rigidbody.gameObject.SetActive(true);
        }

        if (slot.Item is IInventoryEquippable equippable)
        {
            equippable.OnEquipped(gameObject);
        }
    }

    private void StowSlot(InventorySlot slot)
    {
        if (slot.Item is IInventoryEquippable equippable)
        {
            equippable.OnStowed(gameObject);
        }

        Transform itemTransform = slot.Item.Rigidbody.transform;
        itemTransform.SetParent(inventoryRoot, false);
        itemTransform.localPosition = Vector3.zero;
        itemTransform.localRotation = Quaternion.identity;
        if (hideStoredItems && slot.WasActive)
        {
            slot.Item.Rigidbody.gameObject.SetActive(false);
        }
    }

    private void RemoveSlotAt(int index, GameObject owner, bool destroyItem)
    {
        if (index < 0 || index >= slots.Count)
        {
            return;
        }

        InventorySlot slot = slots[index];
        Rigidbody rb = slot.Item != null ? slot.Item.Rigidbody : null;

        if (slot.Item is IInventoryEquippable equippable && index == activeIndex)
        {
            equippable.OnStowed(gameObject);
        }

        if (slot.Item != null)
        {
            slot.Item.OnDrop(owner);
        }

        if (rb != null && !destroyItem)
        {
            rb.transform.SetParent(slot.OriginalParent, true);
            rb.isKinematic = slot.WasKinematic;
            rb.detectCollisions = slot.DetectCollisions;
            if (hideStoredItems && slot.WasActive)
            {
                rb.gameObject.SetActive(true);
            }
        }

        slots.RemoveAt(index);

        if (rb != null && destroyItem)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(rb.gameObject);
            }
            else
            {
                Object.DestroyImmediate(rb.gameObject);
            }
        }

        if (slots.Count == 0)
        {
            activeIndex = -1;
            return;
        }

        activeIndex = Mathf.Clamp(index, 0, slots.Count - 1);
        EquipSlot(slots[activeIndex]);
    }

    private static IPickupable FindPickupable(GameObject target)
    {
        if (target.TryGetComponent(out IPickupable direct))
        {
            return direct;
        }

        Rigidbody rb = target.GetComponentInParent<Rigidbody>();
        if (rb != null && rb.TryGetComponent(out IPickupable rigidbodyOwner))
        {
            return rigidbodyOwner;
        }

        Transform parent = target.transform.parent;
        if (parent != null && parent.TryGetComponent(out IPickupable parentPickupable))
        {
            return parentPickupable;
        }

        return null;
    }

    private void TickRuntimeInventoryItems(float deltaTime)
    {
        if (slots.Count <= 0 || deltaTime <= 0f)
        {
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            IPickupable item = slots[i].Item;
            if (item is not IInventoryRuntimeTickable tickable)
            {
                continue;
            }

            tickable.OnInventoryTick(gameObject, i == activeIndex, deltaTime);
        }
    }

    private void ConfigureViewPointRotationLag()
    {
        if (viewPointTransform == null)
        {
            return;
        }

        if (useViewPointRotationLag && createRuntimeLagPivot && Application.isPlaying)
        {
            Transform followParent = viewPointTransform.parent;
            if (followParent == null)
            {
                return;
            }

            if (runtimeLagPivot == null)
            {
                Transform existingPivot = followParent.Find("ViewPointLagPivot");
                if (existingPivot != null)
                {
                    runtimeLagPivot = existingPivot;
                }
                else
                {
                    GameObject pivot = new GameObject("ViewPointLagPivot");
                    pivot.transform.SetParent(followParent, false);
                    pivot.transform.localPosition = viewPointTransform.localPosition;
                    pivot.transform.localRotation = viewPointTransform.localRotation;
                    runtimeLagPivot = pivot.transform;
                }
            }

            rotationLag = runtimeLagPivot.GetComponent<ChildRotationLag>();
            if (rotationLag == null)
            {
                rotationLag = runtimeLagPivot.gameObject.AddComponent<ChildRotationLag>();
            }

            viewPointTransform = runtimeLagPivot;
            return;
        }

        rotationLag = viewPointTransform.GetComponent<ChildRotationLag>();
        if (rotationLag == null)
        {
            rotationLag = viewPointTransform.gameObject.AddComponent<ChildRotationLag>();
        }
    }

    private void ApplyRotationLagSettings()
    {
        if (rotationLag == null)
        {
            return;
        }

        if (!useViewPointRotationLag)
        {
            rotationLag.enabled = false;
            return;
        }

        rotationLag.enabled = true;
        rotationLag.parentToFollow = viewPointTransform.parent;
        rotationLag.followSpeed = Mathf.Max(0.01f, viewPointRotationFollowSpeed);
        rotationLag.maxAngle = Mathf.Clamp(viewPointRotationMaxAngle, 0f, 45f);
    }
}
