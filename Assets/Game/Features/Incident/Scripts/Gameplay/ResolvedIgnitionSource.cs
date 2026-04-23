using UnityEngine;

public sealed class ResolvedIgnitionSource
{
    public IncidentOriginArea Area { get; set; }
    public IncidentPossibleFireCause Cause { get; set; }
    public bool IsNormalRoomFire { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public FireHazardType HazardType { get; set; }
    public float Weight { get; set; }
    public string DebugLabel { get; set; }
}
