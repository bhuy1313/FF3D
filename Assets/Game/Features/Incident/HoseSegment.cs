using System.Collections.Generic;
using UnityEngine;

public class HoseSegment
{
    public List<Knot> Knots = new List<Knot>();
    public GameObject GameObject;
    public Mesh Mesh;
    public MeshFilter MeshFilter;
    public MeshRenderer MeshRenderer;

    public float Length;

    // 🔥 NEW
    public Vector3 LastUp = Vector3.up;

    public void AddKnot(Knot k)
    {
        if (Knots.Count > 0)
        {
            Length += Vector3.Distance(Knots[Knots.Count - 1].Position, k.Position);
        }

        Knots.Add(k);
    }
}
