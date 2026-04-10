using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WheelSelector : MonoBehaviour
{
    [Header("Wheel State")]
    public Vector2 mousePosition;
    public float currentAngle;
    public int selection = -1;

    [Header("Selection Wheel Settings")]
    [SerializeField] private GameObject selectionWheelCanvas;
    [SerializeField] private CanvasGroup selectionWheelCanvasGroup;
    [SerializeField] private GraphicRaycaster graphicRaycaster;
    [SerializeField] private RectTransform menuItemsParent;
    [SerializeField] private GameObject menuItemPrefab;
    [SerializeField, Range(1, 16)] private int menuItemCount = 4;
    [SerializeField] private Vector2 slotSize = new Vector2(400f, 400f);
    [SerializeField] private TMP_Text pageIndicatorLabel;
    [SerializeField] private Image pageIndicatorBackground;
    [SerializeField] private Vector2 pageIndicatorAnchoredPosition = new Vector2(0f, 154f);
    [SerializeField] private Vector2 pageIndicatorSize = new Vector2(180f, 34f);
    [SerializeField] private Color defaultWheelColor = new Color(0f, 1f, 0.63f, 0.5f);
    [SerializeField] private Color defaultLabelColor = Color.white;
    [SerializeField] private Color defaultIndicatorBackgroundColor = new Color(0f, 0f, 0f, 0.72f);

    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay;
    [SerializeField] private Vector2 debugOverlayOffset = new Vector2(16f, 16f);

    [HideInInspector] public GameObject[] menuItems;

    private MenuItemS[] menuItemComponents;
    private string currentPageLabel = "Core";
    private Color currentWheelColor;
    private Color currentLabelColor;
    private Color currentIndicatorBackgroundColor;
    private string[] currentSlotLabels;
    private bool isSelectionWheelActive;
    private PointerEventData pointerEventData;
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>(16);
    private GUIStyle debugGuiStyle;
    private string hoveredTargetName = "(none)";
    private string hoveredSlotLabel = "-";
    private int hoveredSlotIndex = -1;
    private string hoverSource = "(none)";

    public bool IsSelectionWheelActive => isSelectionWheelActive;
    public System.Action<int> OnOptionSelected;

    private void Awake()
    {
        currentWheelColor = defaultWheelColor;
        currentLabelColor = defaultLabelColor;
        currentIndicatorBackgroundColor = defaultIndicatorBackgroundColor;

        ResolveReferences();
        RebuildMenuItems();
        EnsurePageIndicator();
        RefreshPageIndicator();
        RefreshAllVisuals();
        SetCanvasVisible(false);
    }

    private void OnValidate()
    {
        menuItemCount = Mathf.Max(1, menuItemCount);
        slotSize.x = Mathf.Max(1f, slotSize.x);
        slotSize.y = Mathf.Max(1f, slotSize.y);
        ResolveReferences();
    }

    public void OpenWheel()
    {
        isSelectionWheelActive = true;
        hoveredTargetName = "(none)";
        hoveredSlotLabel = "-";
        hoveredSlotIndex = -1;
        hoverSource = "None";
        selection = -1;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        RefreshSelectionVisuals();
        SetCanvasVisible(true);
    }

    public void CloseWheel()
    {
        isSelectionWheelActive = false;
        hoveredTargetName = "(none)";
        hoveredSlotLabel = "-";
        hoveredSlotIndex = -1;
        hoverSource = "None";
        selection = -1;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        RefreshSelectionVisuals();
        SetCanvasVisible(false);
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
        RefreshAllVisuals();
    }

    public void SetSlotLabels(string[] labels)
    {
        currentSlotLabels = labels;
        ApplySlotLabels();
    }

    private void Update()
    {
        if (!isSelectionWheelActive)
        {
            return;
        }

        UpdateHoveredSlotFromUi();
        ProcessPointerConfirm();
    }

    private void ResolveReferences()
    {
        if (selectionWheelCanvasGroup == null && selectionWheelCanvas != null)
        {
            selectionWheelCanvasGroup = selectionWheelCanvas.GetComponent<CanvasGroup>();
        }

        if (graphicRaycaster == null && selectionWheelCanvas != null)
        {
            graphicRaycaster = selectionWheelCanvas.GetComponent<GraphicRaycaster>();
        }

        if (menuItemsParent == null && selectionWheelCanvas != null)
        {
            RectTransform canvasRect = selectionWheelCanvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                Transform pieMenu = canvasRect.Find("PieMenu");
                menuItemsParent = pieMenu as RectTransform;
            }
        }
    }

    private void RebuildMenuItems()
    {
        if (menuItemsParent == null || menuItemPrefab == null)
        {
            return;
        }

        DestroyExistingMenuItems();

        menuItems = new GameObject[menuItemCount];
        menuItemComponents = new MenuItemS[menuItemCount];
        Vector2 resolvedSlotSize = ResolveSlotSize();

        for (int i = 0; i < menuItemCount; i++)
        {
            GameObject instance = Instantiate(menuItemPrefab, menuItemsParent);
            instance.name = $"MenuItem_{i}";

            MenuItemS item = instance.GetComponent<MenuItemS>();
            if (item == null)
            {
                continue;
            }

            item.ConfigureSlotLayout(i, menuItemCount, resolvedSlotSize);
            if (item.Button != null)
            {
                int capturedIndex = i;
                item.Button.onClick.RemoveAllListeners();
                item.Button.onClick.AddListener(() => HandleItemClicked(capturedIndex));
            }

            menuItems[i] = instance;
            menuItemComponents[i] = item;
        }
    }

    private void DestroyExistingMenuItems()
    {
        if (menuItemsParent == null)
        {
            return;
        }

        for (int i = menuItemsParent.childCount - 1; i >= 0; i--)
        {
            Transform child = menuItemsParent.GetChild(i);
            if (child.GetComponent<MenuItemS>() == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private Vector2 ResolveSlotSize()
    {
        if (menuItemsParent != null && menuItemsParent.rect.width > 1f && menuItemsParent.rect.height > 1f)
        {
            return menuItemsParent.rect.size;
        }

        return slotSize;
    }

    private void UpdateHoveredSlotFromUi()
    {
        if (graphicRaycaster == null || EventSystem.current == null)
        {
            hoveredTargetName = "(no raycaster)";
            hoveredSlotLabel = "-";
            hoveredSlotIndex = -1;
            hoverSource = "Unavailable";
            SetSelection(-1);
            return;
        }

        if (pointerEventData == null)
        {
            pointerEventData = new PointerEventData(EventSystem.current);
        }

        pointerEventData.position = Input.mousePosition;
        mousePosition = (Vector2)Input.mousePosition - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        currentAngle = Mathf.Atan2(mousePosition.y, mousePosition.x) * Mathf.Rad2Deg;

        raycastResults.Clear();
        graphicRaycaster.Raycast(pointerEventData, raycastResults);

        for (int i = 0; i < raycastResults.Count; i++)
        {
            GameObject hitObject = raycastResults[i].gameObject;
            if (hitObject == null)
            {
                continue;
            }

            MenuItemS item = hitObject.GetComponentInParent<MenuItemS>();
            if (item == null || item.SlotIndex < 0)
            {
                continue;
            }

            hoveredTargetName = hitObject.name;
            hoveredSlotIndex = item.SlotIndex;
            hoveredSlotLabel = GetSlotLabel(item.SlotIndex);
            hoverSource = "UI Raycast";
            SetSelection(item.SlotIndex);
            return;
        }

        hoveredTargetName = "(none)";
        hoveredSlotLabel = "-";
        hoveredSlotIndex = -1;
        hoverSource = "UI Raycast Miss";
        SetSelection(-1);
    }

    private void HandleItemClicked(int slotIndex)
    {
        if (!isSelectionWheelActive || slotIndex < 0)
        {
            return;
        }

        hoveredTargetName = $"MenuItem_{slotIndex}";
        hoveredSlotIndex = slotIndex;
        hoveredSlotLabel = GetSlotLabel(slotIndex);
        hoverSource = "Button Click";
        SetSelection(slotIndex);
        OnOptionSelected?.Invoke(slotIndex);
        CloseWheel();
    }

    private void ProcessPointerConfirm()
    {
        if (hoveredSlotIndex < 0)
        {
            return;
        }

        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        HandleItemClicked(hoveredSlotIndex);
    }

    private void SetSelection(int nextSelection)
    {
        if (selection == nextSelection)
        {
            return;
        }

        selection = nextSelection;
        RefreshSelectionVisuals();
    }

    private void RefreshSelectionVisuals()
    {
        if (menuItemComponents == null)
        {
            return;
        }

        for (int i = 0; i < menuItemComponents.Length; i++)
        {
            MenuItemS item = menuItemComponents[i];
            if (item == null)
            {
                continue;
            }

            if (i == selection)
            {
                item.Select();
            }
            else
            {
                item.Deselect();
            }
        }
    }

    private void RefreshAllVisuals()
    {
        ApplyWheelTheme();
        ApplySlotLabels();
        RefreshSelectionVisuals();
    }

    private void ApplyWheelTheme()
    {
        if (menuItemComponents == null)
        {
            return;
        }

        for (int i = 0; i < menuItemComponents.Length; i++)
        {
            MenuItemS item = menuItemComponents[i];
            if (item == null)
            {
                continue;
            }

            item.SetTheme(currentWheelColor, currentLabelColor, currentLabelColor);
        }
    }

    private void ApplySlotLabels()
    {
        if (menuItemComponents == null)
        {
            return;
        }

        for (int i = 0; i < menuItemComponents.Length; i++)
        {
            MenuItemS item = menuItemComponents[i];
            if (item == null)
            {
                continue;
            }

            item.SetDisplay(GetSlotLabel(i));
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

    private void SetCanvasVisible(bool visible)
    {
        if (selectionWheelCanvasGroup == null)
        {
            return;
        }

        selectionWheelCanvasGroup.alpha = visible ? 1f : 0f;
        selectionWheelCanvasGroup.interactable = visible;
        selectionWheelCanvasGroup.blocksRaycasts = visible;
    }

    private string GetSlotLabel(int slotIndex)
    {
        if (slotIndex < 0)
        {
            return "-";
        }

        if (currentSlotLabels != null && slotIndex < currentSlotLabels.Length)
        {
            return currentSlotLabels[slotIndex];
        }

        return (slotIndex + 1).ToString();
    }

    private void OnGUI()
    {
        if (!showDebugOverlay)
        {
            return;
        }

        EnsureDebugGuiStyle();

        string debugText =
            $"Wheel Active: {isSelectionWheelActive}\n" +
            $"Page: {currentPageLabel}\n" +
            $"Selection: {selection}\n" +
            $"Hover Slot: {hoveredSlotIndex}\n" +
            $"Hover Label: {hoveredSlotLabel}\n" +
            $"Hover Target: {hoveredTargetName}\n" +
            $"Hover Source: {hoverSource}";

        Vector2 size = debugGuiStyle.CalcSize(new GUIContent(debugText));
        Rect rect = new Rect(
            debugOverlayOffset.x,
            debugOverlayOffset.y,
            Mathf.Max(280f, size.x + 16f),
            size.y + 12f);

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.Box(rect, GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f), debugText, debugGuiStyle);
        GUI.color = previousColor;
    }

    private void EnsureDebugGuiStyle()
    {
        if (debugGuiStyle != null)
        {
            return;
        }

        debugGuiStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14
        };
        debugGuiStyle.normal.textColor = Color.white;
    }
}
