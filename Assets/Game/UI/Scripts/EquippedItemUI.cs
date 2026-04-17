using UnityEngine;
using UnityEngine.UI;
using TrueJourney.BotBehavior;

namespace FF3D.UI
{
    [System.Serializable]
    public struct ToolIconMapping
    {
        public BreakToolKind ToolKind;
        public Sprite Icon;
    }

    public class EquippedItemUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FPSInventorySystem playerInventory;
        [SerializeField] private Image itemIconImage;

        [Header("Settings")]
        [SerializeField] private Sprite emptySlotSprite;
        [SerializeField] private ToolIconMapping[] toolIcons;
        
        private GameObject currentHeldObject;

        private void Update()
        {
            if (playerInventory == null || itemIconImage == null) return;

            if (!playerInventory.HasItem)
            {
                if (currentHeldObject != null)
                {
                    SetEmptyState();
                }
                return;
            }

            GameObject heldObj = playerInventory.HeldObject;

            if (heldObj != currentHeldObject)
            {
                currentHeldObject = heldObj;
                UpdateIcon(heldObj);
            }
        }

        private void UpdateIcon(GameObject item)
        {
            Tool tool = item.GetComponent<Tool>();
            
            if (tool != null && tool.ToolKind != BreakToolKind.None)
            {
                Sprite foundIcon = null;
                if (toolIcons != null)
                {
                    for (int i = 0; i < toolIcons.Length; i++)
                    {
                        if (toolIcons[i].ToolKind == tool.ToolKind)
                        {
                            foundIcon = toolIcons[i].Icon;
                            break;
                        }
                    }
                }

                if (foundIcon != null)
                {
                    itemIconImage.sprite = foundIcon;
                    itemIconImage.enabled = true;
                    return;
                }
            }
            
            // Fallback to empty state if not a tool or no icon mapped
            SetEmptyState();
        }

        private void SetEmptyState()
        {
            currentHeldObject = null;
            if (emptySlotSprite != null)
            {
                itemIconImage.sprite = emptySlotSprite;
                itemIconImage.enabled = true;
            }
            else
            {
                itemIconImage.enabled = false;
            }
        }
    }
}
