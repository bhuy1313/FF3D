using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class CallPhaseUiChromeShortValueOverride : MonoBehaviour
{
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private LocalizedText localizedText;
    [SerializeField] private string vietnameseShortKey;
    [SerializeField] private string vietnameseShortFallback;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        LanguageManager.LanguageChanged -= HandleLanguageChanged;
        LanguageManager.LanguageChanged += HandleLanguageChanged;
        Refresh();
    }

    private void OnDisable()
    {
        LanguageManager.LanguageChanged -= HandleLanguageChanged;
    }

    public void Configure(string shortKey, string shortFallback)
    {
        vietnameseShortKey = shortKey;
        vietnameseShortFallback = shortFallback;
        Refresh();
    }

    public void Refresh()
    {
        ResolveReferences();

        if (targetText == null)
        {
            return;
        }

        if (!CallPhaseUiChromeText.IsCurrentLanguageVietnamese())
        {
            localizedText?.Refresh();
            return;
        }

        CallPhaseUiChromeText.ApplyCurrentFont(targetText);
        targetText.text = CallPhaseUiChromeText.Tr(vietnameseShortKey, vietnameseShortFallback);
    }

    private void HandleLanguageChanged(AppLanguage _)
    {
        Refresh();
    }

    private void ResolveReferences()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }

        if (localizedText == null)
        {
            localizedText = GetComponent<LocalizedText>();
        }
    }
}
