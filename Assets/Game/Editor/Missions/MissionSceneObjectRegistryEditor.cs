using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(MissionSceneObjectRegistry))]
public class MissionSceneObjectRegistryEditor : Editor
{
    private readonly List<string> warnings = new List<string>();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        DrawTools();
        EditorGUILayout.Space();
        DrawWarnings();
    }

    private void DrawTools()
    {
        MissionSceneObjectRegistry registry = (MissionSceneObjectRegistry)target;
        EditorGUILayout.LabelField("Scene Tools", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Selected Object"))
            {
                TryAddSelectedObject(registry);
            }

            if (GUILayout.Button("Auto Scan Scene"))
            {
                AutoScanScene(registry);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Validate Registry"))
            {
                RefreshWarnings(registry);
            }

            if (GUILayout.Button("Open Mission Authoring"))
            {
                MissionDefinition ownerMission = ResolveSceneMissionDefinition(registry);
                MissionAuthoringWindow.OpenWindow(ownerMission);
            }
        }
    }

    private void DrawWarnings()
    {
        if (warnings.Count == 0)
        {
            return;
        }

        EditorGUILayout.LabelField("Warnings", EditorStyles.boldLabel);
        for (int i = 0; i < warnings.Count; i++)
        {
            EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);
        }
    }

    private void TryAddSelectedObject(MissionSceneObjectRegistry registry)
    {
        GameObject selectedObject = Selection.activeGameObject;
        if (registry == null || selectedObject == null)
        {
            return;
        }

        RegisterObject(registry, selectedObject);
        RefreshWarnings(registry);
    }

    private void AutoScanScene(MissionSceneObjectRegistry registry)
    {
        if (registry == null)
        {
            return;
        }

        Scene scene = registry.gameObject.scene;
        if (!scene.IsValid())
        {
            return;
        }

        GameObject[] candidates = CollectSceneCandidates(scene);
        for (int i = 0; i < candidates.Length; i++)
        {
            RegisterObject(registry, candidates[i]);
        }

        RefreshWarnings(registry);
        EditorUtility.SetDirty(registry);
    }

    private void RegisterObject(MissionSceneObjectRegistry registry, GameObject targetObject)
    {
        if (registry == null || targetObject == null)
        {
            return;
        }

        SerializedObject serializedRegistry = new SerializedObject(registry);
        serializedRegistry.Update();
        SerializedProperty entriesProperty = serializedRegistry.FindProperty("entries");
        if (entriesProperty == null || !entriesProperty.isArray)
        {
            return;
        }

        for (int i = 0; i < entriesProperty.arraySize; i++)
        {
            SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(i);
            GameObject existingObject = entryProperty.FindPropertyRelative("targetObject")?.objectReferenceValue as GameObject;
            if (existingObject == targetObject)
            {
                return;
            }
        }

        string key = BuildUniqueKey(registry, targetObject.name);
        int index = entriesProperty.arraySize;
        entriesProperty.InsertArrayElementAtIndex(index);
        SerializedProperty createdEntry = entriesProperty.GetArrayElementAtIndex(index);
        createdEntry.FindPropertyRelative("key").stringValue = key;
        createdEntry.FindPropertyRelative("targetObject").objectReferenceValue = targetObject;
        serializedRegistry.ApplyModifiedProperties();
    }

    private void RefreshWarnings(MissionSceneObjectRegistry registry)
    {
        warnings.Clear();
        if (registry == null)
        {
            return;
        }

        SerializedObject serializedRegistry = new SerializedObject(registry);
        SerializedProperty entriesProperty = serializedRegistry.FindProperty("entries");
        if (entriesProperty == null || !entriesProperty.isArray)
        {
            return;
        }

        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < entriesProperty.arraySize; i++)
        {
            SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(i);
            string key = entryProperty.FindPropertyRelative("key")?.stringValue?.Trim() ?? string.Empty;
            GameObject targetObject = entryProperty.FindPropertyRelative("targetObject")?.objectReferenceValue as GameObject;

            if (string.IsNullOrWhiteSpace(key))
            {
                warnings.Add($"Entry {i} has an empty key.");
                continue;
            }

            if (targetObject == null)
            {
                warnings.Add($"Registry key '{key}' has no target object.");
            }

            if (counts.ContainsKey(key))
            {
                counts[key]++;
            }
            else
            {
                counts.Add(key, 1);
            }
        }

        foreach (KeyValuePair<string, int> pair in counts)
        {
            if (pair.Value > 1)
            {
                warnings.Add($"Duplicate registry key '{pair.Key}' found {pair.Value} times.");
            }
        }
    }

    private static GameObject[] CollectSceneCandidates(Scene scene)
    {
        List<GameObject> results = new List<GameObject>();
        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            if (transform == null)
            {
                continue;
            }

            GameObject candidate = transform.gameObject;
            if (candidate.scene != scene || !ShouldRegister(candidate))
            {
                continue;
            }

            results.Add(candidate);
        }

        return results.ToArray();
    }

    private static bool ShouldRegister(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (candidate.GetComponent<MissionSceneObjectRegistry>() != null)
        {
            return false;
        }

        return candidate.GetComponent<Explosive>() != null ||
               candidate.GetComponent<MissionSignalSource>() != null ||
               candidate.GetComponent<MissionInteractionSignalRelay>() != null ||
               candidate.GetComponent<MissionBreakableSignalRelay>() != null ||
               candidate.GetComponent<MissionRescueDeliverySignalRelay>() != null ||
               candidate.GetComponent<Breakable>() != null ||
               candidate.GetComponent<Rescuable>() != null ||
               candidate.GetComponent<SafeZone>() != null ||
               candidate.GetComponent(typeof(IInteractable)) != null;
    }

    private static string BuildUniqueKey(MissionSceneObjectRegistry registry, string nameHint)
    {
        string baseKey = Slugify(nameHint);
        HashSet<string> existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        SerializedObject serializedRegistry = new SerializedObject(registry);
        SerializedProperty entriesProperty = serializedRegistry.FindProperty("entries");
        if (entriesProperty != null && entriesProperty.isArray)
        {
            for (int i = 0; i < entriesProperty.arraySize; i++)
            {
                string key = entriesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("key")?.stringValue;
                if (!string.IsNullOrWhiteSpace(key))
                {
                    existingKeys.Add(key.Trim());
                }
            }
        }

        if (!existingKeys.Contains(baseKey))
        {
            return baseKey;
        }

        int suffix = 2;
        string candidate = $"{baseKey}-{suffix}";
        while (existingKeys.Contains(candidate))
        {
            suffix++;
            candidate = $"{baseKey}-{suffix}";
        }

        return candidate;
    }

    private static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "target";
        }

        StringBuilder builder = new StringBuilder(input.Length);
        bool previousWasDash = false;
        string normalized = input.Trim().ToLowerInvariant();
        for (int i = 0; i < normalized.Length; i++)
        {
            char character = normalized[i];
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasDash = false;
                continue;
            }

            if (previousWasDash)
            {
                continue;
            }

            builder.Append('-');
            previousWasDash = true;
        }

        string slug = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "target" : slug;
    }

    private static MissionDefinition ResolveSceneMissionDefinition(MissionSceneObjectRegistry registry)
    {
        if (registry == null)
        {
            return null;
        }

        IncidentMissionSystem[] missionSystems = UnityEngine.Object.FindObjectsByType<IncidentMissionSystem>(FindObjectsInactive.Include);
        for (int i = 0; i < missionSystems.Length; i++)
        {
            IncidentMissionSystem missionSystem = missionSystems[i];
            if (missionSystem == null || missionSystem.gameObject.scene != registry.gameObject.scene)
            {
                continue;
            }

            SerializedObject serializedMissionSystem = new SerializedObject(missionSystem);
            return serializedMissionSystem.FindProperty("missionDefinition")?.objectReferenceValue as MissionDefinition;
        }

        return null;
    }
}
