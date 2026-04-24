using UnityEngine;

public readonly struct FireIncidentPlacement
{
    public FireIncidentPlacement(Vector3 position, Vector3 surfaceNormal, float initialIntensity01)
    {
        Position = position;
        SurfaceNormal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        InitialIntensity01 = Mathf.Clamp01(initialIntensity01);
    }

    public Vector3 Position { get; }
    public Vector3 SurfaceNormal { get; }
    public float InitialIntensity01 { get; }
}
