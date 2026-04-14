using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MissionDefinition))]
public class MissionDefinitionEditor : Editor
{
    private List<MissionAssetValidation.Entry> cachedEntries = new List<MissionAssetValidation.Entry>();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("Open Mission Authoring"))
        {
            MissionAuthoringWindow.OpenWindow((MissionDefinition)target);
        }

        EditorGUILayout.Space();
        DrawCreationTools();
        EditorGUILayout.Space();
        DrawValidationTools();
    }

    [MenuItem("Tools/TrueJourney/Missions/Validate All Mission Assets")]
    private static void ValidateAllMissionAssets()
    {
        string[] missionGuids = AssetDatabase.FindAssets("t:MissionDefinition");
        int missionCount = 0;
        for (int i = 0; i < missionGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(missionGuids[i]);
            MissionDefinition mission = AssetDatabase.LoadAssetAtPath<MissionDefinition>(path);
            if (mission == null)
            {
                continue;
            }

            missionCount++;
            List<MissionAssetValidation.Entry> entries = MissionAssetValidation.ValidateMission(mission);
            MissionAssetValidation.LogEntries($"Mission '{mission.name}'", entries, mission);
        }

        Debug.Log($"Validated {missionCount} mission assets.");
    }

    private void DrawCreationTools()
    {
        EditorGUILayout.LabelField("Quick Create", EditorStyles.boldLabel);
        if (GUILayout.Button("Create Stage Asset"))
        {
            MissionEditorAssetCreationUtility.ShowCreateAssetMenu<MissionStageDefinition>(serializedObject, "stages", "Create Stage");
        }

        if (GUILayout.Button("Create Persistent Objective Asset"))
        {
            MissionEditorAssetCreationUtility.ShowCreateAssetMenu<MissionObjectiveDefinition>(serializedObject, "persistentObjectives", "Create Persistent Objective");
        }

        if (GUILayout.Button("Create Fail Condition Asset"))
        {
            MissionEditorAssetCreationUtility.ShowCreateAssetMenu<MissionFailConditionDefinition>(serializedObject, "failConditions", "Create Fail Condition");
        }
    }

    private void DrawValidationTools()
    {
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
        if (GUILayout.Button("Validate Mission Asset"))
        {
            cachedEntries = MissionAssetValidation.ValidateMission((MissionDefinition)target);
            MissionAssetValidation.LogEntries($"Mission '{target.name}'", cachedEntries, target);
        }

        if (cachedEntries == null || cachedEntries.Count == 0)
        {
            return;
        }

        for (int i = 0; i < cachedEntries.Count; i++)
        {
            MissionAssetValidation.Entry entry = cachedEntries[i];
            MessageType messageType = entry.severity == MissionAssetValidation.Severity.Error
                ? MessageType.Error
                : entry.severity == MissionAssetValidation.Severity.Warning
                    ? MessageType.Warning
                    : MessageType.Info;
            EditorGUILayout.HelpBox(entry.message, messageType);
        }
    }
}
