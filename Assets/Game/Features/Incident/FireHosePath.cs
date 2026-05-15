using System.Collections.Generic;
using UnityEngine;

public struct Knot
{
    public Vector3 Position;
    public Vector3 Normal;
    public Quaternion Rotation;

    public Knot(Vector3 pos, Vector3 normal)
        : this(pos, normal, ResolveFallbackRotation(normal))
    {
    }

    public Knot(Vector3 pos, Vector3 normal, Quaternion rotation)
    {
        Position = pos;
        Normal = normal;
        Rotation = rotation;
    }

    private static Quaternion ResolveFallbackRotation(Vector3 normal)
    {
        Vector3 up = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, up);
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(Vector3.right, up);
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.Cross(up, Vector3.right);
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }

        return Quaternion.LookRotation(forward.normalized, up);
    }
}

public class FireHosePath
{
    public List<Knot> Knots = new List<Knot>();
    public float TotalLength { get; private set; }

    public Knot AddKnot(Vector3 pos, Vector3 normal)
    {
        Quaternion rotation = ResolveKnotRotation(pos, normal);

        if (Knots.Count > 0)
        {
            TotalLength += Vector3.Distance(Knots[Knots.Count - 1].Position, pos);
        }

        Knot knot = new Knot(pos, normal, rotation);
        Knots.Add(knot);
        return knot;
    }

    public void Clear()
    {
        Knots.Clear();
        TotalLength = 0f;
    }

    private Quaternion ResolveKnotRotation(Vector3 pos, Vector3 normal)
    {
        Vector3 up = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        Vector3 forward = Vector3.zero;

        if (Knots.Count > 0)
        {
            Vector3 delta = pos - Knots[Knots.Count - 1].Position;
            forward = Vector3.ProjectOnPlane(delta, up);
        }

        if (forward.sqrMagnitude <= 0.0001f && Knots.Count > 0)
        {
            forward = Vector3.ProjectOnPlane(Knots[Knots.Count - 1].Rotation * Vector3.forward, up);
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(Vector3.forward, up);
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(Vector3.right, up);
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.Cross(up, Vector3.right);
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }

        return Quaternion.LookRotation(forward.normalized, up);
    }
}
