using UnityEngine;

public static class LoadingFlowState
{
    private const string PendingTargetSceneKey = "flow.pending_target_scene";
    private const string PendingScenarioResourcePathKey = "flow.pending_scenario_resource_path";
    private const string PendingCaseIdKey = "flow.pending_case_id";
    private const string CurrentLevelIdKey = "flow.current_level_id";
    private const string PlayerNameKey = "profile.player_name";

    public static void SetPendingTargetScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        PlayerPrefs.SetString(PendingTargetSceneKey, sceneName);
        PlayerPrefs.Save();
    }

    public static bool TryGetPendingTargetScene(out string sceneName)
    {
        sceneName = PlayerPrefs.GetString(PendingTargetSceneKey, string.Empty);
        return !string.IsNullOrWhiteSpace(sceneName);
    }

    public static void ClearPendingTargetScene()
    {
        if (!PlayerPrefs.HasKey(PendingTargetSceneKey))
        {
            return;
        }

        PlayerPrefs.DeleteKey(PendingTargetSceneKey);
        PlayerPrefs.Save();
    }

    public static void SetPendingScenarioResourcePath(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return;
        }

        PlayerPrefs.SetString(PendingScenarioResourcePathKey, resourcePath.Trim());
        PlayerPrefs.Save();
    }

    public static bool TryGetPendingScenarioResourcePath(out string resourcePath)
    {
        resourcePath = PlayerPrefs.GetString(PendingScenarioResourcePathKey, string.Empty);
        return !string.IsNullOrWhiteSpace(resourcePath);
    }

    public static void ClearPendingScenarioResourcePath()
    {
        if (!PlayerPrefs.HasKey(PendingScenarioResourcePathKey))
        {
            return;
        }

        PlayerPrefs.DeleteKey(PendingScenarioResourcePathKey);
        PlayerPrefs.Save();
    }

    public static void SetPendingCaseId(string caseId)
    {
        if (string.IsNullOrWhiteSpace(caseId))
        {
            return;
        }

        PlayerPrefs.SetString(PendingCaseIdKey, caseId.Trim());
        PlayerPrefs.Save();
    }

    public static bool TryGetPendingCaseId(out string caseId)
    {
        caseId = PlayerPrefs.GetString(PendingCaseIdKey, string.Empty);
        return !string.IsNullOrWhiteSpace(caseId);
    }

    public static void ClearPendingCaseId()
    {
        if (!PlayerPrefs.HasKey(PendingCaseIdKey))
        {
            return;
        }

        PlayerPrefs.DeleteKey(PendingCaseIdKey);
        PlayerPrefs.Save();
    }

    public static void SetCurrentLevelId(string levelId)
    {
        if (string.IsNullOrWhiteSpace(levelId))
        {
            return;
        }

        PlayerPrefs.SetString(CurrentLevelIdKey, levelId.Trim());
        PlayerPrefs.Save();
    }

    public static bool TryGetCurrentLevelId(out string levelId)
    {
        levelId = PlayerPrefs.GetString(CurrentLevelIdKey, string.Empty);
        return !string.IsNullOrWhiteSpace(levelId);
    }

    public static string GetCurrentLevelId()
    {
        return PlayerPrefs.GetString(CurrentLevelIdKey, string.Empty);
    }

    public static void ClearCurrentLevelId()
    {
        if (!PlayerPrefs.HasKey(CurrentLevelIdKey))
        {
            return;
        }

        PlayerPrefs.DeleteKey(CurrentLevelIdKey);
        PlayerPrefs.Save();
    }

    public static void SetPlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return;
        }

        PlayerPrefs.SetString(PlayerNameKey, playerName.Trim());
        PlayerPrefs.Save();
    }

    public static string GetPlayerName()
    {
        return PlayerPrefs.GetString(PlayerNameKey, string.Empty);
    }
}
