using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class PlayerProgressProfileStore
{
    private const string ProfileIndexKey = "progress.profile.index";
    private const string ProfileDataKeyPrefix = "progress.profile.data.";

    [Serializable]
    private sealed class ProfileIndexData
    {
        public List<string> profileNames = new List<string>();
    }

    [Serializable]
    private sealed class PlayerProgressProfileData
    {
        public string playerName = string.Empty;
        public List<string> completedLevelIds = new List<string>();
        public long createdUtcTicks;
        public long lastPlayedUtcTicks;
    }

    public readonly struct ProfileSummary
    {
        public ProfileSummary(string playerName, int completedLevelCount)
        {
            PlayerName = playerName ?? string.Empty;
            CompletedLevelCount = completedLevelCount;
        }

        public string PlayerName { get; }
        public int CompletedLevelCount { get; }
    }

    public static bool HasAnyProfiles()
    {
        return GetProfileIndex().profileNames.Count > 0;
    }

    public static List<ProfileSummary> GetAllProfileSummaries()
    {
        ProfileIndexData index = GetProfileIndex();
        List<ProfileSummary> summaries = new List<ProfileSummary>();

        for (int i = 0; i < index.profileNames.Count; i++)
        {
            string profileName = index.profileNames[i];
            if (string.IsNullOrWhiteSpace(profileName))
            {
                continue;
            }

            PlayerProgressProfileData profile = LoadProfile(profileName);
            if (profile == null)
            {
                continue;
            }

            summaries.Add(new ProfileSummary(profile.playerName, profile.completedLevelIds.Count));
        }

        summaries.Sort((left, right) => string.Compare(left.PlayerName, right.PlayerName, StringComparison.OrdinalIgnoreCase));
        return summaries;
    }

    public static void ResetProfile(string playerName)
    {
        string normalizedPlayerName = NormalizePlayerName(playerName);
        if (string.IsNullOrWhiteSpace(normalizedPlayerName))
        {
            return;
        }

        PlayerProgressProfileData profile = new PlayerProgressProfileData
        {
            playerName = normalizedPlayerName,
            createdUtcTicks = DateTime.UtcNow.Ticks,
            lastPlayedUtcTicks = DateTime.UtcNow.Ticks
        };

        SaveProfile(profile);
    }

    public static void TouchProfile(string playerName)
    {
        string normalizedPlayerName = NormalizePlayerName(playerName);
        if (string.IsNullOrWhiteSpace(normalizedPlayerName))
        {
            return;
        }

        PlayerProgressProfileData profile = LoadProfile(normalizedPlayerName) ?? new PlayerProgressProfileData
        {
            playerName = normalizedPlayerName,
            createdUtcTicks = DateTime.UtcNow.Ticks
        };

        profile.lastPlayedUtcTicks = DateTime.UtcNow.Ticks;
        SaveProfile(profile);
    }

    public static void MarkLevelCompleted(string playerName, string levelId)
    {
        string normalizedPlayerName = NormalizePlayerName(playerName);
        string normalizedLevelId = NormalizeLevelId(levelId);
        if (string.IsNullOrWhiteSpace(normalizedPlayerName) || string.IsNullOrWhiteSpace(normalizedLevelId))
        {
            return;
        }

        PlayerProgressProfileData profile = LoadProfile(normalizedPlayerName) ?? new PlayerProgressProfileData
        {
            playerName = normalizedPlayerName,
            createdUtcTicks = DateTime.UtcNow.Ticks
        };

        if (!ContainsValue(profile.completedLevelIds, normalizedLevelId))
        {
            profile.completedLevelIds.Add(normalizedLevelId);
        }

        profile.lastPlayedUtcTicks = DateTime.UtcNow.Ticks;
        SaveProfile(profile);
    }

    public static bool IsLevelCompleted(string playerName, string levelId)
    {
        string normalizedPlayerName = NormalizePlayerName(playerName);
        string normalizedLevelId = NormalizeLevelId(levelId);
        if (string.IsNullOrWhiteSpace(normalizedPlayerName) || string.IsNullOrWhiteSpace(normalizedLevelId))
        {
            return false;
        }

        PlayerProgressProfileData profile = LoadProfile(normalizedPlayerName);
        return profile != null && ContainsValue(profile.completedLevelIds, normalizedLevelId);
    }

    private static void SaveProfile(PlayerProgressProfileData profile)
    {
        if (profile == null || string.IsNullOrWhiteSpace(profile.playerName))
        {
            return;
        }

        string profileName = NormalizePlayerName(profile.playerName);
        profile.playerName = profileName;

        ProfileIndexData index = GetProfileIndex();
        if (!ContainsValue(index.profileNames, profileName))
        {
            index.profileNames.Add(profileName);
            SaveProfileIndex(index);
        }

        string json = JsonUtility.ToJson(profile);
        PlayerPrefs.SetString(GetProfileDataKey(profileName), json);
        PlayerPrefs.Save();
    }

    private static PlayerProgressProfileData LoadProfile(string playerName)
    {
        string normalizedPlayerName = NormalizePlayerName(playerName);
        if (string.IsNullOrWhiteSpace(normalizedPlayerName))
        {
            return null;
        }

        string key = GetProfileDataKey(normalizedPlayerName);
        if (!PlayerPrefs.HasKey(key))
        {
            return null;
        }

        string json = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        PlayerProgressProfileData profile = JsonUtility.FromJson<PlayerProgressProfileData>(json);
        if (profile == null)
        {
            return null;
        }

        if (profile.completedLevelIds == null)
        {
            profile.completedLevelIds = new List<string>();
        }

        if (string.IsNullOrWhiteSpace(profile.playerName))
        {
            profile.playerName = normalizedPlayerName;
        }

        return profile;
    }

    private static ProfileIndexData GetProfileIndex()
    {
        if (!PlayerPrefs.HasKey(ProfileIndexKey))
        {
            return new ProfileIndexData();
        }

        string json = PlayerPrefs.GetString(ProfileIndexKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ProfileIndexData();
        }

        ProfileIndexData index = JsonUtility.FromJson<ProfileIndexData>(json);
        return index ?? new ProfileIndexData();
    }

    private static void SaveProfileIndex(ProfileIndexData index)
    {
        string json = JsonUtility.ToJson(index ?? new ProfileIndexData());
        PlayerPrefs.SetString(ProfileIndexKey, json);
        PlayerPrefs.Save();
    }

    private static bool ContainsValue(List<string> source, string value)
    {
        if (source == null || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        for (int i = 0; i < source.Count; i++)
        {
            if (string.Equals(source[i], value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizePlayerName(string playerName)
    {
        return playerName != null ? playerName.Trim() : string.Empty;
    }

    private static string NormalizeLevelId(string levelId)
    {
        return levelId != null ? levelId.Trim() : string.Empty;
    }

    private static string GetProfileDataKey(string playerName)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(playerName);
        string encoded = Convert.ToBase64String(bytes)
            .Replace('=', '_')
            .Replace('+', '-')
            .Replace('/', '.');
        return ProfileDataKeyPrefix + encoded;
    }
}
