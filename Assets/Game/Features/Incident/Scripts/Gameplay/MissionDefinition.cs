using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "MissionDefinition",
    menuName = "TrueJourney/Missions/Mission Definition")]
public class MissionDefinition : ScriptableObject
{
    [Header("Mission")]
    [SerializeField] private string missionId = "incident";
    [SerializeField] private string missionTitleLocalizationKey;
    [SerializeField] private string missionTitle = "Resolve Incident";
    [SerializeField] private string missionDescriptionLocalizationKey;
    [SerializeField, TextArea] private string missionDescription = "Extinguish fires and rescue civilians.";
    [SerializeField] private float timeLimitSeconds = 0f;
    [SerializeField] private MissionScoreConfig scoreConfig = new MissionScoreConfig();

    [Header("Objectives")]
    [SerializeField, FormerlySerializedAs("objectives")] private List<MissionObjectiveDefinition> persistentObjectives = new List<MissionObjectiveDefinition>();

    [Header("Fail Conditions")]
    [SerializeField] private List<MissionFailConditionDefinition> failConditions = new List<MissionFailConditionDefinition>();

    [Header("Stages")]
    [SerializeField] private List<MissionStageDefinition> stages = new List<MissionStageDefinition>();

    public string MissionId => missionId;
    public string MissionTitle => MissionLocalization.Get(missionTitleLocalizationKey, missionTitle);
    public string MissionDescription => MissionLocalization.Get(missionDescriptionLocalizationKey, missionDescription);
    public float TimeLimitSeconds => timeLimitSeconds;
    public MissionScoreConfig ScoreConfig
    {
        get
        {
            if (scoreConfig == null)
            {
                scoreConfig = new MissionScoreConfig();
            }

            return scoreConfig;
        }
    }
    public bool HasStages
    {
        get
        {
            if (stages == null)
            {
                return false;
            }

            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i] != null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public void CollectObjectives(List<MissionObjectiveDefinition> results)
    {
        CollectPersistentObjectives(results);
    }

    public void CollectObjectives(List<MissionObjectiveDefinition> results, int stageIndex)
    {
        if (stageIndex >= 0)
        {
            CollectStageObjectives(results, stageIndex);
            return;
        }

        CollectPersistentObjectives(results);
    }

    public void CollectPersistentObjectives(List<MissionObjectiveDefinition> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        if (persistentObjectives == null)
        {
            return;
        }

        for (int i = 0; i < persistentObjectives.Count; i++)
        {
            MissionObjectiveDefinition objective = persistentObjectives[i];
            if (objective != null)
            {
                results.Add(objective);
            }
        }
    }

    public void CollectStageObjectives(List<MissionObjectiveDefinition> results, int stageIndex)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        if (HasStages && TryGetStage(stageIndex, out MissionStageDefinition stage))
        {
            stage.CollectObjectives(results);
        }
    }

    public void CollectStages(List<MissionStageDefinition> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        if (stages == null)
        {
            return;
        }

        for (int i = 0; i < stages.Count; i++)
        {
            MissionStageDefinition stage = stages[i];
            if (stage != null)
            {
                results.Add(stage);
            }
        }
    }

    public void CollectFailConditions(List<MissionFailConditionDefinition> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        if (failConditions == null)
        {
            return;
        }

        for (int i = 0; i < failConditions.Count; i++)
        {
            MissionFailConditionDefinition failCondition = failConditions[i];
            if (failCondition != null)
            {
                results.Add(failCondition);
            }
        }
    }

    public bool TryGetStage(int stageIndex, out MissionStageDefinition stage)
    {
        stage = null;
        if (stageIndex < 0)
        {
            return false;
        }

        if (stages == null)
        {
            return false;
        }

        int resolvedIndex = 0;
        for (int i = 0; i < stages.Count; i++)
        {
            MissionStageDefinition candidate = stages[i];
            if (candidate == null)
            {
                continue;
            }

            if (resolvedIndex == stageIndex)
            {
                stage = candidate;
                return true;
            }

            resolvedIndex++;
        }

        return false;
    }
}
