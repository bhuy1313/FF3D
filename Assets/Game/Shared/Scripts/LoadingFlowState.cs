using UnityEngine;

public static class LoadingFlowState
{
    private const string PendingTargetSceneKey = "flow.pending_target_scene";
    private const string PendingScenarioResourcePathKey = "flow.pending_scenario_resource_path";
    private const string PendingCaseIdKey = "flow.pending_case_id";
    private const string PendingIncidentPayloadKey = "flow.pending_incident_payload";
    private const string PendingCallPhaseResultKey = "flow.pending_call_phase_result";
    private const string PendingOnsiteSceneKey = "flow.pending_onsite_scene";
    private const string CurrentLevelIdKey = "flow.current_level_id";
    private const string PlayerNameKey = "profile.player_name";
    private static string lastLoadingFlowActivatedScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeFlowMarkers()
    {
        lastLoadingFlowActivatedScene = string.Empty;
    }

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

    public static void MarkSceneActivatedFromLoadingFlow(string sceneName)
    {
        lastLoadingFlowActivatedScene = string.IsNullOrWhiteSpace(sceneName)
            ? string.Empty
            : sceneName.Trim();
    }

    public static bool WasSceneActivatedFromLoadingFlow(string sceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName) &&
               string.Equals(lastLoadingFlowActivatedScene, sceneName.Trim(), System.StringComparison.Ordinal);
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

    public static void SetPendingIncidentPayload(IncidentWorldSetupPayload payload)
    {
        if (payload == null)
        {
            return;
        }

        string json = JsonUtility.ToJson(payload);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        PlayerPrefs.SetString(PendingIncidentPayloadKey, json);
        PlayerPrefs.Save();
    }

    public static bool TryGetPendingIncidentPayload(out IncidentWorldSetupPayload payload)
    {
        payload = null;

        string json = PlayerPrefs.GetString(PendingIncidentPayloadKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        payload = JsonUtility.FromJson<IncidentWorldSetupPayload>(json);
        return payload != null;
    }

    public static void ClearPendingIncidentPayload()
    {
        if (!PlayerPrefs.HasKey(PendingIncidentPayloadKey))
        {
            return;
        }

        PlayerPrefs.DeleteKey(PendingIncidentPayloadKey);
        PlayerPrefs.Save();
    }

    public static void SetPendingCallPhaseResult(CallPhaseResultSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        string json = JsonUtility.ToJson(snapshot);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        PlayerPrefs.SetString(PendingCallPhaseResultKey, json);
        PlayerPrefs.Save();
    }

    public static bool TryGetPendingCallPhaseResult(out CallPhaseResultSnapshot snapshot)
    {
        snapshot = null;

        string json = PlayerPrefs.GetString(PendingCallPhaseResultKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        snapshot = JsonUtility.FromJson<CallPhaseResultSnapshot>(json);
        return snapshot != null;
    }

    public static void ClearPendingCallPhaseResult()
    {
        if (!PlayerPrefs.HasKey(PendingCallPhaseResultKey))
        {
            return;
        }

        PlayerPrefs.DeleteKey(PendingCallPhaseResultKey);
        PlayerPrefs.Save();
    }

    public static void SetPendingOnsiteScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        PlayerPrefs.SetString(PendingOnsiteSceneKey, sceneName.Trim());
        PlayerPrefs.Save();
    }

    public static bool TryGetPendingOnsiteScene(out string sceneName)
    {
        sceneName = PlayerPrefs.GetString(PendingOnsiteSceneKey, string.Empty);
        return !string.IsNullOrWhiteSpace(sceneName);
    }

    public static void ClearPendingOnsiteScene()
    {
        if (!PlayerPrefs.HasKey(PendingOnsiteSceneKey))
        {
            return;
        }

        PlayerPrefs.DeleteKey(PendingOnsiteSceneKey);
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
