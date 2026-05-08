using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class KeyHintTutorialOverrideSource : KeyHintSourceBase
{
    [Serializable]
    private sealed class OverrideEntry
    {
        public string missionId;
        public string actionName;
        public string labelLocalizationKey;
        public string labelFallback;
        public int priorityOffset;
        public int sortOrder;
        public string groupId = "tutorial";
        public string deduplicationKey;

        public bool Matches(string currentMissionId)
        {
            bool missionMatches = string.IsNullOrWhiteSpace(missionId) ||
                                  string.Equals(missionId.Trim(), currentMissionId, StringComparison.OrdinalIgnoreCase);
            return missionMatches;
        }
    }

    [Header("Behavior")]
    [SerializeField] private bool showOnlyDuringRunningMission = true;
    [SerializeField] private List<OverrideEntry> overrides = new List<OverrideEntry>();

    protected override void CollectHintsInternal(KeyHintContext context, List<KeyHintRequest> results)
    {
        if (context == null || results == null || overrides == null || overrides.Count == 0)
        {
            return;
        }

        if (showOnlyDuringRunningMission && context.MissionSystem != null && !context.IsMissionRunning)
        {
            return;
        }

        for (int index = 0; index < overrides.Count; index++)
        {
            OverrideEntry entry = overrides[index];
            if (entry == null || !entry.Matches(context.MissionId) || string.IsNullOrWhiteSpace(entry.actionName))
            {
                continue;
            }

            results.Add(CreateHint(
                entry.actionName,
                entry.labelLocalizationKey,
                string.IsNullOrWhiteSpace(entry.labelFallback) ? KeyHintGameplayUtility.GetDefaultActionLabelFallback(entry.actionName) : entry.labelFallback,
                entry.priorityOffset,
                entry.sortOrder,
                string.IsNullOrWhiteSpace(entry.groupId) ? "tutorial" : entry.groupId,
                entry.deduplicationKey));
        }
    }
}
