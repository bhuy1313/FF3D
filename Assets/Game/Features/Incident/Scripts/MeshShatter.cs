using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class MeshShatter : MonoBehaviour
{
    [Serializable] public class ShatterUnityEvent : UnityEvent<Vector3, Vector3, float> { }
    [Serializable] public class ShardUnityEvent : UnityEvent<Rigidbody> { }

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

    [Header("Impact Force")]
    [Tooltip("Base impulse magnitude (kg*m/s) applied to each shard along the resolved impact direction.")]
    [SerializeField] private float impulseStrength = 2.5f;
    [Tooltip("How strongly the incoming impact direction biases the shard impulse direction.")]
    [SerializeField] private float impactDirectionWeight = 1f;
    [Tooltip("How strongly each shard's outward surface normal biases the shard impulse direction.")]
    [SerializeField] private float surfaceNormalWeight = 0.5f;
    [Tooltip("Random spread (0..1) added to the resolved impulse direction per shard.")]
    [SerializeField, Range(0f, 1f)] private float impactSpread = 0.2f;
    [Tooltip("Random impulse magnitude (0..1) blended on top of the base impulse per shard.")]
    [SerializeField, Range(0f, 1f)] private float randomImpulse = 0.35f;
    [Tooltip("Random angular velocity magnitude (rad/s) applied to each shard.")]
    [SerializeField] private float randomTorque = 1.5f;

    [Header("Spawn Pacing")]
    [Tooltip("Spawn at most this many shards per frame to avoid CPU spikes. Set to 0 to spawn all in one frame (sync).")]
    [SerializeField] private int shardsPerFrame = 0;

    [Header("Force Scaling")]
    [Tooltip("How much the incoming forceMultiplier scales shard count. 0 = no influence, 1 = full proportional scaling.")]
    [SerializeField, Range(0f, 1f)] private float forceCountInfluence = 0.4f;
    [Tooltip("Radius (mesh-local units) within which surface shards get an extra subdivision level. 0 = uniform subdivision.")]
    [SerializeField] private float impactInfluenceRadius = 0f;

    [Header("Source Inheritance")]
    [Tooltip("Copy PhysicMaterial / layer / tag from the source collider onto each shard for consistent physics & gameplay tagging.")]
    [SerializeField] private bool inheritSourceColliderProperties = true;

    [Header("Cleanup")]
    [SerializeField] private float destroyAfterSeconds = 8f;
    [SerializeField] private bool disableOriginalRenderer = true;
    [SerializeField] private bool disableConfiguredColliders = true;

    [Header("Events")]
    [SerializeField] private ShatterUnityEvent onShatter = new ShatterUnityEvent();
    [SerializeField] private ShardUnityEvent onShardSpawned = new ShardUnityEvent();

    public ShatterUnityEvent OnShatter => onShatter;
    public ShardUnityEvent OnShardSpawned => onShardSpawned;

    [SerializeField] private bool isShattered;

    private const int MaxPreLimitShardCount = 4096;

    private Vector3 currentImpactPoint;
    private Vector3 currentImpactDirection;
    private float currentForceMultiplier;

    private readonly List<Mesh> generatedMeshes = new List<Mesh>();
    private Vector3 lastShatterPoint;
    private Vector3 lastShatterDirection;
    private bool hasRecordedShatter;

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
        impulseStrength = Mathf.Max(0f, impulseStrength);
        impactDirectionWeight = Mathf.Max(0f, impactDirectionWeight);
        surfaceNormalWeight = Mathf.Max(0f, surfaceNormalWeight);
        impactSpread = Mathf.Clamp01(impactSpread);
        randomImpulse = Mathf.Clamp01(randomImpulse);
        randomTorque = Mathf.Max(0f, randomTorque);
        shardsPerFrame = Mathf.Max(0, shardsPerFrame);
        forceCountInfluence = Mathf.Clamp01(forceCountInfluence);
        impactInfluenceRadius = Mathf.Max(0f, impactInfluenceRadius);

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

        currentImpactPoint = impactPoint;
        currentImpactDirection = impactDirection;
        currentForceMultiplier = Mathf.Max(0f, forceMultiplier);
        lastShatterPoint = impactPoint;
        lastShatterDirection = impactDirection;
        hasRecordedShatter = true;

        int patternSeed = randomizeShardPattern
            ? UnityEngine.Random.Range(int.MinValue, int.MaxValue - 1)
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
            if (ShouldUseCoroutineSpawn())
            {
                StartCoroutine(SpawnDebrisChunksRoutine(chunks, shardRoot, shardMaterials, meshTransform, resolvedShardMass, shardRandom));
            }
            else
            {
                for (int i = 0; i < chunks.Count; i++)
                {
                    SpawnDebrisChunk(chunks[i], i, shardRoot, shardMaterials, meshTransform, resolvedShardMass, shardRandom);
                }
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

            int safeDepth = MeshShatterUtility.ResolveSafeSubdivisionDepth(
                sourceMesh,
                subdivisionDepth,
                targetSubmesh,
                MaxPreLimitShardCount);
            List<MeshShatterUtility.TriangleShard> shards;
            try
            {
                if (impactInfluenceRadius > 0f)
                {
                    Vector3 impactLocal = meshTransform.InverseTransformPoint(impactPoint);
                    shards = MeshShatterUtility.CreateTriangleShardsImpactWeighted(
                        sourceMesh,
                        safeDepth,
                        splitJitter,
                        patternSeed,
                        targetSubmesh,
                        impactLocal,
                        impactInfluenceRadius);
                }
                else
                {
                    shards = MeshShatterUtility.CreateTriangleShards(
                        sourceMesh,
                        safeDepth,
                        splitJitter,
                        patternSeed,
                        targetSubmesh);
                }
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
            if (ShouldUseCoroutineSpawn())
            {
                StartCoroutine(SpawnSurfaceShardsRoutine(shards, shardRoot, shardMaterials, meshTransform, resolvedShardMass, shardRandom));
            }
            else
            {
                for (int i = 0; i < shards.Count; i++)
                {
                    SpawnShard(shards[i], i, shardRoot, shardMaterials, meshTransform, resolvedShardMass, shardRandom);
                }
            }
        }

        bool preservedRemainingMesh = TryPreserveRemainingMesh(sourceMesh);
        DisableOriginalState(preservedRemainingMesh);
        ScheduleShardRootDestroy(shardRoot);
        isShattered = true;
        onShatter?.Invoke(impactPoint, impactDirection, currentForceMultiplier);
    }

    private bool ShouldUseCoroutineSpawn()
    {
        return shardsPerFrame > 0 && Application.isPlaying && isActiveAndEnabled;
    }

    private IEnumerator SpawnSurfaceShardsRoutine(
        List<MeshShatterUtility.TriangleShard> shards,
        Transform shardRoot,
        Material[] shardMaterials,
        Transform meshTransform,
        float resolvedShardMass,
        System.Random shardRandom)
    {
        int perFrame = Mathf.Max(1, shardsPerFrame);
        for (int i = 0; i < shards.Count; i++)
        {
            if (shardRoot == null)
            {
                yield break;
            }

            SpawnShard(shards[i], i, shardRoot, shardMaterials, meshTransform, resolvedShardMass, shardRandom);
            if ((i + 1) % perFrame == 0 && i < shards.Count - 1)
            {
                yield return null;
            }
        }
    }

    private IEnumerator SpawnDebrisChunksRoutine(
        List<MeshShatterUtility.BoundsChunk> chunks,
        Transform shardRoot,
        Material[] shardMaterials,
        Transform meshTransform,
        float resolvedShardMass,
        System.Random shardRandom)
    {
        int perFrame = Mathf.Max(1, shardsPerFrame);
        for (int i = 0; i < chunks.Count; i++)
        {
            if (shardRoot == null)
            {
                yield break;
            }

            SpawnDebrisChunk(chunks[i], i, shardRoot, shardMaterials, meshTransform, resolvedShardMass, shardRandom);
            if ((i + 1) % perFrame == 0 && i < chunks.Count - 1)
            {
                yield return null;
            }
        }
    }

    private void ScheduleShardRootDestroy(Transform shardRoot)
    {
        if (shardRoot == null || destroyAfterSeconds <= 0f)
        {
            return;
        }

        Destroy(shardRoot.gameObject, destroyAfterSeconds + 0.5f);
    }

    [ContextMenu("Apply Preset Values")]
    private void ApplyPresetValuesContextMenu()
    {
        ApplyPresetValues();
    }

    [ContextMenu("Shatter Now (Play Mode)")]
    private void ShatterNowContextMenu()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("MeshShatter: 'Shatter Now' only works in Play Mode.", this);
            return;
        }

        Shatter();
    }

    [ContextMenu("Reset Shatter State")]
    private void ResetShatterStateContextMenu()
    {
        isShattered = false;
        hasRecordedShatter = false;
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
        int baseCount;
        if (sourceMassKg > 0f)
        {
            baseCount = ApplySizeMultiplierToPreferredCount(ResolveAutoShardTargetCount(sourceMassKg));
        }
        else if (maxSpawnedShards > 0)
        {
            baseCount = ApplySizeMultiplierToPreferredCount(maxSpawnedShards);
        }
        else if (shatterMode != ShatterMode.DebrisChunks)
        {
            baseCount = 0;
        }
        else
        {
            baseCount = ApplySizeMultiplierToPreferredCount(ResolveDefaultDebrisChunkCount());
        }

        return ApplyForceScalingToShardCount(baseCount);
    }

    private int ApplyForceScalingToShardCount(int baseCount)
    {
        if (baseCount <= 0 || forceCountInfluence <= 0f)
        {
            return baseCount;
        }

        float forceFactor = Mathf.Lerp(1f, Mathf.Max(0.1f, currentForceMultiplier), forceCountInfluence);
        return Mathf.Max(1, Mathf.RoundToInt(baseCount * forceFactor));
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
        InheritSourceProperties(shardObject, shardCollider);
        ApplyShardImpulse(rigidbody, worldCenter, (worldCenter - currentImpactPoint).normalized, shardRandom);
        TrackGeneratedMesh(chunkMesh);
        onShardSpawned?.Invoke(rigidbody);

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
        Vector3 shardNormal = Vector3.Cross(worldB - worldA, worldC - worldA).normalized;
        if (shardNormal.sqrMagnitude < 0.0001f)
        {
            shardNormal = (center - currentImpactPoint).normalized;
        }
        InheritSourceProperties(shardObject, shardCollider);
        ApplyShardImpulse(rigidbody, center, shardNormal, shardRandom);
        TrackGeneratedMesh(shardMesh);
        onShardSpawned?.Invoke(rigidbody);

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

    private void ApplyShardImpulse(Rigidbody body, Vector3 shardCenter, Vector3 surfaceNormal, System.Random shardRandom)
    {
        if (body == null)
        {
            return;
        }

        Vector3 direction = ResolveImpulseDirection(
            currentImpactDirection,
            currentImpactPoint,
            shardCenter,
            surfaceNormal);

        if (impactSpread > 0f && shardRandom != null)
        {
            Vector3 spread = new Vector3(
                (float)shardRandom.NextDouble() * 2f - 1f,
                (float)shardRandom.NextDouble() * 2f - 1f,
                (float)shardRandom.NextDouble() * 2f - 1f) * impactSpread;
            direction = (direction + spread).normalized;
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.up;
        }

        float impulseMagnitude = impulseStrength * Mathf.Max(0f, currentForceMultiplier);
        if (randomImpulse > 0f && shardRandom != null)
        {
            float randomFactor = 1f + ((float)shardRandom.NextDouble() * 2f - 1f) * randomImpulse;
            impulseMagnitude *= Mathf.Max(0f, randomFactor);
        }

        if (impulseMagnitude > 0f)
        {
            body.AddForce(direction * impulseMagnitude, ForceMode.Impulse);
        }

        if (randomTorque > 0f && shardRandom != null)
        {
            Vector3 torque = new Vector3(
                (float)shardRandom.NextDouble() * 2f - 1f,
                (float)shardRandom.NextDouble() * 2f - 1f,
                (float)shardRandom.NextDouble() * 2f - 1f) * randomTorque;
            body.AddTorque(torque, ForceMode.VelocityChange);
        }
    }

    private Vector3 ResolveImpulseDirection(
        Vector3 impactDirection,
        Vector3 impactPoint,
        Vector3 shardCenter,
        Vector3 surfaceNormal)
    {
        Vector3 baseDirection = impactDirection;
        if (baseDirection.sqrMagnitude < 0.0001f)
        {
            baseDirection = shardCenter - impactPoint;
        }

        if (baseDirection.sqrMagnitude < 0.0001f)
        {
            baseDirection = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal : Vector3.up;
        }

        Vector3 normalized = baseDirection.normalized;
        Vector3 normalContribution = surfaceNormal.sqrMagnitude > 0.0001f
            ? surfaceNormal.normalized * surfaceNormalWeight
            : Vector3.zero;
        Vector3 result = normalized * impactDirectionWeight + normalContribution;
        if (result.sqrMagnitude < 0.0001f)
        {
            return normalized;
        }

        return result.normalized;
    }

    private void InheritSourceProperties(GameObject shardObject, Collider shardCollider)
    {
        if (!inheritSourceColliderProperties || shardObject == null)
        {
            return;
        }

        Collider source = ResolveSourceColliderForInheritance();
        if (source == null)
        {
            return;
        }

        shardObject.layer = source.gameObject.layer;
        if (!string.IsNullOrEmpty(source.tag) && source.tag != "Untagged")
        {
            try { shardObject.tag = source.tag; }
            catch (UnityException) { /* tag may be invalid in some test contexts */ }
        }

        if (shardCollider != null && source.sharedMaterial != null)
        {
            shardCollider.sharedMaterial = source.sharedMaterial;
        }
    }

    private Collider ResolveSourceColliderForInheritance()
    {
        if (collidersToDisable != null)
        {
            for (int i = 0; i < collidersToDisable.Length; i++)
            {
                if (collidersToDisable[i] != null)
                {
                    return collidersToDisable[i];
                }
            }
        }

        return GetComponent<Collider>();
    }

    private void TrackGeneratedMesh(Mesh mesh)
    {
        if (mesh == null)
        {
            return;
        }

        generatedMeshes.Add(mesh);
        if (destroyAfterSeconds > 0f)
        {
            Destroy(mesh, destroyAfterSeconds + 0.5f);
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < generatedMeshes.Count; i++)
        {
            Mesh mesh = generatedMeshes[i];
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }

        generatedMeshes.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Mesh sourceMesh = targetMeshFilter != null ? targetMeshFilter.sharedMesh : null;
        if (sourceMesh != null)
        {
            Transform meshTransform = targetMeshFilter.transform;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.85f);
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = meshTransform.localToWorldMatrix;
            Gizmos.DrawWireCube(sourceMesh.bounds.center, sourceMesh.bounds.size);
            Gizmos.matrix = previous;
        }

        if (hasRecordedShatter)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(lastShatterPoint, 0.05f);
            if (lastShatterDirection.sqrMagnitude > 0.0001f)
            {
                Gizmos.DrawLine(lastShatterPoint, lastShatterPoint + lastShatterDirection.normalized * 0.5f);
            }
        }

        if (impactInfluenceRadius > 0f && targetMeshFilter != null)
        {
            Vector3 origin = hasRecordedShatter ? lastShatterPoint : targetMeshFilter.transform.position;
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(origin, impactInfluenceRadius * targetMeshFilter.transform.lossyScale.x);
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
        generatedMeshes.Add(preservedMesh);

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
