using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class MissionAssetValidation
{
    public enum Severity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public readonly struct Entry
    {
        public Entry(Severity severity, string message, Object context = null)
        {
            this.severity = severity;
            this.message = message;
            this.context = context;
        }

        public readonly Severity severity;
        public readonly string message;
        public readonly Object context;
    }

    public static List<Entry> ValidateMission(MissionDefinition mission)
    {
        List<Entry> entries = new List<Entry>();
        if (mission == null)
        {
            entries.Add(new Entry(Severity.Error, "Mission asset is missing."));
            return entries;
        }

        SerializedObject serializedMission = new SerializedObject(mission);
        SerializedProperty missionId = serializedMission.FindProperty("missionId");
        SerializedProperty missionTitle = serializedMission.FindProperty("missionTitle");
        SerializedProperty missionObjectives = serializedMission.FindProperty("persistentObjectives");
        SerializedProperty missionFailConditions = serializedMission.FindProperty("failConditions");
        SerializedProperty missionStages = serializedMission.FindProperty("stages");
        SerializedProperty missionTimeLimit = serializedMission.FindProperty("timeLimitSeconds");

        if (string.IsNullOrWhiteSpace(missionId?.stringValue))
        {
            entries.Add(new Entry(Severity.Warning, "Mission Id is empty.", mission));
        }

        if (string.IsNullOrWhiteSpace(missionTitle?.stringValue))
        {
            entries.Add(new Entry(Severity.Warning, "Mission Title is empty.", mission));
        }

        int objectiveCount = CountAssignedElements(missionObjectives, entries, "persistent objective");
        int failConditionCount = CountAssignedElements(missionFailConditions, entries, "fail condition");
        int stageCount = CountAssignedElements(missionStages, entries, "stage");

        if (objectiveCount == 0 && stageCount == 0)
        {
            entries.Add(new Entry(Severity.Error, "Mission has no persistent objectives and no stages.", mission));
        }

        HashSet<MissionStageDefinition> seenStages = new HashSet<MissionStageDefinition>();
        HashSet<string> seenStageIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        bool hasTimeLimitFailCondition = false;

        if (missionStages != null)
        {
            for (int i = 0; i < missionStages.arraySize; i++)
            {
                MissionStageDefinition stage = missionStages.GetArrayElementAtIndex(i).objectReferenceValue as MissionStageDefinition;
                if (stage == null)
                {
                    continue;
                }

                if (!seenStages.Add(stage))
                {
                    entries.Add(new Entry(Severity.Warning, $"Stage '{stage.name}' is referenced more than once.", stage));
                }

                if (string.IsNullOrWhiteSpace(stage.StageId))
                {
                    entries.Add(new Entry(Severity.Error, $"Stage '{stage.name}' has an empty Stage Id.", stage));
                }
                else if (!seenStageIds.Add(stage.StageId.Trim()))
                {
                    entries.Add(new Entry(Severity.Error, $"Duplicate Stage Id '{stage.StageId}' found in mission.", stage));
                }

                entries.AddRange(ValidateStage(stage));
            }
        }

        if (missionObjectives != null)
        {
            AppendDuplicateReferenceWarnings(missionObjectives, entries, "Objective");
            for (int i = 0; i < missionObjectives.arraySize; i++)
            {
                MissionObjectiveDefinition objective = missionObjectives.GetArrayElementAtIndex(i).objectReferenceValue as MissionObjectiveDefinition;
                if (objective != null)
                {
                    ValidateSignalKeyProperty(objective, "targetSignalKey", entries, "Objective signal key is empty.");
                }
            }
        }

        if (missionFailConditions != null)
        {
            AppendDuplicateReferenceWarnings(missionFailConditions, entries, "Fail condition");
            for (int i = 0; i < missionFailConditions.arraySize; i++)
            {
                MissionFailConditionDefinition failCondition = missionFailConditions.GetArrayElementAtIndex(i).objectReferenceValue as MissionFailConditionDefinition;
                if (failCondition == null)
                {
                    continue;
                }

                if (failCondition is TimeLimitFailConditionDefinition)
                {
                    hasTimeLimitFailCondition = true;
                }
            }
        }

        if (missionTimeLimit != null && missionTimeLimit.floatValue > 0f && hasTimeLimitFailCondition)
        {
            entries.Add(new Entry(Severity.Warning, "Mission uses both legacy Time Limit Seconds and a TimeLimitFailCondition. Prefer a single source.", mission));
        }

        if (entries.Count == 0)
        {
            entries.Add(new Entry(Severity.Info, "No validation issues found.", mission));
        }

        return entries;
    }

    public static List<Entry> ValidateStage(MissionStageDefinition stage)
    {
        List<Entry> entries = new List<Entry>();
        if (stage == null)
        {
            entries.Add(new Entry(Severity.Error, "Stage asset is missing."));
            return entries;
        }

        if (string.IsNullOrWhiteSpace(stage.StageId))
        {
            entries.Add(new Entry(Severity.Error, "Stage Id is empty.", stage));
        }

        if (string.IsNullOrWhiteSpace(stage.StageTitle))
        {
            entries.Add(new Entry(Severity.Warning, "Stage Title is empty.", stage));
        }

        SerializedObject serializedStage = new SerializedObject(stage);
        SerializedProperty objectives = serializedStage.FindProperty("objectives");
        SerializedProperty onStageStartedActions = serializedStage.FindProperty("onStageStartedActions");
        SerializedProperty onStageCompletedActions = serializedStage.FindProperty("onStageCompletedActions");

        int objectiveCount = CountAssignedElements(objectives, entries, "stage objective");
        if (objectiveCount == 0)
        {
            entries.Add(new Entry(Severity.Warning, "Stage has no objectives.", stage));
        }

        AppendDuplicateReferenceWarnings(objectives, entries, "Objective");
        AppendDuplicateReferenceWarnings(onStageStartedActions, entries, "Start action");
        AppendDuplicateReferenceWarnings(onStageCompletedActions, entries, "Complete action");

        ValidateActions(onStageStartedActions, entries, "On Stage Started action");
        ValidateActions(onStageCompletedActions, entries, "On Stage Completed action");

        if (objectives != null)
        {
            for (int i = 0; i < objectives.arraySize; i++)
            {
                MissionObjectiveDefinition objective = objectives.GetArrayElementAtIndex(i).objectReferenceValue as MissionObjectiveDefinition;
                if (objective != null)
                {
                    ValidateSignalKeyProperty(objective, "targetSignalKey", entries, "Objective signal key is empty.");
                }
            }
        }

        return entries;
    }

    public static void LogEntries(string header, IList<Entry> entries, Object defaultContext)
    {
        if (entries == null || entries.Count == 0)
        {
            Debug.Log($"{header}: no validation issues.", defaultContext);
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            Object context = entry.context != null ? entry.context : defaultContext;
            string message = $"{header}: {entry.message}";
            switch (entry.severity)
            {
                case Severity.Error:
                    Debug.LogError(message, context);
                    break;
                case Severity.Warning:
                    Debug.LogWarning(message, context);
                    break;
                default:
                    Debug.Log(message, context);
                    break;
            }
        }
    }

    private static void ValidateActions(SerializedProperty actionsProperty, List<Entry> entries, string actionLabel)
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
            if (targetKey != null && string.IsNullOrWhiteSpace(targetKey.stringValue))
            {
                entries.Add(new Entry(Severity.Warning, $"{actionLabel} '{action.name}' has an empty target key.", action));
            }
        }
    }

    private static void ValidateSignalKeyProperty(ScriptableObject asset, string propertyName, List<Entry> entries, string warning)
    {
        SerializedObject serializedObject = new SerializedObject(asset);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null && string.IsNullOrWhiteSpace(property.stringValue))
        {
            entries.Add(new Entry(Severity.Warning, $"{warning} Asset: '{asset.name}'.", asset));
        }
    }

    private static int CountAssignedElements(SerializedProperty arrayProperty, List<Entry> entries, string label)
    {
        if (arrayProperty == null || !arrayProperty.isArray)
        {
            return 0;
        }

        int assignedCount = 0;
        for (int i = 0; i < arrayProperty.arraySize; i++)
        {
            if (arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue != null)
            {
                assignedCount++;
            }
            else
            {
                entries.Add(new Entry(Severity.Warning, $"Null {label} entry at index {i}."));
            }
        }

        return assignedCount;
    }

    private static void AppendDuplicateReferenceWarnings(SerializedProperty arrayProperty, List<Entry> entries, string label)
    {
        if (arrayProperty == null || !arrayProperty.isArray)
        {
            return;
        }

        HashSet<Object> seenReferences = new HashSet<Object>();
        for (int i = 0; i < arrayProperty.arraySize; i++)
        {
            Object referencedObject = arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue;
            if (referencedObject == null)
            {
                continue;
            }

            if (!seenReferences.Add(referencedObject))
            {
                entries.Add(new Entry(Severity.Warning, $"{label} '{referencedObject.name}' is referenced more than once.", referencedObject));
            }
        }
    }
}
