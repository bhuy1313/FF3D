using System;
using System.Collections.Generic;

public static class MissionAuthoringKeyProvider
{
    public static List<string> CollectSignalKeys(MissionAuthoringSceneScanUtility.ScanResult scanResult)
    {
        List<string> keys = new List<string>();
        if (scanResult == null)
        {
            return keys;
        }

        for (int i = 0; i < scanResult.SignalEmitters.Count; i++)
        {
            string key = scanResult.SignalEmitters[i].Key;
            if (!string.IsNullOrWhiteSpace(key))
            {
                AppendUnique(keys, key.Trim());
            }
        }

        keys.Sort(StringComparer.OrdinalIgnoreCase);
        return keys;
    }

    public static List<string> CollectRegistryKeys(MissionAuthoringSceneScanUtility.ScanResult scanResult)
    {
        List<string> keys = new List<string>();
        if (scanResult == null)
        {
            return keys;
        }

        for (int i = 0; i < scanResult.RegistryEntries.Count; i++)
        {
            string key = scanResult.RegistryEntries[i].Key;
            if (!string.IsNullOrWhiteSpace(key))
            {
                AppendUnique(keys, key.Trim());
            }
        }

        keys.Sort(StringComparer.OrdinalIgnoreCase);
        return keys;
    }

    private static void AppendUnique(List<string> keys, string candidate)
    {
        for (int i = 0; i < keys.Count; i++)
        {
            if (string.Equals(keys[i], candidate, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        keys.Add(candidate);
    }
}
