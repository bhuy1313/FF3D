using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class PlayerCompletionRecordStore
{
    private const string RecordsKeyPrefix = "records.completion.";

    [Serializable]
    private sealed class RecordListData
    {
        public List<PlayerCompletionRecord> records = new List<PlayerCompletionRecord>();
    }

    public static void SaveRecord(PlayerCompletionRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.playerName))
        {
            return;
        }

        string playerName = Normalize(record.playerName);
        record.playerName = playerName;

        if (string.IsNullOrWhiteSpace(record.recordId))
        {
            record.recordId = BuildRecordId(record);
        }

        if (record.savedUtcTicks <= 0)
        {
            record.savedUtcTicks = DateTime.UtcNow.Ticks;
        }

        RecordListData data = LoadRecordList(playerName);
        int existingIndex = FindRecordIndex(data.records, record.recordId);
        if (existingIndex >= 0)
        {
            data.records[existingIndex] = record;
        }
        else
        {
            data.records.Add(record);
        }

        ApplyBestFlags(data.records);
        SaveRecordList(playerName, data);
    }

    public static List<PlayerCompletionRecord> GetRecords(string playerName)
    {
        RecordListData data = LoadRecordList(playerName);
        ApplyBestFlags(data.records);
        data.records.Sort((left, right) =>
        {
            long leftTicks = left != null ? left.savedUtcTicks : 0;
            long rightTicks = right != null ? right.savedUtcTicks : 0;
            return rightTicks.CompareTo(leftTicks);
        });
        return data.records;
    }

    public static bool DeleteRecord(string playerName, string recordId)
    {
        string normalizedPlayerName = Normalize(playerName);
        if (string.IsNullOrWhiteSpace(normalizedPlayerName) || string.IsNullOrWhiteSpace(recordId))
        {
            return false;
        }

        RecordListData data = LoadRecordList(normalizedPlayerName);
        int index = FindRecordIndex(data.records, recordId);
        if (index < 0)
        {
            return false;
        }

        data.records.RemoveAt(index);
        ApplyBestFlags(data.records);
        SaveRecordList(normalizedPlayerName, data);
        return true;
    }

    public static string BuildRecordId(PlayerCompletionRecord record)
    {
        string playerName = SanitizeToken(record != null ? record.playerName : string.Empty);
        string levelId = SanitizeToken(record != null ? record.levelId : string.Empty);
        string scenario = SanitizeToken(ResolveScenarioToken(record));
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        return $"{FallbackToken(playerName, "Player")}_{FallbackToken(levelId, "Level")}_{FallbackToken(scenario, "Scenario")}_{timestamp}";
    }

    private static RecordListData LoadRecordList(string playerName)
    {
        string normalizedPlayerName = Normalize(playerName);
        if (string.IsNullOrWhiteSpace(normalizedPlayerName))
        {
            return new RecordListData();
        }

        string key = GetRecordsKey(normalizedPlayerName);
        string json = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new RecordListData();
        }

        RecordListData data = JsonUtility.FromJson<RecordListData>(json);
        if (data == null)
        {
            return new RecordListData();
        }

        if (data.records == null)
        {
            data.records = new List<PlayerCompletionRecord>();
        }

        return data;
    }

    private static void SaveRecordList(string playerName, RecordListData data)
    {
        string normalizedPlayerName = Normalize(playerName);
        if (string.IsNullOrWhiteSpace(normalizedPlayerName))
        {
            return;
        }

        string json = JsonUtility.ToJson(data ?? new RecordListData());
        PlayerPrefs.SetString(GetRecordsKey(normalizedPlayerName), json);
        PlayerPrefs.Save();
    }

    private static void ApplyBestFlags(List<PlayerCompletionRecord> records)
    {
        if (records == null || records.Count == 0)
        {
            return;
        }

        for (int i = 0; i < records.Count; i++)
        {
            if (records[i] != null)
            {
                records[i].isBest = false;
            }
        }

        Dictionary<string, PlayerCompletionRecord> bestByLevel = new Dictionary<string, PlayerCompletionRecord>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < records.Count; i++)
        {
            PlayerCompletionRecord record = records[i];
            if (record == null)
            {
                continue;
            }

            string levelKey = string.IsNullOrWhiteSpace(record.levelId) ? record.missionId : record.levelId;
            if (string.IsNullOrWhiteSpace(levelKey))
            {
                levelKey = "Unknown";
            }

            if (!bestByLevel.TryGetValue(levelKey, out PlayerCompletionRecord currentBest) ||
                CompareRecordScore(record, currentBest) > 0)
            {
                bestByLevel[levelKey] = record;
            }
        }

        foreach (PlayerCompletionRecord best in bestByLevel.Values)
        {
            if (best != null)
            {
                best.isBest = true;
            }
        }
    }

    private static int CompareRecordScore(PlayerCompletionRecord left, PlayerCompletionRecord right)
    {
        float leftRatio = GetScoreRatio(left);
        float rightRatio = GetScoreRatio(right);
        int scoreCompare = leftRatio.CompareTo(rightRatio);
        if (scoreCompare != 0)
        {
            return scoreCompare;
        }

        float rightTime = right != null ? right.onsiteElapsedSeconds : float.MaxValue;
        float leftTime = left != null ? left.onsiteElapsedSeconds : float.MaxValue;
        int timeCompare = rightTime.CompareTo(leftTime);
        if (timeCompare != 0)
        {
            return timeCompare;
        }

        long leftTicks = left != null ? left.savedUtcTicks : 0;
        long rightTicks = right != null ? right.savedUtcTicks : 0;
        return leftTicks.CompareTo(rightTicks);
    }

    private static float GetScoreRatio(PlayerCompletionRecord record)
    {
        if (record == null || record.totalMaximumScore <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01(record.totalScore / (float)record.totalMaximumScore);
    }

    private static int FindRecordIndex(List<PlayerCompletionRecord> records, string recordId)
    {
        if (records == null || string.IsNullOrWhiteSpace(recordId))
        {
            return -1;
        }

        for (int i = 0; i < records.Count; i++)
        {
            if (records[i] != null &&
                string.Equals(records[i].recordId, recordId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string GetRecordsKey(string playerName)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(Normalize(playerName));
        string encoded = Convert.ToBase64String(bytes)
            .Replace('=', '_')
            .Replace('+', '-')
            .Replace('/', '.');
        return RecordsKeyPrefix + encoded;
    }

    private static string ResolveScenarioToken(PlayerCompletionRecord record)
    {
        if (record == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(record.scenarioId))
        {
            return record.scenarioId;
        }

        if (!string.IsNullOrWhiteSpace(record.logicalFireLocation))
        {
            return record.logicalFireLocation;
        }

        return record.missionId;
    }

    private static string Normalize(string value)
    {
        return value != null ? value.Trim() : string.Empty;
    }

    private static string SanitizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[builder.Length - 1] != '_')
            {
                builder.Append('_');
            }
        }

        return builder.ToString().Trim('_');
    }

    private static string FallbackToken(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
