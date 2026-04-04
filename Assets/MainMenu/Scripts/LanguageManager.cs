using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class LanguageManager : MonoBehaviour
{
    [Serializable]
    private class LanguageFontEntry
    {
        public LanguageFontRole role = LanguageFontRole.Default;
        public TMP_FontAsset vietnameseTMPFont;
        public TMP_FontAsset englishTMPFont;
        public Font vietnameseLegacyFont;
        public Font englishLegacyFont;
    }

    private const string PlayerPrefsLanguageKey = "game.language";

    public static LanguageManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<LanguageManager>();
            }

            return instance;
        }
    }

    public static event Action<AppLanguage> LanguageChanged;

    [Header("Config")]
    [SerializeField] private AppLanguage defaultLanguage = AppLanguage.Vietnamese;
    [SerializeField] private LocalizationTable localizationTable;
    [SerializeField] private bool keepAliveAcrossScenes = true;

    [Header("Default Fonts (Optional)")]
    [SerializeField] private TMP_FontAsset vietnameseTMPFont;
    [SerializeField] private TMP_FontAsset englishTMPFont;
    [SerializeField] private Font vietnameseLegacyFont;
    [SerializeField] private Font englishLegacyFont;

    [Header("Font Sets By Role (Optional)")]
    [SerializeField] private List<LanguageFontEntry> fontEntries = new List<LanguageFontEntry>();

    [Header("Optional Auto Load")]
    [SerializeField] private bool autoLoadTableFromResources = false;
    [SerializeField] private string tableResourcePath = "LocalizationTable";

    private static LanguageManager instance;

    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Vietnamese;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (keepAliveAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (localizationTable == null && autoLoadTableFromResources && !string.IsNullOrWhiteSpace(tableResourcePath))
        {
            localizationTable = Resources.Load<LocalizationTable>(tableResourcePath);
        }

        LoadSavedLanguage();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public void SetLanguage(AppLanguage language)
    {
        SetLanguage(language, true, false);
    }

    public void SetLanguage(AppLanguage language, bool saveToPrefs, bool forceNotify)
    {
        if (!forceNotify && CurrentLanguage == language)
        {
            return;
        }

        CurrentLanguage = language;

        if (saveToPrefs)
        {
            PlayerPrefs.SetInt(PlayerPrefsLanguageKey, (int)CurrentLanguage);
            PlayerPrefs.Save();
        }

        LanguageChanged?.Invoke(CurrentLanguage);
    }

    public bool TryGetText(string key, out string text)
    {
        text = string.Empty;
        if (localizationTable == null)
        {
            return false;
        }

        return localizationTable.TryGet(key, CurrentLanguage, out text);
    }

    public string GetText(string key, string fallback = "")
    {
        if (TryGetText(key, out string text))
        {
            return text;
        }

        if (!string.IsNullOrEmpty(fallback))
        {
            return fallback;
        }

        return key;
    }

    public string GetText(string key, AppLanguage language, string fallback = "")
    {
        if (localizationTable != null && localizationTable.TryGet(key, language, out string text))
        {
            return text;
        }

        if (!string.IsNullOrEmpty(fallback))
        {
            return fallback;
        }

        return key;
    }

    public static string Tr(string key, string fallback = "")
    {
        if (Instance == null)
        {
            return string.IsNullOrEmpty(fallback) ? key : fallback;
        }

        return Instance.GetText(key, fallback);
    }

    public static string Tr(string key, AppLanguage language, string fallback = "")
    {
        if (Instance == null)
        {
            return string.IsNullOrEmpty(fallback) ? key : fallback;
        }

        return Instance.GetText(key, language, fallback);
    }

    public void SetLocalizationTable(LocalizationTable table)
    {
        localizationTable = table;
        LanguageChanged?.Invoke(CurrentLanguage);
    }

    public TMP_FontAsset GetTMPFontFor(AppLanguage language)
    {
        return GetTMPFontFor(language, LanguageFontRole.Default);
    }

    public TMP_FontAsset GetTMPFontFor(AppLanguage language, LanguageFontRole role)
    {
        if (TryGetFontEntry(role, out LanguageFontEntry entry))
        {
            TMP_FontAsset roleFont = language == AppLanguage.Vietnamese
                ? entry.vietnameseTMPFont
                : entry.englishTMPFont;

            if (roleFont != null)
            {
                return roleFont;
            }
        }

        return GetDefaultTMPFont(language);
    }

    public TMP_FontAsset GetCurrentTMPFont(LanguageFontRole role = LanguageFontRole.Default)
    {
        return GetTMPFontFor(CurrentLanguage, role);
    }

    public Font GetLegacyFontFor(AppLanguage language)
    {
        return GetLegacyFontFor(language, LanguageFontRole.Default);
    }

    public Font GetLegacyFontFor(AppLanguage language, LanguageFontRole role)
    {
        if (TryGetFontEntry(role, out LanguageFontEntry entry))
        {
            Font roleFont = language == AppLanguage.Vietnamese
                ? entry.vietnameseLegacyFont
                : entry.englishLegacyFont;

            if (roleFont != null)
            {
                return roleFont;
            }
        }

        return GetDefaultLegacyFont(language);
    }

    public Font GetCurrentLegacyFont(LanguageFontRole role = LanguageFontRole.Default)
    {
        return GetLegacyFontFor(CurrentLanguage, role);
    }

    private void LoadSavedLanguage()
    {
        AppLanguage languageToUse = defaultLanguage;

        if (PlayerPrefs.HasKey(PlayerPrefsLanguageKey))
        {
            int rawValue = PlayerPrefs.GetInt(PlayerPrefsLanguageKey, (int)defaultLanguage);
            if (Enum.IsDefined(typeof(AppLanguage), rawValue))
            {
                languageToUse = (AppLanguage)rawValue;
            }
        }

        SetLanguage(languageToUse, false, true);
    }

    private TMP_FontAsset GetDefaultTMPFont(AppLanguage language)
    {
        return language == AppLanguage.Vietnamese ? vietnameseTMPFont : englishTMPFont;
    }

    private Font GetDefaultLegacyFont(AppLanguage language)
    {
        return language == AppLanguage.Vietnamese ? vietnameseLegacyFont : englishLegacyFont;
    }

    private bool TryGetFontEntry(LanguageFontRole role, out LanguageFontEntry entry)
    {
        for (int i = 0; i < fontEntries.Count; i++)
        {
            LanguageFontEntry candidate = fontEntries[i];
            if (candidate != null && candidate.role == role)
            {
                entry = candidate;
                return true;
            }
        }

        entry = null;
        return false;
    }
}
