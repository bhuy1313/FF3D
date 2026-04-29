using UnityEngine.SceneManagement;

public static class IncidentSeedUtility
{
    public static int StableHash(string value)
    {
        unchecked
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            int hash = 23;
            for (int i = 0; i < value.Length; i++)
            {
                hash = (hash * 31) + value[i];
            }

            return hash;
        }
    }

    public static int CombineHash(int left, int right)
    {
        unchecked
        {
            return (left * 397) ^ right;
        }
    }

    /// <summary>
    /// Returns a deterministic seed for a placement-related RNG pass tied to the current incident payload.
    /// The discriminator separates independent passes (e.g. "secondary", "latent") so they do not share state.
    /// </summary>
    public static int ResolvePlacementSeed(IncidentWorldSetupPayload payload, string discriminator)
    {
        int discriminatorHash = StableHash(discriminator);

        if (payload != null && payload.placementRandomSeed != 0)
        {
            return CombineHash(payload.placementRandomSeed, discriminatorHash);
        }

        int seed = StableHash(SceneManager.GetActiveScene().path);
        if (payload != null)
        {
            seed = CombineHash(seed, StableHash(payload.caseId));
            seed = CombineHash(seed, StableHash(payload.scenarioId));
            seed = CombineHash(seed, StableHash(payload.fireOrigin));
            seed = CombineHash(seed, StableHash(payload.logicalFireLocation));
        }

        seed = CombineHash(seed, discriminatorHash);
        if (seed == 0)
        {
            seed = discriminatorHash != 0 ? discriminatorHash : 1;
        }

        return seed;
    }
}
