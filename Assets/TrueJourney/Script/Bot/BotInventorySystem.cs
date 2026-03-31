using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

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
        [SerializeField] private BotEquippedItemPoseKey equippedPoseKey = BotEquippedItemPoseKey.Default;
        [SerializeField] private Vector3 equippedLocalPosition = new Vector3(0f, 1.1f, 0.45f);
        [SerializeField] private Vector3 equippedLocalEulerAngles;

        [Header("Hand IK")]
        [SerializeField] private bool driveRightHandIk = true;
        [SerializeField] private TwoBoneIKConstraint rightHandIkConstraint;
        [SerializeField] private Transform rightHandIkTarget;
        [SerializeField] private Transform rightHandIkHint;
        [SerializeField] private bool driveLeftHandIk = true;
        [SerializeField] private TwoBoneIKConstraint leftHandIkConstraint;
        [SerializeField] private Transform leftHandIkTarget;
        [SerializeField] private Transform leftHandIkHint;

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
        private Vector3 defaultRightHandIkLocalPosition;
        private Quaternion defaultRightHandIkLocalRotation = Quaternion.identity;
        private Vector3 defaultRightHandIkHintLocalPosition;
        private Quaternion defaultRightHandIkHintLocalRotation = Quaternion.identity;
        private float defaultRightHandIkWeight;
        private bool hasDefaultRightHandIkTargetPose;
        private bool hasDefaultRightHandIkHintPose;
        private Vector3 defaultLeftHandIkLocalPosition;
        private Quaternion defaultLeftHandIkLocalRotation = Quaternion.identity;
        private Vector3 defaultLeftHandIkHintLocalPosition;
        private Quaternion defaultLeftHandIkHintLocalRotation = Quaternion.identity;
        private float defaultLeftHandIkWeight;
        private bool hasDefaultLeftHandIkTargetPose;
        private bool hasDefaultLeftHandIkHintPose;

        public bool IsFull => slots.Count >= maxSlots;
        public int ItemCount => slots.Count;
        public int MaxSlots => maxSlots;
        public bool HasItem => activeIndex >= 0 && activeIndex < slots.Count;
        public Transform InventoryRoot => inventoryRoot;
        public Transform EquippedRoot => equippedRoot;
        public BotEquippedItemPoseKey EquippedPoseKey => equippedPoseKey;

        private int activeIndex = -1;

        private void Awake()
        {
            ResolveDefaultRoots();
            ResolveHandIkReferences();

            if (inventoryRoot == null)
            {
                inventoryRoot = transform;
            }

            if (equippedRoot == null)
            {
                equippedRoot = transform;
            }

            CacheHandIkDefaults();
        }

        private void ResolveDefaultRoots()
        {
            Transform viewPoint = null;
            Transform rightHand = null;
            Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < childTransforms.Length; i++)
            {
                Transform candidate = childTransforms[i];
                if (candidate == null)
                {
                    continue;
                }

                if (viewPoint == null && candidate.name == "ViewPoint")
                {
                    viewPoint = candidate;
                }

                if (rightHand == null && candidate.name == "mixamorig:RightHand")
                {
                    rightHand = candidate;
                }

                if (viewPoint != null && rightHand != null)
                {
                    break;
                }
            }

            if (equippedRoot == null)
            {
                equippedRoot = rightHand != null ? rightHand : viewPoint;
            }

            if (inventoryRoot == null && viewPoint != null)
            {
                Transform inventoryChild = viewPoint.Find("Inventory");
                inventoryRoot = inventoryChild != null ? inventoryChild : viewPoint;
            }
        }

        private void ResolveHandIkReferences()
        {
            ResolveHandIkConstraints();
            ResolveHandIkTargetsAndHints();
        }

        private void ResolveHandIkConstraints()
        {
            if (rightHandIkConstraint != null && leftHandIkConstraint != null)
            {
                return;
            }

            TwoBoneIKConstraint[] constraints = GetComponentsInChildren<TwoBoneIKConstraint>(true);
            for (int i = 0; i < constraints.Length; i++)
            {
                TwoBoneIKConstraint candidate = constraints[i];
                if (candidate == null)
                {
                    continue;
                }

                if (rightHandIkConstraint == null && candidate.gameObject.name == "RightHandIK")
                {
                    rightHandIkConstraint = candidate;
                }

                if (leftHandIkConstraint == null && candidate.gameObject.name == "LeftHandIK")
                {
                    leftHandIkConstraint = candidate;
                }

                if (rightHandIkConstraint != null && leftHandIkConstraint != null)
                {
                    return;
                }
            }
        }

        private void ResolveHandIkTargetsAndHints()
        {
            Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < childTransforms.Length; i++)
            {
                Transform candidate = childTransforms[i];
                if (candidate == null)
                {
                    continue;
                }

                if (rightHandIkTarget == null && candidate.name == "RightHandIK_target")
                {
                    rightHandIkTarget = candidate;
                }

                if (rightHandIkHint == null && candidate.name == "RightHandIK_hint")
                {
                    rightHandIkHint = candidate;
                }

                if (leftHandIkTarget == null && candidate.name == "LeftHandIK_target")
                {
                    leftHandIkTarget = candidate;
                }

                if (leftHandIkHint == null && candidate.name == "LeftHandIK_hint")
                {
                    leftHandIkHint = candidate;
                }

                if (rightHandIkTarget != null &&
                    rightHandIkHint != null &&
                    leftHandIkTarget != null &&
                    leftHandIkHint != null)
                {
                    return;
                }
            }
        }

        private void CacheHandIkDefaults()
        {
            if (rightHandIkTarget != null)
            {
                defaultRightHandIkLocalPosition = rightHandIkTarget.localPosition;
                defaultRightHandIkLocalRotation = rightHandIkTarget.localRotation;
                hasDefaultRightHandIkTargetPose = true;
            }

            if (rightHandIkHint != null)
            {
                defaultRightHandIkHintLocalPosition = rightHandIkHint.localPosition;
                defaultRightHandIkHintLocalRotation = rightHandIkHint.localRotation;
                hasDefaultRightHandIkHintPose = true;
            }

            defaultRightHandIkWeight = rightHandIkConstraint != null ? rightHandIkConstraint.weight : 0f;

            if (leftHandIkTarget != null)
            {
                defaultLeftHandIkLocalPosition = leftHandIkTarget.localPosition;
                defaultLeftHandIkLocalRotation = leftHandIkTarget.localRotation;
                hasDefaultLeftHandIkTargetPose = true;
            }

            if (leftHandIkHint != null)
            {
                defaultLeftHandIkHintLocalPosition = leftHandIkHint.localPosition;
                defaultLeftHandIkHintLocalRotation = leftHandIkHint.localRotation;
                hasDefaultLeftHandIkHintPose = true;
            }

            defaultLeftHandIkWeight = leftHandIkConstraint != null ? leftHandIkConstraint.weight : 0f;
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
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
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

        public void SetEquippedPoseKey(BotEquippedItemPoseKey key)
        {
            if (equippedPoseKey == key)
            {
                return;
            }

            equippedPoseKey = key;
            RefreshEquippedItemPose();
        }

        public void ResetEquippedPoseKey()
        {
            SetEquippedPoseKey(BotEquippedItemPoseKey.Default);
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
            ApplyEquippedPose(slot, itemTransform);
            itemTransform.gameObject.SetActive(slot.WasActive);
        }

        private void StowSlot(InventorySlot slot)
        {
            Transform itemTransform = slot.Item.Rigidbody.transform;
            itemTransform.SetParent(inventoryRoot, false);
            itemTransform.localPosition = Vector3.zero;
            itemTransform.localRotation = Quaternion.identity;
            itemTransform.gameObject.SetActive(false);
            ResetHandIkPose();
        }

        private void ApplyEquippedPose(InventorySlot slot, Transform itemTransform)
        {
            if (slot == null || slot.Item == null || itemTransform == null)
            {
                ResetHandIkPose();
                return;
            }

            if (TryGetEquippedPose(slot.Item, itemTransform, CreateEquippedPoseContext(), out BotEquippedItemPose pose))
            {
                itemTransform.localPosition = pose.equippedLocalPosition;
                itemTransform.localRotation = Quaternion.Euler(pose.equippedLocalEulerAngles);
                ApplyHandIkPose(pose);
                return;
            }

            itemTransform.localPosition = equippedLocalPosition;
            itemTransform.localRotation = Quaternion.Euler(equippedLocalEulerAngles);
            ResetHandIkPose();
        }

        private void RefreshEquippedItemPose()
        {
            if (!HasItem)
            {
                return;
            }

            InventorySlot activeSlot = slots[activeIndex];
            if (activeSlot?.Item?.Rigidbody == null)
            {
                return;
            }

            ApplyEquippedPose(activeSlot, activeSlot.Item.Rigidbody.transform);
        }

        private BotEquippedItemPoseContext CreateEquippedPoseContext()
        {
            return new BotEquippedItemPoseContext
            {
                key = equippedPoseKey
            };
        }

        private static bool TryGetEquippedPose(IPickupable pickupable, Transform itemTransform, BotEquippedItemPoseContext context, out BotEquippedItemPose pose)
        {
            pose = default;
            if (pickupable is IBotContextualEquippedItemPoseSource contextualPickupPoseSource &&
                contextualPickupPoseSource.TryGetBotEquippedItemPose(context, out pose))
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
                MonoBehaviour component = components[i];
                if (component is IBotContextualEquippedItemPoseSource contextualPoseSource &&
                    contextualPoseSource.TryGetBotEquippedItemPose(context, out pose))
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyHandIkPose(BotEquippedItemPose pose)
        {
            ApplyRightHandIkPose(pose);
            ApplyLeftHandIkPose(pose);
        }

        private void ApplyRightHandIkPose(BotEquippedItemPose pose)
        {
            if (!driveRightHandIk || rightHandIkTarget == null)
            {
                return;
            }

            rightHandIkTarget.localPosition = pose.rightHandIkLocalPosition;
            rightHandIkTarget.localRotation = Quaternion.Euler(pose.rightHandIkLocalEulerAngles);
            ApplyRightHandIkHintPose(pose);

            if (rightHandIkConstraint != null)
            {
                rightHandIkConstraint.weight = pose.useRightHandIkTarget ? Mathf.Clamp01(pose.rightHandIkWeight) : 0f;
            }
        }

        private void ApplyLeftHandIkPose(BotEquippedItemPose pose)
        {
            if (!driveLeftHandIk || leftHandIkTarget == null)
            {
                return;
            }

            leftHandIkTarget.localPosition = pose.leftHandIkLocalPosition;
            leftHandIkTarget.localRotation = Quaternion.Euler(pose.leftHandIkLocalEulerAngles);
            ApplyLeftHandIkHintPose(pose);

            if (leftHandIkConstraint != null)
            {
                leftHandIkConstraint.weight = pose.useLeftHandIkTarget ? Mathf.Clamp01(pose.leftHandIkWeight) : 0f;
            }
        }

        private void ApplyRightHandIkHintPose(BotEquippedItemPose pose)
        {
            if (rightHandIkHint == null)
            {
                return;
            }

            if (!pose.useRightHandIkHint)
            {
                ResetRightHandIkHintPose();
                return;
            }

            rightHandIkHint.localPosition = pose.rightHandIkHintLocalPosition;
            rightHandIkHint.localRotation = Quaternion.Euler(pose.rightHandIkHintLocalEulerAngles);
        }

        private void ApplyLeftHandIkHintPose(BotEquippedItemPose pose)
        {
            if (leftHandIkHint == null)
            {
                return;
            }

            if (!pose.useLeftHandIkHint)
            {
                ResetLeftHandIkHintPose();
                return;
            }

            leftHandIkHint.localPosition = pose.leftHandIkHintLocalPosition;
            leftHandIkHint.localRotation = Quaternion.Euler(pose.leftHandIkHintLocalEulerAngles);
        }

        private void ResetHandIkPose()
        {
            ResetRightHandIkPose();
            ResetLeftHandIkPose();
        }

        private void ResetRightHandIkPose()
        {
            if (!driveRightHandIk)
            {
                return;
            }

            if (hasDefaultRightHandIkTargetPose && rightHandIkTarget != null)
            {
                rightHandIkTarget.localPosition = defaultRightHandIkLocalPosition;
                rightHandIkTarget.localRotation = defaultRightHandIkLocalRotation;
            }

            ResetRightHandIkHintPose();

            if (rightHandIkConstraint != null)
            {
                rightHandIkConstraint.weight = defaultRightHandIkWeight;
            }
        }

        private void ResetLeftHandIkPose()
        {
            if (!driveLeftHandIk)
            {
                return;
            }

            if (hasDefaultLeftHandIkTargetPose && leftHandIkTarget != null)
            {
                leftHandIkTarget.localPosition = defaultLeftHandIkLocalPosition;
                leftHandIkTarget.localRotation = defaultLeftHandIkLocalRotation;
            }

            ResetLeftHandIkHintPose();

            if (leftHandIkConstraint != null)
            {
                leftHandIkConstraint.weight = defaultLeftHandIkWeight;
            }
        }

        private void ResetRightHandIkHintPose()
        {
            if (!hasDefaultRightHandIkHintPose || rightHandIkHint == null)
            {
                return;
            }

            rightHandIkHint.localPosition = defaultRightHandIkHintLocalPosition;
            rightHandIkHint.localRotation = defaultRightHandIkHintLocalRotation;
        }

        private void ResetLeftHandIkHintPose()
        {
            if (!hasDefaultLeftHandIkHintPose || leftHandIkHint == null)
            {
                return;
            }

            leftHandIkHint.localPosition = defaultLeftHandIkHintLocalPosition;
            leftHandIkHint.localRotation = defaultLeftHandIkHintLocalRotation;
        }
    }
}
