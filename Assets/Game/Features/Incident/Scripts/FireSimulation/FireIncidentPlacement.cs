using UnityEngine;

public readonly struct FireIncidentPlacement
{
    public FireIncidentPlacement(Vector3 position, Vector3 surfaceNormal, float initialIntensity01)
        : this(position, surfaceNormal, initialIntensity01, FireIncidentNodeKind.Late)
    {
    }

    public FireIncidentPlacement(
        Vector3 position,
        Vector3 surfaceNormal,
        float initialIntensity01,
        FireIncidentNodeKind kind)
    {
        Position = position;
        SurfaceNormal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        InitialIntensity01 = Mathf.Clamp01(initialIntensity01);
        Kind = kind;
    }

    public Vector3 Position { get; }
    public Vector3 SurfaceNormal { get; }
    public float InitialIntensity01 { get; }
    public FireIncidentNodeKind Kind { get; }
}
