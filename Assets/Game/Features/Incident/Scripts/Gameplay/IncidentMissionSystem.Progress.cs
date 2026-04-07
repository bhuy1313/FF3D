using System.Collections.Generic;
using UnityEngine;

public partial class IncidentMissionSystem
{
    private void RefreshProgress()
    {
        RemoveNullEntries(trackedFires);
        RemoveNullEntries(trackedRescuables);
        RemoveNullEntries(trackedVictimConditions);

        totalTrackedFires = trackedFires.Count;
        extinguishedFireCount = CountExtinguishedFires(trackedFires);
        totalTrackedRescuables = trackedRescuables.Count;
        rescuedCount = CountRescuedTargets(trackedRescuables);
        totalTrackedVictims = trackedVictimConditions.Count;
        aliveVictimCount = CountLivingVictims(trackedVictimConditions);
        urgentVictimCount = CountVictimsInState(trackedVictimConditions, VictimCondition.TriageState.Urgent);
        criticalVictimCount = CountVictimsInState(trackedVictimConditions, VictimCondition.TriageState.Critical);
        stabilizedVictimCount = CountStabilizedVictims(trackedVictimConditions);
        extractedVictimCount = CountExtractedVictims(trackedVictimConditions);
        deceasedVictimCount = CountVictimsInState(trackedVictimConditions, VictimCondition.TriageState.Deceased);
    }

    private void RefreshRuntimeStateIfDirty()
    {
        if (!progressDirty)
        {
            return;
        }

        RefreshProgress();
        RefreshObjectiveStatuses();
        RefreshScoreState();
        progressDirty = false;
    }

    private void MarkProgressDirty()
    {
        progressDirty = true;
    }

    private static List<T> CollectSceneObjects<T>() where T : Component
    {
        T[] found = FindObjectsByType<T>();
        List<T> results = new List<T>(found.Length);
        for (int i = 0; i < found.Length; i++)
        {
            T candidate = found[i];
            if (candidate != null && candidate.gameObject.scene.IsValid())
            {
                results.Add(candidate);
            }
        }

        return results;
    }

    private static int CountExtinguishedFires(List<Fire> fires)
    {
        int count = 0;
        for (int i = 0; i < fires.Count; i++)
        {
            Fire fire = fires[i];
            if (fire == null || !fire.IsBurning)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountRescuedTargets(List<Rescuable> rescuables)
    {
        int count = 0;
        for (int i = 0; i < rescuables.Count; i++)
        {
            Rescuable rescuable = rescuables[i];
            if (rescuable == null || !rescuable.NeedsRescue)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountVictimsInState(List<VictimCondition> victims, VictimCondition.TriageState state)
    {
        int count = 0;
        for (int i = 0; i < victims.Count; i++)
        {
            VictimCondition victim = victims[i];
            if (victim != null && victim.CurrentTriageState == state)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountLivingVictims(List<VictimCondition> victims)
    {
        int count = 0;
        for (int i = 0; i < victims.Count; i++)
        {
            VictimCondition victim = victims[i];
            if (victim != null && victim.IsAlive)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountStabilizedVictims(List<VictimCondition> victims)
    {
        int count = 0;
        for (int i = 0; i < victims.Count; i++)
        {
            VictimCondition victim = victims[i];
            if (victim != null && victim.IsAlive && victim.IsStabilized)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountExtractedVictims(List<VictimCondition> victims)
    {
        int count = 0;
        for (int i = 0; i < victims.Count; i++)
        {
            VictimCondition victim = victims[i];
            if (victim != null && victim.IsExtracted)
            {
                count++;
            }
        }

        return count;
    }

    private static void RemoveNullEntries<T>(List<T> items) where T : Object
    {
        if (items == null)
        {
            return;
        }

        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] == null)
            {
                items.RemoveAt(i);
            }
        }
    }
}
