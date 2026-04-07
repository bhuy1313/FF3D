using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LanguageManagedFont : MonoBehaviour
{
    [Header("Text Targets")]
    [SerializeField] private TMP_Text tmpText;
    [SerializeField] private Text legacyText;
    [SerializeField] private bool autoFindTextComponent = true;
    [SerializeField] private bool refreshOnEnable = true;

    [Header("Font By Language")]
    [SerializeField] private LanguageFontRole fontRole = LanguageFontRole.Default;
    [SerializeField] private TMP_FontAsset vietnameseTMPFont;
    [SerializeField] private TMP_FontAsset englishTMPFont;
    [SerializeField] private Font vietnameseLegacyFont;
    [SerializeField] private Font englishLegacyFont;

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

    public void Refresh()
    {
        ResolveTextTargets();
        ApplyFont(GetCurrentLanguage());
    }

    private void OnLanguageChanged(AppLanguage _)
    {
        Refresh();
    }

    private AppLanguage GetCurrentLanguage()
    {
        return LanguageManager.Instance != null
            ? LanguageManager.Instance.CurrentLanguage
            : AppLanguage.Vietnamese;
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
                selectedTMPFont = manager.GetTMPFontFor(language, fontRole);
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
                selectedLegacyFont = manager.GetLegacyFontFor(language, fontRole);
            }

            if (selectedLegacyFont != null)
            {
                legacyText.font = selectedLegacyFont;
            }
        }
    }
}
