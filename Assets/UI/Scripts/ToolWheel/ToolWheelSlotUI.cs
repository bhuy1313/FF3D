using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ToolWheelSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Refs")]
    [SerializeField] private Image background;
    [SerializeField] private Outline outline;
    [SerializeField] private TMP_Text indexText;

    [Header("Info (Center Panel)")]
    [SerializeField] private string title;
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;

    [Header("Hover Colors")]
    [SerializeField] private Color hoverBg = new Color(1, 1, 1, 0.35f);
    [SerializeField] private Color hoverOutline = new Color(1, 1, 1, 1f);

    // Normal colors lấy từ prefab lúc runtime
    private Color normalBg;
    private Color normalOutline;

    public int Index { get; private set; }
    public string Title => title;
    public string Description => description;
    public Sprite Icon => icon;
    private ToolWheelUIController owner;

    public void Init(ToolWheelUIController owner, int index)
    {
        this.owner = owner;
        Index = index;

        if (indexText != null)
            indexText.text = (index + 1).ToString();

        // ✅ Capture màu “đẹp” bạn set sẵn trong prefab
        if (background != null) normalBg = background.color;
        if (outline != null) normalOutline = outline.effectColor;

        SetHighlighted(false);
    }

    public void SetHighlighted(bool on)
    {
        if (background != null) background.color = on ? hoverBg : normalBg;
        if (outline != null) outline.effectColor = on ? hoverOutline : normalOutline;
    }

    public void OnPointerEnter(PointerEventData eventData) => owner?.OnSlotHover(this);
    public void OnPointerExit(PointerEventData eventData) => owner?.OnSlotExit(this);

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        owner?.OnSlotClick(this);
    }
}
