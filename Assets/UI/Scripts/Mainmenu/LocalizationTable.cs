using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LocalizationTable", menuName = "UI/Localization Table")]
public class LocalizationTable : ScriptableObject
{
    [Serializable]
    public class LocalizationEntry
    {
        public string key;
        [TextArea] public string vietnamese;
        [TextArea] public string english;
    }

    [SerializeField] private List<LocalizationEntry> entries = new List<LocalizationEntry>();

    private readonly Dictionary<string, LocalizationEntry> lookup =
        new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);
    private bool lookupDirty = true;

    public bool TryGet(string key, AppLanguage language, out string text)
    {
        text = string.Empty;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        EnsureLookup();
        if (!lookup.TryGetValue(key, out LocalizationEntry entry) || entry == null)
        {
            return false;
        }

        text = language == AppLanguage.Vietnamese ? entry.vietnamese : entry.english;

        if (string.IsNullOrEmpty(text))
        {
            text = language == AppLanguage.Vietnamese ? entry.english : entry.vietnamese;
        }

        return !string.IsNullOrEmpty(text);
    }

    public string Get(string key, AppLanguage language, string fallback = "")
    {
        return TryGet(key, language, out string text) ? text : fallback;
    }

    private void OnValidate()
    {
        lookupDirty = true;
    }

    private void EnsureLookup()
    {
        if (!lookupDirty)
        {
            return;
        }

        lookup.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            LocalizationEntry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            if (lookup.ContainsKey(entry.key))
            {
                Debug.LogWarning($"Duplicate localization key '{entry.key}' in {name}.", this);
                continue;
            }

            lookup.Add(entry.key, entry);
        }

        lookupDirty = false;
    }
}
