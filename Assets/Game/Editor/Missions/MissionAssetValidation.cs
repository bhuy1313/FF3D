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
        bool hasTimeLimitFailCondition = false;

        if (objectiveCount == 0)
        {
            entries.Add(new Entry(Severity.Error, "Mission has no persistent objectives.", mission));
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
            SerializedProperty element = arrayProperty.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue == null)
            {
                entries.Add(new Entry(Severity.Warning, $"Mission has an empty {label} reference at index {i}."));
                continue;
            }

            assignedCount++;
        }

        return assignedCount;
    }

    private static void AppendDuplicateReferenceWarnings(SerializedProperty arrayProperty, List<Entry> entries, string label)
    {
        if (arrayProperty == null || !arrayProperty.isArray)
        {
            return;
        }

        HashSet<Object> seen = new HashSet<Object>();
        for (int i = 0; i < arrayProperty.arraySize; i++)
        {
            Object reference = arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue;
            if (reference == null || seen.Add(reference))
            {
                continue;
            }

            entries.Add(new Entry(Severity.Warning, $"{label} '{reference.name}' is referenced more than once.", reference));
        }
    }
}
