using UnityEngine;
using UnityEngine.UI;

public class WheelSelector : MonoBehaviour
{
    [Header("Wheel Data")]
    public Vector2 mousePosition;
    public float currentAngle;
    public int selection;
    private int previousSelection;
    [HideInInspector]
    public GameObject[] menuItems;
    private GameObject player;

    [Header("Selection Wheel Settings")]
    [SerializeField] private GameObject selectionWheelCanvas;
    [SerializeField] private CanvasGroup selectionWheelCanvasGroup;
    [SerializeField] private RectTransform menuItemsParent;
    [SerializeField] private GameObject menuItemPrefab;
    [SerializeField][Range(1, 16)] private int menuItemCount = 4;
    [SerializeField] private float menuItemRadius = 130f;

    private bool isSelectionWheelActive = false;
    private int previousSelectedItemIndex = -1;

    private void Awake()
    {
        player = GameObject.Find("Player");
        previousSelection = -1;
        InitializeMenuItems();
        if (selectionWheelCanvasGroup != null)
        {
            selectionWheelCanvasGroup.alpha = 0f;
            selectionWheelCanvasGroup.interactable = false;
            selectionWheelCanvasGroup.blocksRaycasts = false;
        }
    }

    private void InitializeMenuItems()
    {
        if (menuItemCount <= 0)
        {
            Debug.LogWarning("WheelSelector: menuItemCount must be at least 1.");
            menuItems = new GameObject[0];
            return;
        }

        if (menuItemPrefab == null)
        {
            Debug.LogError("WheelSelector: menuItemPrefab is not assigned. Cannot auto-create menu items.");
            return;
        }

        if (menuItemsParent == null)
        {
            if (selectionWheelCanvas != null)
            {
                menuItemsParent = selectionWheelCanvas.GetComponent<RectTransform>();
            }

            if (menuItemsParent == null)
            {
                Debug.LogError("WheelSelector: menuItemsParent is not assigned and selectionWheelCanvas has no RectTransform.");
                return;
            }
        }

        // Remove existing auto-created children with MenuItemS
        for (int i = menuItemsParent.childCount - 1; i >= 0; i--)
        {
            Transform child = menuItemsParent.GetChild(i);
            if (child.GetComponent<MenuItemS>() != null)
            {
                Destroy(child.gameObject);
            }
        }

        menuItems = new GameObject[menuItemCount];

        for (int i = 0; i < menuItemCount; i++)
        {
            GameObject instance = Instantiate(menuItemPrefab, menuItemsParent);
            instance.name = $"MenuItem_{i}";
            RectTransform itemRect = instance.GetComponent<RectTransform>();
            float stepAngle = 360f / menuItemCount;
            float angle = 90 + stepAngle * i; // start at 12 o'clock and go counter-clockwise
            if (itemRect != null)
            {
                itemRect.anchorMin = Vector2.one * 0.5f;
                itemRect.anchorMax = Vector2.one * 0.5f;
                itemRect.pivot = Vector2.one * 0.5f;
                float rad = angle * Mathf.Deg2Rad;
                itemRect.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * menuItemRadius;
                itemRect.localEulerAngles = new Vector3(0, 0, -angle + 90f);
            }

            menuItems[i] = instance;

            MenuItemS item = instance.GetComponent<MenuItemS>();
            if (item != null)
            {
                item.Deselect();
            }

            Transform bgW = instance.transform.Find("BgW");
            if (bgW != null)
            {
                Image bgImage = bgW.GetComponent<Image>();
                if (bgImage != null)
                {
                    bgImage.type = Image.Type.Filled;
                    bgImage.fillMethod = Image.FillMethod.Radial360;
                    bgImage.fillOrigin = (int)Image.Origin360.Top;
                    bgImage.fillAmount = 1f / menuItemCount;
                    bgImage.fillClockwise = false;
                }
                else
                {
                    Debug.LogWarning("WheelSelector: 'BgW' exists but has no Image component.");
                }
            }
            else
            {
                Debug.LogWarning("WheelSelector: menu item prefab does not contain child 'BgW'.");
            }
        }
        menuItemsParent.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, -360f / menuItemCount);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            isSelectionWheelActive = !isSelectionWheelActive;
            if (isSelectionWheelActive)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                previousSelection = -1;
                if (selectionWheelCanvasGroup != null)
                {
                    selectionWheelCanvasGroup.alpha = 1f;
                    selectionWheelCanvasGroup.interactable = true;
                    selectionWheelCanvasGroup.blocksRaycasts = true;
                }
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                if (selectionWheelCanvasGroup != null)
                {
                    selectionWheelCanvasGroup.alpha = 0f;
                    selectionWheelCanvasGroup.interactable = false;
                    selectionWheelCanvasGroup.blocksRaycasts = false;
                }
            }
        }

        if (!isSelectionWheelActive)
        {
            return;
        }

        if (menuItems == null || menuItems.Length == 0)
        {
            return;
        }

        // Tính toán góc và xác định mục được chọn khi selection wheel đang mở
        mousePosition = new Vector2(
            Input.mousePosition.x - Screen.width / 2,
            Input.mousePosition.y - Screen.height / 2
        );

        float rawAngle = Mathf.Atan2(mousePosition.y, mousePosition.x) * Mathf.Rad2Deg;
        if (rawAngle < 0)
        {
            rawAngle += 360f;
        }

        // Convert from Atan2's 3h-origin CCW to 12h-origin CW
        currentAngle = (90f - rawAngle + 360f) % 360f;

        float stepAngle = 360f / menuItems.Length;
        selection = Mathf.FloorToInt(currentAngle / stepAngle);
        if (selection >= menuItems.Length)
        {
            selection = 0;
        }


        if (selection != previousSelection)
        {
            if (previousSelection >= 0 && previousSelection < menuItems.Length)
            {
                var previousItemScript = menuItems[previousSelection].GetComponent<MenuItemS>();
                if (previousItemScript != null)
                {
                    previousItemScript.Deselect();
                }
            }

            previousSelection = selection;
            if (selection >= 0 && selection < menuItems.Length)
            {
                var currentItemScript = menuItems[selection].GetComponent<MenuItemS>();
                if (currentItemScript != null)
                {
                    currentItemScript.Select();
                }
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (previousSelectedItemIndex == selection)
                return;

            previousSelectedItemIndex = selection;
            HandleSelection(selection);
            isSelectionWheelActive = false;
            if (selectionWheelCanvasGroup != null)
            {
                selectionWheelCanvasGroup.alpha = 0f;
                selectionWheelCanvasGroup.interactable = false;
                selectionWheelCanvasGroup.blocksRaycasts = false;
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void HandleSelection(int selectedIndex)
    {
        switch (selectedIndex)
        {
            case 0:
                Debug.Log("Selection Wheel: Option 0 selected (FireAxe)");
                // TODO: add logic for FireAxe
                break;
            case 1:
                Debug.Log("Selection Wheel: Option 1 selected");
                // TODO: add logic for option 1
                break;
            case 2:
                Debug.Log("Selection Wheel: Option 2 selected");
                // TODO: add logic for option 2
                break;
            case 3:
                Debug.Log("Selection Wheel: Option 3 selected");
                // TODO: add logic for option 3
                break;
            case 4:
                Debug.Log("Selection Wheel: Option 4 selected");
                // TODO: add logic for option 4
                break;
            case 5:
                Debug.Log("Selection Wheel: Option 5 selected");
                // TODO: add logic for option 5
                break;
            case 6:
                Debug.Log("Selection Wheel: Option 6 selected");
                // TODO: add logic for option 6
                break;
            case 7:
                Debug.Log("Selection Wheel: Option 7 selected");
                // TODO: add logic for option 7
                break;
            default:
                Debug.LogWarning($"Selection Wheel: unknown selection {selectedIndex}");
                break;
        }
    }
}
