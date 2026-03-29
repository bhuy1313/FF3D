using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MissionStageDefinition))]
public class MissionStageDefinitionEditor : Editor
{
    private List<MissionAssetValidation.Entry> cachedEntries = new List<MissionAssetValidation.Entry>();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        DrawCreationTools();
        EditorGUILayout.Space();
        DrawValidationTools();
    }

    private void DrawCreationTools()
    {
        EditorGUILayout.LabelField("Quick Create", EditorStyles.boldLabel);
        if (GUILayout.Button("Create Objective Asset"))
        {
            MissionEditorAssetCreationUtility.ShowCreateAssetMenu<MissionObjectiveDefinition>(serializedObject, "objectives", "Create Objective");
        }

        if (GUILayout.Button("Create Stage Started Action Asset"))
        {
            MissionEditorAssetCreationUtility.ShowCreateAssetMenu<MissionActionDefinition>(serializedObject, "onStageStartedActions", "Create Stage Started Action");
        }

        if (GUILayout.Button("Create Stage Completed Action Asset"))
        {
            MissionEditorAssetCreationUtility.ShowCreateAssetMenu<MissionActionDefinition>(serializedObject, "onStageCompletedActions", "Create Stage Completed Action");
        }
    }

    private void DrawValidationTools()
    {
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
        if (GUILayout.Button("Validate Stage Asset"))
        {
            cachedEntries = MissionAssetValidation.ValidateStage((MissionStageDefinition)target);
            MissionAssetValidation.LogEntries($"Stage '{target.name}'", cachedEntries, target);
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
