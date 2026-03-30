using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ToolWheelUIController : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private CanvasGroup rootGroup;         // CanvasGroup trên ToolWheelRoot
    [SerializeField] private RectTransform slotsContainer;  // RectTransform chứa slots (anchor center)
    [SerializeField] private ToolWheelSlotUI slotPrefab;    // prefab slot
    [SerializeField] private GameObject centerInfoPanel;
    [SerializeField] private TMP_Text centerTitleText;
    [SerializeField] private TMP_Text centerDescText;
    [SerializeField] private Image centerIcon;

    [Header("Config")]
    [SerializeField] private int slotCount = 6;
    [SerializeField] private float baseRadius = 220f;
    [SerializeField] private float slotScale = 1f;
    [Tooltip("Góc bắt đầu (độ). 150° = slot 1 ở vị trí 10 giờ.")]
    [SerializeField] private float startAngleDeg = 150f;

    [Header("Behavior")]
    [SerializeField] private bool closeOnClick = true;

    private readonly List<ToolWheelSlotUI> slots = new();
    private ToolWheelSlotUI hovered;
    private bool isOpen;
    private bool prevCursorVisible;
    private CursorLockMode prevCursorLockState;

    private void Awake()
    {
        BuildSlotsOnce();
        SetVisible(false);
    }

    // PlayerInput (Send Messages) sẽ gọi hàm này nếu action tên "ToolWheel"
    public void OnToolWheel(InputValue value)
    {
        // chỉ xử lý lúc NHẤN xuống, bỏ qua lúc nhả
        if (!value.isPressed) return;

        if (isOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;

        SetVisible(true);

        prevCursorVisible = Cursor.visible;
        prevCursorLockState = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        LayoutClockwise();
        HideInfo();
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        if (hovered != null)
        {
            hovered.SetHighlighted(false);
            hovered = null;
        }

        SetVisible(false);
        HideInfo();

        Cursor.visible = prevCursorVisible;
        Cursor.lockState = prevCursorLockState;
    }

    private void SetVisible(bool on)
    {
        if (rootGroup == null) return;
        rootGroup.alpha = on ? 1f : 0f;
        rootGroup.interactable = on;
        rootGroup.blocksRaycasts = on;
    }

    private void BuildSlotsOnce()
    {
        if (slotsContainer == null || slotPrefab == null) return;

        for (int i = slotsContainer.childCount - 1; i >= 0; i--)
        {
            var child = slotsContainer.GetChild(i);
            if (child.GetComponent<ToolWheelSlotUI>() != null)
                Destroy(child.gameObject);
        }

        slots.Clear();

        if (slotCount <= 0) return;

        for (int i = 0; i < slotCount; i++)
        {
            var s = Instantiate(slotPrefab, slotsContainer);
            s.Init(this, i);
            slots.Add(s);
        }
    }

    // -------------------------
    // Slot callbacks
    // -------------------------
    public void OnSlotHover(ToolWheelSlotUI slot)
    {
        if (!isOpen) return;
        if (hovered == slot) return;

        if (hovered != null) hovered.SetHighlighted(false);
        hovered = slot;
        hovered.SetHighlighted(true);
        ShowInfo(slot);
    }

    public void OnSlotExit(ToolWheelSlotUI slot)
    {
        if (!isOpen) return;
        if (hovered != slot) return;

        hovered.SetHighlighted(false);
        hovered = null;
        HideInfo();
    }

    public void OnSlotClick(ToolWheelSlotUI slot)
    {
        if (!isOpen) return;

        Debug.Log($"Clicked Slot: {slot.Index + 1}");

        if (closeOnClick)
            Close();
    }

    // -------------------------
    // Fixed layout (clockwise)
    // -------------------------
    private void LayoutClockwise()
    {
        if (slots.Count == 0) return;

        float step = 360f / slots.Count;
        float clampedScale = Mathf.Max(0.01f, slotScale);

        for (int i = 0; i < slots.Count; i++)
        {
            var rt = (RectTransform)slots[i].transform;
            rt.localScale = new Vector3(clampedScale, clampedScale, 1f);

            float angle = startAngleDeg - i * step; // clockwise from start angle
            Vector2 pos = PolarToUI(angle, baseRadius);

            rt.anchoredPosition = pos;
            slots[i].SetHighlighted(false);
        }

        hovered = null;
    }

    private static Vector2 PolarToUI(float angleDeg, float radius)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
    }

    private void ShowInfo(ToolWheelSlotUI slot)
    {
        if (centerInfoPanel != null) centerInfoPanel.SetActive(true);
        if (slot == null) return;

        if (centerTitleText != null) centerTitleText.text = slot.Title;
        if (centerDescText != null) centerDescText.text = slot.Description;
        if (centerIcon != null)
        {
            centerIcon.sprite = slot.Icon;
            centerIcon.enabled = slot.Icon != null;
        }
    }

    private void HideInfo()
    {
        if (centerInfoPanel != null) centerInfoPanel.SetActive(false);
    }
}
