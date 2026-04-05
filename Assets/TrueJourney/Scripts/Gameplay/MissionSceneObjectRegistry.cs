using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MissionSceneObjectRegistry : MonoBehaviour
{
    [Serializable]
    private class Entry
    {
        [SerializeField] private string key;
        [SerializeField] private GameObject targetObject;

        public string Key => key;
        public GameObject TargetObject => targetObject;

        public void Set(string newKey, GameObject newTargetObject)
        {
            key = newKey;
            targetObject = newTargetObject;
        }
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public bool TryResolveGameObject(string key, out GameObject targetObject)
    {
        targetObject = null;
        if (string.IsNullOrWhiteSpace(key) || entries == null)
        {
            return false;
        }

        string normalizedKey = key.Trim();
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || entry.TargetObject == null)
            {
                continue;
            }

            if (!string.Equals(entry.Key.Trim(), normalizedKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            targetObject = entry.TargetObject;
            return true;
        }

        return false;
    }

    public void Register(string key, GameObject targetObject)
    {
        if (string.IsNullOrWhiteSpace(key) || targetObject == null)
        {
            return;
        }

        if (entries == null)
        {
            entries = new List<Entry>();
        }

        string normalizedKey = key.Trim();
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
            {
                continue;
            }

            if (!string.Equals(entry.Key.Trim(), normalizedKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            entry.Set(normalizedKey, targetObject);
            return;
        }

        Entry createdEntry = new Entry();
        createdEntry.Set(normalizedKey, targetObject);
        entries.Add(createdEntry);
    }
}
