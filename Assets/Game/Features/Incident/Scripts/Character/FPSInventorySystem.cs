using UnityEngine;

public class FPSInventorySystem : MonoBehaviour
{
    [Header("Pickup")]
    [SerializeField] private bool allowPickup = true;
    [SerializeField] private int maxSlots = 6;
    [SerializeField] private Transform equipRoot;
    [SerializeField] private Transform inventoryRoot;
    [SerializeField] private bool hideStoredItems = true;

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

    public bool HasItem => activeIndex >= 0 && activeIndex < slots.Count;
    public int ItemCount => slots.Count;
    public int MaxSlots => maxSlots;
    public GameObject HeldObject => HasItem ? slots[activeIndex].Item.Rigidbody.gameObject : null;

    private void Awake()
    {
        if (equipRoot == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                GameObject hold = new GameObject("EquipRoot");
                hold.transform.SetParent(cam.transform, false);
                equipRoot = hold.transform;
            }
        }

        if (inventoryRoot == null)
        {
            GameObject root = new GameObject("InventoryRoot");
            root.transform.SetParent(transform, false);
            inventoryRoot = root.transform;
        }
    }

    private void LateUpdate()
    {
        TickRuntimeInventoryItems(Time.deltaTime);
    }

    public bool TryPickup(GameObject target, GameObject picker)
    {
        if (!allowPickup || target == null || equipRoot == null || maxSlots <= 0)
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

        if (HasItem && HandOccupancyUtility.BlocksInventoryStow(HeldObject, picker))
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
        int newSlotIndex = slots.Count - 1;
        bool pickupableBlocksStow = PickupableBlocksInventoryStow(pickupable, picker);

        if (!HasItem)
        {
            activeIndex = newSlotIndex;
            EquipSlot(slot);
        }
        else if (pickupableBlocksStow)
        {
            if (ActiveItemBlocksSelectionChange())
            {
                slots.RemoveAt(newSlotIndex);
                pickupable.OnDrop(picker);
                RestoreDroppedPickup(slot);
                return false;
            }

            StowSlot(slots[activeIndex]);
            activeIndex = newSlotIndex;
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

        if (HasItem && ActiveItemBlocksSelectionChange())
        {
            return index == activeIndex;
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

    private bool ActiveItemBlocksSelectionChange()
    {
        return HasItem &&
            HandOccupancyUtility.BlocksInventorySelectionChange(HeldObject, gameObject);
    }

    private static bool PickupableBlocksInventoryStow(IPickupable pickupable, GameObject owner)
    {
        return pickupable != null &&
            pickupable.Rigidbody != null &&
            HandOccupancyUtility.BlocksInventoryStow(pickupable.Rigidbody.gameObject, owner);
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
        itemTransform.SetParent(equipRoot, false);
        ApplyEquippedPose(slot.Item, itemTransform);
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

    private static void RestoreDroppedPickup(InventorySlot slot)
    {
        if (slot == null || slot.Item == null || slot.Item.Rigidbody == null)
        {
            return;
        }

        Rigidbody rb = slot.Item.Rigidbody;
        rb.transform.SetParent(slot.OriginalParent, true);
        rb.isKinematic = slot.WasKinematic;
        rb.detectCollisions = slot.DetectCollisions;
        if (slot.WasActive)
        {
            rb.gameObject.SetActive(true);
        }
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

    private static void ApplyEquippedPose(IPickupable pickupable, Transform itemTransform)
    {
        if (itemTransform == null)
        {
            return;
        }

        if (TryGetEquippedPose(pickupable, itemTransform, out Vector3 localPosition, out Vector3 localEulerAngles))
        {
            itemTransform.localPosition = localPosition;
            itemTransform.localRotation = Quaternion.Euler(localEulerAngles);
            return;
        }

        itemTransform.localPosition = Vector3.zero;
        itemTransform.localRotation = Quaternion.identity;
    }

    private static bool TryGetEquippedPose(IPickupable pickupable, Transform itemTransform, out Vector3 localPosition, out Vector3 localEulerAngles)
    {
        localPosition = default;
        localEulerAngles = default;

        if (pickupable is PlayerEquippedItemPoseProfile pickupableProfile &&
            pickupableProfile.TryGetEquippedPose(out localPosition, out localEulerAngles))
        {
            return true;
        }

        if (itemTransform == null)
        {
            return false;
        }

        MonoBehaviour[] components = itemTransform.GetComponents<MonoBehaviour>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] is PlayerEquippedItemPoseProfile profile &&
                profile.TryGetEquippedPose(out localPosition, out localEulerAngles))
            {
                return true;
            }
        }

        return false;
    }

}
