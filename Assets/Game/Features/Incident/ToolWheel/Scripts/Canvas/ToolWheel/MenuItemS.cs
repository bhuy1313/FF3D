using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuItemS : MonoBehaviour, ICanvasRaycastFilter
{
    [Header("References")]
    [SerializeField] private RectTransform rootRect;
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text itemLabel;

    [Header("Content Layout")]
    [SerializeField] private float iconRadius = 84f;
    [SerializeField] private float labelRadius = 128f;
    [SerializeField] private Vector2 iconSize = new Vector2(52f, 52f);
    [SerializeField] private Vector2 labelSize = new Vector2(132f, 42f);

    [Header("Hit Area")]
    [SerializeField] private float innerHitRadius = 26f;
    [SerializeField] private float outerHitRadius = 196f;

    [Header("Selection")]
    [SerializeField, Range(0f, 1f)] private float deselectedAlpha = 0.55f;
    [SerializeField, Range(0f, 1f)] private float selectedAlpha = 1f;
    [SerializeField] private float deselectedScale = 1f;
    [SerializeField] private float selectedScale = 1.04f;

    private RectTransform backgroundRect;
    private RectTransform iconRect;
    private RectTransform labelRect;
    private Color currentBackgroundColor = Color.white;
    private Color currentLabelColor = Color.white;
    private Color currentIconColor = Color.white;
    private float startAngleClockwise;
    private float sweepAngleClockwise = 90f;

    public int SlotIndex { get; private set; } = -1;
    public Button Button => button;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    public void ConfigureSlotLayout(int slotIndex, int slotCount, Vector2 slotAreaSize)
    {
        if (slotCount <= 0)
        {
            return;
        }

        ResolveReferences();

        SlotIndex = slotIndex;
        sweepAngleClockwise = 360f / slotCount;
        startAngleClockwise = slotIndex * sweepAngleClockwise;

        if (rootRect != null)
        {
            rootRect.anchorMin = Vector2.one * 0.5f;
            rootRect.anchorMax = Vector2.one * 0.5f;
            rootRect.pivot = Vector2.one * 0.5f;
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = slotAreaSize;
            rootRect.localRotation = Quaternion.identity;
            rootRect.localScale = Vector3.one * deselectedScale;
        }

        ConfigureBackground();
        ConfigureContent(iconRect, iconRadius, iconSize, GetCenterDirectionClockwise());
        ConfigureContent(labelRect, labelRadius, labelSize, GetCenterDirectionClockwise());
        ApplyVisualState(false);
    }

    public void SetDisplay(string label, Sprite icon = null)
    {
        ResolveReferences();

        if (itemLabel != null)
        {
            if (!itemLabel.gameObject.activeSelf)
            {
                itemLabel.gameObject.SetActive(true);
            }

            itemLabel.enabled = true;
            itemLabel.raycastTarget = false;
            itemLabel.text = string.IsNullOrWhiteSpace(label) ? "-" : label;
            itemLabel.ForceMeshUpdate();
        }

        if (iconImage != null)
        {
            if (!iconImage.gameObject.activeSelf)
            {
                iconImage.gameObject.SetActive(true);
            }

            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.raycastTarget = false;
        }
    }

    public void SetTheme(Color backgroundColor, Color labelColor, Color iconColor)
    {
        currentBackgroundColor = backgroundColor;
        currentLabelColor = labelColor;
        currentIconColor = iconColor;
        ApplyVisualState(false);
    }

    public void Select()
    {
        ApplyVisualState(true);
    }

    public void Deselect()
    {
        ApplyVisualState(false);
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (rootRect == null)
        {
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, screenPoint, eventCamera, out Vector2 localPoint))
        {
            return false;
        }

        float distance = localPoint.magnitude;
        if (distance < innerHitRadius || distance > outerHitRadius)
        {
            return false;
        }

        float pointAngleClockwise = Mathf.Atan2(localPoint.x, localPoint.y) * Mathf.Rad2Deg;
        if (pointAngleClockwise < 0f)
        {
            pointAngleClockwise += 360f;
        }

        return IsAngleInsideSector(pointAngleClockwise, startAngleClockwise, sweepAngleClockwise);
    }

    private void ResolveReferences()
    {
        if (rootRect == null)
        {
            rootRect = GetComponent<RectTransform>();
        }

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (backgroundImage == null)
        {
            Transform backgroundTransform = transform.Find("BgW");
            if (backgroundTransform != null)
            {
                backgroundImage = backgroundTransform.GetComponent<Image>();
            }
        }

        if (itemLabel == null)
        {
            itemLabel = GetComponentInChildren<TMP_Text>(true);
        }

        if (iconImage == null && backgroundImage != null)
        {
            Transform iconTransform = backgroundImage.transform.Find("icon");
            if (iconTransform != null)
            {
                iconImage = iconTransform.GetComponent<Image>();
            }
        }

        backgroundRect = backgroundImage != null ? backgroundImage.rectTransform : null;
        iconRect = iconImage != null ? iconImage.rectTransform : null;
        labelRect = itemLabel != null ? itemLabel.rectTransform : null;

        if (iconRect != null)
        {
            iconRect.SetAsLastSibling();
        }

        if (labelRect != null)
        {
            labelRect.SetAsLastSibling();
        }

        if (button != null && backgroundImage != null)
        {
            button.targetGraphic = backgroundImage;
            button.transition = Selectable.Transition.None;
        }

        if (itemLabel != null)
        {
            itemLabel.gameObject.SetActive(true);
            itemLabel.enabled = true;
            itemLabel.alignment = TextAlignmentOptions.Center;
            itemLabel.textWrappingMode = TextWrappingModes.NoWrap;
            itemLabel.overflowMode = TextOverflowModes.Ellipsis;
            itemLabel.raycastTarget = false;
        }
    }

    private void ConfigureBackground()
    {
        if (backgroundRect == null || backgroundImage == null)
        {
            return;
        }

        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        backgroundRect.localScale = Vector3.one;
        backgroundRect.localRotation = Quaternion.Euler(0f, 0f, -startAngleClockwise);

        backgroundImage.type = Image.Type.Filled;
        backgroundImage.fillMethod = Image.FillMethod.Radial360;
        backgroundImage.fillOrigin = (int)Image.Origin360.Top;
        backgroundImage.fillClockwise = true;
        backgroundImage.fillAmount = sweepAngleClockwise / 360f;
        backgroundImage.raycastTarget = true;
    }

    private void ConfigureContent(RectTransform contentRect, float radius, Vector2 size, float angleClockwise)
    {
        if (contentRect == null)
        {
            return;
        }

        contentRect.anchorMin = Vector2.one * 0.5f;
        contentRect.anchorMax = Vector2.one * 0.5f;
        contentRect.pivot = Vector2.one * 0.5f;
        contentRect.anchoredPosition = GetPointOnWheel(radius, angleClockwise);
        contentRect.sizeDelta = size;
        contentRect.localRotation = Quaternion.identity;
        contentRect.localScale = Vector3.one;
    }

    private void ApplyVisualState(bool isSelected)
    {
        if (rootRect != null)
        {
            rootRect.localScale = Vector3.one * (isSelected ? selectedScale : deselectedScale);
        }

        float alpha = isSelected ? selectedAlpha : deselectedAlpha;

        if (backgroundImage != null)
        {
            Color color = currentBackgroundColor;
            color.a *= alpha;
            backgroundImage.color = color;
        }

        if (itemLabel != null)
        {
            Color color = currentLabelColor;
            color.a *= alpha;
            itemLabel.color = color;
        }

        if (iconImage != null)
        {
            Color color = currentIconColor;
            color.a *= alpha;
            iconImage.color = color;
        }
    }

    private float GetCenterDirectionClockwise()
    {
        return startAngleClockwise + sweepAngleClockwise * 0.5f;
    }

    private static Vector2 GetPointOnWheel(float radius, float angleClockwise)
    {
        float radians = angleClockwise * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * radius;
    }

    private static bool IsAngleInsideSector(float angle, float sectorStart, float sectorSweep)
    {
        float normalizedAngle = Mathf.Repeat(angle - sectorStart, 360f);
        return normalizedAngle >= 0f && normalizedAngle <= sectorSweep;
    }
}
