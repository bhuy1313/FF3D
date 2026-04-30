using UnityEngine;

/// <summary>
/// Attach to a SafeZone slot Transform to define the pose a rescued victim
/// should adopt when placed at this slot. Different slots can have different poses.
/// </summary>
public class RescuedSlotPoseProfile : MonoBehaviour
{
    [Header("Pose")]
    [SerializeField] private Vector3 rotation;

    /// <summary>World-space rotation derived from the configured euler angles.</summary>
    public Quaternion Rotation => Quaternion.Euler(rotation);
}
