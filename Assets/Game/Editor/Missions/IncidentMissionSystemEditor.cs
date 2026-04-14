using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(IncidentMissionSystem))]
public class IncidentMissionSystemEditor : Editor
{
    private readonly List<MissionAssetValidation.Entry> cachedEntries = new List<MissionAssetValidation.Entry>();
    private bool showLegacyConfig;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawMissionConfiguration();
        EditorGUILayout.Space();
        DrawOverlayConfiguration();
        EditorGUILayout.Space();
        DrawEventConfiguration();
        EditorGUILayout.Space();
        DrawLegacyConfiguration();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        DrawSceneTools();
        EditorGUILayout.Space();
        DrawValidationTools();
        EditorGUILayout.Space();
        DrawMissionPreview();
        EditorGUILayout.Space();
        DrawRuntimePreview();
    }

    private void DrawMissionConfiguration()
    {
        EditorGUILayout.LabelField("Mission Configuration", EditorStyles.boldLabel);
        DrawProperty("missionDefinition");
        DrawProperty("sceneObjectRegistry");

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open Mission Authoring"))
            {
                MissionAuthoringWindow.OpenWindow(GetMissionDefinition());
            }

            using (new EditorGUI.DisabledScope(GetMissionDefinition() == null))
            {
                if (GUILayout.Button("Ping Mission Asset"))
                {
                    MissionDefinition missionDefinition = GetMissionDefinition();
                    if (missionDefinition != null)
                    {
                        EditorGUIUtility.PingObject(missionDefinition);
                        Selection.activeObject = missionDefinition;
                    }
                }
            }
        }
    }

    private void DrawOverlayConfiguration()
    {
        EditorGUILayout.LabelField("Overlay", EditorStyles.boldLabel);
        DrawProperty("showMissionOverlay");
        DrawProperty("overlayOffset");
    }

    private void DrawEventConfiguration()
    {
        EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
        DrawProperty("onMissionStarted");
        DrawProperty("onMissionCompleted");
        DrawProperty("onMissionFailed");
        DrawProperty("onStageStarted");
        DrawProperty("onStageCompleted");
        DrawProperty("stageActionBindings");
    }

    private void DrawLegacyConfiguration()
    {
        bool usingMissionDefinition = GetMissionDefinition() != null;
        showLegacyConfig = EditorGUILayout.Foldout(
            showLegacyConfig || !usingMissionDefinition,
            usingMissionDefinition ? "Legacy Runtime Configuration" : "Legacy Runtime Configuration (Active)",
            true);
        if (!showLegacyConfig)
        {
            return;
        }

        if (usingMissionDefinition)
        {
            EditorGUILayout.HelpBox("MissionDefinition is assigned. These fields are kept for backward compatibility and should usually stay folded away.", MessageType.Info);
        }

        DrawProperty("missionId");
        DrawProperty("missionTitle");
        DrawProperty("missionDescription");
        DrawProperty("autoStartOnEnable");
        DrawProperty("timeLimitSeconds");
        DrawProperty("autoDiscoverFires");
        DrawProperty("autoDiscoverRescuables");
        DrawProperty("autoDiscoverVictimConditions");
        DrawProperty("requireAllFiresExtinguished");
        DrawProperty("requireAllRescuablesRescued");
        DrawProperty("failOnAnyVictimDeath");
        DrawProperty("maxAllowedVictimDeaths");
        DrawProperty("requireNoCriticalVictimsAtCompletion");
        DrawProperty("requireAllLivingVictimsStabilized");
        DrawProperty("trackedFires");
        DrawProperty("trackedRescuables");
        DrawProperty("trackedVictimConditions");
    }

    private void DrawProperty(string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, true);
        }
    }

    private void DrawSceneTools()
    {
        IncidentMissionSystem missionSystem = (IncidentMissionSystem)target;
        EditorGUILayout.LabelField("Scene Tools", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Assign Local Registry"))
            {
                AssignRegistry(missionSystem, addIfMissing: false, includeSceneSearch: false);
            }

            if (GUILayout.Button("Add Local Registry"))
            {
                AssignRegistry(missionSystem, addIfMissing: true, includeSceneSearch: false);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Find Registry In Scene"))
            {
                AssignRegistry(missionSystem, addIfMissing: false, includeSceneSearch: true);
            }
        }

        if (Application.isPlaying)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Objectives"))
                {
                    missionSystem.RefreshObjectives();
                    Repaint();
                }

                if (GUILayout.Button("Start Mission"))
                {
                    missionSystem.StartMission();
                    Repaint();
                }
            }
        }
    }

    private void DrawValidationTools()
    {
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
        if (GUILayout.Button("Validate Scene Mission Setup"))
        {
            cachedEntries.Clear();
            cachedEntries.AddRange(ValidateIncidentMissionSystem((IncidentMissionSystem)target));
            MissionAssetValidation.LogEntries($"IncidentMissionSystem '{target.name}'", cachedEntries, target);
        }

        if (cachedEntries.Count == 0)
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

    private void DrawMissionPreview()
    {
        MissionDefinition missionDefinition = GetMissionDefinition();
        EditorGUILayout.LabelField("Mission Preview", EditorStyles.boldLabel);

        if (missionDefinition == null)
        {
            EditorGUILayout.HelpBox("No MissionDefinition assigned. Component will use legacy mission fields.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Mission Id", missionDefinition.MissionId);
        EditorGUILayout.LabelField("Title", missionDefinition.MissionTitle);
        if (!string.IsNullOrWhiteSpace(missionDefinition.MissionDescription))
        {
            EditorGUILayout.HelpBox(missionDefinition.MissionDescription, MessageType.None);
        }

        SerializedObject serializedMission = new SerializedObject(missionDefinition);
        SerializedProperty stages = serializedMission.FindProperty("stages");
        SerializedProperty objectives = serializedMission.FindProperty("persistentObjectives");
        SerializedProperty failConditions = serializedMission.FindProperty("failConditions");

        if (stages != null && stages.arraySize > 0)
        {
            EditorGUILayout.LabelField("Stages", EditorStyles.miniBoldLabel);
            for (int i = 0; i < stages.arraySize; i++)
            {
                MissionStageDefinition stage = stages.GetArrayElementAtIndex(i).objectReferenceValue as MissionStageDefinition;
                if (stage == null)
                {
                    continue;
                }

                EditorGUILayout.LabelField($"{i + 1}. {stage.StageTitle} [{stage.StageId}]");
                DrawStagePreview(stage);
            }

            if (objectives != null && objectives.arraySize > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Persistent Objectives", EditorStyles.miniBoldLabel);
                DrawObjectiveList(objectives);
            }
        }
        else if (objectives != null)
        {
            EditorGUILayout.LabelField("Persistent Objectives", EditorStyles.miniBoldLabel);
            DrawObjectiveList(objectives);
        }

        if (failConditions != null && failConditions.arraySize > 0)
        {
            EditorGUILayout.LabelField("Fail Conditions", EditorStyles.miniBoldLabel);
            DrawAssetReferenceList(failConditions);
        }
    }

    private void DrawRuntimePreview()
    {
        IncidentMissionSystem missionSystem = (IncidentMissionSystem)target;
        EditorGUILayout.LabelField("Runtime Preview", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to preview runtime state and objective progress.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("State", missionSystem.State.ToString());
        EditorGUILayout.LabelField("Elapsed", missionSystem.ElapsedTime.ToString("F1"));
        if (missionSystem.TimeLimitSeconds > 0f)
        {
            EditorGUILayout.LabelField("Remaining", missionSystem.RemainingTimeSeconds.ToString("F1"));
        }

        if (missionSystem.DisplayedMaximumScore > 0)
        {
            string scoreLabel = $"{missionSystem.DisplayedScore}/{missionSystem.DisplayedMaximumScore}";
            if (!string.IsNullOrWhiteSpace(missionSystem.DisplayedScoreRank))
            {
                scoreLabel += $" [{missionSystem.DisplayedScoreRank}]";
            }

            EditorGUILayout.LabelField("Score", scoreLabel);
        }

        if (missionSystem.HasActiveStage)
        {
            EditorGUILayout.LabelField("Current Stage", $"{missionSystem.CurrentStageIndex + 1}/{missionSystem.TotalStageCount}: {missionSystem.CurrentStageTitle}");
        }

        if (missionSystem.ObjectiveStatusCount <= 0)
        {
            EditorGUILayout.HelpBox("No active objective statuses to display.", MessageType.None);
            return;
        }

        for (int i = 0; i < missionSystem.ObjectiveStatusCount; i++)
        {
            if (!missionSystem.TryGetObjectiveStatus(i, out MissionObjectiveStatusSnapshot status))
            {
                continue;
            }

            MessageType messageType = status.HasFailed
                ? MessageType.Error
                : status.IsComplete
                    ? MessageType.Info
                    : MessageType.None;
            string summary = status.MaxScore > 0
                ? $"{status.Summary} ({status.Score}/{status.MaxScore})"
                : status.Summary;
            EditorGUILayout.HelpBox(summary, messageType);
        }
    }

    private List<MissionAssetValidation.Entry> ValidateIncidentMissionSystem(IncidentMissionSystem missionSystem)
    {
        List<MissionAssetValidation.Entry> entries = new List<MissionAssetValidation.Entry>();
        if (missionSystem == null)
        {
            entries.Add(new MissionAssetValidation.Entry(MissionAssetValidation.Severity.Error, "Mission system component is missing."));
            return entries;
        }

        MissionDefinition missionDefinition = GetMissionDefinition();
        if (missionDefinition != null)
        {
            entries.AddRange(MissionAssetValidation.ValidateMission(missionDefinition));
        }
        else
        {
            entries.Add(new MissionAssetValidation.Entry(MissionAssetValidation.Severity.Warning, "No MissionDefinition assigned. Using legacy mission config on component.", missionSystem));
        }

        MissionSceneObjectRegistry registry = GetAssignedRegistry();
        SerializedObject serializedMissionSystem = new SerializedObject(missionSystem);
        SerializedProperty stageActionBindings = serializedMissionSystem.FindProperty("stageActionBindings");

        if (registry == null && UsesTargetKeyDrivenActions(missionDefinition))
        {
            entries.Add(new MissionAssetValidation.Entry(MissionAssetValidation.Severity.Warning, "Mission uses target-key actions but no MissionSceneObjectRegistry is assigned.", missionSystem));
        }

        Dictionary<string, GameObject> registryKeys = CollectRegistryKeys(registry, entries);
        ValidateStageActionBindingStageIds(stageActionBindings, missionDefinition, entries);
        ValidateActionTargetKeysAgainstRegistry(missionDefinition, registryKeys, entries);
        ValidateSignalObjectivesAgainstSceneEmitters(missionSystem.gameObject.scene, missionDefinition, entries);

        if (entries.Count == 0)
        {
            entries.Add(new MissionAssetValidation.Entry(MissionAssetValidation.Severity.Info, "No scene setup issues found.", missionSystem));
        }

        return entries;
    }

    private void AssignRegistry(IncidentMissionSystem missionSystem, bool addIfMissing, bool includeSceneSearch)
    {
        if (missionSystem == null)
        {
            return;
        }

        MissionSceneObjectRegistry registry = missionSystem.GetComponent<MissionSceneObjectRegistry>();
        if (registry == null && includeSceneSearch)
        {
            registry = FindAnyObjectByType<MissionSceneObjectRegistry>(FindObjectsInactive.Include);
        }

        if (registry == null && addIfMissing)
        {
            Undo.AddComponent<MissionSceneObjectRegistry>(missionSystem.gameObject);
            registry = missionSystem.GetComponent<MissionSceneObjectRegistry>();
        }

        if (registry == null)
        {
            Debug.LogWarning("No MissionSceneObjectRegistry found for assignment.", missionSystem);
            return;
        }

        SerializedObject serializedMissionSystem = new SerializedObject(missionSystem);
        SerializedProperty registryProperty = serializedMissionSystem.FindProperty("sceneObjectRegistry");
        registryProperty.objectReferenceValue = registry;
        serializedMissionSystem.ApplyModifiedProperties();
        EditorUtility.SetDirty(missionSystem);
        EditorGUIUtility.PingObject(registry);
    }

    private void DrawStagePreview(MissionStageDefinition stage)
    {
        SerializedObject serializedStage = new SerializedObject(stage);
        SerializedProperty objectives = serializedStage.FindProperty("objectives");
        SerializedProperty startedActions = serializedStage.FindProperty("onStageStartedActions");
        SerializedProperty completedActions = serializedStage.FindProperty("onStageCompletedActions");

        DrawIndentedReferenceList("Objectives", objectives);
        DrawIndentedReferenceList("Start Actions", startedActions);
        DrawIndentedReferenceList("Complete Actions", completedActions);
    }

    private void DrawObjectiveList(SerializedProperty property)
    {
        DrawAssetReferenceList(property);
    }

    private void DrawAssetReferenceList(SerializedProperty property)
    {
        if (property == null || !property.isArray)
        {
            return;
        }

        for (int i = 0; i < property.arraySize; i++)
        {
            Object referencedObject = property.GetArrayElementAtIndex(i).objectReferenceValue;
            if (referencedObject != null)
            {
                EditorGUILayout.ObjectField(referencedObject, referencedObject.GetType(), false);
            }
        }
    }

    private void DrawIndentedReferenceList(string label, SerializedProperty property)
    {
        if (property == null || property.arraySize == 0)
        {
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        DrawAssetReferenceList(property);
        EditorGUI.indentLevel--;
    }

    private MissionDefinition GetMissionDefinition()
    {
        return serializedObject.FindProperty("missionDefinition")?.objectReferenceValue as MissionDefinition;
    }

    private MissionSceneObjectRegistry GetAssignedRegistry()
    {
        return serializedObject.FindProperty("sceneObjectRegistry")?.objectReferenceValue as MissionSceneObjectRegistry;
    }

    private static Dictionary<string, GameObject> CollectRegistryKeys(MissionSceneObjectRegistry registry, List<MissionAssetValidation.Entry> entries)
    {
        Dictionary<string, GameObject> keys = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);
        if (registry == null)
        {
            return keys;
        }

        SerializedObject serializedRegistry = new SerializedObject(registry);
        SerializedProperty entriesProperty = serializedRegistry.FindProperty("entries");
        if (entriesProperty == null)
        {
            return keys;
        }

        for (int i = 0; i < entriesProperty.arraySize; i++)
        {
            SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(i);
            string key = entry.FindPropertyRelative("key")?.stringValue;
            GameObject targetObject = entry.FindPropertyRelative("targetObject")?.objectReferenceValue as GameObject;

            if (string.IsNullOrWhiteSpace(key))
            {
                entries.Add(new MissionAssetValidation.Entry(MissionAssetValidation.Severity.Warning, $"Registry entry {i} has an empty key.", registry));
                continue;
            }

            if (targetObject == null)
            {
                entries.Add(new MissionAssetValidation.Entry(MissionAssetValidation.Severity.Warning, $"Registry key '{key}' has no target object.", registry));
                continue;
            }

            if (keys.ContainsKey(key.Trim()))
            {
                entries.Add(new MissionAssetValidation.Entry(MissionAssetValidation.Severity.Error, $"Duplicate registry key '{key}' found.", registry));
                continue;
            }

            keys.Add(key.Trim(), targetObject);
        }

        return keys;
    }

    private static void ValidateStageActionBindingStageIds(SerializedProperty stageActionBindings, MissionDefinition missionDefinition, List<MissionAssetValidation.Entry> entries)
    {
        if (stageActionBindings == null || missionDefinition == null)
        {
            return;
        }

        HashSet<string> validStageIds = CollectMissionStageIds(missionDefinition);
        for (int i = 0; i < stageActionBindings.arraySize; i++)
        {
            SerializedProperty binding = stageActionBindings.GetArrayElementAtIndex(i);
            string stageId = binding.FindPropertyRelative("stageId")?.stringValue;
            if (string.IsNullOrWhiteSpace(stageId))
            {
                entries.Add(new MissionAssetValidation.Entry(MissionAssetValidation.Severity.Warning, $"Stage Action Binding {i} has an empty stageId."));
                continue;
            }

            if (validStageIds.Count > 0 && !validStageIds.Contains(stageId.Trim()))
            {
                entries.Add(new MissionAssetValidation.Entry(MissionAssetValidation.Severity.Warning, $"Stage Action Binding references unknown stageId '{stageId}'."));
            }
        }
    }

    private static HashSet<string> CollectMissionStageIds(MissionDefinition missionDefinition)
    {
        HashSet<string> stageIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        if (missionDefinition == null)
        {
            return stageIds;
        }

        SerializedObject serializedMission = new SerializedObject(missionDefinition);
        SerializedProperty stages = serializedMission.FindProperty("stages");
        if (stages == null)
        {
            return stageIds;
        }

        for (int i = 0; i < stages.arraySize; i++)
        {
            MissionStageDefinition stage = stages.GetArrayElementAtIndex(i).objectReferenceValue as MissionStageDefinition;
            if (stage != null && !string.IsNullOrWhiteSpace(stage.StageId))
            {
                stageIds.Add(stage.StageId.Trim());
            }
        }

        return stageIds;
    }

    private static bool UsesTargetKeyDrivenActions(MissionDefinition missionDefinition)
    {
        if (missionDefinition == null)
        {
            return false;
        }

        SerializedObject serializedMission = new SerializedObject(missionDefinition);
        SerializedProperty stages = serializedMission.FindProperty("stages");
        if (stages == null)
        {
            return false;
        }

        for (int i = 0; i < stages.arraySize; i++)
        {
            MissionStageDefinition stage = stages.GetArrayElementAtIndex(i).objectReferenceValue as MissionStageDefinition;
            if (stage == null)
            {
                continue;
            }

            SerializedObject serializedStage = new SerializedObject(stage);
            if (HasActionWithTargetKey(serializedStage.FindProperty("onStageStartedActions")) ||
                HasActionWithTargetKey(serializedStage.FindProperty("onStageCompletedActions")))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasActionWithTargetKey(SerializedProperty actionsProperty)
    {
        if (actionsProperty == null)
        {
            return false;
        }

        for (int i = 0; i < actionsProperty.arraySize; i++)
        {
            MissionActionDefinition action = actionsProperty.GetArrayElementAtIndex(i).objectReferenceValue as MissionActionDefinition;
            if (action == null)
            {
                continue;
            }

            SerializedObject serializedAction = new SerializedObject(action);
            SerializedProperty targetKey = serializedAction.FindProperty("targetKey");
            if (targetKey != null && !string.IsNullOrWhiteSpace(targetKey.stringValue))
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateActionTargetKeysAgainstRegistry(MissionDefinition missionDefinition, Dictionary<string, GameObject> registryKeys, List<MissionAssetValidation.Entry> entries)
    {
        if (missionDefinition == null)
        {
            return;
        }

        SerializedObject serializedMission = new SerializedObject(missionDefinition);
        SerializedProperty stages = serializedMission.FindProperty("stages");
        if (stages == null)
        {
            return;
        }

        for (int i = 0; i < stages.arraySize; i++)
        {
            MissionStageDefinition stage = stages.GetArrayElementAtIndex(i).objectReferenceValue as MissionStageDefinition;
            if (stage == null)
            {
                continue;
            }

            SerializedObject serializedStage = new SerializedObject(stage);
            ValidateActionListTargetKeys(serializedStage.FindProperty("onStageStartedActions"), registryKeys, entries, stage, "OnStageStarted");
            ValidateActionListTargetKeys(serializedStage.FindProperty("onStageCompletedActions"), registryKeys, entries, stage, "OnStageCompleted");
        }
    }

    private static void ValidateActionListTargetKeys(SerializedProperty actionsProperty, Dictionary<string, GameObject> registryKeys, List<MissionAssetValidation.Entry> entries, MissionStageDefinition stage, string label)
    {
        if (actionsProperty == null)
        {
            return;
        }

        for (int i = 0; i < actionsProperty.arraySize; i++)
        {
            MissionActionDefinition action = actionsProperty.GetArrayElementAtIndex(i).objectReferenceValue as MissionActionDefinition;
            if (action == null)
            {
                continue;
            }

            SerializedObject serializedAction = new SerializedObject(action);
            SerializedProperty targetKey = serializedAction.FindProperty("targetKey");
            if (targetKey == null || string.IsNullOrWhiteSpace(targetKey.stringValue))
            {
                continue;
            }

            if (!registryKeys.ContainsKey(targetKey.stringValue.Trim()))
            {
                entries.Add(new MissionAssetValidation.Entry(MissionAssetValidation.Severity.Warning, $"{label} action '{action.name}' in stage '{stage.StageTitle}' references missing registry key '{targetKey.stringValue}'.", action));
            }
        }
    }

    private static void ValidateSignalObjectivesAgainstSceneEmitters(UnityEngine.SceneManagement.Scene scene, MissionDefinition missionDefinition, List<MissionAssetValidation.Entry> entries)
    {
        if (missionDefinition == null || !scene.IsValid())
        {
            return;
        }

        HashSet<string> sceneSignalKeys = CollectSceneSignalKeys();
        SerializedObject serializedMission = new SerializedObject(missionDefinition);
        SerializedProperty objectives = serializedMission.FindProperty("persistentObjectives");
        SerializedProperty stages = serializedMission.FindProperty("stages");

        ValidateObjectiveSignalKeys(objectives, sceneSignalKeys, entries);
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
                ValidateObjectiveSignalKeys(serializedStage.FindProperty("objectives"), sceneSignalKeys, entries);
            }
        }
    }

    private static void ValidateObjectiveSignalKeys(SerializedProperty objectivesProperty, HashSet<string> sceneSignalKeys, List<MissionAssetValidation.Entry> entries)
    {
        if (objectivesProperty == null)
        {
            return;
        }

        for (int i = 0; i < objectivesProperty.arraySize; i++)
        {
            MissionObjectiveDefinition objective = objectivesProperty.GetArrayElementAtIndex(i).objectReferenceValue as MissionObjectiveDefinition;
            if (objective == null)
            {
                continue;
            }

            SerializedObject serializedObjective = new SerializedObject(objective);
            SerializedProperty targetSignalKey = serializedObjective.FindProperty("targetSignalKey");
            if (targetSignalKey == null || string.IsNullOrWhiteSpace(targetSignalKey.stringValue))
            {
                continue;
            }

            string normalizedKey = targetSignalKey.stringValue.Trim();
            if (!sceneSignalKeys.Contains(normalizedKey))
            {
                entries.Add(new MissionAssetValidation.Entry(MissionAssetValidation.Severity.Warning, $"Objective '{objective.name}' references signal key '{normalizedKey}' but no matching scene emitter/relay was found.", objective));
            }
        }
    }

    private static HashSet<string> CollectSceneSignalKeys()
    {
        HashSet<string> keys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        AppendSignalKeys(FindObjectsByType<MissionSignalSource>(FindObjectsInactive.Include), keys);
        AppendSignalKeys(FindObjectsByType<MissionInteractionSignalRelay>(FindObjectsInactive.Include), keys);
        AppendSignalKeys(FindObjectsByType<MissionBreakableSignalRelay>(FindObjectsInactive.Include), keys);
        AppendSignalKeys(FindObjectsByType<MissionRescueDeliverySignalRelay>(FindObjectsInactive.Include), keys);
        return keys;
    }

    private static void AppendSignalKeys<T>(T[] behaviours, HashSet<string> keys) where T : MonoBehaviour
    {
        if (behaviours == null)
        {
            return;
        }

        for (int i = 0; i < behaviours.Length; i++)
        {
            T behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            SerializedObject serializedBehaviour = new SerializedObject(behaviour);
            SerializedProperty signalKey = serializedBehaviour.FindProperty("signalKey");
            if (signalKey != null && !string.IsNullOrWhiteSpace(signalKey.stringValue))
            {
                keys.Add(signalKey.stringValue.Trim());
            }
        }
    }
}
