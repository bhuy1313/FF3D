using UnityEngine;

public class FPSInventorySystem : MonoBehaviour
{
    [Header("Pickup")]
    [SerializeField] private bool allowPickup = true;
    [SerializeField] private int maxSlots = 4;
    [SerializeField] private Transform viewPointTransform;
    [SerializeField] private Transform inventoryRoot;
    [SerializeField] private Vector3 viewPoint;
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

        if (inventoryRoot == null)
        {
            GameObject root = new GameObject("InventoryRoot");
            root.transform.SetParent(transform, false);
            inventoryRoot = root.transform;
        }
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

        InventorySlot slot = slots[activeIndex];
        Rigidbody rb = slot.Item.Rigidbody;
        rb.transform.SetParent(slot.OriginalParent, true);
        rb.isKinematic = slot.WasKinematic;
        rb.detectCollisions = slot.DetectCollisions;
        if (hideStoredItems && slot.WasActive)
        {
            rb.gameObject.SetActive(true);
        }

        slot.Item.OnDrop(dropper);

        slots.RemoveAt(activeIndex);
        if (slots.Count == 0)
        {
            activeIndex = -1;
            return;
        }

        activeIndex = -1;
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
    }

    private void StowSlot(InventorySlot slot)
    {
        Transform itemTransform = slot.Item.Rigidbody.transform;
        itemTransform.SetParent(inventoryRoot, false);
        itemTransform.localPosition = Vector3.zero;
        itemTransform.localRotation = Quaternion.identity;
        if (hideStoredItems && slot.WasActive)
        {
            slot.Item.Rigidbody.gameObject.SetActive(false);
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
}
