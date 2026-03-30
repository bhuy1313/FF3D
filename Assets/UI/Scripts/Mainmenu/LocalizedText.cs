using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LocalizedText : MonoBehaviour
{
    [Header("Localization")]
    [SerializeField] private string localizationKey;
    [SerializeField] private string fallbackText = "";
    [SerializeField] private bool useKeyAsFallback = true;
    [SerializeField] private bool refreshOnEnable = true;

    [Header("Text Targets")]
    [SerializeField] private TMP_Text tmpText;
    [SerializeField] private Text legacyText;
    [SerializeField] private bool autoFindTextComponent = true;

    [Header("Font By Language (Optional)")]
    [SerializeField] private TMP_FontAsset vietnameseTMPFont;
    [SerializeField] private TMP_FontAsset englishTMPFont;
    [SerializeField] private Font vietnameseLegacyFont;
    [SerializeField] private Font englishLegacyFont;

    public string LocalizationKey => localizationKey;

    private void Awake()
    {
        ResolveTextTargets();
    }

    private void OnEnable()
    {
        LanguageManager.LanguageChanged -= OnLanguageChanged;
        LanguageManager.LanguageChanged += OnLanguageChanged;

        if (refreshOnEnable)
        {
            Refresh();
        }
    }

    private void OnDisable()
    {
        LanguageManager.LanguageChanged -= OnLanguageChanged;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        ResolveTextTargets();
        Refresh();
    }

    public void SetKey(string newKey, bool refreshNow = true)
    {
        localizationKey = newKey;
        if (refreshNow)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        ResolveTextTargets();

        AppLanguage language = LanguageManager.Instance != null
            ? LanguageManager.Instance.CurrentLanguage
            : AppLanguage.Vietnamese;

        string resolvedText = ResolveText(language);
        ApplyText(resolvedText);
        ApplyFont(language);
    }

    private void OnLanguageChanged(AppLanguage _)
    {
        Refresh();
    }

    private void ResolveTextTargets()
    {
        if (!autoFindTextComponent)
        {
            return;
        }

        if (tmpText == null)
        {
            tmpText = GetComponent<TMP_Text>();
        }

        if (legacyText == null)
        {
            legacyText = GetComponent<Text>();
        }
    }

    private string ResolveText(AppLanguage language)
    {
        if (!string.IsNullOrWhiteSpace(localizationKey) && LanguageManager.Instance != null)
        {
            string fallback = GetFallbackText();
            return LanguageManager.Instance.GetText(localizationKey, fallback);
        }

        return GetFallbackText();
    }

    private string GetFallbackText()
    {
        if (!string.IsNullOrEmpty(fallbackText))
        {
            return fallbackText;
        }

        if (useKeyAsFallback && !string.IsNullOrWhiteSpace(localizationKey))
        {
            return localizationKey;
        }

        if (tmpText != null)
        {
            return tmpText.text;
        }

        if (legacyText != null)
        {
            return legacyText.text;
        }

        return string.Empty;
    }

    private void ApplyText(string value)
    {
        if (tmpText != null)
        {
            tmpText.text = value;
        }

        if (legacyText != null)
        {
            legacyText.text = value;
        }
    }

    private void ApplyFont(AppLanguage language)
    {
        LanguageManager manager = LanguageManager.Instance;

        if (tmpText != null)
        {
            TMP_FontAsset selectedTMPFont = language == AppLanguage.Vietnamese
                ? vietnameseTMPFont
                : englishTMPFont;

            if (selectedTMPFont == null && manager != null)
            {
                selectedTMPFont = manager.GetTMPFontFor(language);
            }

            if (selectedTMPFont != null)
            {
                tmpText.font = selectedTMPFont;
            }
        }

        if (legacyText != null)
        {
            Font selectedLegacyFont = language == AppLanguage.Vietnamese
                ? vietnameseLegacyFont
                : englishLegacyFont;

            if (selectedLegacyFont == null && manager != null)
            {
                selectedLegacyFont = manager.GetLegacyFontFor(language);
            }

            if (selectedLegacyFont != null)
            {
                legacyText.font = selectedLegacyFont;
            }
        }
    }
}
