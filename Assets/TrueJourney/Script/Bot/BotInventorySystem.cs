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

        private void Awake()
        {
            if (inventoryRoot == null)
            {
                inventoryRoot = transform;
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
            Transform itemTransform = rb.transform;
            itemTransform.SetParent(inventoryRoot, false);
            itemTransform.localPosition = Vector3.zero;
            itemTransform.localRotation = Quaternion.identity;
            
            // Hide the object
            rb.gameObject.SetActive(false);

            slots.Add(slot);
            itemCount = slots.Count;
            
            Debug.Log($"[BotInventorySystem] Bot {gameObject.name} picked up item {rb.gameObject.name}. Total items: {itemCount}/{maxSlots}");

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
    }
}
