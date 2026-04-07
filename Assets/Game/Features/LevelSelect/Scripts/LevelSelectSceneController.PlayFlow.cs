using UnityEngine;
using UnityEngine.SceneManagement;

public partial class LevelSelectSceneController
{
    private void PlaySelectedLevel()
    {
        if (selectedLevelDefinition == null || !HasPlayableRoute(selectedLevelDefinition))
        {
            return;
        }

        ScenarioDefinition scenario = IsPlayableScenario(selectedLevelDefinition, selectedScenarioOverride)
            ? selectedScenarioOverride
            : GetRandomPlayableScenario(selectedLevelDefinition);
        string targetSceneName = ResolveTargetSceneName(selectedLevelDefinition, scenario);
        if (!IsPlayableScene(targetSceneName))
        {
            return;
        }

        LoadingFlowState.SetPendingTargetScene(targetSceneName);
        if (!string.IsNullOrWhiteSpace(selectedLevelDefinition.levelId))
        {
            LoadingFlowState.SetCurrentLevelId(selectedLevelDefinition.levelId);
        }
        else
        {
            LoadingFlowState.ClearCurrentLevelId();
        }

        string scenarioResourcePath = ResolveScenarioResourcePath(selectedLevelDefinition, scenario);
        if (!string.IsNullOrWhiteSpace(scenarioResourcePath))
        {
            LoadingFlowState.SetPendingScenarioResourcePath(scenarioResourcePath);
        }
        else
        {
            LoadingFlowState.ClearPendingScenarioResourcePath();
        }

        string caseId = ResolveCaseId(selectedLevelDefinition, scenario);
        if (!string.IsNullOrWhiteSpace(caseId))
        {
            LoadingFlowState.SetPendingCaseId(caseId);
        }
        else
        {
            LoadingFlowState.ClearPendingCaseId();
        }

        CloseLevelInfo();
        SceneManager.LoadScene(LoadingSceneName);
    }

    private bool HasPlayableRoute(LevelDefinition definition)
    {
        if (definition == null)
        {
            return false;
        }

        if (HasPlayableScenarioRoute(definition))
        {
            return true;
        }

        return IsPlayableScene(definition.targetSceneName);
    }

    private bool HasPlayableScenarioRoute(LevelDefinition definition)
    {
        if (definition == null || definition.scenarioDefinitions == null || definition.scenarioDefinitions.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < definition.scenarioDefinitions.Length; i++)
        {
            if (IsPlayableScenario(definition, definition.scenarioDefinitions[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasMultipleConfiguredScenarios(LevelDefinition definition)
    {
        return GetConfiguredScenarioCount(definition) > 1;
    }

    private ScenarioDefinition GetRandomPlayableScenario(LevelDefinition definition)
    {
        if (!HasPlayableScenarioRoute(definition))
        {
            return null;
        }

        int playableScenarioCount = 0;
        for (int i = 0; i < definition.scenarioDefinitions.Length; i++)
        {
            if (IsPlayableScenario(definition, definition.scenarioDefinitions[i]))
            {
                playableScenarioCount++;
            }
        }

        if (playableScenarioCount <= 0)
        {
            return null;
        }

        int selectedIndex = UnityEngine.Random.Range(0, playableScenarioCount);
        int currentIndex = 0;

        for (int i = 0; i < definition.scenarioDefinitions.Length; i++)
        {
            ScenarioDefinition scenario = definition.scenarioDefinitions[i];
            if (!IsPlayableScenario(definition, scenario))
            {
                continue;
            }

            if (currentIndex == selectedIndex)
            {
                return scenario;
            }

            currentIndex++;
        }

        return null;
    }

    private bool IsPlayableScenario(LevelDefinition definition, ScenarioDefinition scenario)
    {
        return scenario != null && IsPlayableScene(ResolveTargetSceneName(definition, scenario));
    }

    private static bool IsPlayableScene(string sceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName) &&
               Application.CanStreamedLevelBeLoaded(sceneName.Trim());
    }

    private static string ResolveTargetSceneName(LevelDefinition definition, ScenarioDefinition scenario)
    {
        if (scenario != null && !string.IsNullOrWhiteSpace(scenario.targetSceneName))
        {
            return scenario.targetSceneName.Trim();
        }

        return definition != null && !string.IsNullOrWhiteSpace(definition.targetSceneName)
            ? definition.targetSceneName.Trim()
            : string.Empty;
    }

    private static string ResolveScenarioResourcePath(LevelDefinition definition, ScenarioDefinition scenario)
    {
        if (scenario != null && !string.IsNullOrWhiteSpace(scenario.scenarioResourcePath))
        {
            return scenario.scenarioResourcePath.Trim();
        }

        return definition != null && !string.IsNullOrWhiteSpace(definition.scenarioResourcePath)
            ? definition.scenarioResourcePath.Trim()
            : string.Empty;
    }

    private static string ResolveCaseId(LevelDefinition definition, ScenarioDefinition scenario)
    {
        if (scenario != null && !string.IsNullOrWhiteSpace(scenario.caseId))
        {
            return scenario.caseId.Trim();
        }

        return definition != null && !string.IsNullOrWhiteSpace(definition.caseId)
            ? definition.caseId.Trim()
            : string.Empty;
    }
}
