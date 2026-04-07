using System;
using System.Collections.Generic;

/// <summary>
/// Small helper for the prototype's hazard-only multi-value behavior.
/// Hazards are stored as a normalized, duplicate-free comma-separated string.
/// </summary>
public static class HazardValueUtility
{
    private static readonly char[] Separators = { ',', ';', '\n', '\r', '|' };

    public static string MergeValues(string existingValue, string incomingValue)
    {
        List<string> mergedValues = ParseValues(existingValue);
        List<string> incomingValues = ParseValues(incomingValue);

        for (int i = 0; i < incomingValues.Count; i++)
        {
            AddUnique(mergedValues, incomingValues[i]);
        }

        return JoinValues(mergedValues);
    }

    public static bool ContainsValue(string storedValue, string expectedValue)
    {
        List<string> storedValues = ParseValues(storedValue);
        List<string> expectedValues = ParseValues(expectedValue);
        if (storedValues.Count <= 0 || expectedValues.Count <= 0)
        {
            return false;
        }

        for (int i = 0; i < expectedValues.Count; i++)
        {
            if (!ContainsNormalizedValue(storedValues, expectedValues[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool MatchesExpectedSet(string actualValue, string expectedValue)
    {
        List<string> actualValues = ParseValues(actualValue);
        List<string> expectedValues = ParseValues(expectedValue);
        if (actualValues.Count <= 0 || expectedValues.Count <= 0)
        {
            return false;
        }

        if (actualValues.Count != expectedValues.Count)
        {
            return false;
        }

        return ContainsValue(actualValue, expectedValue)
            && ContainsValue(expectedValue, actualValue);
    }

    public static string BuildMismatchText(string actualValue, string expectedValue)
    {
        List<string> actualValues = ParseValues(actualValue);
        List<string> expectedValues = ParseValues(expectedValue);
        List<string> missingValues = new List<string>();
        List<string> extraValues = new List<string>();

        for (int i = 0; i < expectedValues.Count; i++)
        {
            if (!ContainsNormalizedValue(actualValues, expectedValues[i]))
            {
                missingValues.Add(expectedValues[i]);
            }
        }

        for (int i = 0; i < actualValues.Count; i++)
        {
            if (!ContainsNormalizedValue(expectedValues, actualValues[i]))
            {
                extraValues.Add(actualValues[i]);
            }
        }

        if (missingValues.Count > 0 && extraValues.Count > 0)
        {
            string format = CallPhaseUiChromeText.Tr(
                "callphase.result.issue.hazard_missing_and_unexpected",
                "Hazard information was incomplete. Missing: {0}. Unexpected: {1}.");
            return string.Format(format, JoinValues(missingValues), JoinValues(extraValues));
        }

        if (missingValues.Count > 0)
        {
            string format = CallPhaseUiChromeText.Tr(
                "callphase.result.issue.hazard_missing",
                "Hazard information was incomplete. Missing: {0}.");
            return string.Format(format, JoinValues(missingValues));
        }

        if (extraValues.Count > 0)
        {
            string format = CallPhaseUiChromeText.Tr(
                "callphase.result.issue.hazard_unexpected",
                "Hazard information included unexpected values: {0}.");
            return string.Format(format, JoinValues(extraValues));
        }

        return CallPhaseUiChromeText.Tr(
            "callphase.result.issue.hazard_incomplete",
            "Hazard information was incomplete.");
    }

    public static List<string> ParseValues(string combinedValue)
    {
        List<string> values = new List<string>();
        if (string.IsNullOrWhiteSpace(combinedValue))
        {
            return values;
        }

        string[] parts = combinedValue.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            AddUnique(values, parts[i]);
        }

        return values;
    }

    public static string JoinValues(List<string> values)
    {
        if (values == null || values.Count <= 0)
        {
            return string.Empty;
        }

        return string.Join(", ", values);
    }

    private static void AddUnique(List<string> values, string rawValue)
    {
        string normalizedValue = NormalizeValue(rawValue);
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return;
        }

        if (!ContainsNormalizedValue(values, normalizedValue))
        {
            values.Add(normalizedValue);
        }
    }

    private static bool ContainsNormalizedValue(List<string> values, string candidate)
    {
        string normalizedCandidate = NormalizeValue(candidate);
        if (values == null || string.IsNullOrWhiteSpace(normalizedCandidate))
        {
            return false;
        }

        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(NormalizeValue(values[i]), normalizedCandidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
