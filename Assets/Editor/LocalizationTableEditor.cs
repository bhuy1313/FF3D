using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LocalizationTable))]
public class LocalizationTableEditor : Editor
{
    private const string SearchControlName = "LocalizationTableKeySearch";

    private SerializedProperty entriesProperty;
    private string keySearch = string.Empty;
    private Vector2 scrollPosition;

    private void OnEnable()
    {
        entriesProperty = serializedObject.FindProperty("entries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawSearchToolbar();
        DrawSummary();
        DrawEntryList();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSearchToolbar()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Quick Key Search", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUI.SetNextControlName(SearchControlName);
        keySearch = EditorGUILayout.TextField("Key", keySearch ?? string.Empty);

        if (GUILayout.Button("Clear", GUILayout.Width(60f)))
        {
            keySearch = string.Empty;
            GUI.FocusControl(null);
        }

        if (GUILayout.Button("Add Entry", GUILayout.Width(80f)))
        {
            int newIndex = entriesProperty.arraySize;
            entriesProperty.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newEntry = entriesProperty.GetArrayElementAtIndex(newIndex);
            newEntry.FindPropertyRelative("key").stringValue = string.Empty;
            newEntry.FindPropertyRelative("vietnamese").stringValue = string.Empty;
            newEntry.FindPropertyRelative("english").stringValue = string.Empty;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Filter is applied by localization key only. Matching entries remain editable in-place.",
            MessageType.None);
        EditorGUILayout.EndVertical();
    }

    private void DrawSummary()
    {
        int totalEntries = entriesProperty != null ? entriesProperty.arraySize : 0;
        int matchedEntries = CountMatchedEntries();
        string summary = string.IsNullOrWhiteSpace(keySearch)
            ? $"Entries: {totalEntries}"
            : $"Entries: {totalEntries} | Matches: {matchedEntries}";
        EditorGUILayout.LabelField(summary, EditorStyles.miniBoldLabel);
    }

    private void DrawEntryList()
    {
        if (entriesProperty == null)
        {
            EditorGUILayout.HelpBox("Could not resolve 'entries' property.", MessageType.Error);
            return;
        }

        int matchedEntries = CountMatchedEntries();
        if (!string.IsNullOrWhiteSpace(keySearch) && matchedEntries == 0)
        {
            EditorGUILayout.HelpBox($"No localization keys matched \"{keySearch}\".", MessageType.Info);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        for (int i = 0; i < entriesProperty.arraySize; i++)
        {
            SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(i);
            SerializedProperty keyProperty = entryProperty.FindPropertyRelative("key");

            if (!MatchesFilter(keyProperty.stringValue))
            {
                continue;
            }

            DrawEntry(entryProperty, i);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawEntry(SerializedProperty entryProperty, int index)
    {
        SerializedProperty keyProperty = entryProperty.FindPropertyRelative("key");
        SerializedProperty vietnameseProperty = entryProperty.FindPropertyRelative("vietnamese");
        SerializedProperty englishProperty = entryProperty.FindPropertyRelative("english");

        string headerKey = string.IsNullOrWhiteSpace(keyProperty.stringValue)
            ? "<empty key>"
            : keyProperty.stringValue;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"[{index}] {headerKey}", EditorStyles.boldLabel);

        if (GUILayout.Button("Copy Key", GUILayout.Width(75f)))
        {
            EditorGUIUtility.systemCopyBuffer = keyProperty.stringValue ?? string.Empty;
        }

        if (GUILayout.Button("Delete", GUILayout.Width(60f)))
        {
            entriesProperty.DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(keyProperty);
        EditorGUILayout.PropertyField(vietnameseProperty);
        EditorGUILayout.PropertyField(englishProperty);

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private int CountMatchedEntries()
    {
        if (entriesProperty == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < entriesProperty.arraySize; i++)
        {
            SerializedProperty keyProperty = entriesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("key");
            if (MatchesFilter(keyProperty.stringValue))
            {
                count++;
            }
        }

        return count;
    }

    private bool MatchesFilter(string key)
    {
        if (string.IsNullOrWhiteSpace(keySearch))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(key)
            && key.IndexOf(keySearch.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
