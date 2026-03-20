using System.Collections.Generic;
using UnityEngine;

namespace TrueJourney.BotBehavior
{
    public class BotInventorySystem : MonoBehaviour
    {
        [Header("Inventory Settings")]
        [SerializeField] private int maxSlots = 6;
        [Tooltip("Optional: Transform where items will be parented to when picked up. If left empty, it will use this GameObject itself.")]
        [SerializeField] private Transform inventoryRoot;
        [Tooltip("Optional: Transform where the equipped item should be displayed.")]
        [SerializeField] private Transform equippedRoot;
        [SerializeField] private Vector3 equippedLocalPosition = new Vector3(0f, 1.1f, 0.45f);
        [SerializeField] private Vector3 equippedLocalEulerAngles;

        [Header("Debug")]
        [SerializeField] private bool logInventoryActions = false;
        
        [Header("Runtime Info")]
        [SerializeField] private int itemCount = 0;

        private class InventorySlot
        {
            public IPickupable Item;
            public Transform OriginalParent;
            public bool WasKinematic;
            public bool DetectCollisions;
            public bool WasActive;
        }

        private readonly List<InventorySlot> slots = new List<InventorySlot>();

        public bool IsFull => slots.Count >= maxSlots;
        public int ItemCount => slots.Count;
        public int MaxSlots => maxSlots;
        public bool HasItem => activeIndex >= 0 && activeIndex < slots.Count;
        public Transform InventoryRoot => inventoryRoot;
        public Transform EquippedRoot => equippedRoot;

        private int activeIndex = -1;

        private void Awake()
        {
            ResolveDefaultRoots();

            if (inventoryRoot == null)
            {
                inventoryRoot = transform;
            }

            if (equippedRoot == null)
            {
                equippedRoot = transform;
            }
        }

        private void ResolveDefaultRoots()
        {
            Transform viewPoint = null;
            Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < childTransforms.Length; i++)
            {
                if (childTransforms[i] != null && childTransforms[i].name == "ViewPoint")
                {
                    viewPoint = childTransforms[i];
                    break;
                }
            }

            if (viewPoint == null)
            {
                return;
            }

            if (equippedRoot == null)
            {
                equippedRoot = viewPoint;
            }

            if (inventoryRoot == null)
            {
                Transform inventoryChild = viewPoint.Find("Inventory");
                inventoryRoot = inventoryChild != null ? inventoryChild : viewPoint;
            }
        }

        /// <summary>
        /// Attempts to add a pickupable item to the bot's inventory invisibly.
        /// </summary>
        public bool TryPickup(IPickupable pickupable)
        {
            if (pickupable == null || pickupable.Rigidbody == null)
            {
                return false;
            }

            if (IsFull)
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

            // Disable physics so it doesn't collide while inside the "inventory"
            rb.isKinematic = true;
            rb.detectCollisions = false;

            // Trigger the item's pickup logic
            pickupable.OnPickup(gameObject);

            // Stow the item invisibly inside the designated inventory root
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

            itemCount = slots.Count;

            if (logInventoryActions)
            {
                Debug.Log($"[BotInventorySystem] Bot {gameObject.name} picked up item {rb.gameObject.name}. Total items: {itemCount}/{maxSlots}");
            }

            return true;
        }

        public bool TryPickup(IBotExtinguisherItem extinguisherItem)
        {
            return extinguisherItem is IPickupable pickupable && TryPickup(pickupable);
        }

        public bool TryGetItem<T>(out T item) where T : class
        {
            item = null;
            int index = FindFirstSlotIndex<T>();
            if (index < 0)
            {
                return false;
            }

            item = slots[index].Item as T;
            return item != null;
        }

        public bool TryEquipItem<T>(out T item) where T : class
        {
            item = null;
            int index = FindFirstSlotIndex<T>();
            if (index < 0)
            {
                return false;
            }

            if (activeIndex == index)
            {
                item = slots[index].Item as T;
                return item != null;
            }

            if (HasItem)
            {
                StowSlot(slots[activeIndex]);
            }

            activeIndex = index;
            EquipSlot(slots[activeIndex]);
            item = slots[activeIndex].Item as T;
            return item != null;
        }

        public bool TryEquipItem(IPickupable pickupable)
        {
            if (pickupable == null)
            {
                return false;
            }

            int index = -1;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Item == pickupable)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                return false;
            }

            if (activeIndex == index)
            {
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

        public void CollectItems<T>(List<T> results) where T : class
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            for (int i = 0; i < slots.Count; i++)
            {
                T item = slots[i].Item as T;
                if (item != null)
                {
                    results.Add(item);
                }
            }
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

        private int FindFirstSlotIndex<T>() where T : class
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Item is T)
                {
                    return i;
                }
            }

            return -1;
        }

        private void EquipSlot(InventorySlot slot)
        {
            Transform itemTransform = slot.Item.Rigidbody.transform;
            itemTransform.SetParent(equippedRoot != null ? equippedRoot : transform, false);
            itemTransform.localPosition = equippedLocalPosition;
            itemTransform.localRotation = Quaternion.Euler(equippedLocalEulerAngles);
            itemTransform.gameObject.SetActive(slot.WasActive);
        }

        private void StowSlot(InventorySlot slot)
        {
            Transform itemTransform = slot.Item.Rigidbody.transform;
            itemTransform.SetParent(inventoryRoot, false);
            itemTransform.localPosition = Vector3.zero;
            itemTransform.localRotation = Quaternion.identity;
            itemTransform.gameObject.SetActive(false);
        }
    }
}
