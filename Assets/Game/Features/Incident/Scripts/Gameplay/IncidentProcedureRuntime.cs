using System.Collections.Generic;
using System.Text;
using UnityEngine;

public sealed class IncidentProcedureRuntime
{
    private sealed class ChecklistRuntimeState
    {
        public IncidentProcedureChecklistItem Item;
        public bool IsRelevant = true;
        public bool IsCompleted;
        public bool IsContradicted;
    }

    private readonly IncidentProcedureDefinition definition;
    private readonly List<ChecklistRuntimeState> checklistStates = new List<ChecklistRuntimeState>();

    public IncidentProcedureRuntime(IncidentProcedureDefinition definition)
    {
        this.definition = definition;

        if (definition == null || definition.ChecklistItems == null)
        {
            return;
        }

        for (int i = 0; i < definition.ChecklistItems.Count; i++)
        {
            IncidentProcedureChecklistItem item = definition.ChecklistItems[i];
            if (item == null)
            {
                continue;
            }

            checklistStates.Add(new ChecklistRuntimeState
            {
                Item = item,
                IsCompleted = item.DefaultChecked
            });
        }
    }

    public IncidentProcedureDefinition Definition => definition;
    public bool HasDefinition => definition != null;
    public int ChecklistCount => checklistStates.Count;
    public int CompletedChecklistCount => CountCompletedChecklistItems();
    public int ContradictedChecklistCount => CountContradictedChecklistItems();

    public void ConsumeSignal(string signalKey)
    {
        if (string.IsNullOrWhiteSpace(signalKey) || checklistStates.Count == 0)
        {
            return;
        }

        for (int i = 0; i < checklistStates.Count; i++)
        {
            ChecklistRuntimeState state = checklistStates[i];
            if (state == null || state.Item == null)
            {
                continue;
            }

            if (MatchesSignal(state.Item.CompletionSignalKey, signalKey))
            {
                state.IsCompleted = true;
            }

            if (MatchesSignal(state.Item.InvalidationSignalKey, signalKey))
            {
                state.IsContradicted = true;
            }
        }
    }

    public void RefreshFromMission(IncidentMissionSystem missionSystem)
    {
        if (missionSystem == null || checklistStates.Count == 0)
        {
            return;
        }

        for (int i = 0; i < checklistStates.Count; i++)
        {
            ChecklistRuntimeState state = checklistStates[i];
            if (state == null || state.Item == null)
            {
                continue;
            }

            if (EvaluateStateKey(missionSystem, state.Item.CompletionStateKey))
            {
                state.IsCompleted = true;
            }

            if (EvaluateStateKey(missionSystem, state.Item.InvalidationStateKey))
            {
                state.IsContradicted = true;
            }
        }
    }

    public int EvaluateScore()
    {
        int score = 0;
        for (int i = 0; i < checklistStates.Count; i++)
        {
            ChecklistRuntimeState state = checklistStates[i];
            if (state == null || state.Item == null || !state.IsRelevant)
            {
                continue;
            }

            int itemScore = state.Item.ScoreDelta;
            if (state.IsContradicted)
            {
                score -= Mathf.Abs(itemScore);
                continue;
            }

            if (state.IsCompleted)
            {
                score += Mathf.Max(0, itemScore);
            }
            else if (state.Item.ItemType == IncidentProcedureChecklistItemType.Required)
            {
                score -= Mathf.Max(0, itemScore);
            }
        }

        return score;
    }

    public int EvaluateMaximumScore()
    {
        int score = 0;
        for (int i = 0; i < checklistStates.Count; i++)
        {
            ChecklistRuntimeState state = checklistStates[i];
            if (state == null || state.Item == null || !state.IsRelevant)
            {
                continue;
            }

            score += Mathf.Max(0, state.Item.ScoreDelta);
        }

        return score;
    }

    public int BuildResultStatuses(List<MissionObjectiveStatusSnapshot> results)
    {
        if (results == null || definition == null)
        {
            return 0;
        }

        int initialCount = results.Count;
        for (int i = 0; i < checklistStates.Count; i++)
        {
            ChecklistRuntimeState state = checklistStates[i];
            if (state == null || state.Item == null || !state.IsRelevant)
            {
                continue;
            }

            string summary = BuildChecklistSummary(state);
            int maxScore = Mathf.Max(0, state.Item.ScoreDelta);
            int score = state.IsCompleted && !state.IsContradicted
                ? maxScore
                : 0;

            results.Add(new MissionObjectiveStatusSnapshot(
                state.Item.Title,
                summary,
                state.IsCompleted && !state.IsContradicted,
                state.IsContradicted,
                score,
                maxScore));
        }

        AppendDebriefLines(results);
        return results.Count - initialCount;
    }

    public void BuildChecklistStatuses(List<IncidentProcedureChecklistStatusSnapshot> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        for (int i = 0; i < checklistStates.Count; i++)
        {
            ChecklistRuntimeState state = checklistStates[i];
            if (state == null || state.Item == null)
            {
                continue;
            }

            results.Add(new IncidentProcedureChecklistStatusSnapshot(
                state.Item.ItemId,
                state.Item.Title,
                state.Item.Description,
                state.Item.ItemType,
                state.Item.Priority,
                state.IsCompleted,
                state.IsContradicted,
                state.IsRelevant));
        }
    }

    public bool TryGetChecklistStatus(string itemId, out bool isCompleted, out bool isContradicted, out bool isRelevant)
    {
        isCompleted = false;
        isContradicted = false;
        isRelevant = false;

        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        for (int i = 0; i < checklistStates.Count; i++)
        {
            ChecklistRuntimeState state = checklistStates[i];
            if (state == null || state.Item == null)
            {
                continue;
            }

            if (!string.Equals(state.Item.ItemId, itemId, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            isCompleted = state.IsCompleted;
            isContradicted = state.IsContradicted;
            isRelevant = state.IsRelevant;
            return true;
        }

        return false;
    }

    public bool SetChecklistCompleted(string itemId, bool isCompleted)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        for (int i = 0; i < checklistStates.Count; i++)
        {
            ChecklistRuntimeState state = checklistStates[i];
            if (state == null || state.Item == null)
            {
                continue;
            }

            if (!string.Equals(state.Item.ItemId, itemId, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            state.IsCompleted = isCompleted;
            if (isCompleted)
            {
                state.IsContradicted = false;
            }

            return true;
        }

        return false;
    }

    public string BuildOverlaySummary()
    {
        if (definition == null)
        {
            return string.Empty;
        }

        int totalCount = 0;
        int completedCount = 0;
        int contradictedCount = 0;
        int requiredPendingCount = 0;

        for (int i = 0; i < checklistStates.Count; i++)
        {
            ChecklistRuntimeState state = checklistStates[i];
            if (state == null || state.Item == null || !state.IsRelevant)
            {
                continue;
            }

            totalCount++;
            if (state.IsCompleted && !state.IsContradicted)
            {
                completedCount++;
            }

            if (state.IsContradicted)
            {
                contradictedCount++;
            }

            if (state.Item.ItemType == IncidentProcedureChecklistItemType.Required &&
                !state.IsCompleted &&
                !state.IsContradicted)
            {
                requiredPendingCount++;
            }
        }

        return MissionLocalization.Format(
            "mission.procedure.overlay.summary",
            "Procedure: {0} | Verified {1}/{2} | Contradicted {3} | Required Pending {4}",
            definition.Title,
            completedCount,
            totalCount,
            contradictedCount,
            requiredPendingCount);
    }

    private void AppendDebriefLines(List<MissionObjectiveStatusSnapshot> results)
    {
        if (results == null || definition == null)
        {
            return;
        }

        IReadOnlyList<IncidentProcedureDebriefLine> source = HasCriticalFailure()
            ? definition.FailureDebriefLines
            : definition.SuccessDebriefLines;
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            IncidentProcedureDebriefLine line = source[i];
            if (line == null || string.IsNullOrWhiteSpace(line.Text))
            {
                continue;
            }

            results.Add(new MissionObjectiveStatusSnapshot(
                line.LineId,
                line.Text,
                !HasCriticalFailure(),
                HasCriticalFailure(),
                0,
                0));
        }
    }

    private bool HasCriticalFailure()
    {
        for (int i = 0; i < checklistStates.Count; i++)
        {
            ChecklistRuntimeState state = checklistStates[i];
            if (state == null || state.Item == null || !state.IsRelevant)
            {
                continue;
            }

            if (state.Item.ItemType == IncidentProcedureChecklistItemType.Required &&
                (state.IsContradicted || !state.IsCompleted))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildChecklistSummary(ChecklistRuntimeState state)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(state.Item.Title);
        builder.Append(": ");

        if (state.IsContradicted)
        {
            builder.Append(MissionLocalization.Get("mission.procedure.status.contradicted", "contradicted"));
            if (!string.IsNullOrWhiteSpace(state.Item.FailureFeedback))
            {
                builder.Append(" - ");
                builder.Append(state.Item.FailureFeedback);
            }

            return builder.ToString();
        }

        if (state.IsCompleted)
        {
            builder.Append(MissionLocalization.Get("mission.procedure.status.verified", "verified"));
            return builder.ToString();
        }

        builder.Append(MissionLocalization.Get("mission.procedure.status.pending", "not verified"));
        if (!string.IsNullOrWhiteSpace(state.Item.FailureFeedback))
        {
            builder.Append(" - ");
            builder.Append(state.Item.FailureFeedback);
        }

        return builder.ToString();
    }

    private static bool MatchesSignal(string expectedSignal, string actualSignal)
    {
        return !string.IsNullOrWhiteSpace(expectedSignal) &&
               !string.IsNullOrWhiteSpace(actualSignal) &&
               string.Equals(expectedSignal.Trim(), actualSignal.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool EvaluateStateKey(IncidentMissionSystem missionSystem, string stateKey)
    {
        if (missionSystem == null || string.IsNullOrWhiteSpace(stateKey))
        {
            return false;
        }

        switch (stateKey.Trim().ToLowerInvariant())
        {
            case "mission-complete":
                return missionSystem.State == IncidentMissionSystem.MissionState.Completed;
            case "mission-failed":
                return missionSystem.State == IncidentMissionSystem.MissionState.Failed;
            case "any-rescue-delivered":
                return missionSystem.RescuedCount > 0 || missionSystem.ExtractedVictimCount > 0;
            case "all-rescuables-rescued":
                return missionSystem.TotalTrackedRescuables > 0 &&
                       missionSystem.RescuedCount >= missionSystem.TotalTrackedRescuables;
            case "all-fires-extinguished":
                return missionSystem.TotalTrackedFires > 0 &&
                       missionSystem.ExtinguishedFireCount >= missionSystem.TotalTrackedFires;
            case "any-victim-dead":
                return missionSystem.DeceasedVictimCount > 0;
            case "no-victim-dead":
                return missionSystem.TotalTrackedVictims > 0 &&
                       missionSystem.DeceasedVictimCount <= 0;
            default:
                return false;
        }
    }

    private int CountCompletedChecklistItems()
    {
        int count = 0;
        for (int i = 0; i < checklistStates.Count; i++)
        {
            ChecklistRuntimeState state = checklistStates[i];
            if (state != null && state.Item != null && state.IsRelevant && state.IsCompleted && !state.IsContradicted)
            {
                count++;
            }
        }

        return count;
    }

    private int CountContradictedChecklistItems()
    {
        int count = 0;
        for (int i = 0; i < checklistStates.Count; i++)
        {
            ChecklistRuntimeState state = checklistStates[i];
            if (state != null && state.Item != null && state.IsRelevant && state.IsContradicted)
            {
                count++;
            }
        }

        return count;
    }
}
