using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class MissionAuthoringWindow : EditorWindow
{
    private enum AssetSection
    {
        Objectives = 0,
        FailConditions = 1
    }

    private MissionDefinition missionDefinition;
    private ScriptableObject selectedAsset;
    private Editor selectedAssetEditor;
    private Vector2 leftScroll;
    private Vector2 rightScroll;
    private MissionObjectiveDefinition objectiveToAdd;
    private MissionFailConditionDefinition failConditionToAdd;

    public static void OpenWindow(MissionDefinition mission)
    {
        MissionAuthoringWindow window = GetWindow<MissionAuthoringWindow>("Mission Authoring");
        window.minSize = new Vector2(960f, 560f);
        window.SetMission(mission);
        window.Show();
    }

    public static void OpenWindow()
    {
        OpenWindow(null);
    }

    private void OnDisable()
    {
        ClearSelectedAssetEditor();
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (missionDefinition == null)
        {
            EditorGUILayout.HelpBox("Select a MissionDefinition to manage persistent objectives and fail conditions.", MessageType.Info);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawMissionWorkspace();
            DrawSelectionInspector();
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.LabelField("Mission Authoring", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            MissionDefinition nextMission = EditorGUILayout.ObjectField("Mission", missionDefinition, typeof(MissionDefinition), false) as MissionDefinition;
            if (nextMission != missionDefinition)
            {
                SetMission(nextMission);
            }

            if (GUILayout.Button("New", GUILayout.Width(64f)))
            {
                SetMission(MissionAuthoringAssetUtility.CreateMissionAssetInteractive());
            }

            using (new EditorGUI.DisabledScope(missionDefinition == null))
            {
                if (GUILayout.Button("Ping", GUILayout.Width(64f)))
                {
                    EditorGUIUtility.PingObject(missionDefinition);
                    Selection.activeObject = missionDefinition;
                }
            }
        }

        EditorGUILayout.Space();
    }

    private void DrawMissionWorkspace()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(560f)))
        {
            leftScroll = EditorGUILayout.BeginScrollView(leftScroll);
            DrawMissionSettings();
            EditorGUILayout.Space();
            DrawAssetSection(
                "Persistent Objectives",
                "persistentObjectives",
                ref objectiveToAdd,
                AssetSection.Objectives);
            EditorGUILayout.Space();
            DrawAssetSection(
                "Fail Conditions",
                "failConditions",
                ref failConditionToAdd,
                AssetSection.FailConditions);
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawMissionSettings()
    {
        SerializedObject serializedMission = new SerializedObject(missionDefinition);
        serializedMission.Update();

        EditorGUILayout.LabelField("Mission", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedMission.FindProperty("missionId"));
        EditorGUILayout.PropertyField(serializedMission.FindProperty("missionTitleLocalizationKey"));
        EditorGUILayout.PropertyField(serializedMission.FindProperty("missionTitle"));
        EditorGUILayout.PropertyField(serializedMission.FindProperty("missionDescriptionLocalizationKey"));
        EditorGUILayout.PropertyField(serializedMission.FindProperty("missionDescription"));
        EditorGUILayout.PropertyField(serializedMission.FindProperty("timeLimitSeconds"));
        EditorGUILayout.PropertyField(serializedMission.FindProperty("scoreConfig"), true);

        serializedMission.ApplyModifiedProperties();
    }

    private void DrawAssetSection<TAsset>(string label, string propertyName, ref TAsset addCandidate, AssetSection section)
        where TAsset : ScriptableObject
    {
        SerializedObject missionObject = new SerializedObject(missionDefinition);
        missionObject.Update();
        SerializedProperty listProperty = missionObject.FindProperty(propertyName);
        if (listProperty == null || !listProperty.isArray)
        {
            missionObject.ApplyModifiedProperties();
            return;
        }

        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create"))
            {
                ShowCreateAssetMenu<TAsset>(propertyName, section);
            }

            addCandidate = EditorGUILayout.ObjectField(addCandidate, typeof(TAsset), false) as TAsset;
            using (new EditorGUI.DisabledScope(addCandidate == null))
            {
                if (GUILayout.Button("Add Existing", GUILayout.Width(100f)))
                {
                    MissionAuthoringAssetUtility.AppendReference(missionObject, propertyName, addCandidate);
                    addCandidate = null;
                    Repaint();
                }
            }
        }

        if (listProperty.arraySize == 0)
        {
            EditorGUILayout.HelpBox($"No {label.ToLowerInvariant()} assigned.", MessageType.None);
            missionObject.ApplyModifiedProperties();
            return;
        }

        for (int i = 0; i < listProperty.arraySize; i++)
        {
            SerializedProperty element = listProperty.GetArrayElementAtIndex(i);
            TAsset asset = element.objectReferenceValue as TAsset;
            DrawAssetRow(missionObject, propertyName, i, asset);
        }

        missionObject.ApplyModifiedProperties();
    }

    private void DrawAssetRow(SerializedObject owner, string propertyName, int index, ScriptableObject asset)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(asset, typeof(ScriptableObject), false);

                if (GUILayout.Button("Select", GUILayout.Width(56f)))
                {
                    SetSelectedAsset(asset);
                }

                using (new EditorGUI.DisabledScope(asset == null))
                {
                    if (GUILayout.Button("Ping", GUILayout.Width(48f)))
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }
                }

                using (new EditorGUI.DisabledScope(index <= 0))
                {
                    if (GUILayout.Button("Up", GUILayout.Width(40f)))
                    {
                        MissionAuthoringAssetUtility.MoveReference(owner, propertyName, index, index - 1);
                        GUIUtility.ExitGUI();
                    }
                }

                SerializedProperty listProperty = owner.FindProperty(propertyName);
                using (new EditorGUI.DisabledScope(listProperty == null || index >= listProperty.arraySize - 1))
                {
                    if (GUILayout.Button("Down", GUILayout.Width(52f)))
                    {
                        MissionAuthoringAssetUtility.MoveReference(owner, propertyName, index, index + 1);
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUILayout.Button("Remove", GUILayout.Width(64f)))
                {
                    if (selectedAsset == asset)
                    {
                        SetSelectedAsset(null);
                    }

                    MissionAuthoringAssetUtility.RemoveReferenceAt(owner, propertyName, index);
                    GUIUtility.ExitGUI();
                }
            }

            if (asset == null)
            {
                EditorGUILayout.HelpBox("Missing asset reference.", MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                int refCount = MissionAuthoringAssetUtility.CountMissionReferences(missionDefinition, asset);
                bool isOwnedSubAsset = MissionAuthoringAssetUtility.IsOwnedSubAsset(missionDefinition, asset);
                EditorGUILayout.LabelField(isOwnedSubAsset ? $"Owned sub-asset | refs: {refCount}" : $"External asset | refs: {refCount}", EditorStyles.miniLabel);

                if (!isOwnedSubAsset && GUILayout.Button("Convert To Sub-Asset", GUILayout.Width(132f)))
                {
                    ScriptableObject converted = MissionAuthoringAssetUtility.ConvertToOwnedSubAsset(missionDefinition, asset);
                    if (converted != null && converted != asset)
                    {
                        MissionAuthoringAssetUtility.ReplaceReferenceAt(owner, propertyName, index, converted);
                        if (selectedAsset == asset)
                        {
                            SetSelectedAsset(converted);
                        }
                        GUIUtility.ExitGUI();
                    }
                }

                if (isOwnedSubAsset && GUILayout.Button("Delete Asset", GUILayout.Width(92f)))
                {
                    if (selectedAsset == asset)
                    {
                        SetSelectedAsset(null);
                    }

                    MissionAuthoringAssetUtility.RemoveReferenceAt(owner, propertyName, index);
                    MissionAuthoringAssetUtility.DeleteOwnedSubAsset(missionDefinition, asset);
                    GUIUtility.ExitGUI();
                }
            }
        }
    }

    private void DrawSelectionInspector()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(320f, position.width * 0.36f))))
        {
            EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);
            rightScroll = EditorGUILayout.BeginScrollView(rightScroll);

            if (selectedAsset == null)
            {
                EditorGUILayout.HelpBox("Select an objective or fail condition to edit it inline.", MessageType.None);
            }
            else
            {
                EnsureSelectedAssetEditor();
                if (selectedAssetEditor != null)
                {
                    selectedAssetEditor.OnInspectorGUI();
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void ShowCreateAssetMenu<TAsset>(string propertyName, AssetSection section)
        where TAsset : ScriptableObject
    {
        List<Type> types = MissionAuthoringAssetUtility.GetConcreteDerivedTypes<TAsset>();
        if (types.Count == 0)
        {
            Debug.LogWarning($"No types found for {typeof(TAsset).Name}.", missionDefinition);
            return;
        }

        GenericMenu menu = new GenericMenu();
        for (int i = 0; i < types.Count; i++)
        {
            Type assetType = types[i];
            menu.AddItem(new GUIContent(assetType.Name), false, () => CreateOwnedAsset(propertyName, assetType, section));
        }

        menu.ShowAsContext();
    }

    private void CreateOwnedAsset(string propertyName, Type assetType, AssetSection section)
    {
        if (missionDefinition == null || assetType == null)
        {
            return;
        }

        ScriptableObject createdAsset = MissionAuthoringAssetUtility.CreateMissionChildAsset(missionDefinition, assetType, missionDefinition.name);
        if (createdAsset == null)
        {
            return;
        }

        SerializedObject missionObject = new SerializedObject(missionDefinition);
        MissionAuthoringAssetUtility.AppendReference(missionObject, propertyName, createdAsset);

        if (section == AssetSection.Objectives)
        {
            objectiveToAdd = null;
        }
        else
        {
            failConditionToAdd = null;
        }

        SetSelectedAsset(createdAsset);
        Repaint();
    }

    private void SetMission(MissionDefinition mission)
    {
        missionDefinition = mission;
        objectiveToAdd = null;
        failConditionToAdd = null;

        if (selectedAsset != null)
        {
            string selectedPath = AssetDatabase.GetAssetPath(selectedAsset);
            string missionPath = missionDefinition != null ? AssetDatabase.GetAssetPath(missionDefinition) : string.Empty;
            if (!string.Equals(selectedPath, missionPath, StringComparison.OrdinalIgnoreCase))
            {
                SetSelectedAsset(null);
            }
        }
    }

    private void SetSelectedAsset(ScriptableObject asset)
    {
        if (selectedAsset == asset)
        {
            return;
        }

        selectedAsset = asset;
        ClearSelectedAssetEditor();
    }

    private void EnsureSelectedAssetEditor()
    {
        if (selectedAsset == null || selectedAssetEditor != null)
        {
            return;
        }

        selectedAssetEditor = Editor.CreateEditor(selectedAsset);
    }

    private void ClearSelectedAssetEditor()
    {
        if (selectedAssetEditor != null)
        {
            DestroyImmediate(selectedAssetEditor);
            selectedAssetEditor = null;
        }
    }
}
