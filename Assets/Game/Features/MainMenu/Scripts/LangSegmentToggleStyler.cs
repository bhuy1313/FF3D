using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LangSegmentToggleStyler : MonoBehaviour
{
    private class ToggleVisual
    {
        public Toggle toggle;
        public Graphic background;
        public Graphic label;
    }

    [Header("Toggles")]
    [SerializeField] private List<Toggle> toggles = new List<Toggle>();
    [SerializeField] private bool autoCollectChildToggles = false;

    [Header("Colors")]
    [SerializeField] private Color selectedBackground = new Color(0.12941177f, 0.5294118f, 0.95686275f, 1f);
    [SerializeField] private Color unselectedBackground = new Color(0.8862745f, 0.90588236f, 0.92941177f, 1f);
    [SerializeField] private Color selectedText = Color.white;
    [SerializeField] private Color unselectedText = new Color(0.18039216f, 0.22352941f, 0.29411766f, 1f);
    [SerializeField] private bool useFixedLabelFontSizes = false;
    [SerializeField] private float selectedLabelFontSize = 18f;
    [SerializeField] private float unselectedLabelFontSize = 14f;
    [SerializeField] private bool useBoldForSelected = false;

    private readonly List<ToggleVisual> runtimeToggles = new List<ToggleVisual>();
    private readonly List<Toggle> registeredToggles = new List<Toggle>();
    private readonly Dictionary<Graphic, float> baseLabelFontSizes = new Dictionary<Graphic, float>();
    private readonly Dictionary<TMP_Text, FontStyles> baseTmpFontStyles = new Dictionary<TMP_Text, FontStyles>();
    private readonly Dictionary<Text, FontStyle> baseLegacyFontStyles = new Dictionary<Text, FontStyle>();

    private void OnEnable()
    {
        RebuildRuntimeToggles();
        RegisterAll();
        ApplyState();
    }

    private void OnDisable()
    {
        UnregisterAll();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        RebuildRuntimeToggles();
        ApplyState();
    }

    private void RegisterAll()
    {
        for (int i = 0; i < runtimeToggles.Count; i++)
        {
            Toggle toggle = runtimeToggles[i].toggle;
            if (toggle == null || registeredToggles.Contains(toggle))
            {
                continue;
            }

            toggle.onValueChanged.AddListener(OnToggleChanged);
            registeredToggles.Add(toggle);
        }
    }

    private void UnregisterAll()
    {
        for (int i = 0; i < registeredToggles.Count; i++)
        {
            Toggle toggle = registeredToggles[i];
            if (toggle == null)
            {
                continue;
            }

            toggle.onValueChanged.RemoveListener(OnToggleChanged);
        }

        registeredToggles.Clear();
    }

    private void RebuildRuntimeToggles()
    {
        runtimeToggles.Clear();
        baseLabelFontSizes.Clear();
        baseTmpFontStyles.Clear();
        baseLegacyFontStyles.Clear();

        if (toggles != null)
        {
            for (int i = 0; i < toggles.Count; i++)
            {
                Toggle toggle = toggles[i];
                if (toggle == null)
                {
                    continue;
                }

                AddToggleIfMissing(toggle);
            }
        }

        if (!autoCollectChildToggles)
        {
            return;
        }

        Toggle[] childToggles = GetComponentsInChildren<Toggle>(true);
        for (int i = 0; i < childToggles.Length; i++)
        {
            AddToggleIfMissing(childToggles[i]);
        }
    }

    private void AddToggleIfMissing(Toggle toggle)
    {
        if (toggle == null || ContainsToggle(toggle))
        {
            return;
        }

        Graphic label = ResolveLabel(toggle);
        CacheBaseFontSize(label);
        CacheBaseFontStyle(label);

        runtimeToggles.Add(new ToggleVisual
        {
            toggle = toggle,
            background = ResolveBackground(toggle),
            label = label
        });
    }

    private Graphic ResolveBackground(Toggle toggle)
    {
        if (toggle == null)
        {
            return null;
        }

        if (toggle.targetGraphic != null)
        {
            return toggle.targetGraphic;
        }

        Graphic[] graphics = toggle.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic g = graphics[i];
            if (g == null || g.gameObject == toggle.gameObject)
            {
                continue;
            }

            string name = g.gameObject.name.ToLowerInvariant();
            if (name.Contains("background") || name.Contains("bg") || name.Contains("panel"))
            {
                return g;
            }
        }

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic g = graphics[i];
            if (g == null || g.gameObject == toggle.gameObject || IsTextGraphic(g))
            {
                continue;
            }

            return g;
        }

        return null;
    }

    private Graphic ResolveLabel(Toggle toggle)
    {
        if (toggle == null)
        {
            return null;
        }

        Graphic[] graphics = toggle.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic g = graphics[i];
            if (!IsTextGraphic(g))
            {
                continue;
            }

            string name = g.gameObject.name.ToLowerInvariant();
            if (name.Contains("label") || name.Contains("text"))
            {
                return g;
            }
        }

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic g = graphics[i];
            if (IsTextGraphic(g))
            {
                return g;
            }
        }

        return null;
    }

    private bool IsTextGraphic(Graphic graphic)
    {
        if (graphic == null)
        {
            return false;
        }

        if (graphic is Text)
        {
            return true;
        }

        string typeName = graphic.GetType().Name;
        return typeName.Contains("TextMeshPro", StringComparison.Ordinal);
    }

    private bool ContainsToggle(Toggle toggle)
    {
        for (int i = 0; i < runtimeToggles.Count; i++)
        {
            if (runtimeToggles[i].toggle == toggle)
            {
                return true;
            }
        }

        return false;
    }

    private void OnToggleChanged(bool _)
    {
        ApplyState();
    }

    private void ApplyState()
    {
        for (int i = 0; i < runtimeToggles.Count; i++)
        {
            ToggleVisual toggleVisual = runtimeToggles[i];
            if (toggleVisual == null || toggleVisual.toggle == null)
            {
                continue;
            }

            ApplyVisual(toggleVisual.toggle.isOn, toggleVisual.background, toggleVisual.label);
        }
    }

    public void RefreshVisualState()
    {
        ApplyState();
    }

    private void ApplyVisual(bool isSelected, Graphic background, Graphic label)
    {
        if (background != null)
        {
            background.color = isSelected ? selectedBackground : unselectedBackground;
        }

        if (label != null)
        {
            label.color = isSelected ? selectedText : unselectedText;
            ApplyLabelFontSize(label, isSelected);
            ApplyLabelFontStyle(label, isSelected);
        }
    }

    private void CacheBaseFontSize(Graphic label)
    {
        if (label == null || baseLabelFontSizes.ContainsKey(label))
        {
            return;
        }

        if (!TryGetLabelFontSize(label, out float size))
        {
            return;
        }

        baseLabelFontSizes.Add(label, size);
    }

    private void CacheBaseFontStyle(Graphic label)
    {
        if (label is TMP_Text tmpText)
        {
            if (!baseTmpFontStyles.ContainsKey(tmpText))
            {
                baseTmpFontStyles.Add(tmpText, tmpText.fontStyle);
            }

            return;
        }

        if (label is Text legacyText && !baseLegacyFontStyles.ContainsKey(legacyText))
        {
            baseLegacyFontStyles.Add(legacyText, legacyText.fontStyle);
        }
    }

    private void ApplyLabelFontSize(Graphic label, bool isSelected)
    {
        if (label == null)
        {
            return;
        }

        if (useFixedLabelFontSizes)
        {
            float fixedSize = isSelected ? selectedLabelFontSize : unselectedLabelFontSize;
            SetLabelFontSize(label, fixedSize);
            return;
        }

        if (!baseLabelFontSizes.TryGetValue(label, out float baseSize))
        {
            CacheBaseFontSize(label);
            if (!baseLabelFontSizes.TryGetValue(label, out baseSize))
            {
                return;
            }
        }

        SetLabelFontSize(label, baseSize);
    }

    private void ApplyLabelFontStyle(Graphic label, bool isSelected)
    {
        if (label == null)
        {
            return;
        }

        if (label is TMP_Text tmpText)
        {
            if (!baseTmpFontStyles.TryGetValue(tmpText, out FontStyles baseStyle))
            {
                CacheBaseFontStyle(label);
                if (!baseTmpFontStyles.TryGetValue(tmpText, out baseStyle))
                {
                    return;
                }
            }

            tmpText.fontStyle = useBoldForSelected && isSelected
                ? baseStyle | FontStyles.Bold
                : baseStyle;
            return;
        }

        if (label is Text legacyText)
        {
            if (!baseLegacyFontStyles.TryGetValue(legacyText, out FontStyle baseStyle))
            {
                CacheBaseFontStyle(label);
                if (!baseLegacyFontStyles.TryGetValue(legacyText, out baseStyle))
                {
                    return;
                }
            }

            legacyText.fontStyle = useBoldForSelected && isSelected
                ? CombineWithBold(baseStyle)
                : baseStyle;
        }
    }

    private bool TryGetLabelFontSize(Graphic label, out float size)
    {
        size = 0f;

        if (label is TMP_Text tmpText)
        {
            size = tmpText.fontSize;
            return true;
        }

        if (label is Text legacyText)
        {
            size = legacyText.fontSize;
            return true;
        }

        return false;
    }

    private void SetLabelFontSize(Graphic label, float size)
    {
        if (label is TMP_Text tmpText)
        {
            tmpText.fontSize = size;
            return;
        }

        if (label is Text legacyText)
        {
            legacyText.fontSize = Mathf.RoundToInt(size);
        }
    }

    private static FontStyle CombineWithBold(FontStyle baseStyle)
    {
        return baseStyle switch
        {
            FontStyle.Italic => FontStyle.BoldAndItalic,
            FontStyle.BoldAndItalic => FontStyle.BoldAndItalic,
            FontStyle.Bold => FontStyle.Bold,
            _ => FontStyle.Bold
        };
    }
}
