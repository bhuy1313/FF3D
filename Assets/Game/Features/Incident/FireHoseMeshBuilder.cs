using System.Collections.Generic;
using UnityEngine;

public static class FireHoseMeshBuilder
{
    public static Mesh Build(
        List<Vector3> points,
        Mesh mesh,
        Vector3 initialUp,
        out Vector3 lastUp,
        float radius = 0.12f,
        int radialSegments = 12)
    {
        mesh ??= new Mesh();
        mesh.Clear();

        int ringCount = points.Count;
        Vector3 safeInitialUp = initialUp.sqrMagnitude > 0f ? initialUp.normalized : Vector3.up;
        if (ringCount < 2)
        {
            lastUp = safeInitialUp;
            return mesh;
        }

        int vertCount = ringCount * radialSegments;

        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        int[] triangles = new int[(ringCount - 1) * radialSegments * 6];

        Vector3 prevUp = safeInitialUp;

        for (int i = 0; i < ringCount; i++)
        {
            Vector3 forward;

            if (i < ringCount - 1)
            {
                forward = (points[i + 1] - points[i]).normalized;
            }
            else
            {
                forward = (points[i] - points[i - 1]).normalized;
            }

            Vector3 up = prevUp;

            if (Mathf.Abs(Vector3.Dot(up, forward)) > 0.9f)
            {
                up = Vector3.Cross(forward, Vector3.right).normalized;
                if (up.sqrMagnitude <= Mathf.Epsilon)
                {
                    up = Vector3.Cross(forward, Vector3.forward).normalized;
                }
            }

            Vector3 right = Vector3.Cross(up, forward).normalized;
            if (right.sqrMagnitude <= Mathf.Epsilon)
            {
                right = Vector3.Cross(Vector3.up, forward).normalized;
            }

            up = Vector3.Cross(forward, right).normalized;
            prevUp = up;

            for (int j = 0; j < radialSegments; j++)
            {
                float angle = (j / (float)radialSegments) * Mathf.PI * 2f;
                Vector3 offset =
                    Mathf.Cos(angle) * right * radius +
                    Mathf.Sin(angle) * up * radius;

                int index = i * radialSegments + j;
                vertices[index] = points[i] + offset;
                uvs[index] = new Vector2(
                    j / (float)radialSegments,
                    i / (float)ringCount);
            }
        }

        int triIndex = 0;

        for (int i = 0; i < ringCount - 1; i++)
        {
            for (int j = 0; j < radialSegments; j++)
            {
                int curr = i * radialSegments + j;
                int next = curr + radialSegments;
                int nextJ = (j + 1) % radialSegments;
                int currNext = i * radialSegments + nextJ;
                int nextNext = currNext + radialSegments;

                triangles[triIndex++] = curr;
                triangles[triIndex++] = currNext;
                triangles[triIndex++] = next;

                triangles[triIndex++] = currNext;
                triangles[triIndex++] = nextNext;
                triangles[triIndex++] = next;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();

        lastUp = prevUp;
        return mesh;
    }
}
