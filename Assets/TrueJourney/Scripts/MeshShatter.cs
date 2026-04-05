using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class MeshShatter : MonoBehaviour
{
    public enum ShatterPreset
    {
        Subtle,
        Standard,
        Chaotic,
        Chunky,
        Custom
    }

    [Header("Preset")]
    [SerializeField] private ShatterPreset preset = ShatterPreset.Standard;
    [SerializeField] private bool usePresetValues = true;

    [Header("References")]
    [SerializeField] private MeshFilter targetMeshFilter;
    [SerializeField] private MeshRenderer targetRenderer;
    [SerializeField] private Collider[] collidersToDisable;
    [SerializeField] private Transform shardContainer;
    [SerializeField] private Material overrideMaterial;
    [SerializeField] private int targetSubmesh = -1;
    [SerializeField] private bool keepNonShatteredSubmeshes = true;

    [Header("Shard Generation")]
    [SerializeField] [Range(0, 3)] private int subdivisionDepth = 2;
    [SerializeField] [Range(0f, 0.35f)] private float splitJitter = 0.18f;
    [SerializeField] private bool randomizeShardPattern = true;
    [SerializeField] private int shardPatternSeed = 12345;
    [SerializeField] private float shardThickness = 0.01f;
    [SerializeField] private float shardMass = 0.05f;

    [Header("Force")]
    [SerializeField] private float impulseStrength = 2.5f;
    [SerializeField] [Range(0f, 2f)] private float impactDirectionWeight = 1.35f;
    [FormerlySerializedAs("outwardBias")]
    [SerializeField] [Range(0f, 1f)] private float surfaceNormalWeight = 0.35f;
    [SerializeField] [Range(0f, 1f)] private float impactSpread = 0.28f;
    [SerializeField] private float randomImpulse = 0.4f;
    [SerializeField] private float randomTorque = 0.15f;

    [Header("Cleanup")]
    [SerializeField] private float destroyAfterSeconds = 8f;
    [SerializeField] private bool disableOriginalRenderer = true;
    [SerializeField] private bool disableConfiguredColliders = true;

    [SerializeField] private bool isShattered;

    public bool IsShattered => isShattered;

    private void Awake()
    {
        ApplyPresetIfNeeded();
        ResolveReferences();
    }

    private void OnValidate()
    {
        ApplyPresetIfNeeded();
        subdivisionDepth = Mathf.Clamp(subdivisionDepth, 0, 3);
        splitJitter = Mathf.Clamp(splitJitter, 0f, 0.35f);
        targetSubmesh = Mathf.Max(-1, targetSubmesh);
        shardThickness = Mathf.Max(0.001f, shardThickness);
        shardMass = Mathf.Max(0.001f, shardMass);
        impulseStrength = Mathf.Max(0f, impulseStrength);
        impactDirectionWeight = Mathf.Max(0f, impactDirectionWeight);
        surfaceNormalWeight = Mathf.Clamp01(surfaceNormalWeight);
        impactSpread = Mathf.Clamp01(impactSpread);
        randomImpulse = Mathf.Max(0f, randomImpulse);
        randomTorque = Mathf.Max(0f, randomTorque);
        destroyAfterSeconds = Mathf.Max(0f, destroyAfterSeconds);

        if (!Application.isPlaying)
        {
            ResolveReferences();
        }
    }

    public void Shatter()
    {
        Shatter(transform.position, transform.forward, 1f);
    }

    public void Shatter(Vector3 impactPoint, Vector3 impactDirection, float forceMultiplier = 1f)
    {
        if (isShattered)
        {
            return;
        }

        ResolveReferences();

        Mesh sourceMesh = targetMeshFilter != null ? targetMeshFilter.sharedMesh : null;
        if (sourceMesh == null)
        {
            return;
        }

        if (!sourceMesh.isReadable)
        {
            Debug.LogWarning(
                $"MeshShatter on '{name}' requires a readable mesh. Enable Read/Write on '{sourceMesh.name}'.",
                this);
            return;
        }

        int patternSeed = randomizeShardPattern
            ? UnityEngine.Random.Range(int.MinValue, int.MaxValue)
            : shardPatternSeed;
        List<MeshShatterUtility.TriangleShard> shards;
        try
        {
            shards = MeshShatterUtility.CreateTriangleShards(
                sourceMesh,
                subdivisionDepth,
                splitJitter,
                patternSeed,
                targetSubmesh);
        }
        catch (UnityException exception)
        {
            Debug.LogWarning(
                $"MeshShatter could not read mesh data on '{name}'. Enable Read/Write on '{sourceMesh.name}'. {exception.Message}",
                this);
            return;
        }

        if (shards.Count == 0)
        {
            return;
        }

        Material[] shardMaterials = ResolveShardMaterials();
        Transform shardRoot = CreateShardRoot();
        Transform meshTransform = targetMeshFilter.transform;
        Vector3 fallbackDirection = impactDirection.sqrMagnitude > 0.001f
            ? impactDirection.normalized
            : (meshTransform.forward.sqrMagnitude > 0.001f ? meshTransform.forward.normalized : Vector3.forward);

        for (int i = 0; i < shards.Count; i++)
        {
            SpawnShard(shards[i], i, shardRoot, shardMaterials, meshTransform, impactPoint, fallbackDirection, forceMultiplier);
        }

        bool preservedRemainingMesh = TryPreserveRemainingMesh(sourceMesh);
        DisableOriginalState(preservedRemainingMesh);
        isShattered = true;
    }

    [ContextMenu("Apply Preset Values")]
    private void ApplyPresetValuesContextMenu()
    {
        ApplyPresetValues();
    }

    private void ResolveReferences()
    {
        if (targetMeshFilter == null)
        {
            targetMeshFilter = GetComponent<MeshFilter>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<MeshRenderer>();
        }

        if ((collidersToDisable == null || collidersToDisable.Length == 0) && TryGetComponent(out Collider attachedCollider))
        {
            collidersToDisable = new[] { attachedCollider };
        }
    }

    private void ApplyPresetIfNeeded()
    {
        if (!usePresetValues || preset == ShatterPreset.Custom)
        {
            return;
        }

        ApplyPresetValues();
    }

    private void ApplyPresetValues()
    {
        switch (preset)
        {
            case ShatterPreset.Subtle:
                subdivisionDepth = 1;
                splitJitter = 0.12f;
                shardThickness = 0.01f;
                shardMass = 0.06f;
                randomizeShardPattern = true;
                break;

            case ShatterPreset.Standard:
                subdivisionDepth = 1;
                splitJitter = 0.2f;
                shardThickness = 0.01f;
                shardMass = 0.05f;
                randomizeShardPattern = true;
                break;

            case ShatterPreset.Chaotic:
                subdivisionDepth = 2;
                splitJitter = 0.28f;
                shardThickness = 0.009f;
                shardMass = 0.04f;
                randomizeShardPattern = true;
                break;

            case ShatterPreset.Chunky:
                subdivisionDepth = 1;
                splitJitter = 0.1f;
                shardThickness = 0.018f;
                shardMass = 0.09f;
                randomizeShardPattern = true;
                break;

            case ShatterPreset.Custom:
            default:
                break;
        }
    }

    private Material[] ResolveShardMaterials()
    {
        if (overrideMaterial != null)
        {
            return new[] { overrideMaterial };
        }

        if (targetRenderer != null && targetRenderer.sharedMaterials != null && targetRenderer.sharedMaterials.Length > 0)
        {
            if (targetSubmesh >= 0 && targetSubmesh < targetRenderer.sharedMaterials.Length)
            {
                return new[] { targetRenderer.sharedMaterials[targetSubmesh] };
            }

            return new[] { targetRenderer.sharedMaterials[0] };
        }

        return new Material[0];
    }

    private Transform CreateShardRoot()
    {
        GameObject shardRootObject = new GameObject(name + "_Shards");
        Transform shardRootTransform = shardRootObject.transform;

        Transform parent = shardContainer;
        shardRootTransform.SetPositionAndRotation(transform.position, transform.rotation);
        if (parent != null)
        {
            shardRootTransform.SetParent(parent, true);
        }

        shardRootTransform.localScale = Vector3.one;
        return shardRootTransform;
    }

    private void SpawnShard(
        MeshShatterUtility.TriangleShard shard,
        int index,
        Transform shardRoot,
        Material[] shardMaterials,
        Transform meshTransform,
        Vector3 impactPoint,
        Vector3 fallbackDirection,
        float forceMultiplier)
    {
        Vector3 worldA = meshTransform.TransformPoint(shard.A);
        Vector3 worldB = meshTransform.TransformPoint(shard.B);
        Vector3 worldC = meshTransform.TransformPoint(shard.C);
        Vector3 center = (worldA + worldB + worldC) / 3f;
        Vector3 normal = Vector3.Cross(worldB - worldA, worldC - worldA).normalized;
        if (normal.sqrMagnitude <= 0.0001f)
        {
            normal = fallbackDirection;
        }

        Mesh shardMesh = MeshShatterUtility.CreateShardMesh(worldA, worldB, worldC, shardThickness);

        GameObject shardObject = new GameObject($"{name}_Shard_{index}");
        shardObject.transform.SetPositionAndRotation(center, Quaternion.identity);
        shardObject.transform.SetParent(shardRoot, true);

        MeshFilter shardFilter = shardObject.AddComponent<MeshFilter>();
        shardFilter.sharedMesh = shardMesh;

        MeshRenderer shardRenderer = shardObject.AddComponent<MeshRenderer>();
        if (shardMaterials.Length > 0)
        {
            shardRenderer.sharedMaterials = shardMaterials;
        }

        BoxCollider shardCollider = shardObject.AddComponent<BoxCollider>();
        Bounds bounds = shardMesh.bounds;
        Vector3 colliderSize = bounds.size;
        colliderSize.x = Mathf.Max(colliderSize.x, shardThickness);
        colliderSize.y = Mathf.Max(colliderSize.y, shardThickness);
        colliderSize.z = Mathf.Max(colliderSize.z, shardThickness);
        shardCollider.center = bounds.center;
        shardCollider.size = colliderSize;

        Rigidbody rigidbody = shardObject.AddComponent<Rigidbody>();
        rigidbody.mass = shardMass;

        Vector3 impulseDirection = ResolveImpulseDirection(center, impactPoint, fallbackDirection, normal);
        if (impulseDirection.sqrMagnitude <= 0.0001f)
        {
            impulseDirection = fallbackDirection;
        }

        rigidbody.AddForce(impulseDirection * impulseStrength * Mathf.Max(0f, forceMultiplier), ForceMode.Impulse);
        rigidbody.AddTorque(Random.insideUnitSphere * randomTorque * Mathf.Max(0f, forceMultiplier), ForceMode.Impulse);

        if (destroyAfterSeconds > 0f)
        {
            Destroy(shardObject, destroyAfterSeconds);
        }
    }

    private bool TryPreserveRemainingMesh(Mesh sourceMesh)
    {
        if (!keepNonShatteredSubmeshes || sourceMesh == null || targetMeshFilter == null)
        {
            return false;
        }

        if (targetSubmesh < 0 || targetSubmesh >= sourceMesh.subMeshCount || sourceMesh.subMeshCount <= 1)
        {
            return false;
        }

        Mesh preservedMesh = MeshShatterUtility.CreateMeshWithoutSubmesh(sourceMesh, targetSubmesh);
        if (preservedMesh == null)
        {
            return false;
        }

        targetMeshFilter.mesh = preservedMesh;

        if (targetRenderer != null && targetRenderer.sharedMaterials != null && targetRenderer.sharedMaterials.Length > 0)
        {
            Material[] sourceMaterials = targetRenderer.sharedMaterials;
            List<Material> preservedMaterials = new List<Material>(Mathf.Max(0, sourceMaterials.Length - 1));
            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                if (i != targetSubmesh)
                {
                    preservedMaterials.Add(sourceMaterials[i]);
                }
            }

            targetRenderer.sharedMaterials = preservedMaterials.ToArray();
        }

        return true;
    }

    private void DisableOriginalState(bool preservedRemainingMesh)
    {
        if (disableOriginalRenderer && targetRenderer != null && !preservedRemainingMesh)
        {
            targetRenderer.enabled = false;
        }

        if (!disableConfiguredColliders || collidersToDisable == null)
        {
            return;
        }

        for (int i = 0; i < collidersToDisable.Length; i++)
        {
            if (collidersToDisable[i] != null)
            {
                collidersToDisable[i].enabled = false;
            }
        }
    }

    private Vector3 ResolveImpulseDirection(Vector3 shardCenter, Vector3 impactPoint, Vector3 impactDirection, Vector3 shardNormal)
    {
        Vector3 resolvedImpactDirection = impactDirection.sqrMagnitude > 0.0001f
            ? impactDirection.normalized
            : transform.forward;
        if (resolvedImpactDirection.sqrMagnitude <= 0.0001f)
        {
            resolvedImpactDirection = Vector3.forward;
        }

        Vector3 orientedNormal = Vector3.Dot(shardNormal, resolvedImpactDirection) >= 0f
            ? shardNormal
            : -shardNormal;
        Vector3 impactOffset = shardCenter - impactPoint;
        Vector3 lateralSpread = impactOffset.sqrMagnitude > 0.001f
            ? Vector3.ProjectOnPlane(impactOffset.normalized, resolvedImpactDirection)
            : Vector3.zero;
        if (lateralSpread.sqrMagnitude > 0.0001f)
        {
            lateralSpread.Normalize();
        }

        Vector3 randomDirection = Random.insideUnitSphere;
        Vector3 combinedDirection =
            resolvedImpactDirection * impactDirectionWeight +
            orientedNormal * surfaceNormalWeight +
            lateralSpread * impactSpread +
            randomDirection * randomImpulse;

        return combinedDirection.sqrMagnitude > 0.0001f
            ? combinedDirection.normalized
            : resolvedImpactDirection;
    }
}

public static class MeshShatterUtility
{
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
