using UnityEditor;
using UnityEngine;

public class MissionAuthoringWindow : EditorWindow
{
    private MissionDefinition missionDefinition;
    private Editor missionEditor;

    public static void OpenWindow(MissionDefinition mission)
    {
        MissionAuthoringWindow window = GetWindow<MissionAuthoringWindow>("Mission Authoring");
        window.SetMission(mission);
        window.Show();
    }

    public static void OpenWindow()
    {
        OpenWindow(null);
    }

    private void OnDisable()
    {
        if (missionEditor != null)
        {
            DestroyImmediate(missionEditor);
            missionEditor = null;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Persistent Mission Authoring", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            MissionDefinition selectedMission = EditorGUILayout.ObjectField("Mission", missionDefinition, typeof(MissionDefinition), false) as MissionDefinition;
            if (selectedMission != missionDefinition)
            {
                SetMission(selectedMission);
            }

            if (GUILayout.Button("New", GUILayout.Width(60f)))
            {
                SetMission(MissionAuthoringAssetUtility.CreateMissionAssetInteractive());
            }
        }

        if (missionDefinition == null)
        {
            EditorGUILayout.HelpBox("Select a MissionDefinition to edit persistent objectives and fail conditions.", MessageType.Info);
            return;
        }

        EnsureEditor();
        if (missionEditor != null)
        {
            missionEditor.OnInspectorGUI();
        }
    }

    private void SetMission(MissionDefinition mission)
    {
        missionDefinition = mission;
        if (missionEditor != null)
        {
            DestroyImmediate(missionEditor);
            missionEditor = null;
        }
    }

    private void EnsureEditor()
    {
        if (missionDefinition == null || missionEditor != null)
        {
            return;
        }

        missionEditor = Editor.CreateEditor(missionDefinition);
    }
}
