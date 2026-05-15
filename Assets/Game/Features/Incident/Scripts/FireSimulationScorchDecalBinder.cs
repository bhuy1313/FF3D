using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class FireSimulationScorchDecalBinder : MonoBehaviour
{
    [SerializeField] private FireSimulationManager simulationManager;
    [SerializeField] private Material scorchMaterial;
    [SerializeField] private Texture2D scorchMaskTexture;
    [SerializeField] private Color scorchTint = new Color(0.11f, 0.085f, 0.065f, 0.85f);
    [SerializeField] private LayerMask surfaceMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private int maxDecals = 96;
    [SerializeField] private float minSize = 0.5f;
    [SerializeField] private float maxSize = 2.6f;
    [SerializeField] [Range(0f, 1f)] private float maxOpacity = 0.72f;
    [SerializeField] private float surfaceOffset = 0.015f;
    [SerializeField] private float raycastBackoff = 0.65f;
    [SerializeField] private float raycastDistance = 1.8f;
    [SerializeField] private float projectorDepth = 0.18f;
    [SerializeField] [Min(1)] private int maxProjectorsPerNode = 3;
    [SerializeField] [Min(0f)] private float surfaceProbeRadius = 0.45f;
    [SerializeField] [Range(0f, 1f)] private float sideProbeBias = 0.7f;
    [SerializeField] [Range(0f, 1f)] private float diagonalProbeBias = 0.35f;
    [SerializeField] [Range(0f, 180f)] private float minSurfaceSeparationAngle = 22f;
    [SerializeField] private bool requireSurfaceHit = true;
    [SerializeField] [Range(0f, 90f)] private float maxSurfaceNormalAngle = 35f;
    [SerializeField] [Range(0f, 180f)] private float startAngleFade = 40f;
    [SerializeField] [Range(0f, 180f)] private float endAngleFade = 65f;
    [SerializeField] private float growthPerSecond = 0.35f;
    [SerializeField] [Min(0.02f)] private float updateInterval = 0.12f;
    [SerializeField] [Min(0f)] private float placementRefreshInterval = 1.5f;
    [SerializeField] [Min(1)] private int maxSnapshotsPerUpdate = 32;
    [SerializeField] [Range(0f, 1f)] private float minSnapshotIntensity = 0.08f;
    [SerializeField] private bool cullByCameraDistance = true;
    [SerializeField] [Min(0f)] private float maxCameraDistance = 45f;

    private readonly Dictionary<int, RuntimeScorchDecal> decalsByNodeIndex = new Dictionary<int, RuntimeScorchDecal>();
    private readonly List<int> staleNodeIndices = new List<int>();
    private Transform decalRoot;
    private Material runtimeScorchMaterial;
    private bool warnedMissingMaterial;
    private float updateTimer;
    private int nextSnapshotStartIndex;
    private Camera cachedMainCamera;
    private float nextCameraResolveTime;

    private sealed class RuntimeScorchDecal
    {
        public readonly List<DecalProjector> Projectors = new List<DecalProjector>();
        public readonly List<SurfacePlacement> Placements = new List<SurfacePlacement>();
        public float ScorchAmount;
        public float LastSeenTime;
        public float NextPlacementRefreshTime;
        public bool HasResolvedPlacements;
    }

    private struct SurfacePlacement
    {
        public Vector3 Position;
        public Vector3 Normal;
        public float Score;
    }

    public void Configure(FireSimulationManager manager, Material material, LayerMask mask, int decalLimit)
    {
        simulationManager = manager != null ? manager : simulationManager;
        if (material != null)
        {
            scorchMaterial = material;
        }

        surfaceMask = mask;
        maxDecals = Mathf.Max(1, decalLimit);
    }

    private void Awake()
    {
        if (simulationManager == null)
        {
            simulationManager = GetComponent<FireSimulationManager>();
        }

        ClampSettings();
    }

    private void OnValidate()
    {
        ClampSettings();
    }

    private void OnDestroy()
    {
        if (runtimeScorchMaterial != null)
        {
            Destroy(runtimeScorchMaterial);
            runtimeScorchMaterial = null;
        }

        foreach (KeyValuePair<int, RuntimeScorchDecal> pair in decalsByNodeIndex)
        {
            RuntimeScorchDecal decal = pair.Value;
            if (decal == null)
            {
                continue;
            }

            for (int i = 0; i < decal.Projectors.Count; i++)
            {
                if (decal.Projectors[i] != null)
                {
                    Destroy(decal.Projectors[i].gameObject);
                }
            }
        }

    }

    private void Update()
    {
        if (simulationManager == null || !simulationManager.IsInitialized)
        {
            return;
        }

        updateTimer += Time.deltaTime;
        if (updateTimer < updateInterval)
        {
            return;
        }

        float deltaTime = updateTimer;
        updateTimer = 0f;
        float currentTime = Time.time;

        IReadOnlyList<FireNodeSnapshot> snapshots = simulationManager.NodeSnapshots;
        int snapshotCount = snapshots.Count;
        if (snapshotCount <= 0)
        {
            HideStaleDecals(currentTime);
            return;
        }

        int startIndex = snapshotCount > 0 ? nextSnapshotStartIndex % snapshotCount : 0;
        int processedCount = 0;
        int maxCount = Mathf.Min(snapshotCount, Mathf.Max(1, maxSnapshotsPerUpdate));
        for (int offset = 0; offset < snapshotCount && processedCount < maxCount; offset++)
        {
            int snapshotIndex = (startIndex + offset) % snapshotCount;
            SyncSnapshot(snapshots[snapshotIndex], deltaTime, currentTime);
            processedCount++;
        }

        nextSnapshotStartIndex = snapshotCount > 0 ? (startIndex + processedCount) % snapshotCount : 0;

        HideStaleDecals(currentTime);
    }

    private void SyncSnapshot(FireNodeSnapshot snapshot, float deltaTime, float currentTime)
    {
        if (snapshot.Intensity < minSnapshotIntensity)
        {
            return;
        }

        if (cullByCameraDistance && !IsWithinCameraDistance(snapshot.Position, currentTime))
        {
            return;
        }

        RuntimeScorchDecal decal = ResolveDecal(snapshot.NodeIndex);
        if (decal == null)
        {
            return;
        }

        decal.LastSeenTime = currentTime;
        decal.ScorchAmount = Mathf.Clamp01(decal.ScorchAmount + deltaTime * growthPerSecond * Mathf.Max(0.2f, snapshot.Intensity));
        int activeProjectorCount = TryPlaceProjectors(decal, snapshot.Position, snapshot.SurfaceNormal, currentTime);
        if (activeProjectorCount <= 0)
        {
            SetProjectorsActive(decal, 0, false);
            return;
        }

        float size = Mathf.Lerp(minSize, maxSize, decal.ScorchAmount);
        float opacity = Mathf.Clamp01(Mathf.Max(decal.ScorchAmount, snapshot.Intensity * 0.25f)) * maxOpacity;
        ApplyProjectorVisuals(decal, activeProjectorCount, size, opacity);
    }

    private RuntimeScorchDecal ResolveDecal(int nodeIndex)
    {
        if (decalsByNodeIndex.TryGetValue(nodeIndex, out RuntimeScorchDecal decal))
        {
            return decal;
        }

        if (decalsByNodeIndex.Count >= maxDecals)
        {
            return null;
        }

        Transform root = ResolveRoot();
        decal = new RuntimeScorchDecal
        {
            LastSeenTime = Time.time
        };

        EnsureProjectorCount(decal, root, nodeIndex);
        decalsByNodeIndex.Add(nodeIndex, decal);
        return decal;
    }

    private int TryPlaceProjectors(RuntimeScorchDecal decal, Vector3 nodePosition, Vector3 surfaceNormal, float currentTime)
    {
        if (decal == null)
        {
            return 0;
        }

        EnsureProjectorCount(decal, ResolveRoot(), -1);

        bool shouldRefreshPlacements =
            !decal.HasResolvedPlacements ||
            decal.Placements.Count <= 0 ||
            (placementRefreshInterval > 0f && currentTime >= decal.NextPlacementRefreshTime);

        if (shouldRefreshPlacements)
        {
            CollectSurfacePlacements(decal.Placements, nodePosition, surfaceNormal);
            decal.HasResolvedPlacements = true;
            decal.NextPlacementRefreshTime = currentTime + placementRefreshInterval;
        }

        List<SurfacePlacement> placements = decal.Placements;
        int activeCount = Mathf.Min(placements.Count, decal.Projectors.Count);
        for (int i = 0; i < activeCount; i++)
        {
            PlaceProjector(decal.Projectors[i], placements[i].Position, placements[i].Normal);
        }

        SetProjectorsActive(decal, activeCount, false);
        return activeCount;
    }

    private void CollectSurfacePlacements(List<SurfacePlacement> placements, Vector3 nodePosition, Vector3 surfaceNormal)
    {
        placements.Clear();
        Vector3 normal = surfaceNormal.sqrMagnitude > 0.001f ? surfaceNormal.normalized : Vector3.up;
        Vector3 tangent = ResolveTangent(normal);
        Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

        TryAddSurfacePlacement(placements, nodePosition, normal, normal, 1f);
        TryAddSurfacePlacement(placements, nodePosition, normal, Vector3.Normalize(normal + tangent * sideProbeBias), 0.85f);
        TryAddSurfacePlacement(placements, nodePosition, normal, Vector3.Normalize(normal - tangent * sideProbeBias), 0.85f);
        TryAddSurfacePlacement(placements, nodePosition, normal, Vector3.Normalize(normal + bitangent * sideProbeBias), 0.85f);
        TryAddSurfacePlacement(placements, nodePosition, normal, Vector3.Normalize(normal - bitangent * sideProbeBias), 0.85f);
        TryAddSurfacePlacement(placements, nodePosition, normal, Vector3.Normalize(normal + (tangent + bitangent) * diagonalProbeBias), 0.7f);
        TryAddSurfacePlacement(placements, nodePosition, normal, Vector3.Normalize(normal + (tangent - bitangent) * diagonalProbeBias), 0.7f);
        TryAddSurfacePlacement(placements, nodePosition, normal, Vector3.Normalize(normal + (-tangent + bitangent) * diagonalProbeBias), 0.7f);
        TryAddSurfacePlacement(placements, nodePosition, normal, Vector3.Normalize(normal - (tangent + bitangent) * diagonalProbeBias), 0.7f);
    }

    private void TryAddSurfacePlacement(
        List<SurfacePlacement> placements,
        Vector3 nodePosition,
        Vector3 fallbackNormal,
        Vector3 probeNormal,
        float probeWeight)
    {
        if (placements.Count >= maxProjectorsPerNode)
        {
            return;
        }

        Vector3 normalizedProbe = probeNormal.sqrMagnitude > 0.001f ? probeNormal.normalized : fallbackNormal;
        Vector3 origin = nodePosition + normalizedProbe * (raycastBackoff + surfaceProbeRadius);
        if (Physics.Raycast(origin, -normalizedProbe, out RaycastHit hit, raycastBackoff + raycastDistance + surfaceProbeRadius, surfaceMask, triggerInteraction))
        {
            Vector3 hitNormal = hit.normal.sqrMagnitude > 0.001f ? hit.normal.normalized : fallbackNormal;
            if (Vector3.Angle(normalizedProbe, hitNormal) > maxSurfaceNormalAngle)
            {
                return;
            }

            if (!IsDistinctSurface(placements, hitNormal))
            {
                return;
            }

            float distanceScore = 1f - Mathf.Clamp01(hit.distance / Mathf.Max(0.01f, raycastBackoff + raycastDistance + surfaceProbeRadius));
            AddPlacementSorted(placements, new SurfacePlacement
            {
                Position = hit.point,
                Normal = hitNormal,
                Score = probeWeight * distanceScore
            });
        }
        else if (requireSurfaceHit)
        {
            return;
        }
    }

    private void HideStaleDecals(float currentTime)
    {
        staleNodeIndices.Clear();
        foreach (KeyValuePair<int, RuntimeScorchDecal> pair in decalsByNodeIndex)
        {
            RuntimeScorchDecal decal = pair.Value;
            if (decal == null || decal.Projectors.Count == 0)
            {
                staleNodeIndices.Add(pair.Key);
                continue;
            }

            if (currentTime - decal.LastSeenTime > 0.5f)
            {
                if (decal.ScorchAmount <= 0.001f)
                {
                    SetProjectorsActive(decal, 0, false);
                }
            }
        }

        for (int i = 0; i < staleNodeIndices.Count; i++)
        {
            decalsByNodeIndex.Remove(staleNodeIndices[i]);
        }
    }

    private Material ResolveMaterial()
    {
        if (scorchMaterial == null && !warnedMissingMaterial)
        {
            warnedMissingMaterial = true;
            Debug.LogWarning($"{nameof(FireSimulationScorchDecalBinder)} needs a URP Decal material assigned to render scorch decals.", this);
        }

        if (scorchMaterial == null)
        {
            return null;
        }

        if (runtimeScorchMaterial == null)
        {
            runtimeScorchMaterial = new Material(scorchMaterial)
            {
                name = $"{scorchMaterial.name}_Runtime"
            };
            ApplyScorchMaterialSettings(runtimeScorchMaterial);
        }

        return runtimeScorchMaterial;
    }

    private Transform ResolveRoot()
    {
        if (decalRoot != null)
        {
            return decalRoot;
        }

        GameObject rootObject = GameObject.Find("FireScorchDecals");
        if (rootObject == null)
        {
            rootObject = new GameObject("FireScorchDecals");
        }

        decalRoot = rootObject.transform;
        return decalRoot;
    }

    private bool IsWithinCameraDistance(Vector3 worldPosition, float currentTime)
    {
        Camera mainCamera = ResolveMainCamera(currentTime);
        if (mainCamera == null)
        {
            return true;
        }

        float maxDistance = Mathf.Max(0f, maxCameraDistance);
        if (maxDistance <= 0f)
        {
            return true;
        }

        return (mainCamera.transform.position - worldPosition).sqrMagnitude <= maxDistance * maxDistance;
    }

    private Camera ResolveMainCamera(float currentTime)
    {
        if (cachedMainCamera != null && cachedMainCamera.isActiveAndEnabled)
        {
            return cachedMainCamera;
        }

        if (currentTime < nextCameraResolveTime)
        {
            return cachedMainCamera;
        }

        cachedMainCamera = Camera.main;
        nextCameraResolveTime = currentTime + 1f;
        return cachedMainCamera;
    }

    private void EnsureProjectorCount(RuntimeScorchDecal decal, Transform root, int nodeIndex)
    {
        if (decal == null || root == null)
        {
            return;
        }

        while (decal.Projectors.Count < maxProjectorsPerNode)
        {
            int projectorIndex = decal.Projectors.Count;
            string objectName = nodeIndex >= 0
                ? $"FireNode_{nodeIndex}_ScorchDecal_{projectorIndex}"
                : $"FireNode_ScorchDecal_{projectorIndex}";
            GameObject decalObject = new GameObject(objectName);
            decalObject.transform.SetParent(root, true);

            DecalProjector projector = decalObject.AddComponent<DecalProjector>();
            projector.material = ResolveMaterial();
            projector.drawDistance = 70f;
            projector.fadeScale = 0.35f;
            projector.startAngleFade = startAngleFade;
            projector.endAngleFade = Mathf.Max(startAngleFade, endAngleFade);
            projector.uvScale = Vector2.one;
            projector.uvBias = Vector2.zero;
            projector.size = new Vector3(minSize, minSize, projectorDepth);
            projector.pivot = new Vector3(0f, 0f, projectorDepth * 0.5f);
            projector.fadeFactor = 0f;
            decalObject.SetActive(false);
            decal.Projectors.Add(projector);
        }
    }

    private void PlaceProjector(DecalProjector projector, Vector3 nodePosition, Vector3 normal)
    {
        if (projector == null)
        {
            return;
        }

        projector.transform.position = nodePosition + normal * surfaceOffset;
        projector.transform.rotation = Quaternion.LookRotation(-normal, ResolveTangent(normal));
        projector.startAngleFade = startAngleFade;
        projector.endAngleFade = Mathf.Max(startAngleFade, endAngleFade);
    }

    private void ApplyProjectorVisuals(RuntimeScorchDecal decal, int activeProjectorCount, float size, float opacity)
    {
        if (decal == null)
        {
            return;
        }

        float opacityPerProjector = activeProjectorCount > 1
            ? Mathf.Clamp01(opacity / Mathf.Sqrt(activeProjectorCount))
            : opacity;

        for (int i = 0; i < decal.Projectors.Count; i++)
        {
            DecalProjector projector = decal.Projectors[i];
            if (projector == null)
            {
                continue;
            }

            bool active = i < activeProjectorCount && opacityPerProjector > 0.001f;
            projector.size = new Vector3(size, size, projectorDepth);
            projector.pivot = new Vector3(0f, 0f, projectorDepth * 0.5f);
            projector.fadeFactor = active ? opacityPerProjector : 0f;
            projector.gameObject.SetActive(active);
        }
    }

    private void SetProjectorsActive(RuntimeScorchDecal decal, int activeProjectorCount, bool active)
    {
        if (decal == null)
        {
            return;
        }

        int threshold = active ? Mathf.Clamp(activeProjectorCount, 0, decal.Projectors.Count) : 0;
        for (int i = threshold; i < decal.Projectors.Count; i++)
        {
            DecalProjector projector = decal.Projectors[i];
            if (projector == null)
            {
                continue;
            }

            projector.fadeFactor = 0f;
            projector.gameObject.SetActive(false);
        }
    }

    private bool IsDistinctSurface(List<SurfacePlacement> placements, Vector3 normal)
    {
        for (int i = 0; i < placements.Count; i++)
        {
            if (Vector3.Angle(placements[i].Normal, normal) < minSurfaceSeparationAngle)
            {
                return false;
            }
        }

        return true;
    }

    private void AddPlacementSorted(List<SurfacePlacement> placements, SurfacePlacement placement)
    {
        int insertIndex = placements.Count;
        for (int i = 0; i < placements.Count; i++)
        {
            if (placement.Score > placements[i].Score)
            {
                insertIndex = i;
                break;
            }
        }

        placements.Add(placement);
        for (int i = placements.Count - 1; i > insertIndex; i--)
        {
            placements[i] = placements[i - 1];
        }

        placements[insertIndex] = placement;
    }

    private static Vector3 ResolveTangent(Vector3 normal)
    {
        Vector3 tangent = Vector3.ProjectOnPlane(Vector3.forward, normal);
        if (tangent.sqrMagnitude <= 0.001f)
        {
            tangent = Vector3.ProjectOnPlane(Vector3.right, normal);
        }

        return tangent.sqrMagnitude > 0.001f ? tangent.normalized : Vector3.up;
    }

    private void ClampSettings()
    {
        maxDecals = Mathf.Max(1, maxDecals);
        minSize = Mathf.Max(0.01f, minSize);
        maxSize = Mathf.Max(minSize, maxSize);
        surfaceOffset = Mathf.Max(0f, surfaceOffset);
        raycastBackoff = Mathf.Max(0f, raycastBackoff);
        raycastDistance = Mathf.Max(0.01f, raycastDistance);
        projectorDepth = Mathf.Max(0.02f, projectorDepth);
        maxProjectorsPerNode = Mathf.Max(1, maxProjectorsPerNode);
        surfaceProbeRadius = Mathf.Max(0f, surfaceProbeRadius);
        minSurfaceSeparationAngle = Mathf.Clamp(minSurfaceSeparationAngle, 0f, 180f);
        endAngleFade = Mathf.Max(startAngleFade, endAngleFade);
        growthPerSecond = Mathf.Max(0f, growthPerSecond);
        updateInterval = Mathf.Max(0.02f, updateInterval);
        placementRefreshInterval = Mathf.Max(0f, placementRefreshInterval);
        maxSnapshotsPerUpdate = Mathf.Max(1, maxSnapshotsPerUpdate);
        minSnapshotIntensity = Mathf.Clamp01(minSnapshotIntensity);
        maxCameraDistance = Mathf.Max(0f, maxCameraDistance);
    }

    private void ApplyScorchMaterialSettings(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (scorchMaskTexture != null)
        {
            scorchMaskTexture.wrapMode = TextureWrapMode.Clamp;
            scorchMaskTexture.wrapModeU = TextureWrapMode.Clamp;
            scorchMaskTexture.wrapModeV = TextureWrapMode.Clamp;
            SetTextureIfPresent(material, "Base_Map", scorchMaskTexture);
            SetTextureIfPresent(material, "_BaseMap", scorchMaskTexture);
            SetTextureIfPresent(material, "_MainTex", scorchMaskTexture);
        }

        SetColorIfPresent(material, "_BaseColor", scorchTint);
        SetColorIfPresent(material, "_Color", scorchTint);
        SetFloatIfPresent(material, "_Smoothness", 0f);
        SetFloatIfPresent(material, "_SpecularHighlights", 0f);
        SetFloatIfPresent(material, "_DecalAngleFadeSupported", 1f);
    }

    private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetTexture(propertyName, texture);
        }
    }

    private static void SetColorIfPresent(Material material, string propertyName, Color color)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, color);
        }
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }
}
