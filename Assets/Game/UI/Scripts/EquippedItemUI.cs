using UnityEngine;
using UnityEngine.UI;
using TMPro;
using StarterAssets;

namespace FF3D.UI
{
    public class EquippedItemUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FPSInventorySystem playerInventory;
        [SerializeField] private Image itemIconImage;

        [Header("Settings")]
        [SerializeField] private Sprite emptySlotSprite;

        [Header("Extinguisher Fill UI")]
        [SerializeField] private GameObject extinguisherFillRoot;
        [SerializeField] private Image extinguisherFillImage;
        [SerializeField] private TMP_Text extinguisherFillValueText;
        [SerializeField] private string extinguisherFillValueFormat = "{0:0.#} / {1:0.#}";
        
        private GameObject currentHeldObject;
        private FireExtinguisher currentHeldExtinguisher;

        private void Awake()
        {
            ResolveReferences();
            ResolveExtinguisherFillReferences();
            SetExtinguisherFillVisible(false);
            HideItemIcon();
        }

        private void Update()
        {
            ResolveReferences();
            if (playerInventory == null || itemIconImage == null) return;

            if (!playerInventory.HasItem)
            {
                if (currentHeldObject != null)
                {
                    SetEmptyState();
                }

                UpdateExtinguisherFillRuntime();
                return;
            }

            GameObject heldObj = playerInventory.HeldObject;

            if (heldObj != currentHeldObject)
            {
                currentHeldObject = heldObj;
                UpdateIcon(heldObj);
            }

            UpdateExtinguisherFillRuntime();
        }

        private void ResolveReferences()
        {
            if (playerInventory == null)
            {
                FirstPersonController playerController = FindAnyObjectByType<FirstPersonController>();
                if (playerController != null)
                {
                    playerInventory = playerController.GetComponent<FPSInventorySystem>();
                }

                if (playerInventory == null)
                {
                    playerInventory = FindAnyObjectByType<FPSInventorySystem>();
                }
            }

            if (itemIconImage == null)
            {
                itemIconImage = GetComponent<Image>();
            }

            if (itemIconImage == null)
            {
                Transform iconTransform = FindDeepChildByName(transform, "IconEquipment");
                if (iconTransform != null)
                {
                    itemIconImage = iconTransform.GetComponent<Image>();
                }
            }
        }

        private void UpdateIcon(GameObject item)
        {
            Item itemData = item != null ? item.GetComponent<Item>() : null;
            if (itemData != null && itemData.ItemIcon != null)
            {
                ShowItemIcon(itemData.ItemIcon);
            }
            else
            {
                HideItemIcon();
            }

            FireExtinguisher extinguisher = item != null ? item.GetComponent<FireExtinguisher>() : null;
            if (extinguisher != null)
            {
                currentHeldExtinguisher = extinguisher;
            }
            else
            {
                currentHeldExtinguisher = null;
            }
        }

        private void SetEmptyState()
        {
            currentHeldObject = null;
            currentHeldExtinguisher = null;
            SetExtinguisherFillVisible(false);
            UpdateExtinguisherFillVisual(0f, 0f);
            HideItemIcon();
        }

        private void UpdateExtinguisherFillRuntime()
        {
            if (currentHeldExtinguisher == null)
            {
                SetExtinguisherFillVisible(false);
                return;
            }

            SetExtinguisherFillVisible(true);
            UpdateExtinguisherFillVisual(currentHeldExtinguisher.CurrentCharge, currentHeldExtinguisher.MaxCharge);
        }

        private void UpdateExtinguisherFillVisual(float current, float max)
        {
            if (extinguisherFillImage != null)
            {
                extinguisherFillImage.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            }

            if (extinguisherFillValueText != null)
            {
                extinguisherFillValueText.text = string.Format(
                    string.IsNullOrWhiteSpace(extinguisherFillValueFormat) ? "{0:0.#} / {1:0.#}" : extinguisherFillValueFormat,
                    Mathf.Max(0f, current),
                    Mathf.Max(0f, max));
            }
        }

        private void SetExtinguisherFillVisible(bool visible)
        {
            if (extinguisherFillRoot != null && extinguisherFillRoot.activeSelf != visible)
            {
                extinguisherFillRoot.SetActive(visible);
            }
        }

        private void ResolveExtinguisherFillReferences()
        {
            if (extinguisherFillRoot == null)
            {
                Transform fillRoot = FindDeepChildByName(transform, "FillBar");
                if (fillRoot != null)
                {
                    extinguisherFillRoot = fillRoot.gameObject;
                }
            }

            if (extinguisherFillImage == null && extinguisherFillRoot != null)
            {
                extinguisherFillImage = extinguisherFillRoot.GetComponent<Image>();
            }

            if (extinguisherFillValueText == null)
            {
                Transform valueText = FindDeepChildByName(transform, "Txt_Value");
                if (valueText != null)
                {
                    extinguisherFillValueText = valueText.GetComponent<TMP_Text>();
                }
            }
        }

        private static Transform FindDeepChildByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = FindDeepChildByName(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void ShowItemIcon(Sprite icon)
        {
            if (itemIconImage == null)
            {
                return;
            }

            itemIconImage.sprite = icon;
            itemIconImage.enabled = true;
            SetItemIconAlpha(1f);
        }

        private void HideItemIcon()
        {
            if (itemIconImage == null)
            {
                return;
            }

            if (emptySlotSprite != null)
            {
                itemIconImage.sprite = emptySlotSprite;
            }

            itemIconImage.enabled = true;
            SetItemIconAlpha(0f);
        }

        private void SetItemIconAlpha(float alpha)
        {
            if (itemIconImage == null)
            {
                return;
            }

            Color color = itemIconImage.color;
            color.a = Mathf.Clamp01(alpha);
            itemIconImage.color = color;
        }
    }
}
