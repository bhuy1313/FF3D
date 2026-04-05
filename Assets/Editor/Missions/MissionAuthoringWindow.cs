using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public class MissionAuthoringWindow : EditorWindow
{
    private sealed class KeyUsageReference
    {
        public string Label;
        public UnityEngine.Object Context;
    }

    private enum WindowTab
    {
        Authoring = 0,
        SceneScan = 1
    }

    private MissionDefinition missionDefinition;
    private MissionStageDefinition selectedStage;
    private readonly HashSet<ScriptableObject> expandedAssets = new HashSet<ScriptableObject>();
    private MissionAuthoringSceneScanUtility.ScanResult sceneScan;
    private Vector2 authoringScroll;
    private Vector2 sceneScanScroll;
    private WindowTab activeTab;
    private bool showLegacyObjectives;
    private bool showFailConditions = true;
    private readonly Dictionary<string, string> keyPickerSearchTerms = new Dictionary<string, string>();
    private string signalEmitterFilter = string.Empty;
    private string registryEntryFilter = string.Empty;

    [MenuItem("Tools/TrueJourney/Missions/Authoring")]
    public static void OpenWindow()
    {
        MissionAuthoringWindow window = GetWindow<MissionAuthoringWindow>("Mission Authoring");
        window.minSize = new Vector2(860f, 560f);
        window.TryAssignMissionFromObject(Selection.activeObject);
        window.Show();
    }

    public static void OpenWindow(MissionDefinition mission)
    {
        MissionAuthoringWindow window = GetWindow<MissionAuthoringWindow>("Mission Authoring");
        window.minSize = new Vector2(860f, 560f);
        window.SetMission(mission);
        window.Show();
    }

    private void OnEnable()
    {
        if (missionDefinition == null)
        {
            TryAssignMissionFromObject(Selection.activeObject);
        }

        RefreshSceneScan();
    }

    private void OnFocus()
    {
        RefreshSceneScan();
    }

    private void OnHierarchyChange()
    {
        RefreshSceneScan();
        Repaint();
    }

    private void OnSelectionChange()
    {
        if (missionDefinition == null)
        {
            TryAssignMissionFromObject(Selection.activeObject);
        }

        Repaint();
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (missionDefinition == null)
        {
            EditorGUILayout.HelpBox("Assign a MissionDefinition or select one in the Project window to start authoring.", MessageType.Info);
            return;
        }

        string missionPath = AssetDatabase.GetAssetPath(missionDefinition);
        if (string.IsNullOrWhiteSpace(missionPath))
        {
            EditorGUILayout.HelpBox("Mission must be saved as an asset before sub-assets can be created.", MessageType.Warning);
        }

        activeTab = (WindowTab)GUILayout.Toolbar((int)activeTab, new[] { "Authoring", "Scene Scan" });
        EditorGUILayout.Space();

        switch (activeTab)
        {
            case WindowTab.SceneScan:
                DrawSceneScanTab();
                break;
            default:
                DrawAuthoringTab();
                break;
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("New Mission", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            {
                MissionDefinition createdMission = MissionAuthoringAssetUtility.CreateMissionAssetInteractive();
                if (createdMission != null)
                {
                    SetMission(createdMission);
                }
            }

            if (GUILayout.Button("Use Selection", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            {
                TryAssignMissionFromObject(Selection.activeObject);
            }

            MissionDefinition nextMission = EditorGUILayout.ObjectField(missionDefinition, typeof(MissionDefinition), false, GUILayout.MinWidth(220f)) as MissionDefinition;
            if (nextMission != missionDefinition)
            {
                SetMission(nextMission);
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(missionDefinition == null))
            {
                if (GUILayout.Button("Refresh Scene Scan", EditorStyles.toolbarButton, GUILayout.Width(125f)))
                {
                    RefreshSceneScan();
                }

                if (GUILayout.Button("Add Selected To Registry", EditorStyles.toolbarButton, GUILayout.Width(145f)))
                {
                    TryAddSelectedObjectToRegistry();
                }

                if (GUILayout.Button("Create Objective From Selected", EditorStyles.toolbarButton, GUILayout.Width(180f)))
                {
                    TryCreateObjectiveFromSelection();
                }

                if (GUILayout.Button("Create Action From Selected", EditorStyles.toolbarButton, GUILayout.Width(170f)))
                {
                    TryShowCreateActionMenuFromSelection(null);
                }

                if (GUILayout.Button("Convert All To Sub-Assets", EditorStyles.toolbarButton, GUILayout.Width(170f)))
                {
                    TryConvertAllLinkedNodesToSubAssets();
                }
            }
        }
    }

    private void DrawAuthoringTab()
    {
        authoringScroll = EditorGUILayout.BeginScrollView(authoringScroll);

        DrawMissionSection();
        EditorGUILayout.Space();
        DrawStagesSection();
        EditorGUILayout.Space();
        DrawLegacyObjectivesSection();
        EditorGUILayout.Space();
        DrawFailConditionsSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawMissionSection()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Mission", EditorStyles.boldLabel);

            SerializedObject serializedMission = new SerializedObject(missionDefinition);
            serializedMission.Update();
            DrawPropertyIfExists(serializedMission, "missionId");
            DrawPropertyIfExists(serializedMission, "missionTitle");
            DrawPropertyIfExists(serializedMission, "missionDescription");
            DrawPropertyIfExists(serializedMission, "timeLimitSeconds");
            serializedMission.ApplyModifiedProperties();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ping Mission Asset"))
                {
                    EditorGUIUtility.PingObject(missionDefinition);
                    Selection.activeObject = missionDefinition;
                }

                if (GUILayout.Button("Select Mission Asset"))
                {
                    Selection.activeObject = missionDefinition;
                }
            }
        }
    }

    private void DrawStagesSection()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            SerializedObject serializedMission = new SerializedObject(missionDefinition);
            serializedMission.Update();
            DrawReferenceList<MissionStageDefinition>(
                "Stages",
                serializedMission,
                "stages",
                "No stages assigned. Create one to move mission authoring into the stage workflow.");
            serializedMission.ApplyModifiedProperties();
        }
    }

    private void DrawLegacyObjectivesSection()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            showLegacyObjectives = EditorGUILayout.Foldout(showLegacyObjectives, "Mission Objectives (Legacy)", true);
            if (!showLegacyObjectives)
            {
                return;
            }

            if (MissionHasStages())
            {
                EditorGUILayout.HelpBox("Mission has stages. Runtime uses stage objectives and ignores this legacy top-level list.", MessageType.Info);
            }

            SerializedObject serializedMission = new SerializedObject(missionDefinition);
            serializedMission.Update();
            DrawReferenceList<MissionObjectiveDefinition>(
                "Objectives",
                serializedMission,
                "objectives",
                "No top-level objectives assigned.");
            serializedMission.ApplyModifiedProperties();
        }
    }

    private void DrawFailConditionsSection()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            showFailConditions = EditorGUILayout.Foldout(showFailConditions, "Fail Conditions", true);
            if (!showFailConditions)
            {
                return;
            }

            SerializedObject serializedMission = new SerializedObject(missionDefinition);
            serializedMission.Update();
            DrawReferenceList<MissionFailConditionDefinition>(
                "Fail Conditions",
                serializedMission,
                "failConditions",
                "No fail conditions assigned.");
            serializedMission.ApplyModifiedProperties();
        }
    }

    private void DrawReferenceList<TBase>(string title, SerializedObject owner, string propertyName, string emptyMessage) where TBase : ScriptableObject
    {
        SerializedProperty listProperty = owner.FindProperty(propertyName);
        if (listProperty == null || !listProperty.isArray)
        {
            EditorGUILayout.HelpBox($"Property '{propertyName}' is missing.", MessageType.Error);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Create", GUILayout.Width(70f)))
            {
                ShowCreateMenu<TBase>(owner, propertyName);
            }

            if (GUILayout.Button("Relink Existing", GUILayout.Width(105f)))
            {
                ShowRelinkExistingMenu<TBase>(owner, propertyName);
            }

            using (new EditorGUI.DisabledScope(!CanLinkSelection<TBase>()))
            {
                if (GUILayout.Button("Link Selected", GUILayout.Width(100f)))
                {
                    MissionAuthoringAssetUtility.AppendReference(owner, propertyName, Selection.activeObject);
                    GUIUtility.ExitGUI();
                }
            }
        }

        if (listProperty.arraySize == 0)
        {
            EditorGUILayout.HelpBox(emptyMessage, MessageType.None);
            return;
        }

        for (int i = 0; i < listProperty.arraySize; i++)
        {
            ScriptableObject asset = listProperty.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableObject;
            DrawReferenceEntry(owner, propertyName, i, asset);
        }
    }

    private void DrawReferenceEntry(SerializedObject owner, string propertyName, int index, ScriptableObject asset)
    {
        bool isOwnedSubAsset = asset != null && MissionAuthoringAssetUtility.IsOwnedSubAsset(missionDefinition, asset);
        int ownedReferenceCount = isOwnedSubAsset
            ? MissionAuthoringAssetUtility.CountMissionReferences(missionDefinition, asset)
            : 0;

        using (new EditorGUILayout.VerticalScope("box"))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool isExpanded = IsExpanded(asset);
                string label = asset == null
                    ? $"Missing Reference {index + 1}"
                    : GetAssetDisplayName(asset);
                bool nextExpanded = EditorGUILayout.Foldout(isExpanded, label, true);
                if (nextExpanded != isExpanded)
                {
                    SetExpanded(asset, nextExpanded);
                    if (asset is MissionStageDefinition stageAsset)
                    {
                        selectedStage = nextExpanded ? stageAsset : selectedStage == stageAsset ? null : selectedStage;
                    }
                }

                using (new EditorGUI.DisabledScope(asset == null))
                {
                    if (GUILayout.Button("Ping", GUILayout.Width(48f)))
                    {
                        EditorGUIUtility.PingObject(asset);
                    }

                    if (GUILayout.Button("Select", GUILayout.Width(52f)))
                    {
                        Selection.activeObject = asset;
                    }
                }

                using (new EditorGUI.DisabledScope(index <= 0))
                {
                    if (GUILayout.Button("Up", GUILayout.Width(38f)))
                    {
                        MissionAuthoringAssetUtility.MoveReference(owner, propertyName, index, index - 1);
                        GUIUtility.ExitGUI();
                    }
                }

                SerializedProperty ownerProperty = owner.FindProperty(propertyName);
                using (new EditorGUI.DisabledScope(ownerProperty == null || index >= ownerProperty.arraySize - 1))
                {
                    if (GUILayout.Button("Down", GUILayout.Width(48f)))
                    {
                        MissionAuthoringAssetUtility.MoveReference(owner, propertyName, index, index + 1);
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUILayout.Button("Unlink", GUILayout.Width(52f)))
                {
                    MissionAuthoringAssetUtility.RemoveReferenceAt(owner, propertyName, index);
                    SetExpanded(asset, false);

                    if (selectedStage == asset as MissionStageDefinition)
                    {
                        selectedStage = null;
                    }

                    GUIUtility.ExitGUI();
                }

                bool canConvertToOwnedSubAsset = asset != null &&
                                                missionDefinition != null &&
                                                !MissionAuthoringAssetUtility.IsOwnedSubAsset(missionDefinition, asset) &&
                                                AssetDatabase.Contains(asset);
                using (new EditorGUI.DisabledScope(!canConvertToOwnedSubAsset))
                {
                    if (GUILayout.Button("Convert", GUILayout.Width(58f)))
                    {
                        if (EditorUtility.DisplayDialog(
                            "Convert To Sub-Asset",
                            $"Convert '{GetAssetDisplayName(asset)}' into a sub-asset of this mission? The original asset file will be kept.",
                            "Convert",
                            "Cancel"))
                        {
                            ScriptableObject convertedAsset = MissionAuthoringAssetUtility.ConvertToOwnedSubAsset(missionDefinition, asset);
                            if (convertedAsset != null &&
                                MissionAuthoringAssetUtility.ReplaceReferenceAt(owner, propertyName, index, convertedAsset))
                            {
                                SetExpanded(asset, false);
                                SetExpanded(convertedAsset, true);
                                if (convertedAsset is MissionStageDefinition convertedStage)
                                {
                                    selectedStage = convertedStage;
                                }

                                GUIUtility.ExitGUI();
                            }
                        }
                    }
                }

                bool canDeleteOwnedAsset = isOwnedSubAsset && ownedReferenceCount <= 1;
                using (new EditorGUI.DisabledScope(!canDeleteOwnedAsset))
                {
                    if (GUILayout.Button("Delete", GUILayout.Width(52f)))
                    {
                        if (EditorUtility.DisplayDialog(
                            "Delete Mission Node",
                            $"Delete '{GetAssetDisplayName(asset)}' from this mission? This removes the sub-asset permanently.",
                            "Delete",
                            "Cancel"))
                        {
                            MissionAuthoringAssetUtility.RemoveReferenceAt(owner, propertyName, index);
                            MissionAuthoringAssetUtility.DeleteOwnedSubAsset(missionDefinition, asset);
                            SetExpanded(asset, false);

                            if (selectedStage == asset as MissionStageDefinition)
                            {
                                selectedStage = null;
                            }

                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }

            if (asset == null)
            {
                EditorGUILayout.HelpBox("Reference is missing.", MessageType.Warning);
                return;
            }

            if (!IsExpanded(asset))
            {
                if (isOwnedSubAsset && ownedReferenceCount > 1)
                {
                    EditorGUILayout.HelpBox($"This sub-asset is referenced {ownedReferenceCount} times in the mission. Delete is disabled here; use Unlink or remove the other references first.", MessageType.None);
                }

                return;
            }

            if (asset is MissionStageDefinition stage)
            {
                selectedStage = stage;
                DrawStageDetails(stage);
                return;
            }

            DrawScriptableObjectFields(asset);
            DrawOwnedSubAssetHint(asset);

            if (isOwnedSubAsset && ownedReferenceCount > 1)
            {
                EditorGUILayout.HelpBox($"This sub-asset is referenced {ownedReferenceCount} times in the mission. Delete is disabled here; use Unlink or remove the other references first.", MessageType.None);
            }
        }
    }

    private void DrawStageDetails(MissionStageDefinition stage)
    {
        if (stage == null)
        {
            return;
        }

        DrawScriptableObjectFields(stage, "objectives", "onStageStartedActions", "onStageCompletedActions");

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create Objective From Selected Object"))
            {
                selectedStage = stage;
                TryCreateObjectiveFromSelection();
            }

            if (GUILayout.Button("Create Start Action From Selected"))
            {
                selectedStage = stage;
                TryShowCreateActionMenuFromSelection("onStageStartedActions");
            }

            if (GUILayout.Button("Create Complete Action From Selected"))
            {
                selectedStage = stage;
                TryShowCreateActionMenuFromSelection("onStageCompletedActions");
            }
        }

        EditorGUILayout.Space();
        SerializedObject serializedStage = new SerializedObject(stage);
        serializedStage.Update();
        DrawReferenceList<MissionObjectiveDefinition>(
            "Objectives",
            serializedStage,
            "objectives",
            "This stage has no objectives.");
        EditorGUILayout.Space();
        DrawReferenceList<MissionActionDefinition>(
            "On Stage Started Actions",
            serializedStage,
            "onStageStartedActions",
            "No start actions assigned.");
        EditorGUILayout.Space();
        DrawReferenceList<MissionActionDefinition>(
            "On Stage Completed Actions",
            serializedStage,
            "onStageCompletedActions",
            "No completion actions assigned.");
        serializedStage.ApplyModifiedProperties();
        DrawOwnedSubAssetHint(stage);
    }

    private void DrawScriptableObjectFields(ScriptableObject asset, params string[] excludedProperties)
    {
        if (asset == null)
        {
            return;
        }

        SerializedObject serializedAsset = new SerializedObject(asset);
        serializedAsset.Update();
        SerializedProperty iterator = serializedAsset.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (ShouldSkipProperty(iterator, excludedProperties))
            {
                continue;
            }

            if (iterator.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }

                continue;
            }

            if (TryDrawSpecialProperty(iterator))
            {
                continue;
            }

            EditorGUILayout.PropertyField(iterator, true);
        }

        serializedAsset.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);
    }

    private bool TryDrawSpecialProperty(SerializedProperty property)
    {
        if (property == null || property.propertyType != SerializedPropertyType.String)
        {
            return false;
        }

        if (property.name == "targetSignalKey")
        {
            DrawKeyPicker(property, "Target Signal Key", MissionAuthoringKeyProvider.CollectSignalKeys(sceneScan), "No scene signal keys found.");
            return true;
        }

        if (property.name == "targetKey")
        {
            DrawKeyPicker(property, "Target Key", MissionAuthoringKeyProvider.CollectRegistryKeys(sceneScan), "No registry keys found.");
            return true;
        }

        return false;
    }

    private void DrawKeyPicker(SerializedProperty property, string label, List<string> availableKeys, string emptyMessage)
    {
        using (new EditorGUILayout.VerticalScope())
        {
            string searchKey = BuildPropertySearchKey(property);
            string searchTerm = GetSearchTerm(searchKey);
            string nextSearchTerm = EditorGUILayout.TextField($"{label} Search", searchTerm);
            if (!string.Equals(nextSearchTerm, searchTerm, StringComparison.Ordinal))
            {
                keyPickerSearchTerms[searchKey] = nextSearchTerm;
                searchTerm = nextSearchTerm;
            }

            List<string> filteredKeys = FilterKeys(availableKeys, searchTerm);
            string[] options = BuildKeyOptions(filteredKeys);
            int selectedIndex = FindKeyIndex(filteredKeys, property.stringValue);
            int popupIndex = EditorGUILayout.Popup(label, selectedIndex >= 0 ? selectedIndex + 1 : 0, options);
            if (popupIndex > 0 && popupIndex - 1 < filteredKeys.Count)
            {
                property.stringValue = filteredKeys[popupIndex - 1];
            }

            string manualValue = EditorGUILayout.TextField("Manual Value", property.stringValue);
            if (!string.Equals(manualValue, property.stringValue, StringComparison.Ordinal))
            {
                property.stringValue = manualValue;
            }

            if (availableKeys.Count == 0)
            {
                EditorGUILayout.HelpBox(emptyMessage, MessageType.Info);
                return;
            }

            if (filteredKeys.Count == 0 && !string.IsNullOrWhiteSpace(searchTerm))
            {
                EditorGUILayout.HelpBox($"No keys match search '{searchTerm}'.", MessageType.Info);
            }

            if (!string.IsNullOrWhiteSpace(property.stringValue) && FindKeyIndex(availableKeys, property.stringValue) < 0)
            {
                EditorGUILayout.HelpBox($"Current value '{property.stringValue}' was not found in the active scene scan.", MessageType.Warning);
            }
        }
    }

    private void DrawSceneScanTab()
    {
        if (sceneScan == null)
        {
            RefreshSceneScan();
        }

        sceneScanScroll = EditorGUILayout.BeginScrollView(sceneScanScroll);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            string sceneName = sceneScan != null && sceneScan.Scene.IsValid() ? sceneScan.Scene.name : "(No Active Scene)";
            EditorGUILayout.LabelField("Scene Scan", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Active Scene", sceneName);
            if (sceneScan == null || !sceneScan.Scene.IsValid())
            {
                EditorGUILayout.HelpBox("Open a scene to scan mission relays and registry keys.", MessageType.Info);
            }
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Mission Coverage", EditorStyles.boldLabel);
            DrawMissionCoverage();
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Signal Emitters", EditorStyles.boldLabel);
            signalEmitterFilter = EditorGUILayout.TextField("Filter", signalEmitterFilter);
            DrawSignalEmitterTable();
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Scene Registry", EditorStyles.boldLabel);
            registryEntryFilter = EditorGUILayout.TextField("Filter", registryEntryFilter);
            DrawRegistryTable();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawMissionCoverage()
    {
        Dictionary<string, List<KeyUsageReference>> signalUsageMap = CollectMissionKeyUsageMap("targetSignalKey");
        Dictionary<string, List<KeyUsageReference>> registryUsageMap = CollectMissionKeyUsageMap("targetKey");
        Dictionary<string, List<UnityEngine.Object>> signalSceneMap = BuildSignalSceneContextMap();
        Dictionary<string, List<UnityEngine.Object>> registrySceneMap = BuildRegistrySceneContextMap();

        if (signalUsageMap.Count == 0 && registryUsageMap.Count == 0)
        {
            EditorGUILayout.HelpBox("Mission does not currently reference signal keys or registry keys.", MessageType.Info);
            return;
        }

        DrawKeyCoverageSection("Signal Key Coverage", signalUsageMap, signalSceneMap, "No signal keys referenced by the mission.");
        EditorGUILayout.Space();
        DrawKeyCoverageSection("Registry Key Coverage", registryUsageMap, registrySceneMap, "No registry keys referenced by the mission.");
    }

    private void DrawSignalEmitterTable()
    {
        if (sceneScan == null || sceneScan.SignalEmitters.Count == 0)
        {
            EditorGUILayout.HelpBox("No MissionSignalSource or mission relay components found in the active scene.", MessageType.None);
            return;
        }

        Dictionary<string, int> duplicateCounts = BuildSignalDuplicateCountMap(sceneScan.SignalEmitters);
        for (int i = 0; i < sceneScan.SignalEmitters.Count; i++)
        {
            MissionAuthoringSceneScanUtility.SignalEmitterEntry entry = sceneScan.SignalEmitters[i];
            if (!MatchesFilter(entry.Key, signalEmitterFilter) && !MatchesFilter(entry.EmitterType, signalEmitterFilter))
            {
                continue;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                string keyLabel = string.IsNullOrWhiteSpace(entry.Key) ? "(empty key)" : entry.Key;
                EditorGUILayout.LabelField(keyLabel, GUILayout.MinWidth(220f));
                EditorGUILayout.LabelField(entry.EmitterType, GUILayout.Width(130f));

                if (!string.IsNullOrWhiteSpace(entry.Key) && duplicateCounts.TryGetValue(entry.Key, out int count) && count > 1)
                {
                    EditorGUILayout.LabelField("Duplicate", EditorStyles.miniBoldLabel, GUILayout.Width(65f));
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Ping", GUILayout.Width(48f)))
                {
                    EditorGUIUtility.PingObject(entry.Component);
                    Selection.activeObject = entry.Component;
                }
            }
        }
    }

    private void DrawRegistryTable()
    {
        if (sceneScan == null || sceneScan.Registry == null)
        {
            EditorGUILayout.HelpBox("No MissionSceneObjectRegistry found in the active scene.", MessageType.Warning);
            return;
        }

        EditorGUILayout.ObjectField("Registry", sceneScan.Registry, typeof(MissionSceneObjectRegistry), true);
        if (sceneScan.RegistryEntries.Count == 0)
        {
            EditorGUILayout.HelpBox("Registry has no entries.", MessageType.None);
            return;
        }

        Dictionary<string, int> duplicateCounts = BuildRegistryDuplicateCountMap(sceneScan.RegistryEntries);
        for (int i = 0; i < sceneScan.RegistryEntries.Count; i++)
        {
            MissionAuthoringSceneScanUtility.RegistryEntry entry = sceneScan.RegistryEntries[i];
            string objectName = entry.TargetObject != null ? entry.TargetObject.name : string.Empty;
            if (!MatchesFilter(entry.Key, registryEntryFilter) && !MatchesFilter(objectName, registryEntryFilter))
            {
                continue;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                string keyLabel = string.IsNullOrWhiteSpace(entry.Key) ? "(empty key)" : entry.Key;
                EditorGUILayout.LabelField(keyLabel, GUILayout.MinWidth(220f));
                EditorGUILayout.ObjectField(entry.TargetObject, typeof(GameObject), true);

                if (!string.IsNullOrWhiteSpace(entry.Key) && duplicateCounts.TryGetValue(entry.Key, out int count) && count > 1)
                {
                    EditorGUILayout.LabelField("Duplicate", EditorStyles.miniBoldLabel, GUILayout.Width(65f));
                }
            }
        }
    }

    private void ShowCreateMenu<TBase>(SerializedObject owner, string propertyName) where TBase : ScriptableObject
    {
        List<Type> types = MissionAuthoringAssetUtility.GetConcreteDerivedTypes<TBase>();
        if (types.Count == 0)
        {
            return;
        }

        UnityEngine.Object ownerTarget = owner.targetObject;
        GenericMenu menu = new GenericMenu();
        for (int i = 0; i < types.Count; i++)
        {
            Type assetType = types[i];
            menu.AddItem(new GUIContent(assetType.Name), false, () =>
            {
                ScriptableObject createdAsset = MissionAuthoringAssetUtility.CreateMissionChildAsset(missionDefinition, assetType, GetChildNameHint());
                if (createdAsset == null)
                {
                    return;
                }

                MissionAuthoringAssetUtility.AppendReference(new SerializedObject(ownerTarget), propertyName, createdAsset);
                SetExpanded(createdAsset, true);
                if (createdAsset is MissionStageDefinition createdStage)
                {
                    selectedStage = createdStage;
                }

                RefreshSceneScan();
                Repaint();
            });
        }

        menu.ShowAsContext();
    }

    private void ShowRelinkExistingMenu<TBase>(SerializedObject owner, string propertyName) where TBase : ScriptableObject
    {
        List<TBase> candidates = GetRelinkCandidates<TBase>(owner, propertyName);
        if (candidates.Count == 0)
        {
            ShowNotification(new GUIContent("No unlinked sub-assets available."));
            return;
        }

        UnityEngine.Object ownerTarget = owner.targetObject;
        GenericMenu menu = new GenericMenu();
        for (int i = 0; i < candidates.Count; i++)
        {
            TBase candidate = candidates[i];
            string label = GetAssetDisplayName(candidate);
            menu.AddItem(new GUIContent(label), false, () =>
            {
                MissionAuthoringAssetUtility.AppendReference(new SerializedObject(ownerTarget), propertyName, candidate);
                SetExpanded(candidate, true);
                if (candidate is MissionStageDefinition stage)
                {
                    selectedStage = stage;
                }

                Repaint();
            });
        }

        menu.ShowAsContext();
    }

    private void TryAddSelectedObjectToRegistry()
    {
        if (sceneScan == null)
        {
            RefreshSceneScan();
        }

        if (sceneScan == null || sceneScan.Registry == null)
        {
            ShowNotification(new GUIContent("No scene registry found."));
            return;
        }

        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject == null)
        {
            ShowNotification(new GUIContent("Select a scene object first."));
            return;
        }

        string key = BuildUniqueRegistryKey(selectedObject);
        SerializedObject serializedRegistry = new SerializedObject(sceneScan.Registry);
        serializedRegistry.Update();
        SerializedProperty entries = serializedRegistry.FindProperty("entries");
        if (entries == null || !entries.isArray)
        {
            ShowNotification(new GUIContent("Registry entries property not found."));
            return;
        }

        int index = entries.arraySize;
        entries.InsertArrayElementAtIndex(index);
        SerializedProperty entry = entries.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("key").stringValue = key;
        entry.FindPropertyRelative("targetObject").objectReferenceValue = selectedObject;
        serializedRegistry.ApplyModifiedProperties();
        EditorUtility.SetDirty(sceneScan.Registry);
        RefreshSceneScan();
        ShowNotification(new GUIContent($"Added registry key '{key}'."));
    }

    private void TryCreateObjectiveFromSelection()
    {
        if (missionDefinition == null)
        {
            return;
        }

        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject == null)
        {
            ShowNotification(new GUIContent("Select a scene object first."));
            return;
        }

        MissionStageDefinition objectiveStage = ResolveObjectiveStageTarget();
        if (MissionHasStages() && objectiveStage == null)
        {
            ShowNotification(new GUIContent("Select a stage first."));
            return;
        }

        MissionObjectiveDefinition createdObjective = CreateObjectiveFromSelectedObject(selectedObject, objectiveStage);
        if (createdObjective == null)
        {
            ShowNotification(new GUIContent("Unsupported selection for mission objective creation."));
            return;
        }

        SetExpanded(createdObjective, true);
        RefreshSceneScan();
        Repaint();
    }

    private void TryShowCreateActionMenuFromSelection(string defaultPropertyName)
    {
        if (missionDefinition == null)
        {
            return;
        }

        if (selectedStage == null)
        {
            ShowNotification(new GUIContent("Select a stage first."));
            return;
        }

        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject == null)
        {
            ShowNotification(new GUIContent("Select a scene object first."));
            return;
        }

        GenericMenu menu = new GenericMenu();
        AddActionMenuItem<ActivateMissionObjectAction>(menu, "Activate Object", selectedObject, defaultPropertyName);
        AddActionMenuItem<DeactivateMissionObjectAction>(menu, "Deactivate Object", selectedObject, defaultPropertyName);
        AddActionMenuItem<ToggleMissionObjectAction>(menu, "Toggle Object", selectedObject, defaultPropertyName);
        if (selectedObject.GetComponent<Explosive>() != null || selectedObject.GetComponentInParent<Explosive>() != null)
        {
            AddActionMenuItem<ActivateExplosiveMissionAction>(menu, "Activate Explosive", selectedObject, defaultPropertyName);
        }

        menu.ShowAsContext();
    }

    private void AddActionMenuItem<TAction>(GenericMenu menu, string label, GameObject selectedObject, string propertyName)
        where TAction : MissionActionDefinition
    {
        menu.AddItem(new GUIContent(label), false, () =>
        {
            if (!TryCreateActionForSelection<TAction>(selectedObject, propertyName, out MissionActionDefinition createdAction))
            {
                ShowNotification(new GUIContent("Could not create action for selected object."));
                return;
            }

            SetExpanded(createdAction, true);
            Repaint();
        });
    }

    private bool TryCreateActionForSelection<TAction>(GameObject selectedObject, string propertyName, out MissionActionDefinition createdAction)
        where TAction : MissionActionDefinition
    {
        createdAction = null;
        if (selectedObject == null || selectedStage == null)
        {
            return false;
        }

        MissionSceneObjectRegistry registry = ResolveOrCreateSceneRegistry();
        if (registry == null)
        {
            ShowNotification(new GUIContent("No MissionSceneObjectRegistry found or created."));
            return false;
        }

        string targetKey = EnsureRegistryEntry(registry, selectedObject);
        if (string.IsNullOrWhiteSpace(targetKey))
        {
            return false;
        }

        ScriptableObject createdAsset = MissionAuthoringAssetUtility.CreateMissionChildAsset(missionDefinition, typeof(TAction), selectedObject.name);
        if (createdAsset is not MissionActionDefinition action)
        {
            return false;
        }

        SetStringProperty(action, "targetKey", targetKey);
        SerializedObject stageObject = new SerializedObject(selectedStage);
        MissionAuthoringAssetUtility.AppendReference(stageObject, ResolveActionPropertyName(propertyName), action);
        RefreshSceneScan();
        createdAction = action;
        return true;
    }

    private MissionObjectiveDefinition CreateObjectiveFromSelectedObject(GameObject selectedObject, MissionStageDefinition objectiveStage)
    {
        if (selectedObject == null)
        {
            return null;
        }

        if (TryCreateBreakObjective(selectedObject, objectiveStage, out MissionObjectiveDefinition breakObjective))
        {
            return breakObjective;
        }

        if (TryCreateRescueDeliveryObjective(selectedObject, objectiveStage, out MissionObjectiveDefinition rescueObjective))
        {
            return rescueObjective;
        }

        if (TryCreateReachAreaObjective(selectedObject, objectiveStage, out MissionObjectiveDefinition reachObjective))
        {
            return reachObjective;
        }

        if (TryCreateInteractObjective(selectedObject, objectiveStage, out MissionObjectiveDefinition interactObjective))
        {
            return interactObjective;
        }

        return null;
    }

    private bool TryCreateBreakObjective(GameObject selectedObject, MissionStageDefinition objectiveStage, out MissionObjectiveDefinition createdObjective)
    {
        createdObjective = null;
        Breakable breakable = selectedObject.GetComponent<Breakable>() ?? selectedObject.GetComponentInParent<Breakable>();
        if (breakable == null)
        {
            return false;
        }

        MissionBreakableSignalRelay relay = breakable.GetComponent<MissionBreakableSignalRelay>();
        if (relay == null)
        {
            relay = Undo.AddComponent<MissionBreakableSignalRelay>(breakable.gameObject);
        }

        string signalKey = EnsureSignalKey(relay, "signalKey", "break-target", breakable.gameObject.name, objectiveStage);
        SetObjectReference(relay, "breakable", breakable);
        createdObjective = CreateSignalObjective<BreakTargetObjectiveDefinition>(objectiveStage, signalKey, breakable.gameObject.name);
        return createdObjective != null;
    }

    private bool TryCreateRescueDeliveryObjective(GameObject selectedObject, MissionStageDefinition objectiveStage, out MissionObjectiveDefinition createdObjective)
    {
        createdObjective = null;
        Rescuable rescuable = selectedObject.GetComponent<Rescuable>() ?? selectedObject.GetComponentInParent<Rescuable>();
        if (rescuable == null)
        {
            return false;
        }

        MissionRescueDeliverySignalRelay relay = rescuable.GetComponent<MissionRescueDeliverySignalRelay>();
        if (relay == null)
        {
            relay = Undo.AddComponent<MissionRescueDeliverySignalRelay>(rescuable.gameObject);
        }

        SafeZone safeZone = FindSceneSafeZone(rescuable.gameObject);
        SetObjectReference(relay, "rescuable", rescuable);
        if (safeZone != null)
        {
            SetObjectReference(relay, "safeZone", safeZone);
        }

        string signalKey = EnsureSignalKey(relay, "signalKey", "deliver-target", rescuable.gameObject.name, objectiveStage);
        createdObjective = CreateSignalObjective<DeliverTargetToZoneObjectiveDefinition>(objectiveStage, signalKey, rescuable.gameObject.name);
        if (safeZone == null)
        {
            ShowNotification(new GUIContent("Objective created. Assign SafeZone on the relay."));
        }

        return createdObjective != null;
    }

    private bool TryCreateReachAreaObjective(GameObject selectedObject, MissionStageDefinition objectiveStage, out MissionObjectiveDefinition createdObjective)
    {
        createdObjective = null;
        GameObject hostObject = selectedObject;
        MissionSignalSource signalSource = selectedObject.GetComponent<MissionSignalSource>();
        Collider triggerCollider = selectedObject.GetComponent<Collider>();

        if (signalSource == null && triggerCollider == null)
        {
            Collider childCollider = selectedObject.GetComponentInChildren<Collider>();
            if (childCollider != null)
            {
                hostObject = childCollider.gameObject;
                triggerCollider = childCollider;
                signalSource = hostObject.GetComponent<MissionSignalSource>();
            }
        }

        if (signalSource == null && (triggerCollider == null || !triggerCollider.isTrigger))
        {
            return false;
        }

        if (signalSource == null)
        {
            signalSource = Undo.AddComponent<MissionSignalSource>(hostObject);
        }

        string signalKey = EnsureSignalKey(signalSource, "signalKey", "signal", hostObject.name, objectiveStage);
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            SetObjectReference(signalSource, "triggerZone", triggerCollider);
        }

        createdObjective = CreateSignalObjective<ReachAreaObjectiveDefinition>(objectiveStage, signalKey, hostObject.name);
        return createdObjective != null;
    }

    private bool TryCreateInteractObjective(GameObject selectedObject, MissionStageDefinition objectiveStage, out MissionObjectiveDefinition createdObjective)
    {
        createdObjective = null;
        GameObject hostObject = selectedObject;
        Component interactable = selectedObject.GetComponent(typeof(IInteractable)) ?? selectedObject.GetComponentInParent(typeof(IInteractable));
        if (interactable == null)
        {
            return false;
        }

        hostObject = interactable.gameObject;
        MissionInteractionSignalRelay relay = hostObject.GetComponent<MissionInteractionSignalRelay>();
        if (relay == null)
        {
            relay = Undo.AddComponent<MissionInteractionSignalRelay>(hostObject);
        }

        string signalKey = EnsureSignalKey(relay, "signalKey", "interact-target", hostObject.name, objectiveStage);
        createdObjective = CreateSignalObjective<InteractTargetObjectiveDefinition>(objectiveStage, signalKey, hostObject.name);
        return createdObjective != null;
    }

    private TObjective CreateSignalObjective<TObjective>(MissionStageDefinition objectiveStage, string signalKey, string nameHint)
        where TObjective : MissionObjectiveDefinition
    {
        ScriptableObject createdAsset = MissionAuthoringAssetUtility.CreateMissionChildAsset(missionDefinition, typeof(TObjective), nameHint);
        if (createdAsset is not TObjective objective)
        {
            return null;
        }

        SetStringProperty(objective, "targetSignalKey", signalKey);
        AppendObjectiveReference(objectiveStage, objective);
        return objective;
    }

    private void AppendObjectiveReference(MissionStageDefinition objectiveStage, MissionObjectiveDefinition objective)
    {
        SerializedObject owner = objectiveStage != null
            ? new SerializedObject(objectiveStage)
            : new SerializedObject(missionDefinition);
        MissionAuthoringAssetUtility.AppendReference(owner, "objectives", objective);
        if (objectiveStage != null)
        {
            selectedStage = objectiveStage;
        }
    }

    private string EnsureSignalKey(Component component, string propertyName, string defaultValue, string objectName, MissionStageDefinition objectiveStage)
    {
        SerializedObject serializedComponent = new SerializedObject(component);
        serializedComponent.Update();
        SerializedProperty keyProperty = serializedComponent.FindProperty(propertyName);
        string currentValue = keyProperty?.stringValue?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(currentValue) || string.Equals(currentValue, defaultValue, StringComparison.OrdinalIgnoreCase))
        {
            currentValue = BuildUniqueSignalKey(objectName, objectiveStage);
            if (keyProperty != null)
            {
                keyProperty.stringValue = currentValue;
            }
        }

        serializedComponent.ApplyModifiedProperties();
        EditorUtility.SetDirty(component);
        return currentValue;
    }

    private void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        if (target == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
    }

    private void SetStringProperty(UnityEngine.Object target, string propertyName, string value)
    {
        if (target == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
    }

    private void TryConvertAllLinkedNodesToSubAssets()
    {
        if (missionDefinition == null)
        {
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Convert All Linked Nodes",
                "Convert every mission node reference reachable from this mission into sub-assets under the mission where possible? Existing external assets will be kept.",
                "Convert All",
                "Cancel"))
        {
            return;
        }

        MissionAuthoringAssetUtility.ConversionSummary summary = MissionAuthoringAssetUtility.ConvertAllLinkedNodesToSubAssets(missionDefinition);
        expandedAssets.Clear();
        selectedStage = null;
        RefreshSceneScan();
        Repaint();
        ShowNotification(new GUIContent($"Converted {summary.ConvertedAssetCount} asset(s), rewrote {summary.RewrittenReferenceCount} reference(s)."));
    }

    private SafeZone FindSceneSafeZone(GameObject contextObject)
    {
        SafeZone[] safeZones = FindObjectsByType<SafeZone>(FindObjectsInactive.Include);
        for (int i = 0; i < safeZones.Length; i++)
        {
            SafeZone safeZone = safeZones[i];
            if (safeZone != null && contextObject != null && safeZone.gameObject.scene == contextObject.scene)
            {
                return safeZone;
            }
        }

        return null;
    }

    private MissionSceneObjectRegistry ResolveOrCreateSceneRegistry()
    {
        if (sceneScan != null && sceneScan.Registry != null)
        {
            return sceneScan.Registry;
        }

        IncidentMissionSystem[] missionSystems = FindObjectsByType<IncidentMissionSystem>(FindObjectsInactive.Include);
        for (int i = 0; i < missionSystems.Length; i++)
        {
            IncidentMissionSystem missionSystem = missionSystems[i];
            if (missionSystem == null || !missionSystem.gameObject.scene.IsValid())
            {
                continue;
            }

            MissionSceneObjectRegistry registry = missionSystem.GetComponent<MissionSceneObjectRegistry>();
            if (registry == null)
            {
                registry = Undo.AddComponent<MissionSceneObjectRegistry>(missionSystem.gameObject);
            }

            RefreshSceneScan();
            return registry;
        }

        return null;
    }

    private string EnsureRegistryEntry(MissionSceneObjectRegistry registry, GameObject selectedObject)
    {
        if (registry == null || selectedObject == null)
        {
            return null;
        }

        SerializedObject serializedRegistry = new SerializedObject(registry);
        serializedRegistry.Update();
        SerializedProperty entries = serializedRegistry.FindProperty("entries");
        if (entries == null || !entries.isArray)
        {
            return null;
        }

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            GameObject targetObject = entry.FindPropertyRelative("targetObject")?.objectReferenceValue as GameObject;
            string key = entry.FindPropertyRelative("key")?.stringValue?.Trim();
            if (targetObject == selectedObject && !string.IsNullOrWhiteSpace(key))
            {
                return key;
            }
        }

        string createdKey = BuildUniqueRegistryKey(selectedObject);
        int index = entries.arraySize;
        entries.InsertArrayElementAtIndex(index);
        SerializedProperty createdEntry = entries.GetArrayElementAtIndex(index);
        createdEntry.FindPropertyRelative("key").stringValue = createdKey;
        createdEntry.FindPropertyRelative("targetObject").objectReferenceValue = selectedObject;
        serializedRegistry.ApplyModifiedProperties();
        EditorUtility.SetDirty(registry);
        RefreshSceneScan();
        return createdKey;
    }

    private static string ResolveActionPropertyName(string propertyName)
    {
        return string.Equals(propertyName, "onStageStartedActions", StringComparison.Ordinal)
            ? "onStageStartedActions"
            : "onStageCompletedActions";
    }

    private void DrawOwnedSubAssetHint(ScriptableObject asset)
    {
        if (asset == null || !MissionAuthoringAssetUtility.IsOwnedSubAsset(missionDefinition, asset))
        {
            return;
        }

        EditorGUILayout.HelpBox("This node is stored as a sub-asset under the mission asset.", MessageType.None);
    }

    private List<string> CollectMissingMissionSignalKeys()
    {
        List<string> missingKeys = new List<string>();
        List<string> sceneKeys = MissionAuthoringKeyProvider.CollectSignalKeys(sceneScan);
        List<string> missionKeys = CollectMissionStringProperties("targetSignalKey");
        for (int i = 0; i < missionKeys.Count; i++)
        {
            string missionKey = missionKeys[i];
            if (FindKeyIndex(sceneKeys, missionKey) < 0)
            {
                AppendUnique(missingKeys, missionKey);
            }
        }

        return missingKeys;
    }

    private List<string> CollectMissingMissionTargetKeys()
    {
        List<string> missingKeys = new List<string>();
        List<string> sceneKeys = MissionAuthoringKeyProvider.CollectRegistryKeys(sceneScan);
        List<string> missionKeys = CollectMissionStringProperties("targetKey");
        for (int i = 0; i < missionKeys.Count; i++)
        {
            string missionKey = missionKeys[i];
            if (FindKeyIndex(sceneKeys, missionKey) < 0)
            {
                AppendUnique(missingKeys, missionKey);
            }
        }

        return missingKeys;
    }

    private List<string> CollectMissionStringProperties(string propertyName)
    {
        List<string> values = new List<string>();
        if (missionDefinition == null || string.IsNullOrWhiteSpace(propertyName))
        {
            return values;
        }

        CollectStringPropertyValuesFromList(new SerializedObject(missionDefinition).FindProperty("objectives"), propertyName, values);
        CollectStringPropertyValuesFromList(new SerializedObject(missionDefinition).FindProperty("failConditions"), propertyName, values);

        SerializedProperty stages = new SerializedObject(missionDefinition).FindProperty("stages");
        if (stages != null)
        {
            for (int i = 0; i < stages.arraySize; i++)
            {
                MissionStageDefinition stage = stages.GetArrayElementAtIndex(i).objectReferenceValue as MissionStageDefinition;
                if (stage == null)
                {
                    continue;
                }

                SerializedObject serializedStage = new SerializedObject(stage);
                CollectStringPropertyValuesFromList(serializedStage.FindProperty("objectives"), propertyName, values);
                CollectStringPropertyValuesFromList(serializedStage.FindProperty("onStageStartedActions"), propertyName, values);
                CollectStringPropertyValuesFromList(serializedStage.FindProperty("onStageCompletedActions"), propertyName, values);
            }
        }

        return values;
    }

    private void CollectStringPropertyValuesFromList(SerializedProperty listProperty, string propertyName, List<string> results)
    {
        if (listProperty == null || results == null)
        {
            return;
        }

        for (int i = 0; i < listProperty.arraySize; i++)
        {
            ScriptableObject asset = listProperty.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableObject;
            if (asset == null)
            {
                continue;
            }

            SerializedObject serializedAsset = new SerializedObject(asset);
            SerializedProperty stringProperty = serializedAsset.FindProperty(propertyName);
            string value = stringProperty?.stringValue?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(value))
            {
                AppendUnique(results, value);
            }
        }
    }

    private Dictionary<string, int> BuildSignalDuplicateCountMap(IList<MissionAuthoringSceneScanUtility.SignalEmitterEntry> entries)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < entries.Count; i++)
        {
            string key = entries[i].Key;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
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

        return counts;
    }

    private Dictionary<string, int> BuildRegistryDuplicateCountMap(IList<MissionAuthoringSceneScanUtility.RegistryEntry> entries)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < entries.Count; i++)
        {
            string key = entries[i].Key;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
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

        return counts;
    }

    private void DrawKeyCoverageSection(string title, Dictionary<string, List<KeyUsageReference>> usageMap, Dictionary<string, List<UnityEngine.Object>> sceneMap, string emptyMessage)
    {
        EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
        if (usageMap == null || usageMap.Count == 0)
        {
            EditorGUILayout.HelpBox(emptyMessage, MessageType.None);
            return;
        }

        List<string> keys = new List<string>(usageMap.Keys);
        keys.Sort(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            usageMap.TryGetValue(key, out List<KeyUsageReference> usageReferences);
            sceneMap.TryGetValue(key, out List<UnityEngine.Object> sceneContexts);
            int sceneCount = sceneContexts != null ? sceneContexts.Count : 0;
            string status = sceneCount == 0 ? "Missing" : sceneCount > 1 ? "Duplicate" : "OK";
            MessageType messageType = sceneCount == 0 || sceneCount > 1 ? MessageType.Warning : MessageType.None;

            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(key, GUILayout.MinWidth(220f));
                    EditorGUILayout.LabelField(status, GUILayout.Width(70f));
                    EditorGUILayout.LabelField($"{usageReferences.Count} use(s)", GUILayout.Width(80f));
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(sceneContexts == null || sceneContexts.Count == 0))
                    {
                        if (GUILayout.Button("Ping Scene", GUILayout.Width(72f)))
                        {
                            PingObject(sceneContexts[0]);
                        }
                    }

                    using (new EditorGUI.DisabledScope(usageReferences == null || usageReferences.Count == 0 || usageReferences[0].Context == null))
                    {
                        if (GUILayout.Button("Ping Asset", GUILayout.Width(72f)))
                        {
                            PingObject(usageReferences[0].Context);
                        }
                    }
                }

                if (messageType != MessageType.None)
                {
                    string sceneSummary = sceneCount == 0
                        ? "No matching scene object found."
                        : $"Found {sceneCount} matching scene objects.";
                    EditorGUILayout.HelpBox(sceneSummary, messageType);
                }

                EditorGUILayout.LabelField("Used By", JoinUsageLabels(usageReferences), EditorStyles.wordWrappedMiniLabel);
                if (sceneContexts != null && sceneContexts.Count > 0)
                {
                    EditorGUILayout.LabelField("Scene Matches", JoinObjectNames(sceneContexts), EditorStyles.wordWrappedMiniLabel);
                }
            }
        }
    }

    private void DrawCoverageList(string label, List<string> values, MessageType messageType)
    {
        if (values == null || values.Count == 0)
        {
            return;
        }

        string prefix = label.EndsWith(":", StringComparison.Ordinal) ? label : $"{label}:";
        EditorGUILayout.HelpBox($"{prefix} {string.Join(", ", values)}", messageType);
    }

    private List<string> CollectDuplicateKeys(IList<MissionAuthoringSceneScanUtility.SignalEmitterEntry> entries)
    {
        List<string> duplicates = new List<string>();
        Dictionary<string, int> counts = BuildSignalDuplicateCountMap(entries ?? Array.Empty<MissionAuthoringSceneScanUtility.SignalEmitterEntry>());
        foreach (KeyValuePair<string, int> pair in counts)
        {
            if (pair.Value > 1)
            {
                duplicates.Add(pair.Key);
            }
        }

        duplicates.Sort(StringComparer.OrdinalIgnoreCase);
        return duplicates;
    }

    private List<string> CollectDuplicateKeys(IList<MissionAuthoringSceneScanUtility.RegistryEntry> entries)
    {
        List<string> duplicates = new List<string>();
        Dictionary<string, int> counts = BuildRegistryDuplicateCountMap(entries ?? Array.Empty<MissionAuthoringSceneScanUtility.RegistryEntry>());
        foreach (KeyValuePair<string, int> pair in counts)
        {
            if (pair.Value > 1)
            {
                duplicates.Add(pair.Key);
            }
        }

        duplicates.Sort(StringComparer.OrdinalIgnoreCase);
        return duplicates;
    }

    private Dictionary<string, List<KeyUsageReference>> CollectMissionKeyUsageMap(string propertyName)
    {
        Dictionary<string, List<KeyUsageReference>> usages = new Dictionary<string, List<KeyUsageReference>>(StringComparer.OrdinalIgnoreCase);
        if (missionDefinition == null || string.IsNullOrWhiteSpace(propertyName))
        {
            return usages;
        }

        CollectKeyUsageFromList(new SerializedObject(missionDefinition).FindProperty("objectives"), propertyName, "Mission Objective", usages);
        CollectKeyUsageFromList(new SerializedObject(missionDefinition).FindProperty("failConditions"), propertyName, "Fail Condition", usages);

        SerializedProperty stages = new SerializedObject(missionDefinition).FindProperty("stages");
        if (stages != null)
        {
            for (int i = 0; i < stages.arraySize; i++)
            {
                MissionStageDefinition stage = stages.GetArrayElementAtIndex(i).objectReferenceValue as MissionStageDefinition;
                if (stage == null)
                {
                    continue;
                }

                SerializedObject serializedStage = new SerializedObject(stage);
                string stageLabel = string.IsNullOrWhiteSpace(stage.StageTitle) ? stage.name : stage.StageTitle;
                CollectKeyUsageFromList(serializedStage.FindProperty("objectives"), propertyName, $"Stage Objective ({stageLabel})", usages);
                CollectKeyUsageFromList(serializedStage.FindProperty("onStageStartedActions"), propertyName, $"Stage Start Action ({stageLabel})", usages);
                CollectKeyUsageFromList(serializedStage.FindProperty("onStageCompletedActions"), propertyName, $"Stage Complete Action ({stageLabel})", usages);
            }
        }

        return usages;
    }

    private void CollectKeyUsageFromList(SerializedProperty listProperty, string propertyName, string scopeLabel, Dictionary<string, List<KeyUsageReference>> usages)
    {
        if (listProperty == null || usages == null)
        {
            return;
        }

        for (int i = 0; i < listProperty.arraySize; i++)
        {
            ScriptableObject asset = listProperty.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableObject;
            if (asset == null)
            {
                continue;
            }

            SerializedObject serializedAsset = new SerializedObject(asset);
            SerializedProperty stringProperty = serializedAsset.FindProperty(propertyName);
            string value = stringProperty?.stringValue?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!usages.TryGetValue(value, out List<KeyUsageReference> usageReferences))
            {
                usageReferences = new List<KeyUsageReference>();
                usages.Add(value, usageReferences);
            }

            usageReferences.Add(new KeyUsageReference
            {
                Label = $"{scopeLabel}: {GetAssetDisplayName(asset)}",
                Context = asset
            });
        }
    }

    private Dictionary<string, List<UnityEngine.Object>> BuildSignalSceneContextMap()
    {
        Dictionary<string, List<UnityEngine.Object>> results = new Dictionary<string, List<UnityEngine.Object>>(StringComparer.OrdinalIgnoreCase);
        if (sceneScan == null)
        {
            return results;
        }

        for (int i = 0; i < sceneScan.SignalEmitters.Count; i++)
        {
            MissionAuthoringSceneScanUtility.SignalEmitterEntry entry = sceneScan.SignalEmitters[i];
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Component == null)
            {
                continue;
            }

            if (!results.TryGetValue(entry.Key, out List<UnityEngine.Object> contexts))
            {
                contexts = new List<UnityEngine.Object>();
                results.Add(entry.Key, contexts);
            }

            contexts.Add(entry.Component);
        }

        return results;
    }

    private Dictionary<string, List<UnityEngine.Object>> BuildRegistrySceneContextMap()
    {
        Dictionary<string, List<UnityEngine.Object>> results = new Dictionary<string, List<UnityEngine.Object>>(StringComparer.OrdinalIgnoreCase);
        if (sceneScan == null)
        {
            return results;
        }

        for (int i = 0; i < sceneScan.RegistryEntries.Count; i++)
        {
            MissionAuthoringSceneScanUtility.RegistryEntry entry = sceneScan.RegistryEntries[i];
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.TargetObject == null)
            {
                continue;
            }

            if (!results.TryGetValue(entry.Key, out List<UnityEngine.Object> contexts))
            {
                contexts = new List<UnityEngine.Object>();
                results.Add(entry.Key, contexts);
            }

            contexts.Add(entry.TargetObject);
        }

        return results;
    }

    private string BuildPropertySearchKey(SerializedProperty property)
    {
        if (property == null || property.serializedObject == null || property.serializedObject.targetObject == null)
        {
            return string.Empty;
        }

        GlobalObjectId globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(property.serializedObject.targetObject);
        return $"{globalObjectId}::{property.propertyPath}";
    }

    private string GetSearchTerm(string searchKey)
    {
        if (string.IsNullOrWhiteSpace(searchKey))
        {
            return string.Empty;
        }

        return keyPickerSearchTerms.TryGetValue(searchKey, out string searchTerm)
            ? searchTerm
            : string.Empty;
    }

    private static List<string> FilterKeys(List<string> keys, string searchTerm)
    {
        if (keys == null)
        {
            return new List<string>();
        }

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new List<string>(keys);
        }

        List<string> filteredKeys = new List<string>();
        for (int i = 0; i < keys.Count; i++)
        {
            if (MatchesFilter(keys[i], searchTerm))
            {
                filteredKeys.Add(keys[i]);
            }
        }

        return filteredKeys;
    }

    private static bool MatchesFilter(string value, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.IndexOf(filter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string JoinUsageLabels(List<KeyUsageReference> usageReferences)
    {
        if (usageReferences == null || usageReferences.Count == 0)
        {
            return "(none)";
        }

        List<string> labels = new List<string>();
        for (int i = 0; i < usageReferences.Count; i++)
        {
            KeyUsageReference reference = usageReferences[i];
            if (reference != null && !string.IsNullOrWhiteSpace(reference.Label))
            {
                labels.Add(reference.Label);
            }
        }

        return labels.Count == 0 ? "(none)" : string.Join(", ", labels);
    }

    private static string JoinObjectNames(List<UnityEngine.Object> contexts)
    {
        if (contexts == null || contexts.Count == 0)
        {
            return "(none)";
        }

        List<string> names = new List<string>();
        for (int i = 0; i < contexts.Count; i++)
        {
            UnityEngine.Object context = contexts[i];
            if (context != null)
            {
                names.Add(context.name);
            }
        }

        return names.Count == 0 ? "(none)" : string.Join(", ", names);
    }

    private static void PingObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        EditorGUIUtility.PingObject(target);
        Selection.activeObject = target;
    }

    private static void DrawPropertyIfExists(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, true);
        }
    }

    private static bool ShouldSkipProperty(SerializedProperty property, string[] excludedProperties)
    {
        if (property == null)
        {
            return true;
        }

        if (excludedProperties == null)
        {
            return false;
        }

        for (int i = 0; i < excludedProperties.Length; i++)
        {
            if (string.Equals(property.name, excludedProperties[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private string GetAssetDisplayName(ScriptableObject asset)
    {
        if (asset == null)
        {
            return "Missing Reference";
        }

        if (asset is MissionStageDefinition stage)
        {
            string stageTitle = string.IsNullOrWhiteSpace(stage.StageTitle) ? asset.name : stage.StageTitle;
            string stageId = string.IsNullOrWhiteSpace(stage.StageId) ? "no-stage-id" : stage.StageId;
            return $"{stageTitle} [{stageId}]";
        }

        if (asset is MissionObjectiveDefinition objective)
        {
            return string.IsNullOrWhiteSpace(objective.ObjectiveTitle) ? asset.name : objective.ObjectiveTitle;
        }

        if (asset is MissionActionDefinition action)
        {
            return string.IsNullOrWhiteSpace(action.ActionTitle) ? asset.name : action.ActionTitle;
        }

        if (asset is MissionFailConditionDefinition failCondition)
        {
            return string.IsNullOrWhiteSpace(failCondition.FailConditionTitle) ? asset.name : failCondition.FailConditionTitle;
        }

        return asset.name;
    }

    private string GetChildNameHint()
    {
        if (selectedStage != null && !string.IsNullOrWhiteSpace(selectedStage.StageId))
        {
            return selectedStage.StageId;
        }

        if (missionDefinition != null && !string.IsNullOrWhiteSpace(missionDefinition.MissionId))
        {
            return missionDefinition.MissionId;
        }

        return missionDefinition != null ? missionDefinition.name : "Mission";
    }

    private MissionStageDefinition ResolveObjectiveStageTarget()
    {
        if (selectedStage != null)
        {
            return selectedStage;
        }

        foreach (ScriptableObject expandedAsset in expandedAssets)
        {
            if (expandedAsset is MissionStageDefinition stage)
            {
                return stage;
            }
        }

        return null;
    }

    private bool MissionHasStages()
    {
        return missionDefinition != null && missionDefinition.HasStages;
    }

    private List<TBase> GetRelinkCandidates<TBase>(SerializedObject owner, string propertyName) where TBase : ScriptableObject
    {
        List<TBase> candidates = MissionAuthoringAssetUtility.GetOwnedSubAssets<TBase>(missionDefinition);
        SerializedProperty listProperty = owner.FindProperty(propertyName);
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (ListContainsReference(listProperty, candidates[i]))
            {
                candidates.RemoveAt(i);
            }
        }

        candidates.Sort((left, right) => string.Compare(GetAssetDisplayName(left), GetAssetDisplayName(right), StringComparison.OrdinalIgnoreCase));
        return candidates;
    }

    private static bool ListContainsReference(SerializedProperty listProperty, UnityEngine.Object candidate)
    {
        if (listProperty == null || !listProperty.isArray || candidate == null)
        {
            return false;
        }

        for (int i = 0; i < listProperty.arraySize; i++)
        {
            if (listProperty.GetArrayElementAtIndex(i).objectReferenceValue == candidate)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsExpanded(ScriptableObject asset)
    {
        return asset != null && expandedAssets.Contains(asset);
    }

    private void SetExpanded(ScriptableObject asset, bool expanded)
    {
        if (asset == null)
        {
            return;
        }

        if (expanded)
        {
            expandedAssets.Add(asset);
        }
        else
        {
            expandedAssets.Remove(asset);
        }
    }

    private bool CanLinkSelection<TBase>() where TBase : ScriptableObject
    {
        return Selection.activeObject is TBase;
    }

    private void SetMission(MissionDefinition mission)
    {
        missionDefinition = mission;
        expandedAssets.Clear();
        selectedStage = null;
        RefreshSceneScan();
        Repaint();
    }

    private void RefreshSceneScan()
    {
        sceneScan = MissionAuthoringSceneScanUtility.ScanActiveScene();
    }

    private void TryAssignMissionFromObject(UnityEngine.Object candidate)
    {
        if (TryResolveMission(candidate, out MissionDefinition resolvedMission))
        {
            SetMission(resolvedMission);
        }
    }

    private bool TryResolveMission(UnityEngine.Object candidate, out MissionDefinition resolvedMission)
    {
        resolvedMission = null;
        if (candidate == null)
        {
            return false;
        }

        if (candidate is MissionDefinition mission)
        {
            resolvedMission = mission;
            return true;
        }

        string assetPath = AssetDatabase.GetAssetPath(candidate);
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return false;
        }

        resolvedMission = AssetDatabase.LoadMainAssetAtPath(assetPath) as MissionDefinition;
        return resolvedMission != null;
    }

    private string BuildUniqueSignalKey(string objectName, MissionStageDefinition objectiveStage)
    {
        string missionPrefix = missionDefinition != null && !string.IsNullOrWhiteSpace(missionDefinition.MissionId)
            ? Slugify(missionDefinition.MissionId)
            : "mission";
        string body = objectiveStage != null && !string.IsNullOrWhiteSpace(objectiveStage.StageId)
            ? Slugify(objectiveStage.StageId)
            : Slugify(objectName);
        string candidate = $"{missionPrefix}-{body}";
        List<string> existingKeys = MissionAuthoringKeyProvider.CollectSignalKeys(sceneScan);
        return EnsureUniqueKey(candidate, existingKeys);
    }

    private string BuildUniqueRegistryKey(GameObject selectedObject)
    {
        string missionPrefix = missionDefinition != null && !string.IsNullOrWhiteSpace(missionDefinition.MissionId)
            ? Slugify(missionDefinition.MissionId)
            : "mission";
        string objectSlug = selectedObject != null ? Slugify(selectedObject.name) : "target";
        return EnsureUniqueKey($"{missionPrefix}-{objectSlug}", MissionAuthoringKeyProvider.CollectRegistryKeys(sceneScan));
    }

    private static string EnsureUniqueKey(string baseKey, List<string> existingKeys)
    {
        string candidate = string.IsNullOrWhiteSpace(baseKey) ? "mission-key" : baseKey.Trim();
        if (FindKeyIndex(existingKeys, candidate) < 0)
        {
            return candidate;
        }

        int suffix = 2;
        string suffixedCandidate = $"{candidate}-{suffix}";
        while (FindKeyIndex(existingKeys, suffixedCandidate) >= 0)
        {
            suffix++;
            suffixedCandidate = $"{candidate}-{suffix}";
        }

        return suffixedCandidate;
    }

    private static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "item";
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
        return string.IsNullOrWhiteSpace(slug) ? "item" : slug;
    }

    private static string[] BuildKeyOptions(List<string> keys)
    {
        string[] options = new string[(keys?.Count ?? 0) + 1];
        options[0] = "Manual";
        if (keys == null)
        {
            return options;
        }

        for (int i = 0; i < keys.Count; i++)
        {
            options[i + 1] = keys[i];
        }

        return options;
    }

    private static int FindKeyIndex(List<string> keys, string value)
    {
        if (keys == null || string.IsNullOrWhiteSpace(value))
        {
            return -1;
        }

        for (int i = 0; i < keys.Count; i++)
        {
            if (string.Equals(keys[i], value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static void AppendUnique(List<string> values, string candidate)
    {
        if (values == null || string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], candidate, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        values.Add(candidate);
    }
}
