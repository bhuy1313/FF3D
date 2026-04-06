using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MeshShatter : MonoBehaviour
{
    public enum ShatterMode
    {
        SurfaceShards,
        DebrisChunks
    }

    public enum ShatterPreset
    {
        Subtle,
        Standard,
        Chaotic,
        Chunky,
        Custom
    }

    [Header("Mode")]
    [SerializeField] private ShatterMode shatterMode = ShatterMode.SurfaceShards;

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
    [SerializeField] [Range(0f, 1f)] private float shardThicknessVariation = 0.1f;
    [SerializeField] [Range(0f, 1f)] private float shardScaleVariation = 0f;
    [SerializeField] [Range(0.25f, 2f)] private float shardSizeMultiplier = 1f;

    [Header("Optimization")]
    [Tooltip("Maximum number of shard GameObjects to spawn. Set to 0 for no cap.")]
    [SerializeField] private int maxSpawnedShards = 0;

    [Header("Auto Tuning")]
    [Tooltip("Derive shard count and per-shard rigidbody mass from the source object's mass.")]
    [SerializeField] private bool autoTuneFromSourceMass = false;
    [Tooltip("Optional override when no IMovementWeightSource is available. Set to 0 to resolve mass from the hierarchy.")]
    [SerializeField] private float sourceMassOverrideKg = 0f;

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
        shardThicknessVariation = Mathf.Clamp01(shardThicknessVariation);
        shardScaleVariation = Mathf.Clamp01(shardScaleVariation);
        shardSizeMultiplier = Mathf.Clamp(shardSizeMultiplier, 0.25f, 2f);
        maxSpawnedShards = Mathf.Max(0, maxSpawnedShards);
        sourceMassOverrideKg = Mathf.Max(0f, sourceMassOverrideKg);
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

        int patternSeed = randomizeShardPattern
            ? UnityEngine.Random.Range(int.MinValue, int.MaxValue)
            : shardPatternSeed;
        Material[] shardMaterials = ResolveShardMaterials();
        Transform shardRoot = CreateShardRoot();
        Transform meshTransform = targetMeshFilter.transform;
        System.Random shardRandom = new System.Random(patternSeed ^ 13996801);
        int preferredShardCount = ResolvePreferredShardCount();

        if (shatterMode == ShatterMode.DebrisChunks)
        {
            List<MeshShatterUtility.BoundsChunk> chunks = MeshShatterUtility.CreateBoundsChunks(
                sourceMesh.bounds,
                preferredShardCount,
                splitJitter,
                patternSeed);
            if (chunks.Count == 0)
            {
                return;
            }

            float resolvedShardMass = ResolveRuntimeShardMass(chunks.Count);
            for (int i = 0; i < chunks.Count; i++)
            {
                SpawnDebrisChunk(
                    chunks[i],
                    i,
                    shardRoot,
                    shardMaterials,
                    meshTransform,
                    resolvedShardMass,
                    shardRandom);
            }
        }
        else
        {
            if (!sourceMesh.isReadable)
            {
                Debug.LogWarning(
                    $"MeshShatter on '{name}' requires a readable mesh. Enable Read/Write on '{sourceMesh.name}'.",
                    this);
                return;
            }

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

            shards = MeshShatterUtility.LimitShardCount(shards, preferredShardCount, patternSeed ^ 486187739);
            if (shards.Count == 0)
            {
                return;
            }

            float resolvedShardMass = ResolveRuntimeShardMass(shards.Count);
            for (int i = 0; i < shards.Count; i++)
            {
                SpawnShard(
                    shards[i],
                    i,
                    shardRoot,
                    shardMaterials,
                    meshTransform,
                    resolvedShardMass,
                    shardRandom);
            }
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
                ApplyModeAwarePresetValues(
                    surfaceSubdivisionDepth: 1,
                    surfaceSplitJitter: 0.12f,
                    surfaceShardThickness: 0.01f,
                    surfaceShardMass: 0.06f,
                    surfaceMaxSpawnedShards: 0,
                    surfaceShardThicknessVariation: 0.08f,
                    surfaceShardScaleVariation: 0f,
                    debrisSubdivisionDepth: 1,
                    debrisSplitJitter: 0.08f,
                    debrisShardThickness: 0.05f,
                    debrisShardMass: 0.14f,
                    debrisMaxSpawnedShards: 16,
                    debrisShardThicknessVariation: 0.25f,
                    debrisShardScaleVariation: 0.12f);
                break;

            case ShatterPreset.Standard:
                ApplyModeAwarePresetValues(
                    surfaceSubdivisionDepth: 1,
                    surfaceSplitJitter: 0.2f,
                    surfaceShardThickness: 0.01f,
                    surfaceShardMass: 0.05f,
                    surfaceMaxSpawnedShards: 0,
                    surfaceShardThicknessVariation: 0.1f,
                    surfaceShardScaleVariation: 0f,
                    debrisSubdivisionDepth: 1,
                    debrisSplitJitter: 0.12f,
                    debrisShardThickness: 0.08f,
                    debrisShardMass: 0.18f,
                    debrisMaxSpawnedShards: 24,
                    debrisShardThicknessVariation: 0.35f,
                    debrisShardScaleVariation: 0.18f);
                break;

            case ShatterPreset.Chaotic:
                ApplyModeAwarePresetValues(
                    surfaceSubdivisionDepth: 2,
                    surfaceSplitJitter: 0.28f,
                    surfaceShardThickness: 0.009f,
                    surfaceShardMass: 0.04f,
                    surfaceMaxSpawnedShards: 0,
                    surfaceShardThicknessVariation: 0.14f,
                    surfaceShardScaleVariation: 0f,
                    debrisSubdivisionDepth: 2,
                    debrisSplitJitter: 0.18f,
                    debrisShardThickness: 0.09f,
                    debrisShardMass: 0.2f,
                    debrisMaxSpawnedShards: 40,
                    debrisShardThicknessVariation: 0.45f,
                    debrisShardScaleVariation: 0.25f);
                break;

            case ShatterPreset.Chunky:
                ApplyModeAwarePresetValues(
                    surfaceSubdivisionDepth: 1,
                    surfaceSplitJitter: 0.1f,
                    surfaceShardThickness: 0.018f,
                    surfaceShardMass: 0.09f,
                    surfaceMaxSpawnedShards: 0,
                    surfaceShardThicknessVariation: 0.12f,
                    surfaceShardScaleVariation: 0.04f,
                    debrisSubdivisionDepth: 1,
                    debrisSplitJitter: 0.26f,
                    debrisShardThickness: 0.14f,
                    debrisShardMass: 0.28f,
                    debrisMaxSpawnedShards: 14,
                    debrisShardThicknessVariation: 0.5f,
                    debrisShardScaleVariation: 0.45f);
                break;

            case ShatterPreset.Custom:
            default:
                break;
        }
    }

    private void ApplyModeAwarePresetValues(
        int surfaceSubdivisionDepth,
        float surfaceSplitJitter,
        float surfaceShardThickness,
        float surfaceShardMass,
        int surfaceMaxSpawnedShards,
        float surfaceShardThicknessVariation,
        float surfaceShardScaleVariation,
        int debrisSubdivisionDepth,
        float debrisSplitJitter,
        float debrisShardThickness,
        float debrisShardMass,
        int debrisMaxSpawnedShards,
        float debrisShardThicknessVariation,
        float debrisShardScaleVariation)
    {
        randomizeShardPattern = true;

        if (shatterMode == ShatterMode.DebrisChunks)
        {
            subdivisionDepth = debrisSubdivisionDepth;
            splitJitter = debrisSplitJitter;
            shardThickness = debrisShardThickness;
            shardMass = debrisShardMass;
            maxSpawnedShards = debrisMaxSpawnedShards;
            shardThicknessVariation = debrisShardThicknessVariation;
            shardScaleVariation = debrisShardScaleVariation;
            return;
        }

        subdivisionDepth = surfaceSubdivisionDepth;
        splitJitter = surfaceSplitJitter;
        shardThickness = surfaceShardThickness;
        shardMass = surfaceShardMass;
        maxSpawnedShards = surfaceMaxSpawnedShards;
        shardThicknessVariation = surfaceShardThicknessVariation;
        shardScaleVariation = surfaceShardScaleVariation;
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

    private int ResolvePreferredShardCount()
    {
        float sourceMassKg = autoTuneFromSourceMass
            ? ResolveSourceMassKg()
            : 0f;
        if (sourceMassKg > 0f)
        {
            return ApplySizeMultiplierToPreferredCount(ResolveAutoShardTargetCount(sourceMassKg));
        }

        if (maxSpawnedShards > 0)
        {
            return ApplySizeMultiplierToPreferredCount(maxSpawnedShards);
        }

        if (shatterMode != ShatterMode.DebrisChunks)
        {
            return 0;
        }

        return ApplySizeMultiplierToPreferredCount(ResolveDefaultDebrisChunkCount());
    }

    private int ApplySizeMultiplierToPreferredCount(int preferredCount)
    {
        if (preferredCount <= 0)
        {
            return preferredCount;
        }

        float countExponent = shatterMode == ShatterMode.DebrisChunks ? 2f : 1f;
        float adjustedCount = preferredCount / Mathf.Pow(shardSizeMultiplier, countExponent);
        return Mathf.Max(1, Mathf.RoundToInt(adjustedCount));
    }

    private float ResolveSourceMassKg()
    {
        if (sourceMassOverrideKg > 0f)
        {
            return sourceMassOverrideKg;
        }

        Transform origin = targetMeshFilter != null
            ? targetMeshFilter.transform
            : transform;
        IMovementWeightSource weightSource = FindMovementWeightSource(origin);
        if (weightSource != null)
        {
            return Mathf.Max(0f, weightSource.MovementWeightKg);
        }

        Rigidbody sourceBody = FindSourceRigidbody(origin);
        return sourceBody != null
            ? Mathf.Max(0f, sourceBody.mass)
            : 0f;
    }

    private int ResolveAutoShardTargetCount(float sourceMassKg)
    {
        float normalizedMass;
        float targetCount;

        if (shatterMode == ShatterMode.DebrisChunks)
        {
            normalizedMass = Mathf.InverseLerp(4f, 60f, sourceMassKg);
            targetCount = Mathf.Lerp(8f, 28f, normalizedMass);
        }
        else
        {
            normalizedMass = Mathf.InverseLerp(0.5f, 15f, sourceMassKg);
            targetCount = Mathf.Lerp(12f, 48f, normalizedMass);
        }

        int resolvedCount = Mathf.RoundToInt(targetCount);
        resolvedCount = Mathf.Clamp(resolvedCount, 1, shatterMode == ShatterMode.DebrisChunks ? 48 : 96);
        return resolvedCount;
    }

    private int ResolveDefaultDebrisChunkCount()
    {
        switch (subdivisionDepth)
        {
            case 0:
                return 8;

            case 1:
                return 12;

            case 2:
                return 18;

            default:
                return 27;
        }
    }

    private float ResolveRuntimeShardMass(int shardCount)
    {
        if (!autoTuneFromSourceMass)
        {
            return shardMass;
        }

        float sourceMassKg = ResolveSourceMassKg();
        return ResolveAutoShardMass(sourceMassKg, shardCount);
    }

    private float ResolveAutoShardMass(float sourceMassKg, int shardCount)
    {
        if (sourceMassKg <= 0f || shardCount <= 0)
        {
            return shardMass;
        }

        float retainedMassRatio = shatterMode == ShatterMode.DebrisChunks ? 0.55f : 0.2f;
        float minShardMass = shatterMode == ShatterMode.DebrisChunks ? 0.08f : 0.015f;
        float maxShardMass = shatterMode == ShatterMode.DebrisChunks ? 1.25f : 0.2f;
        float autoShardMass = (sourceMassKg * retainedMassRatio) / shardCount;
        return Mathf.Clamp(autoShardMass, minShardMass, maxShardMass);
    }

    private static IMovementWeightSource FindMovementWeightSource(Transform origin)
    {
        Transform current = origin;
        while (current != null)
        {
            IMovementWeightSource weightSource = current.GetComponent<IMovementWeightSource>();
            if (weightSource != null)
            {
                return weightSource;
            }

            current = current.parent;
        }

        return null;
    }

    private static Rigidbody FindSourceRigidbody(Transform origin)
    {
        Transform current = origin;
        while (current != null)
        {
            Rigidbody body = current.GetComponent<Rigidbody>();
            if (body != null)
            {
                return body;
            }

            current = current.parent;
        }

        return null;
    }

    private void SpawnDebrisChunk(
        MeshShatterUtility.BoundsChunk chunk,
        int index,
        Transform shardRoot,
        Material[] shardMaterials,
        Transform meshTransform,
        float resolvedShardMass,
        System.Random shardRandom)
    {
        Vector3 scale = ResolveShardScale(shardRandom);
        Vector3 scaledChunkSize = Vector3.Scale(chunk.Size, scale);
        Vector3 worldCenter = meshTransform.TransformPoint(chunk.Center);
        Vector3 axisX = meshTransform.TransformVector(Vector3.right * (scaledChunkSize.x * 0.5f));
        Vector3 axisY = meshTransform.TransformVector(Vector3.up * (scaledChunkSize.y * 0.5f));
        Vector3 axisZ = meshTransform.TransformVector(Vector3.forward * (scaledChunkSize.z * 0.5f));
        Mesh chunkMesh = MeshShatterUtility.CreateBoxChunkMesh(
            worldCenter,
            axisX,
            axisY,
            axisZ,
            ResolveDebrisCornerJitter(),
            shardRandom);

        GameObject shardObject = new GameObject($"{name}_Chunk_{index}");
        shardObject.transform.SetPositionAndRotation(worldCenter, Quaternion.identity);
        shardObject.transform.SetParent(shardRoot, true);

        MeshFilter shardFilter = shardObject.AddComponent<MeshFilter>();
        shardFilter.sharedMesh = chunkMesh;

        MeshRenderer shardRenderer = shardObject.AddComponent<MeshRenderer>();
        if (shardMaterials.Length > 0)
        {
            shardRenderer.sharedMaterials = shardMaterials;
        }

        BoxCollider shardCollider = shardObject.AddComponent<BoxCollider>();
        Bounds bounds = chunkMesh.bounds;
        shardCollider.center = bounds.center;
        shardCollider.size = bounds.size;

        Rigidbody rigidbody = shardObject.AddComponent<Rigidbody>();
        rigidbody.mass = resolvedShardMass;

        if (destroyAfterSeconds > 0f)
        {
            Destroy(shardObject, destroyAfterSeconds);
        }
    }

    private float ResolveDebrisCornerJitter()
    {
        if (shatterMode != ShatterMode.DebrisChunks)
        {
            return 0f;
        }

        return Mathf.Clamp(splitJitter * 1.15f, 0f, 0.32f);
    }

    private void SpawnShard(
        MeshShatterUtility.TriangleShard shard,
        int index,
        Transform shardRoot,
        Material[] shardMaterials,
        Transform meshTransform,
        float resolvedShardMass,
        System.Random shardRandom)
    {
        Vector3 worldA = meshTransform.TransformPoint(shard.A);
        Vector3 worldB = meshTransform.TransformPoint(shard.B);
        Vector3 worldC = meshTransform.TransformPoint(shard.C);
        Vector3 center = (worldA + worldB + worldC) / 3f;
        float resolvedThickness = ResolveShardThickness(shardRandom);
        Mesh shardMesh = MeshShatterUtility.CreateShardMesh(worldA, worldB, worldC, resolvedThickness);

        GameObject shardObject = new GameObject($"{name}_Shard_{index}");
        shardObject.transform.SetPositionAndRotation(center, Quaternion.identity);
        shardObject.transform.SetParent(shardRoot, true);
        shardObject.transform.localScale = ResolveShardScale(shardRandom);

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
        colliderSize.x = Mathf.Max(colliderSize.x, resolvedThickness);
        colliderSize.y = Mathf.Max(colliderSize.y, resolvedThickness);
        colliderSize.z = Mathf.Max(colliderSize.z, resolvedThickness);
        shardCollider.center = bounds.center;
        shardCollider.size = colliderSize;

        Rigidbody rigidbody = shardObject.AddComponent<Rigidbody>();
        rigidbody.mass = resolvedShardMass;

        if (destroyAfterSeconds > 0f)
        {
            Destroy(shardObject, destroyAfterSeconds);
        }
    }

    private float ResolveShardThickness(System.Random shardRandom)
    {
        float baseThickness = shardThickness * shardSizeMultiplier;
        if (shardRandom == null || shardThicknessVariation <= 0f)
        {
            return baseThickness;
        }

        float min = Mathf.Max(0.001f, baseThickness * (1f - shardThicknessVariation));
        float max = Mathf.Max(min, baseThickness * (1f + shardThicknessVariation));
        return Mathf.Lerp(min, max, (float)shardRandom.NextDouble());
    }

    private Vector3 ResolveShardScale(System.Random shardRandom)
    {
        float baseScale = shardSizeMultiplier;
        if (shardRandom == null || shardScaleVariation <= 0f)
        {
            return Vector3.one * baseScale;
        }

        float min = Mathf.Max(0.1f, baseScale * (1f - shardScaleVariation));
        float max = baseScale * (1f + shardScaleVariation);
        float uniformScale = Mathf.Lerp(min, max, (float)shardRandom.NextDouble());
        return new Vector3(uniformScale, uniformScale, uniformScale);
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

}

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
