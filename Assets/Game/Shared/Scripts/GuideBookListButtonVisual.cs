using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GuideBookListButtonVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Graphic backgroundGraphic;
    [SerializeField] private RectTransform animatedRoot;
    [SerializeField] private bool autoCollectGraphics = true;
    [SerializeField] private Graphic[] explicitAccentGraphics = new Graphic[0];
    [SerializeField] private TMP_Text[] explicitLabels = new TMP_Text[0];

    [Header("Hover")]
    [SerializeField] private Color hoverBackgroundColor = new Color(0.44f, 0.35f, 0.24f, 0.18f);
    [SerializeField] private Color hoverAccentColor = new Color(0.74f, 0.47f, 0.2f, 1f);
    [SerializeField] private Color hoverLabelColor = new Color(0.33f, 0.19f, 0.1f, 1f);
    [SerializeField] private Vector3 hoverScale = new Vector3(1.025f, 1.025f, 1f);

    [Header("Selected")]
    [SerializeField] private Color selectedBackgroundColor = new Color(0.58f, 0.44f, 0.28f, 0.24f);
    [SerializeField] private Color selectedAccentColor = new Color(0.63f, 0.26f, 0.13f, 1f);
    [SerializeField] private Color selectedLabelColor = new Color(0.38f, 0.14f, 0.08f, 1f);
    [SerializeField] private Vector3 selectedScale = new Vector3(1.04f, 1.04f, 1f);
    [SerializeField] private FontStyles selectedFontStyle = FontStyles.Bold;

    private readonly List<Graphic> accentGraphics = new List<Graphic>();
    private readonly List<TMP_Text> labels = new List<TMP_Text>();
    private readonly Dictionary<Graphic, Color> baseColors = new Dictionary<Graphic, Color>();
    private readonly Dictionary<TMP_Text, FontStyles> baseFontStyles = new Dictionary<TMP_Text, FontStyles>();

    private bool initialized;
    private bool isHovered;
    private bool isSelected;
    private Vector3 baseScale = Vector3.one;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        ApplyCurrentState();
    }

    private void OnDisable()
    {
        RestoreBaseState();
        isHovered = false;
    }

    public void SetSelected(bool selected)
    {
        Initialize();
        isSelected = selected;
        ApplyCurrentState();
    }

    public void RefreshBindings()
    {
        initialized = false;
        Initialize();
        ApplyCurrentState();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsInteractable())
        {
            return;
        }

        isHovered = true;
        ApplyCurrentState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        ApplyCurrentState();
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (backgroundGraphic == null && button != null)
        {
            backgroundGraphic = button.targetGraphic;
        }

        if (backgroundGraphic == null)
        {
            backgroundGraphic = GetComponent<Graphic>();
        }

        if (animatedRoot == null)
        {
            animatedRoot = transform as RectTransform;
        }

        if (button != null)
        {
            button.transition = Selectable.Transition.None;
        }

        baseScale = animatedRoot != null ? animatedRoot.localScale : transform.localScale;
        CollectGraphics();
        CaptureBaseState();
        initialized = true;
    }

    private void CollectGraphics()
    {
        accentGraphics.Clear();
        labels.Clear();

        AddAccentGraphics(explicitAccentGraphics);
        AddLabels(explicitLabels);

        if (!autoCollectGraphics)
        {
            return;
        }

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null || graphic == backgroundGraphic)
            {
                continue;
            }

            TMP_Text tmpText = graphic as TMP_Text;
            if (tmpText != null)
            {
                AddLabel(tmpText);
                continue;
            }

            AddAccentGraphic(graphic);
        }
    }

    private void CaptureBaseState()
    {
        baseColors.Clear();
        baseFontStyles.Clear();

        CacheGraphic(backgroundGraphic);

        for (int i = 0; i < accentGraphics.Count; i++)
        {
            CacheGraphic(accentGraphics[i]);
        }

        for (int i = 0; i < labels.Count; i++)
        {
            TMP_Text label = labels[i];
            if (label == null)
            {
                continue;
            }

            CacheGraphic(label);
            if (!baseFontStyles.ContainsKey(label))
            {
                baseFontStyles[label] = label.fontStyle;
            }
        }
    }

    private void ApplyCurrentState()
    {
        if (!initialized)
        {
            return;
        }

        RestoreBaseState();

        if (!IsInteractable())
        {
            return;
        }

        if (isSelected)
        {
            ApplyVisualState(selectedBackgroundColor, selectedAccentColor, selectedLabelColor, selectedScale, selectedFontStyle);
            return;
        }

        if (isHovered)
        {
            ApplyVisualState(hoverBackgroundColor, hoverAccentColor, hoverLabelColor, hoverScale, FontStyles.Normal);
        }
    }

    private void ApplyVisualState(Color backgroundColor, Color accentColor, Color labelColor, Vector3 scale, FontStyles selectedStyle)
    {
        SetGraphicColor(backgroundGraphic, backgroundColor);

        for (int i = 0; i < accentGraphics.Count; i++)
        {
            SetGraphicColor(accentGraphics[i], accentColor);
        }

        for (int i = 0; i < labels.Count; i++)
        {
            TMP_Text label = labels[i];
            if (label == null)
            {
                continue;
            }

            SetGraphicColor(label, labelColor);
            if (isSelected)
            {
                label.fontStyle = selectedStyle;
            }
        }

        if (animatedRoot != null)
        {
            animatedRoot.localScale = scale;
        }
    }

    private void RestoreBaseState()
    {
        foreach (KeyValuePair<Graphic, Color> pair in baseColors)
        {
            if (pair.Key != null)
            {
                pair.Key.color = pair.Value;
            }
        }

        foreach (KeyValuePair<TMP_Text, FontStyles> pair in baseFontStyles)
        {
            if (pair.Key != null)
            {
                pair.Key.fontStyle = pair.Value;
            }
        }

        if (animatedRoot != null)
        {
            animatedRoot.localScale = baseScale;
        }
    }

    private bool IsInteractable()
    {
        return button == null || button.IsInteractable();
    }

    private void AddAccentGraphics(IList<Graphic> graphics)
    {
        if (graphics == null)
        {
            return;
        }

        for (int i = 0; i < graphics.Count; i++)
        {
            AddAccentGraphic(graphics[i]);
        }
    }

    private void AddLabels(IList<TMP_Text> textLabels)
    {
        if (textLabels == null)
        {
            return;
        }

        for (int i = 0; i < textLabels.Count; i++)
        {
            AddLabel(textLabels[i]);
        }
    }

    private void AddAccentGraphic(Graphic graphic)
    {
        if (graphic == null || graphic == backgroundGraphic || accentGraphics.Contains(graphic))
        {
            return;
        }

        accentGraphics.Add(graphic);
    }

    private void AddLabel(TMP_Text label)
    {
        if (label == null || labels.Contains(label))
        {
            return;
        }

        labels.Add(label);
    }

    private void CacheGraphic(Graphic graphic)
    {
        if (graphic == null || baseColors.ContainsKey(graphic))
        {
            return;
        }

        baseColors[graphic] = graphic.color;
    }

    private static void SetGraphicColor(Graphic graphic, Color targetColor)
    {
        if (graphic == null)
        {
            return;
        }

        Color color = targetColor;
        color.a = targetColor.a;
        graphic.color = color;
    }
}
