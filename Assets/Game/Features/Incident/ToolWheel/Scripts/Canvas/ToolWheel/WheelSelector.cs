using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    [SerializeField] private TMP_Text pageIndicatorLabel;
    [SerializeField] private Image pageIndicatorBackground;
    [SerializeField] private Vector2 pageIndicatorAnchoredPosition = new Vector2(0f, 154f);
    [SerializeField] private Vector2 pageIndicatorSize = new Vector2(180f, 34f);
    [SerializeField] private Color defaultWheelColor = new Color(0f, 1f, 0.63f, 0.5f);
    [SerializeField] private Color defaultLabelColor = Color.white;
    [SerializeField] private Color defaultIndicatorBackgroundColor = new Color(0f, 0f, 0f, 0.72f);

    private bool isSelectionWheelActive = false;
    private string currentPageLabel = "Core";
    private Color currentWheelColor;
    private Color currentLabelColor;
    private Color currentIndicatorBackgroundColor;
    private string[] currentSlotLabels;

    public bool IsSelectionWheelActive => isSelectionWheelActive;
    public System.Action<int> OnOptionSelected;

    public void OpenWheel()
    {
        isSelectionWheelActive = true;
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

    public void CloseWheel()
    {
        isSelectionWheelActive = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (selectionWheelCanvasGroup != null)
        {
            selectionWheelCanvasGroup.alpha = 0f;
            selectionWheelCanvasGroup.interactable = false;
            selectionWheelCanvasGroup.blocksRaycasts = false;
        }
    }

    private void Awake()
    {
        player = GameObject.Find("Player");
        previousSelection = -1;
        currentWheelColor = defaultWheelColor;
        currentLabelColor = defaultLabelColor;
        currentIndicatorBackgroundColor = defaultIndicatorBackgroundColor;
        InitializeMenuItems();
        EnsurePageIndicator();
        RefreshPageIndicator();
        ApplyWheelTheme();
        ApplySlotLabels();
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
        ApplySlotLabels();
    }

    public void SetPageTheme(string pageLabel, Color wheelColor)
    {
        SetPageTheme(pageLabel, wheelColor, defaultLabelColor, defaultIndicatorBackgroundColor);
    }

    public void SetPageTheme(string pageLabel, Color wheelColor, Color labelColor, Color indicatorBackgroundColor)
    {
        currentPageLabel = string.IsNullOrWhiteSpace(pageLabel) ? "Commands" : pageLabel;
        currentWheelColor = wheelColor;
        currentLabelColor = labelColor;
        currentIndicatorBackgroundColor = indicatorBackgroundColor;
        RefreshPageIndicator();
        ApplyWheelTheme();
    }

    public void SetSlotLabels(string[] labels)
    {
        currentSlotLabels = labels;
        ApplySlotLabels();
    }

    private void ApplyWheelTheme()
    {
        if (menuItems == null)
        {
            return;
        }

        for (int i = 0; i < menuItems.Length; i++)
        {
            GameObject menuItem = menuItems[i];
            if (menuItem == null)
            {
                continue;
            }

            Transform bgTransform = menuItem.transform.Find("BgW");
            if (bgTransform == null)
            {
                continue;
            }

            Image bgImage = bgTransform.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.color = currentWheelColor;
            }
        }
    }

    private void ApplySlotLabels()
    {
        if (menuItems == null)
        {
            return;
        }

        for (int i = 0; i < menuItems.Length; i++)
        {
            GameObject menuItem = menuItems[i];
            if (menuItem == null)
            {
                continue;
            }

            MenuItemS item = menuItem.GetComponent<MenuItemS>();
            if (item == null)
            {
                continue;
            }

            string label = currentSlotLabels != null && i < currentSlotLabels.Length
                ? currentSlotLabels[i]
                : (i + 1).ToString();
            item.SetDisplayLabel(label);
        }
    }

    private void EnsurePageIndicator()
    {
        if (selectionWheelCanvas == null)
        {
            return;
        }

        RectTransform canvasRect = selectionWheelCanvas.GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            return;
        }

        if (pageIndicatorBackground == null)
        {
            Transform existing = canvasRect.Find("PageIndicatorBackground");
            if (existing != null)
            {
                pageIndicatorBackground = existing.GetComponent<Image>();
            }
        }

        if (pageIndicatorBackground == null)
        {
            GameObject backgroundObject = new GameObject("PageIndicatorBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.SetParent(canvasRect, false);
            backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.anchoredPosition = pageIndicatorAnchoredPosition;
            backgroundRect.sizeDelta = pageIndicatorSize;

            pageIndicatorBackground = backgroundObject.GetComponent<Image>();
            pageIndicatorBackground.raycastTarget = false;
        }

        if (pageIndicatorLabel == null)
        {
            Transform existing = pageIndicatorBackground.transform.Find("PageIndicatorLabel");
            if (existing != null)
            {
                pageIndicatorLabel = existing.GetComponent<TMP_Text>();
            }
        }

        if (pageIndicatorLabel == null)
        {
            GameObject textObject = new GameObject("PageIndicatorLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(pageIndicatorBackground.transform, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 20f;
            text.fontStyle = FontStyles.Bold;
            text.enableAutoSizing = false;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            pageIndicatorLabel = text;
        }
    }

    private void RefreshPageIndicator()
    {
        EnsurePageIndicator();

        if (pageIndicatorBackground != null)
        {
            pageIndicatorBackground.color = currentIndicatorBackgroundColor;
            RectTransform backgroundRect = pageIndicatorBackground.rectTransform;
            backgroundRect.anchoredPosition = pageIndicatorAnchoredPosition;
            backgroundRect.sizeDelta = pageIndicatorSize;
        }

        if (pageIndicatorLabel != null)
        {
            pageIndicatorLabel.text = currentPageLabel;
            pageIndicatorLabel.color = currentLabelColor;
        }
    }

    private void Update()
    {
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
            OnOptionSelected?.Invoke(selection);
            CloseWheel();
        }
    }
}
