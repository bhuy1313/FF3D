using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Level Select/Scenario Catalog", fileName = "LevelScenarioCatalog")]
public class LevelScenarioCatalog : ScriptableObject
{
    [SerializeField] private List<LevelScenarioCatalogEntry> levels = new List<LevelScenarioCatalogEntry>();

    public bool TryApplyTo(string levelId, ref string onsiteSceneName, ref LevelScenarioDefinition[] scenarios)
    {
        LevelScenarioCatalogEntry entry = FindEntry(levelId);
        if (entry == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(entry.OnsiteSceneName))
        {
            onsiteSceneName = entry.OnsiteSceneName.Trim();
        }

        scenarios = entry.BuildScenarioDefinitions();
        return true;
    }

    private LevelScenarioCatalogEntry FindEntry(string levelId)
    {
        if (string.IsNullOrWhiteSpace(levelId) || levels == null)
        {
            return null;
        }

        string normalizedLevelId = levelId.Trim();
        for (int i = 0; i < levels.Count; i++)
        {
            LevelScenarioCatalogEntry entry = levels[i];
            if (entry != null && string.Equals(entry.LevelId, normalizedLevelId, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }
}

[Serializable]
public sealed class LevelScenarioCatalogEntry
{
    [SerializeField] private string levelId;
    [SerializeField] private string onsiteSceneName;
    [SerializeField] private List<LevelScenarioDataReference> scenarios = new List<LevelScenarioDataReference>();

    public string LevelId => levelId;
    public string OnsiteSceneName => onsiteSceneName;

    public LevelScenarioDefinition[] BuildScenarioDefinitions()
    {
        if (scenarios == null || scenarios.Count == 0)
        {
            return Array.Empty<LevelScenarioDefinition>();
        }

        Dictionary<string, List<LevelScenarioDefinition>> variantsByFamily = new Dictionary<string, List<LevelScenarioDefinition>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < scenarios.Count; i++)
        {
            LevelScenarioDataReference reference = scenarios[i];
            LevelScenarioDefinition definition = reference != null ? reference.BuildDefinition(onsiteSceneName) : null;
            if (definition != null)
            {
                string familyId = !string.IsNullOrWhiteSpace(definition.scenarioFamilyId)
                    ? definition.scenarioFamilyId
                    : definition.scenarioId;
                if (!variantsByFamily.TryGetValue(familyId, out List<LevelScenarioDefinition> familyVariants))
                {
                    familyVariants = new List<LevelScenarioDefinition>();
                    variantsByFamily.Add(familyId, familyVariants);
                }

                familyVariants.Add(definition);
            }
        }

        List<LevelScenarioDefinition> definitions = new List<LevelScenarioDefinition>(variantsByFamily.Count);
        foreach (KeyValuePair<string, List<LevelScenarioDefinition>> pair in variantsByFamily)
        {
            List<LevelScenarioDefinition> familyVariants = pair.Value;
            if (familyVariants == null || familyVariants.Count <= 0)
            {
                continue;
            }

            LevelScenarioDefinition representative = familyVariants[0];
            if (familyVariants.Count > 1)
            {
                representative.variantDefinitions = new List<LevelScenarioDefinition>(familyVariants.Count - 1);
                for (int i = 0; i < familyVariants.Count; i++)
                {
                    LevelScenarioDefinition variant = familyVariants[i];
                    if (variant == null || ReferenceEquals(variant, representative))
                    {
                        continue;
                    }

                    representative.variantDefinitions.Add(variant);
                }
            }

            definitions.Add(representative);
        }

        return definitions.Count > 0 ? definitions.ToArray() : Array.Empty<LevelScenarioDefinition>();
    }
}

[Serializable]
public sealed class LevelScenarioDataReference
{
    [SerializeField] private CallPhaseScenarioData scenarioData;
    [SerializeField] private string caseIdOverride;
    [SerializeField] private string targetSceneNameOverride;
    [SerializeField] private string onsiteSceneNameOverride;

    public LevelScenarioDefinition BuildDefinition(string levelOnsiteSceneName)
    {
        if (scenarioData == null)
        {
            return null;
        }

        string resourcePath = scenarioData.name;
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return null;
        }

        string caseId = !string.IsNullOrWhiteSpace(caseIdOverride)
            ? caseIdOverride.Trim()
            : scenarioData.caseId;
        string onsiteSceneName = !string.IsNullOrWhiteSpace(onsiteSceneNameOverride)
            ? onsiteSceneNameOverride.Trim()
            : levelOnsiteSceneName;

        return new LevelScenarioDefinition
        {
            scenarioId = scenarioData.scenarioId,
            scenarioFamilyId = scenarioData.GetScenarioFamilyId(),
            selectionWeight = Mathf.Max(0.01f, scenarioData.selectionWeight),
            displayName = scenarioData.displayName,
            caseId = caseId,
            targetSceneName = !string.IsNullOrWhiteSpace(targetSceneNameOverride) ? targetSceneNameOverride.Trim() : "CallPhaseScene",
            onsiteSceneName = onsiteSceneName,
            scenarioResourcePath = resourcePath.Trim()
        };
    }
}

[Serializable]
public sealed class LevelScenarioDefinition
{
    public string scenarioId;
    public string scenarioFamilyId;
    public float selectionWeight = 1f;
    public string displayName;
    public string caseId;
    public string targetSceneName;
    public string onsiteSceneName;
    public string scenarioResourcePath;
    [NonSerialized] public List<LevelScenarioDefinition> variantDefinitions;
}
