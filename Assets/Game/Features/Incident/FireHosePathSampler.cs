using System.Collections.Generic;
using UnityEngine;

public static class FireHosePathSampler
{
    public static List<Vector3> Resample(List<Knot> knots, float spacing)
    {
        List<Vector3> result = new List<Vector3>();
        if (knots.Count < 2) return result;
        if (spacing <= 0f)
        {
            for (int i = 0; i < knots.Count; i++)
            {
                result.Add(knots[i].Position);
            }

            return result;
        }

        float accumulated = 0f;
        result.Add(knots[0].Position);

        for (int i = 1; i < knots.Count; i++)
        {
            Vector3 a = knots[i - 1].Position;
            Vector3 b = knots[i].Position;

            float dist = Vector3.Distance(a, b);

            while (accumulated + dist >= spacing)
            {
                float t = (spacing - accumulated) / dist;
                Vector3 point = Vector3.Lerp(a, b, t);

                result.Add(point);

                a = point;
                dist = Vector3.Distance(a, b);
                accumulated = 0f;
            }

            accumulated += dist;
        }

        Vector3 finalPoint = knots[knots.Count - 1].Position;
        if (result.Count == 0 || Vector3.Distance(result[result.Count - 1], finalPoint) > 0.001f)
        {
            result.Add(finalPoint);
        }

        return result;
    }

    public static List<Vector3> CatmullRom(List<Vector3> points, int resolution = 8)
    {
        List<Vector3> result = new List<Vector3>();

        if (points.Count < 2)
        {
            return result;
        }

        if (points.Count < 4)
        {
            return new List<Vector3>(points);
        }

        List<Vector3> padded = new List<Vector3>(points.Count + 2)
        {
            points[0]
        };
        padded.AddRange(points);
        padded.Add(points[points.Count - 1]);

        result.Add(points[0]);

        for (int i = 0; i < padded.Count - 3; i++)
        {
            Vector3 p0 = padded[i];
            Vector3 p1 = padded[i + 1];
            Vector3 p2 = padded[i + 2];
            Vector3 p3 = padded[i + 3];

            for (int j = 1; j <= resolution; j++)
            {
                float t = j / (float)resolution;
                result.Add(Catmull(p0, p1, p2, p3, t));
            }
        }

        return result;
    }

    static Vector3 Catmull(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t
        );
    }
}
