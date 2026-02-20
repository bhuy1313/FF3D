using System;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class LanguageManager : MonoBehaviour
{
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
        return language == AppLanguage.Vietnamese ? vietnameseTMPFont : englishTMPFont;
    }

    public Font GetLegacyFontFor(AppLanguage language)
    {
        return language == AppLanguage.Vietnamese ? vietnameseLegacyFont : englishLegacyFont;
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
}
