using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SettingTabButtonVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private bool allowExternalConfiguration = false;
    [SerializeField] private bool autoCollectGraphics = true;
    [SerializeField] private Graphic[] explicitAccentGraphics = new Graphic[0];
    [SerializeField] private TMP_Text[] explicitLabels = new TMP_Text[0];
    [SerializeField] private Color hoverBackgroundColor = new Color32(0x34, 0x34, 0x34, 0xFF);
    [SerializeField] private Color selectedBackgroundColor = new Color32(0x4C, 0x4C, 0x4C, 0xFF);
    [SerializeField] private Color hoverAccentColor = new Color32(0xFF, 0xB0, 0x4A, 0xFF);
    [SerializeField] private Color selectedAccentColor = new Color32(0xFF, 0x8A, 0x00, 0xFF);
    [SerializeField] private Color hoverLabelColor = new Color32(0xFF, 0xB0, 0x4A, 0xFF);
    [SerializeField] private Color selectedLabelColor = new Color32(0xFF, 0x8A, 0x00, 0xFF);

    private readonly List<Graphic> accentGraphics = new List<Graphic>();
    private readonly List<TMP_Text> labels = new List<TMP_Text>();
    private readonly Dictionary<Graphic, Color> baseColors = new Dictionary<Graphic, Color>();
    private readonly Dictionary<TMP_Text, FontStyles> baseFontStyles = new Dictionary<TMP_Text, FontStyles>();

    private bool initialized;
    private bool isHovered;
    private bool isSelected;

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

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        initialized = false;
        Initialize();
        ApplyCurrentState();
    }

    public void ConfigureColors(
        Color hoverBackground,
        Color selectedBackground,
        Color hoverAccent,
        Color selectedAccent,
        Color hoverLabel,
        Color selectedLabel)
    {
        if (!allowExternalConfiguration)
        {
            return;
        }

        hoverBackgroundColor = hoverBackground;
        selectedBackgroundColor = selectedBackground;
        hoverAccentColor = hoverAccent;
        selectedAccentColor = selectedAccent;
        hoverLabelColor = hoverLabel;
        selectedLabelColor = selectedLabel;
        ApplyCurrentState();
    }

    public void RefreshBindings()
    {
        initialized = false;
        Initialize();
        ApplyCurrentState();
    }

    public void SetSelected(bool selected)
    {
        Initialize();
        isSelected = selected;
        ApplyCurrentState();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Initialize();
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

        if (backgroundImage == null && button != null)
        {
            backgroundImage = button.targetGraphic as Image;
        }

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

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
        for (int index = 0; index < graphics.Length; index++)
        {
            Graphic graphic = graphics[index];
            if (graphic == null || graphic == backgroundImage)
            {
                continue;
            }

            TMP_Text label = graphic as TMP_Text;
            if (label != null)
            {
                AddLabel(label);
                continue;
            }

            AddAccentGraphic(graphic);
        }
    }

    private void AddAccentGraphics(IList<Graphic> graphics)
    {
        if (graphics == null)
        {
            return;
        }

        for (int index = 0; index < graphics.Count; index++)
        {
            AddAccentGraphic(graphics[index]);
        }
    }

    private void AddLabels(IList<TMP_Text> textLabels)
    {
        if (textLabels == null)
        {
            return;
        }

        for (int index = 0; index < textLabels.Count; index++)
        {
            AddLabel(textLabels[index]);
        }
    }

    private void AddAccentGraphic(Graphic graphic)
    {
        if (graphic == null || graphic == backgroundImage || accentGraphics.Contains(graphic))
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

    private void CaptureBaseState()
    {
        baseColors.Clear();
        baseFontStyles.Clear();

        CacheGraphic(backgroundImage);

        for (int index = 0; index < accentGraphics.Count; index++)
        {
            CacheGraphic(accentGraphics[index]);
        }

        for (int index = 0; index < labels.Count; index++)
        {
            TMP_Text label = labels[index];
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

    private void CacheGraphic(Graphic graphic)
    {
        if (graphic == null || baseColors.ContainsKey(graphic))
        {
            return;
        }

        baseColors[graphic] = graphic.color;
    }

    private void ApplyCurrentState()
    {
        if (!initialized)
        {
            return;
        }

        if (isSelected)
        {
            ApplySelectedState();
            return;
        }

        if (isHovered)
        {
            ApplyHoverState();
            return;
        }

        RestoreBaseState();
    }

    private void ApplyHoverState()
    {
        ApplyBackgroundColor(hoverBackgroundColor);
        ApplyAccentColor(hoverAccentColor);
        ApplyLabelState(false, hoverLabelColor);
    }

    private void ApplySelectedState()
    {
        ApplyBackgroundColor(selectedBackgroundColor);
        ApplyAccentColor(selectedAccentColor);
        ApplyLabelState(true, selectedLabelColor);
    }

    private void ApplyBackgroundColor(Color color)
    {
        if (backgroundImage == null)
        {
            return;
        }

        backgroundImage.color = PreserveAlpha(color, backgroundImage);
    }

    private void ApplyAccentColor(Color color)
    {
        for (int index = 0; index < accentGraphics.Count; index++)
        {
            Graphic graphic = accentGraphics[index];
            if (graphic == null)
            {
                continue;
            }

            graphic.color = PreserveAlpha(color, graphic);
        }
    }

    private void ApplyLabelState(bool underline, Color color)
    {
        for (int index = 0; index < labels.Count; index++)
        {
            TMP_Text label = labels[index];
            if (label == null)
            {
                continue;
            }

            if (!baseColors.TryGetValue(label, out Color baseColor))
            {
                baseColor = label.color;
            }

            if (!baseFontStyles.TryGetValue(label, out FontStyles baseStyle))
            {
                baseStyle = label.fontStyle;
            }

            label.color = new Color(color.r, color.g, color.b, baseColor.a);
            label.fontStyle = underline ? baseStyle | FontStyles.Underline : baseStyle;
        }
    }

    private void RestoreBaseState()
    {
        foreach (KeyValuePair<Graphic, Color> entry in baseColors)
        {
            if (entry.Key != null)
            {
                entry.Key.color = entry.Value;
            }
        }

        foreach (KeyValuePair<TMP_Text, FontStyles> entry in baseFontStyles)
        {
            if (entry.Key != null)
            {
                entry.Key.fontStyle = entry.Value;
            }
        }
    }

    private static Color PreserveAlpha(Color targetColor, Graphic graphic)
    {
        Color sourceColor = graphic != null ? graphic.color : Color.white;
        return new Color(targetColor.r, targetColor.g, targetColor.b, sourceColor.a);
    }
}
