using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LanguageToggleBinder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Toggle vietnameseToggle;
    [SerializeField] private Toggle englishToggle;
    [SerializeField] private bool applyLanguageImmediately = false;

    [Header("Auto Find (Optional)")]
    [SerializeField] private bool autoFindChildToggles = true;
    [SerializeField] private string vietnameseToggleName = "Toggle_VI";
    [SerializeField] private string englishToggleName = "Toggle_EN";
    [SerializeField] private LangSegmentToggleStyler segmentStyler;

    private bool isSyncing;

    private void OnEnable()
    {
        ResolveToggles();
        ResolveStyler();
        RegisterToggleEvents();

        LanguageManager.LanguageChanged -= OnLanguageChanged;
        LanguageManager.LanguageChanged += OnLanguageChanged;

        SyncTogglesFromLanguage();
    }

    private void OnDisable()
    {
        UnregisterToggleEvents();
        LanguageManager.LanguageChanged -= OnLanguageChanged;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        ResolveToggles();
    }

    private void RegisterToggleEvents()
    {
        if (vietnameseToggle != null)
        {
            vietnameseToggle.onValueChanged.RemoveListener(OnVietnameseToggleChanged);
            vietnameseToggle.onValueChanged.AddListener(OnVietnameseToggleChanged);
        }

        if (englishToggle != null)
        {
            englishToggle.onValueChanged.RemoveListener(OnEnglishToggleChanged);
            englishToggle.onValueChanged.AddListener(OnEnglishToggleChanged);
        }
    }

    private void UnregisterToggleEvents()
    {
        if (vietnameseToggle != null)
        {
            vietnameseToggle.onValueChanged.RemoveListener(OnVietnameseToggleChanged);
        }

        if (englishToggle != null)
        {
            englishToggle.onValueChanged.RemoveListener(OnEnglishToggleChanged);
        }
    }

    private void ResolveToggles()
    {
        if (!autoFindChildToggles)
        {
            return;
        }

        Toggle[] childToggles = GetComponentsInChildren<Toggle>(true);
        for (int i = 0; i < childToggles.Length; i++)
        {
            Toggle toggle = childToggles[i];
            if (toggle == null)
            {
                continue;
            }

            if (vietnameseToggle == null && MatchesName(toggle.gameObject.name, vietnameseToggleName))
            {
                vietnameseToggle = toggle;
                continue;
            }

            if (englishToggle == null && MatchesName(toggle.gameObject.name, englishToggleName))
            {
                englishToggle = toggle;
            }
        }
    }

    private void ResolveStyler()
    {
        if (segmentStyler == null)
        {
            segmentStyler = GetComponent<LangSegmentToggleStyler>();
        }

        if (segmentStyler == null)
        {
            segmentStyler = GetComponentInParent<LangSegmentToggleStyler>();
        }
    }

    private void SyncTogglesFromLanguage()
    {
        if (LanguageManager.Instance == null)
        {
            return;
        }

        isSyncing = true;
        AppLanguage lang = LanguageManager.Instance.CurrentLanguage;

        if (vietnameseToggle != null)
        {
            vietnameseToggle.SetIsOnWithoutNotify(lang == AppLanguage.Vietnamese);
        }

        if (englishToggle != null)
        {
            englishToggle.SetIsOnWithoutNotify(lang == AppLanguage.English);
        }

        if (segmentStyler != null)
        {
            segmentStyler.RefreshVisualState();
        }

        isSyncing = false;
    }

    private void OnLanguageChanged(AppLanguage _)
    {
        SyncTogglesFromLanguage();
    }

    private void OnVietnameseToggleChanged(bool isOn)
    {
        if (isSyncing || !isOn || LanguageManager.Instance == null)
        {
            return;
        }

        if (applyLanguageImmediately)
        {
            // Optional immediate-preview mode. Does not persist to prefs.
            LanguageManager.Instance.SetLanguage(AppLanguage.Vietnamese, false, false);
        }
    }

    private void OnEnglishToggleChanged(bool isOn)
    {
        if (isSyncing || !isOn || LanguageManager.Instance == null)
        {
            return;
        }

        if (applyLanguageImmediately)
        {
            // Optional immediate-preview mode. Does not persist to prefs.
            LanguageManager.Instance.SetLanguage(AppLanguage.English, false, false);
        }
    }

    public void RevertToCurrentLanguage()
    {
        SyncTogglesFromLanguage();
    }

    public bool TryGetSelectedLanguage(out AppLanguage language)
    {
        if (englishToggle != null && englishToggle.isOn)
        {
            language = AppLanguage.English;
            return true;
        }

        if (vietnameseToggle != null && vietnameseToggle.isOn)
        {
            language = AppLanguage.Vietnamese;
            return true;
        }

        if (LanguageManager.Instance != null)
        {
            language = LanguageManager.Instance.CurrentLanguage;
            return true;
        }

        language = AppLanguage.Vietnamese;
        return false;
    }

    public void ApplySelectedLanguage(bool saveToPrefs, bool forceNotify)
    {
        if (LanguageManager.Instance == null)
        {
            return;
        }

        if (TryGetSelectedLanguage(out AppLanguage selectedLanguage))
        {
            LanguageManager.Instance.SetLanguage(selectedLanguage, saveToPrefs, forceNotify);
        }
    }

    private bool MatchesName(string candidate, string expected)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        return candidate.Trim().ToLowerInvariant() == expected.Trim().ToLowerInvariant();
    }
}
