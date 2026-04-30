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
    [SerializeField] private bool requireSurfaceHit = true;
    [SerializeField] [Range(0f, 90f)] private float maxSurfaceNormalAngle = 35f;
    [SerializeField] [Range(0f, 180f)] private float startAngleFade = 40f;
    [SerializeField] [Range(0f, 180f)] private float endAngleFade = 65f;
    [SerializeField] private float growthPerSecond = 0.35f;

    private readonly Dictionary<int, RuntimeScorchDecal> decalsByNodeIndex = new Dictionary<int, RuntimeScorchDecal>();
    private readonly List<int> staleNodeIndices = new List<int>();
    private Transform decalRoot;
    private Material runtimeScorchMaterial;
    private bool warnedMissingMaterial;

    private sealed class RuntimeScorchDecal
    {
        public DecalProjector Projector;
        public float ScorchAmount;
        public float LastSeenTime;
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
            if (pair.Value?.Projector != null)
            {
                Destroy(pair.Value.Projector.gameObject);
            }
        }

    }

    private void Update()
    {
        if (simulationManager == null || !simulationManager.IsInitialized)
        {
            return;
        }

        IReadOnlyList<FireNodeSnapshot> snapshots = simulationManager.NodeSnapshots;
        for (int i = 0; i < snapshots.Count; i++)
        {
            SyncSnapshot(snapshots[i]);
        }

        HideStaleDecals();
    }

    private void SyncSnapshot(FireNodeSnapshot snapshot)
    {
        if (snapshot.Intensity <= 0.01f)
        {
            return;
        }

        RuntimeScorchDecal decal = ResolveDecal(snapshot.NodeIndex);
        if (decal == null)
        {
            return;
        }

        decal.LastSeenTime = Time.time;
        decal.ScorchAmount = Mathf.Clamp01(decal.ScorchAmount + Time.deltaTime * growthPerSecond * Mathf.Max(0.2f, snapshot.Intensity));
        if (!TryPlaceProjector(decal.Projector, snapshot.Position, snapshot.SurfaceNormal))
        {
            decal.Projector.gameObject.SetActive(false);
            return;
        }

        float size = Mathf.Lerp(minSize, maxSize, decal.ScorchAmount);
        float opacity = Mathf.Clamp01(Mathf.Max(decal.ScorchAmount, snapshot.Intensity * 0.25f)) * maxOpacity;
        decal.Projector.size = new Vector3(size, size, projectorDepth);
        decal.Projector.pivot = new Vector3(0f, 0f, projectorDepth * 0.5f);
        decal.Projector.fadeFactor = opacity;
        decal.Projector.gameObject.SetActive(opacity > 0.001f);
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
        GameObject decalObject = new GameObject($"FireNode_{nodeIndex}_ScorchDecal");
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

        decal = new RuntimeScorchDecal
        {
            Projector = projector,
            LastSeenTime = Time.time
        };
        decalsByNodeIndex.Add(nodeIndex, decal);
        return decal;
    }

    private bool TryPlaceProjector(DecalProjector projector, Vector3 nodePosition, Vector3 surfaceNormal)
    {
        Vector3 normal = surfaceNormal.sqrMagnitude > 0.001f ? surfaceNormal.normalized : Vector3.up;
        Vector3 origin = nodePosition + normal * raycastBackoff;
        if (Physics.Raycast(origin, -normal, out RaycastHit hit, raycastBackoff + raycastDistance, surfaceMask, triggerInteraction))
        {
            Vector3 hitNormal = hit.normal.sqrMagnitude > 0.001f ? hit.normal.normalized : normal;
            if (Vector3.Angle(normal, hitNormal) > maxSurfaceNormalAngle)
            {
                return false;
            }

            normal = hitNormal;
            nodePosition = hit.point;
        }
        else if (requireSurfaceHit)
        {
            return false;
        }

        projector.transform.position = nodePosition + normal * surfaceOffset;
        projector.transform.rotation = Quaternion.LookRotation(-normal, ResolveTangent(normal));
        projector.startAngleFade = startAngleFade;
        projector.endAngleFade = Mathf.Max(startAngleFade, endAngleFade);
        return true;
    }

    private void HideStaleDecals()
    {
        staleNodeIndices.Clear();
        foreach (KeyValuePair<int, RuntimeScorchDecal> pair in decalsByNodeIndex)
        {
            RuntimeScorchDecal decal = pair.Value;
            if (decal?.Projector == null)
            {
                staleNodeIndices.Add(pair.Key);
                continue;
            }

            if (Time.time - decal.LastSeenTime > 0.5f)
            {
                decal.Projector.gameObject.SetActive(decal.ScorchAmount > 0.001f);
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
        endAngleFade = Mathf.Max(startAngleFade, endAngleFade);
        growthPerSecond = Mathf.Max(0f, growthPerSecond);
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
