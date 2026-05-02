using UnityEngine;

[DisallowMultipleComponent]
public class VictimSpawnPoint : MonoBehaviour
{
    [Header("Matching")]
    [SerializeField] private string logicalLocationKey = string.Empty;
    [SerializeField] private string[] fireOriginHintKeys = System.Array.Empty<string>();
    [SerializeField] private bool fallbackCandidate = true;

    [Header("Placement")]
    [SerializeField] private bool alignToSurfaceNormal;
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [SerializeField] private Vector3 rotationOffsetEuler = Vector3.zero;

    [Header("Condition Bias")]
    [SerializeField] private bool preferCriticalVictims;
    [SerializeField] private bool preferUrgentVictims = true;

    public string LogicalLocationKey => logicalLocationKey;
    public bool FallbackCandidate => fallbackCandidate;
    public bool PreferCriticalVictims => preferCriticalVictims;
    public bool PreferUrgentVictims => preferUrgentVictims;

    public bool MatchesLogicalLocation(string key)
    {
        return Normalize(logicalLocationKey) == Normalize(key);
    }

    public bool MatchesFireOrigin(string fireOrigin)
    {
        string normalizedOrigin = Normalize(fireOrigin);
        if (string.IsNullOrWhiteSpace(normalizedOrigin))
        {
            return false;
        }

        for (int i = 0; i < fireOriginHintKeys.Length; i++)
        {
            if (normalizedOrigin == Normalize(fireOriginHintKeys[i]))
            {
                return true;
            }
        }

        return false;
    }

    public Pose ResolveSpawnPose()
    {
        Quaternion rotation = transform.rotation * Quaternion.Euler(rotationOffsetEuler);
        Vector3 position = transform.position + transform.TransformVector(positionOffset);
        if (alignToSurfaceNormal)
        {
            rotation = Quaternion.FromToRotation(Vector3.up, transform.up) * rotation;
        }

        return new Pose(position, rotation);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }
}
