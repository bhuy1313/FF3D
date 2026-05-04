using System.Collections.Generic;
using UnityEngine;

public struct Knot
{
    public Vector3 Position;
    public Vector3 Normal;

    public Knot(Vector3 pos, Vector3 normal)
    {
        Position = pos;
        Normal = normal;
    }
}

public class FireHosePath
{
    public List<Knot> Knots = new List<Knot>();
    public float TotalLength { get; private set; }

    public void AddKnot(Vector3 pos, Vector3 normal)
    {
        if (Knots.Count > 0)
        {
            TotalLength += Vector3.Distance(Knots[Knots.Count - 1].Position, pos);
        }

        Knots.Add(new Knot(pos, normal));
    }

    public void Clear()
    {
        Knots.Clear();
        TotalLength = 0f;
    }
}