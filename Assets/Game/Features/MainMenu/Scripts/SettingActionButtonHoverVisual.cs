using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SettingActionButtonHoverVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Graphic backgroundGraphic;
    [SerializeField] private bool allowExternalConfiguration = false;
    [SerializeField] private bool autoCollectGraphics = true;
    [SerializeField] private bool disableButtonTransition = true;
    [SerializeField] private bool preserveSourceAlpha = true;
    [SerializeField] private Graphic[] explicitAccentGraphics = new Graphic[0];
    [SerializeField] private TMP_Text[] explicitTmpLabels = new TMP_Text[0];
    [SerializeField] private Text[] explicitLegacyLabels = new Text[0];
    [SerializeField] private Color hoverBackgroundColor = new Color32(0x66, 0x66, 0x66, 0xFF);
    [SerializeField] private Color hoverAccentColor = new Color32(0xFF, 0xB0, 0x4A, 0xFF);
    [SerializeField] private Color hoverLabelColor = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    [SerializeField] private Vector3 hoverScale = new Vector3(1.02f, 1.02f, 1f);

    private readonly List<Graphic> accentGraphics = new List<Graphic>();
    private readonly List<TMP_Text> tmpLabels = new List<TMP_Text>();
    private readonly List<Text> legacyLabels = new List<Text>();
    private readonly Dictionary<Graphic, Color> baseColors = new Dictionary<Graphic, Color>();

    private bool initialized;
    private bool isHovered;
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

    private void LateUpdate()
    {
        if (!initialized)
        {
            Initialize();
        }

        if (isHovered && !IsInteractable())
        {
            isHovered = false;
            ApplyCurrentState();
        }
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

    public void Configure(Color backgroundColor, Color accentColor, Color labelColor, Vector3 scale)
    {
        if (!allowExternalConfiguration)
        {
            return;
        }

        hoverBackgroundColor = backgroundColor;
        hoverAccentColor = accentColor;
        hoverLabelColor = labelColor;
        hoverScale = scale;
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

        if (button != null)
        {
            if (disableButtonTransition)
            {
                button.transition = Selectable.Transition.None;
            }
        }

        baseScale = transform.localScale;
        CollectGraphics();
        CaptureBaseColors();
        initialized = true;
    }

    private void CollectGraphics()
    {
        accentGraphics.Clear();
        tmpLabels.Clear();
        legacyLabels.Clear();

        AddAccentGraphics(explicitAccentGraphics);
        AddTmpLabels(explicitTmpLabels);
        AddLegacyLabels(explicitLegacyLabels);

        if (!autoCollectGraphics)
        {
            return;
        }

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int index = 0; index < graphics.Length; index++)
        {
            Graphic graphic = graphics[index];
            if (graphic == null || graphic == backgroundGraphic)
            {
                continue;
            }

            if (graphic is TMP_Text tmpText)
            {
                AddTmpLabel(tmpText);
                continue;
            }

            if (graphic is Text legacyText)
            {
                AddLegacyLabel(legacyText);
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

    private void AddTmpLabels(IList<TMP_Text> labels)
    {
        if (labels == null)
        {
            return;
        }

        for (int index = 0; index < labels.Count; index++)
        {
            AddTmpLabel(labels[index]);
        }
    }

    private void AddLegacyLabels(IList<Text> labels)
    {
        if (labels == null)
        {
            return;
        }

        for (int index = 0; index < labels.Count; index++)
        {
            AddLegacyLabel(labels[index]);
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

    private void AddTmpLabel(TMP_Text label)
    {
        if (label == null || tmpLabels.Contains(label))
        {
            return;
        }

        tmpLabels.Add(label);
    }

    private void AddLegacyLabel(Text label)
    {
        if (label == null || legacyLabels.Contains(label))
        {
            return;
        }

        legacyLabels.Add(label);
    }

    private void CaptureBaseColors()
    {
        baseColors.Clear();
        CacheGraphicColor(backgroundGraphic);

        for (int index = 0; index < accentGraphics.Count; index++)
        {
            CacheGraphicColor(accentGraphics[index]);
        }

        for (int index = 0; index < tmpLabels.Count; index++)
        {
            CacheGraphicColor(tmpLabels[index]);
        }

        for (int index = 0; index < legacyLabels.Count; index++)
        {
            CacheGraphicColor(legacyLabels[index]);
        }
    }

    private void CacheGraphicColor(Graphic graphic)
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

        if (!isHovered || !IsInteractable())
        {
            RestoreBaseState();
            return;
        }

        ApplyBackgroundColor();
        ApplyAccentColors();
        ApplyLabelColors();
        transform.localScale = hoverScale;
    }

    private void ApplyBackgroundColor()
    {
        if (backgroundGraphic == null)
        {
            return;
        }

        backgroundGraphic.color = ResolveHoverColor(hoverBackgroundColor, backgroundGraphic);
    }

    private void ApplyAccentColors()
    {
        for (int index = 0; index < accentGraphics.Count; index++)
        {
            Graphic graphic = accentGraphics[index];
            if (graphic == null)
            {
                continue;
            }

            graphic.color = ResolveHoverColor(hoverAccentColor, graphic);
        }
    }

    private void ApplyLabelColors()
    {
        for (int index = 0; index < tmpLabels.Count; index++)
        {
            TMP_Text label = tmpLabels[index];
            if (label == null || !baseColors.TryGetValue(label, out Color baseColor))
            {
                continue;
            }

            label.color = ResolveHoverColor(hoverLabelColor, baseColor);
        }

        for (int index = 0; index < legacyLabels.Count; index++)
        {
            Text label = legacyLabels[index];
            if (label == null || !baseColors.TryGetValue(label, out Color baseColor))
            {
                continue;
            }

            label.color = ResolveHoverColor(hoverLabelColor, baseColor);
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

        transform.localScale = baseScale;

        if (button != null && disableButtonTransition)
        {
            button.transition = Selectable.Transition.None;
        }
    }

    private bool IsInteractable()
    {
        return button == null || button.IsInteractable();
    }

    private Color ResolveHoverColor(Color targetColor, Graphic sourceGraphic)
    {
        Color sourceColor = sourceGraphic != null ? sourceGraphic.color : Color.white;
        return ResolveHoverColor(targetColor, sourceColor);
    }

    private Color ResolveHoverColor(Color targetColor, Color sourceColor)
    {
        return preserveSourceAlpha
            ? new Color(targetColor.r, targetColor.g, targetColor.b, sourceColor.a)
            : targetColor;
    }
}
