using System.Collections.Generic;
using UnityEngine;

public sealed class MissionRuntimeSceneData
{
    private readonly HashSet<Fire> fires = new HashSet<Fire>();
    private readonly HashSet<Rescuable> rescuables = new HashSet<Rescuable>();
    private readonly HashSet<VictimCondition> victimConditions = new HashSet<VictimCondition>();

    public void CollectSceneFires()
    {
        CollectSceneObjects(fires);
    }

    public void CollectSceneRescuables()
    {
        CollectSceneObjects(rescuables);
    }

    public void CollectSceneVictimConditions()
    {
        CollectSceneObjects(victimConditions);
    }

    public List<Fire> CreateFireList()
    {
        return new List<Fire>(fires);
    }

    public List<Rescuable> CreateRescuableList()
    {
        return new List<Rescuable>(rescuables);
    }

    public List<VictimCondition> CreateVictimConditionList()
    {
        return new List<VictimCondition>(victimConditions);
    }

    private static void CollectSceneObjects<T>(HashSet<T> targetSet) where T : Component
    {
        if (targetSet == null)
        {
            return;
        }

        T[] found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            T candidate = found[i];
            if (candidate != null && candidate.gameObject.scene.IsValid())
            {
                targetSet.Add(candidate);
            }
        }
    }
}
