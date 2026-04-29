using System.Collections.Generic;
using UnityEngine;

public static class MeshShatterUtility
{
    public readonly struct BoundsChunk
    {
        public BoundsChunk(Vector3 center, Vector3 size)
        {
            Center = center;
            Size = size;
        }

        public Vector3 Center { get; }
        public Vector3 Size { get; }
    }

    public readonly struct TriangleShard
    {
        public TriangleShard(Vector3 a, Vector3 b, Vector3 c)
        {
            A = a;
            B = b;
            C = c;
        }

        public Vector3 A { get; }
        public Vector3 B { get; }
        public Vector3 C { get; }
    }

    public static List<TriangleShard> CreateTriangleShards(Mesh mesh, int subdivisionDepth)
    {
        return CreateTriangleShards(mesh, subdivisionDepth, 0f, 0, -1);
    }

    public static List<TriangleShard> CreateTriangleShards(Mesh mesh, int subdivisionDepth, float splitJitter, int seed)
    {
        return CreateTriangleShards(mesh, subdivisionDepth, splitJitter, seed, -1);
    }

    public static List<TriangleShard> CreateTriangleShards(Mesh mesh, int subdivisionDepth, float splitJitter, int seed, int targetSubmesh)
    {
        List<TriangleShard> shards = new List<TriangleShard>();
        if (mesh == null)
        {
            return shards;
        }

        Vector3[] vertices = mesh.vertices;
        int[] triangles = ResolveTriangles(mesh, targetSubmesh);
        if (vertices == null || triangles == null || triangles.Length < 3)
        {
            return shards;
        }

        subdivisionDepth = Mathf.Clamp(subdivisionDepth, 0, 3);
        splitJitter = Mathf.Clamp(splitJitter, 0f, 0.35f);
        System.Random random = splitJitter > 0f ? new System.Random(seed) : null;
        for (int i = 0; i <= triangles.Length - 3; i += 3)
        {
            TriangleShard triangle = new TriangleShard(
                vertices[triangles[i]],
                vertices[triangles[i + 1]],
                vertices[triangles[i + 2]]);
            SubdivideTriangle(triangle, subdivisionDepth, splitJitter, random, shards);
        }

        return shards;
    }

    public static List<TriangleShard> CreateTriangleShardsImpactWeighted(
        Mesh mesh,
        int baseDepth,
        float splitJitter,
        int seed,
        int targetSubmesh,
        Vector3 impactPointLocal,
        float impactInfluenceRadius)
    {
        List<TriangleShard> shards = new List<TriangleShard>();
        if (mesh == null)
        {
            return shards;
        }

        Vector3[] vertices = mesh.vertices;
        int[] triangles = ResolveTriangles(mesh, targetSubmesh);
        if (vertices == null || triangles == null || triangles.Length < 3)
        {
            return shards;
        }

        baseDepth = Mathf.Clamp(baseDepth, 0, 3);
        splitJitter = Mathf.Clamp(splitJitter, 0f, 0.35f);
        float safeRadius = Mathf.Max(0.0001f, impactInfluenceRadius);
        System.Random random = splitJitter > 0f ? new System.Random(seed) : null;
        for (int i = 0; i <= triangles.Length - 3; i += 3)
        {
            Vector3 a = vertices[triangles[i]];
            Vector3 b = vertices[triangles[i + 1]];
            Vector3 c = vertices[triangles[i + 2]];
            Vector3 centroid = (a + b + c) / 3f;
            float distance = Vector3.Distance(centroid, impactPointLocal);
            float t = Mathf.Clamp01(distance / safeRadius);
            int delta = Mathf.RoundToInt(Mathf.Lerp(1f, -1f, t));
            int depth = Mathf.Clamp(baseDepth + delta, 0, 3);
            SubdivideTriangle(new TriangleShard(a, b, c), depth, splitJitter, random, shards);
        }

        return shards;
    }

    public static int ResolveSafeSubdivisionDepth(Mesh mesh, int requestedDepth, int targetSubmesh, int maxShardBudget)
    {
        if (mesh == null || maxShardBudget <= 0)
        {
            return Mathf.Clamp(requestedDepth, 0, 3);
        }

        int[] triangles = ResolveTriangles(mesh, targetSubmesh);
        if (triangles == null || triangles.Length < 3)
        {
            return Mathf.Clamp(requestedDepth, 0, 3);
        }

        int triangleCount = triangles.Length / 3;
        int depth = Mathf.Clamp(requestedDepth, 0, 3);
        while (depth > 0)
        {
            long projected = triangleCount * (long)Mathf.RoundToInt(Mathf.Pow(4f, depth));
            if (projected <= maxShardBudget)
            {
                break;
            }

            depth--;
        }

        return depth;
    }

    public static Mesh CreateMeshWithoutSubmesh(Mesh sourceMesh, int removedSubmesh)
    {
        if (sourceMesh == null || removedSubmesh < 0 || removedSubmesh >= sourceMesh.subMeshCount || sourceMesh.subMeshCount <= 1)
        {
            return null;
        }

        Mesh mesh = new Mesh
        {
            name = sourceMesh.name + "_WithoutSubmesh_" + removedSubmesh
        };
        mesh.vertices = sourceMesh.vertices;
        mesh.normals = sourceMesh.normals;
        mesh.tangents = sourceMesh.tangents;
        mesh.colors = sourceMesh.colors;
        mesh.uv = sourceMesh.uv;
        mesh.uv2 = sourceMesh.uv2;
        mesh.uv3 = sourceMesh.uv3;
        mesh.uv4 = sourceMesh.uv4;
        mesh.subMeshCount = sourceMesh.subMeshCount - 1;

        int writeIndex = 0;
        for (int i = 0; i < sourceMesh.subMeshCount; i++)
        {
            if (i == removedSubmesh)
            {
                continue;
            }

            mesh.SetTriangles(sourceMesh.GetTriangles(i), writeIndex++);
        }

        mesh.RecalculateBounds();
        if (mesh.normals == null || mesh.normals.Length == 0)
        {
            mesh.RecalculateNormals();
        }

        return mesh;
    }

    public static Mesh CreateShardMesh(Vector3 worldA, Vector3 worldB, Vector3 worldC, float thickness)
    {
        Vector3 center = (worldA + worldB + worldC) / 3f;
        Vector3 normal = Vector3.Cross(worldB - worldA, worldC - worldA).normalized;
        if (normal.sqrMagnitude <= 0.0001f)
        {
            normal = Vector3.forward;
        }

        float halfThickness = Mathf.Max(0.001f, thickness) * 0.5f;
        Vector3[] vertices =
        {
            worldA - center + normal * halfThickness,
            worldB - center + normal * halfThickness,
            worldC - center + normal * halfThickness,
            worldA - center - normal * halfThickness,
            worldB - center - normal * halfThickness,
            worldC - center - normal * halfThickness
        };

        int[] triangles =
        {
            0, 1, 2,
            5, 4, 3,
            0, 3, 4,
            0, 4, 1,
            1, 4, 5,
            1, 5, 2,
            2, 5, 3,
            2, 3, 0
        };

        Vector2[] uv =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 1f)
        };

        Mesh mesh = new Mesh
        {
            name = "ShatterShard"
        };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static List<BoundsChunk> CreateBoundsChunks(Bounds bounds, int preferredChunkCount, float splitJitter, int seed)
    {
        List<BoundsChunk> chunks = new List<BoundsChunk>();
        Vector3 size = bounds.size;
        if (size.x <= 0f || size.y <= 0f || size.z <= 0f)
        {
            return chunks;
        }

        int targetChunkCount = Mathf.Max(1, preferredChunkCount);
        Vector3Int grid = ResolveChunkGrid(size, targetChunkCount);
        System.Random random = splitJitter > 0f ? new System.Random(seed) : null;
        List<AxisSegment> xSegments = BuildAxisSegments(bounds.min.x, bounds.max.x, grid.x, splitJitter, random);
        List<AxisSegment> ySegments = BuildAxisSegments(bounds.min.y, bounds.max.y, grid.y, splitJitter, random);
        List<AxisSegment> zSegments = BuildAxisSegments(bounds.min.z, bounds.max.z, grid.z, splitJitter, random);

        for (int x = 0; x < xSegments.Count; x++)
        {
            for (int y = 0; y < ySegments.Count; y++)
            {
                for (int z = 0; z < zSegments.Count; z++)
                {
                    Vector3 chunkSize = new Vector3(xSegments[x].Length, ySegments[y].Length, zSegments[z].Length);
                    if (chunkSize.x <= 0.0001f || chunkSize.y <= 0.0001f || chunkSize.z <= 0.0001f)
                    {
                        continue;
                    }

                    Vector3 center = new Vector3(
                        (xSegments[x].Min + xSegments[x].Max) * 0.5f,
                        (ySegments[y].Min + ySegments[y].Max) * 0.5f,
                        (zSegments[z].Min + zSegments[z].Max) * 0.5f);
                    chunks.Add(new BoundsChunk(center, chunkSize));
                }
            }
        }

        return LimitBoundsChunkCount(chunks, targetChunkCount, seed ^ 165041);
    }

    public static Mesh CreateBoxChunkMesh(
        Vector3 worldCenter,
        Vector3 axisX,
        Vector3 axisY,
        Vector3 axisZ,
        float cornerJitter,
        System.Random random)
    {
        Vector3[] corners =
        {
            worldCenter + ResolveCornerOffset(-axisX, -axisY, -axisZ, cornerJitter, random),
            worldCenter + ResolveCornerOffset(axisX, -axisY, -axisZ, cornerJitter, random),
            worldCenter + ResolveCornerOffset(axisX, axisY, -axisZ, cornerJitter, random),
            worldCenter + ResolveCornerOffset(-axisX, axisY, -axisZ, cornerJitter, random),
            worldCenter + ResolveCornerOffset(-axisX, -axisY, axisZ, cornerJitter, random),
            worldCenter + ResolveCornerOffset(axisX, -axisY, axisZ, cornerJitter, random),
            worldCenter + ResolveCornerOffset(axisX, axisY, axisZ, cornerJitter, random),
            worldCenter + ResolveCornerOffset(-axisX, axisY, axisZ, cornerJitter, random)
        };

        Vector3[] vertices =
        {
            corners[4] - worldCenter, corners[5] - worldCenter, corners[6] - worldCenter, corners[7] - worldCenter,
            corners[1] - worldCenter, corners[0] - worldCenter, corners[3] - worldCenter, corners[2] - worldCenter,
            corners[0] - worldCenter, corners[4] - worldCenter, corners[7] - worldCenter, corners[3] - worldCenter,
            corners[5] - worldCenter, corners[1] - worldCenter, corners[2] - worldCenter, corners[6] - worldCenter,
            corners[7] - worldCenter, corners[6] - worldCenter, corners[2] - worldCenter, corners[3] - worldCenter,
            corners[0] - worldCenter, corners[1] - worldCenter, corners[5] - worldCenter, corners[4] - worldCenter
        };

        int[] triangles =
        {
            0, 1, 2, 0, 2, 3,
            4, 5, 6, 4, 6, 7,
            8, 9, 10, 8, 10, 11,
            12, 13, 14, 12, 14, 15,
            16, 17, 18, 16, 18, 19,
            20, 21, 22, 20, 22, 23
        };

        Vector2[] uv =
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
        };

        Mesh mesh = new Mesh
        {
            name = "ShatterChunk"
        };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector3 ResolveCornerOffset(
        Vector3 axisX,
        Vector3 axisY,
        Vector3 axisZ,
        float cornerJitter,
        System.Random random)
    {
        if (random == null || cornerJitter <= 0f)
        {
            return axisX + axisY + axisZ;
        }

        float minScale = Mathf.Max(0.55f, 1f - cornerJitter);
        float maxScale = 1f + cornerJitter;
        float scaleX = Mathf.Lerp(minScale, maxScale, (float)random.NextDouble());
        float scaleY = Mathf.Lerp(minScale, maxScale, (float)random.NextDouble());
        float scaleZ = Mathf.Lerp(minScale, maxScale, (float)random.NextDouble());
        return axisX * scaleX + axisY * scaleY + axisZ * scaleZ;
    }

    public static List<TriangleShard> LimitShardCount(List<TriangleShard> shards, int maxShardCount, int seed)
    {
        if (shards == null || shards.Count == 0 || maxShardCount <= 0 || shards.Count <= maxShardCount)
        {
            return shards;
        }

        List<TriangleShard> reducedShards = new List<TriangleShard>(maxShardCount);
        System.Random random = new System.Random(seed);
        float step = (float)shards.Count / maxShardCount;
        float cursor = (float)random.NextDouble() * Mathf.Max(1f, step);

        for (int i = 0; i < maxShardCount; i++)
        {
            int sourceIndex = Mathf.Clamp(Mathf.FloorToInt(cursor), 0, shards.Count - 1);
            reducedShards.Add(shards[sourceIndex]);
            cursor += step;
        }

        return reducedShards;
    }

    public static List<BoundsChunk> LimitBoundsChunkCount(List<BoundsChunk> chunks, int maxChunkCount, int seed)
    {
        if (chunks == null || chunks.Count == 0 || maxChunkCount <= 0 || chunks.Count <= maxChunkCount)
        {
            return chunks;
        }

        List<BoundsChunk> reducedChunks = new List<BoundsChunk>(maxChunkCount);
        System.Random random = new System.Random(seed);
        float step = (float)chunks.Count / maxChunkCount;
        float cursor = (float)random.NextDouble() * Mathf.Max(1f, step);

        for (int i = 0; i < maxChunkCount; i++)
        {
            int sourceIndex = Mathf.Clamp(Mathf.FloorToInt(cursor), 0, chunks.Count - 1);
            reducedChunks.Add(chunks[sourceIndex]);
            cursor += step;
        }

        return reducedChunks;
    }

    private static void SubdivideTriangle(
        TriangleShard triangle,
        int remainingDepth,
        float splitJitter,
        System.Random random,
        List<TriangleShard> shards)
    {
        if (remainingDepth <= 0)
        {
            shards.Add(triangle);
            return;
        }

        Vector3 midAB = Vector3.Lerp(triangle.A, triangle.B, GetSplitRatio(random, splitJitter));
        Vector3 midBC = Vector3.Lerp(triangle.B, triangle.C, GetSplitRatio(random, splitJitter));
        Vector3 midCA = Vector3.Lerp(triangle.C, triangle.A, GetSplitRatio(random, splitJitter));

        int nextDepth = remainingDepth - 1;
        SubdivideTriangle(new TriangleShard(triangle.A, midAB, midCA), nextDepth, splitJitter, random, shards);
        SubdivideTriangle(new TriangleShard(midAB, triangle.B, midBC), nextDepth, splitJitter, random, shards);
        SubdivideTriangle(new TriangleShard(midCA, midBC, triangle.C), nextDepth, splitJitter, random, shards);
        SubdivideTriangle(new TriangleShard(midAB, midBC, midCA), nextDepth, splitJitter, random, shards);
    }

    private static float GetSplitRatio(System.Random random, float splitJitter)
    {
        if (random == null || splitJitter <= 0f)
        {
            return 0.5f;
        }

        float min = 0.5f - splitJitter;
        float max = 0.5f + splitJitter;
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private readonly struct AxisSegment
    {
        public AxisSegment(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public float Min { get; }
        public float Max { get; }
        public float Length => Mathf.Max(0f, Max - Min);
    }

    private static Vector3Int ResolveChunkGrid(Vector3 size, int targetChunkCount)
    {
        Vector3 safeSize = new Vector3(
            Mathf.Max(0.001f, size.x),
            Mathf.Max(0.001f, size.y),
            Mathf.Max(0.001f, size.z));
        float volume = safeSize.x * safeSize.y * safeSize.z;
        float density = Mathf.Pow(targetChunkCount / volume, 1f / 3f);
        Vector3 scaledCounts = safeSize * density;

        Vector3Int grid = new Vector3Int(
            Mathf.Max(1, Mathf.RoundToInt(scaledCounts.x)),
            Mathf.Max(1, Mathf.RoundToInt(scaledCounts.y)),
            Mathf.Max(1, Mathf.RoundToInt(scaledCounts.z)));

        while (grid.x * grid.y * grid.z < targetChunkCount)
        {
            float cellX = safeSize.x / grid.x;
            float cellY = safeSize.y / grid.y;
            float cellZ = safeSize.z / grid.z;

            if (cellX >= cellY && cellX >= cellZ)
            {
                grid.x++;
            }
            else if (cellY >= cellZ)
            {
                grid.y++;
            }
            else
            {
                grid.z++;
            }
        }

        return grid;
    }

    private static List<AxisSegment> BuildAxisSegments(float min, float max, int segmentCount, float splitJitter, System.Random random)
    {
        List<AxisSegment> segments = new List<AxisSegment>(Mathf.Max(1, segmentCount));
        float length = max - min;
        if (segmentCount <= 1 || length <= 0f)
        {
            segments.Add(new AxisSegment(min, max));
            return segments;
        }

        float[] weights = new float[segmentCount];
        float weightSum = 0f;
        for (int i = 0; i < segmentCount; i++)
        {
            float weight = 1f;
            if (random != null && splitJitter > 0f)
            {
                float jitter = Mathf.Lerp(-splitJitter, splitJitter, (float)random.NextDouble());
                weight = Mathf.Max(0.15f, 1f + jitter);
            }

            weights[i] = weight;
            weightSum += weight;
        }

        float cursor = min;
        for (int i = 0; i < segmentCount; i++)
        {
            float segmentLength = i == segmentCount - 1
                ? max - cursor
                : length * (weights[i] / weightSum);
            float next = cursor + segmentLength;
            segments.Add(new AxisSegment(cursor, next));
            cursor = next;
        }

        return segments;
    }

    private static int[] ResolveTriangles(Mesh mesh, int targetSubmesh)
    {
        if (mesh == null)
        {
            return null;
        }

        if (targetSubmesh >= 0 && targetSubmesh < mesh.subMeshCount)
        {
            return mesh.GetTriangles(targetSubmesh);
        }

        return mesh.triangles;
    }
}
