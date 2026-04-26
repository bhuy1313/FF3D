using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FireClusterView : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Light clusterLight;
    [SerializeField] private ParticleSystem[] particleSystems;
    [SerializeField] private float maxVisualRadius = 2.5f;
    [SerializeField] private float maxLightIntensity = 2f;
    [SerializeField] private Vector2 flameScaleRange = new Vector2(0.7f, 1.35f);
    [SerializeField] private Vector3 memberVisualOffset = Vector3.zero;
    [SerializeField] private Vector3 clusterLightLocalOffset = Vector3.zero;
    [Header("Cluster Visual Prefabs")]
    [SerializeField] private GameObject ordinaryFireVisualPrefab;
    [SerializeField] private GameObject electricalFireVisualPrefab;
    [SerializeField] private GameObject flammableLiquidFireVisualPrefab;
    [SerializeField] private GameObject gasFireVisualPrefab;
    [Header("Fallback Visual")]
    [SerializeField] private bool createFallbackVisualWhenNoParticles = true;
    [SerializeField] private Color fallbackCoreColor = new Color(1f, 0.45f, 0.1f, 0.95f);
    [SerializeField] private Color fallbackGlowColor = new Color(1f, 0.2f, 0.02f, 0.55f);

    private int boundClusterId = -1;
    private Transform fallbackVisualRoot;
    private Renderer fallbackCoreRenderer;
    private Renderer fallbackGlowRenderer;
    private Material fallbackCoreMaterial;
    private Material fallbackGlowMaterial;
    private bool fallbackVisualInitialized;
    private readonly List<RuntimeFlameEmitter> flameEmitters = new List<RuntimeFlameEmitter>();

    public int BoundClusterId => boundClusterId;

    private sealed class RuntimeFlameEmitter
    {
        public Transform Root;
        public GameObject Instance;
        public ParticleSystem[] ParticleSystems;
        public FireHazardType HazardType;
        public bool UsesFallback;
        public Renderer FallbackCoreRenderer;
        public Renderer FallbackGlowRenderer;
        public Material FallbackCoreMaterial;
        public Material FallbackGlowMaterial;
    }

    private void Awake()
    {
        EnsureFallbackVisual();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < flameEmitters.Count; i++)
        {
            DestroyEmitterResources(flameEmitters[i]);
        }

        if (fallbackCoreMaterial != null)
        {
            Destroy(fallbackCoreMaterial);
        }

        if (fallbackGlowMaterial != null)
        {
            Destroy(fallbackGlowMaterial);
        }
    }

    public void Bind(int clusterId)
    {
        boundClusterId = clusterId;
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    public void Unbind()
    {
        boundClusterId = -1;
        SetClusterParticlesActive(false);

        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    public void ApplySnapshot(FireClusterSnapshot snapshot)
    {
        EnsureFallbackVisual();
        EnsureFlameEmitterCapacity(snapshot.Members.Count);

        transform.position = snapshot.Center;
        transform.rotation = ResolveVisualRotation(snapshot.AverageNormal);
        transform.localScale = Vector3.one;

        if (visualRoot != null)
        {
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;
        }

        if (clusterLight != null)
        {
            clusterLight.transform.localPosition = clusterLightLocalOffset;
            clusterLight.enabled = snapshot.Intensity > 0.01f;
            clusterLight.intensity = snapshot.Intensity * maxLightIntensity;
        }

        ApplyFallbackVisual(snapshot);
        SyncFlameEmitters(snapshot);
        SetClusterParticlesActive(snapshot.Intensity > 0.01f && snapshot.Members.Count <= 0);
    }

    private void SyncFlameEmitters(FireClusterSnapshot snapshot)
    {
        Transform root = visualRoot != null ? visualRoot : transform;
        IReadOnlyList<FireClusterMemberSnapshot> members = snapshot.Members;
        for (int i = 0; i < members.Count; i++)
        {
            RuntimeFlameEmitter emitter = flameEmitters[i];
            FireClusterMemberSnapshot member = members[i];
            SyncEmitterVisual(emitter, member.HazardType);

            emitter.Root.SetParent(root, false);
            emitter.Root.position = member.Position + memberVisualOffset;
            emitter.Root.rotation = ResolveVisualRotation(member.SurfaceNormal);
            emitter.Root.localScale = Vector3.one * Mathf.Lerp(
                flameScaleRange.x,
                flameScaleRange.y,
                Mathf.Clamp01(member.Intensity));

            bool visible = member.Intensity > 0.01f;
            emitter.Root.gameObject.SetActive(visible);
            ApplyEmitterFallback(emitter, member);
            SetEmitterParticlesActive(emitter, visible);
        }

        for (int i = members.Count; i < flameEmitters.Count; i++)
        {
            flameEmitters[i].Root.gameObject.SetActive(false);
            SetEmitterParticlesActive(flameEmitters[i], false);
        }
    }

    private Quaternion ResolveVisualRotation(Vector3 averageNormal)
    {
        Vector3 up = averageNormal.sqrMagnitude > 0.001f ? averageNormal.normalized : Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up);
        if (forward.sqrMagnitude <= 0.001f)
        {
            forward = Vector3.ProjectOnPlane(Vector3.forward, up);
        }

        if (forward.sqrMagnitude <= 0.001f)
        {
            forward = Vector3.ProjectOnPlane(Vector3.right, up);
        }

        return Quaternion.LookRotation(forward.normalized, up);
    }

    private void EnsureFallbackVisual()
    {
        if (fallbackVisualInitialized)
        {
            return;
        }

        fallbackVisualInitialized = true;
        if (!createFallbackVisualWhenNoParticles || HasAnyConfiguredVisuals())
        {
            return;
        }

        Transform root = visualRoot != null ? visualRoot : transform;
        fallbackVisualRoot = new GameObject("FallbackVisual").transform;
        fallbackVisualRoot.SetParent(root, false);

        fallbackCoreRenderer = CreateFallbackPrimitive("Core", PrimitiveType.Sphere, fallbackCoreColor, out fallbackCoreMaterial);
        fallbackGlowRenderer = CreateFallbackPrimitive("Glow", PrimitiveType.Sphere, fallbackGlowColor, out fallbackGlowMaterial);

        if (fallbackCoreRenderer != null)
        {
            fallbackCoreRenderer.transform.SetParent(fallbackVisualRoot, false);
            fallbackCoreRenderer.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            fallbackCoreRenderer.transform.localScale = new Vector3(0.32f, 0.55f, 0.32f);
        }

        if (fallbackGlowRenderer != null)
        {
            fallbackGlowRenderer.transform.SetParent(fallbackVisualRoot, false);
            fallbackGlowRenderer.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            fallbackGlowRenderer.transform.localScale = new Vector3(0.75f, 0.22f, 0.75f);
        }
    }

    private void ApplyFallbackVisual(FireClusterSnapshot snapshot)
    {
        if (fallbackVisualRoot == null)
        {
            return;
        }

        bool visible = snapshot.Intensity > 0.01f;
        fallbackVisualRoot.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        float intensity = Mathf.Clamp01(snapshot.Intensity);
        float radius = Mathf.Clamp(snapshot.Radius, 0.25f, maxVisualRadius);
        fallbackVisualRoot.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.4f, intensity) * radius;

        if (fallbackCoreMaterial != null)
        {
            Color color = Color.Lerp(new Color(1f, 0.35f, 0.08f, 0.8f), fallbackCoreColor, intensity);
            fallbackCoreMaterial.color = color;
        }

        if (fallbackGlowMaterial != null)
        {
            Color color = Color.Lerp(new Color(1f, 0.12f, 0.02f, 0.2f), fallbackGlowColor, intensity);
            fallbackGlowMaterial.color = color;
        }
    }

    private bool HasConfiguredParticles()
    {
        if (particleSystems == null || particleSystems.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasAnyConfiguredVisuals()
    {
        return
            HasConfiguredParticles() ||
            ordinaryFireVisualPrefab != null ||
            electricalFireVisualPrefab != null ||
            flammableLiquidFireVisualPrefab != null ||
            gasFireVisualPrefab != null;
    }

    private void EnsureFlameEmitterCapacity(int count)
    {
        while (flameEmitters.Count < count)
        {
            flameEmitters.Add(CreateEmitter());
        }
    }

    private RuntimeFlameEmitter CreateEmitter()
    {
        Transform rootParent = visualRoot != null ? visualRoot : transform;
        GameObject emitterObject = new GameObject("FlameEmitter");
        emitterObject.transform.SetParent(rootParent, false);
        emitterObject.SetActive(false);
        return new RuntimeFlameEmitter
        {
            Root = emitterObject.transform,
            HazardType = (FireHazardType)(-1)
        };
    }

    private GameObject ResolveVisualPrefab(FireHazardType hazardType)
    {
        switch (hazardType)
        {
            case FireHazardType.Electrical:
                return electricalFireVisualPrefab != null ? electricalFireVisualPrefab : ordinaryFireVisualPrefab;

            case FireHazardType.FlammableLiquid:
                return flammableLiquidFireVisualPrefab != null ? flammableLiquidFireVisualPrefab : ordinaryFireVisualPrefab;

            case FireHazardType.GasFed:
                return gasFireVisualPrefab != null ? gasFireVisualPrefab : ordinaryFireVisualPrefab;

            default:
                return ordinaryFireVisualPrefab;
        }
    }

    private void SyncEmitterVisual(RuntimeFlameEmitter emitter, FireHazardType hazardType)
    {
        GameObject prefab = ResolveVisualPrefab(hazardType);
        if (prefab == null)
        {
            if (emitter.Instance != null)
            {
                Destroy(emitter.Instance);
                emitter.Instance = null;
                emitter.ParticleSystems = null;
            }

            EnsureEmitterFallback(emitter);
            emitter.HazardType = hazardType;
            return;
        }

        if (emitter.Instance != null && emitter.HazardType == hazardType && !emitter.UsesFallback)
        {
            return;
        }

        ClearEmitterFallback(emitter);
        if (emitter.Instance != null)
        {
            Destroy(emitter.Instance);
        }

        Object visualObject = Instantiate((Object)prefab);
        emitter.Instance = visualObject as GameObject;
        if (emitter.Instance == null)
        {
            Debug.LogWarning(
                $"{nameof(FireClusterView)} on '{name}' could not instantiate visual prefab '{prefab.name}'.",
                this);
            emitter.ParticleSystems = null;
            EnsureEmitterFallback(emitter);
            emitter.HazardType = hazardType;
            return;
        }

        emitter.Instance.name = $"{prefab.name}_Node";
        emitter.Instance.transform.SetParent(emitter.Root, false);
        emitter.Instance.transform.localPosition = Vector3.zero;
        emitter.Instance.transform.localRotation = Quaternion.identity;
        emitter.Instance.transform.localScale = Vector3.one;
        emitter.ParticleSystems = emitter.Instance.GetComponentsInChildren<ParticleSystem>(true);
        emitter.HazardType = hazardType;
        emitter.UsesFallback = false;
    }

    private void SetClusterParticlesActive(bool shouldPlay)
    {
        if (particleSystems == null)
        {
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null)
            {
                continue;
            }

            if (shouldPlay)
            {
                if (!ps.isPlaying)
                {
                    ps.Play(true);
                }
            }
            else if (ps.isPlaying || ps.particleCount > 0)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private static void SetEmitterParticlesActive(RuntimeFlameEmitter emitter, bool shouldPlay)
    {
        if (emitter.ParticleSystems == null)
        {
            return;
        }

        for (int i = 0; i < emitter.ParticleSystems.Length; i++)
        {
            ParticleSystem ps = emitter.ParticleSystems[i];
            if (ps == null)
            {
                continue;
            }

            if (shouldPlay)
            {
                if (!ps.isPlaying)
                {
                    ps.Play(true);
                }
            }
            else if (ps.isPlaying || ps.particleCount > 0)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private void EnsureEmitterFallback(RuntimeFlameEmitter emitter)
    {
        if (!createFallbackVisualWhenNoParticles || emitter.UsesFallback)
        {
            return;
        }

        emitter.UsesFallback = true;
        emitter.FallbackCoreRenderer = CreateFallbackPrimitive("Core", PrimitiveType.Sphere, fallbackCoreColor, out emitter.FallbackCoreMaterial);
        emitter.FallbackGlowRenderer = CreateFallbackPrimitive("Glow", PrimitiveType.Sphere, fallbackGlowColor, out emitter.FallbackGlowMaterial);

        if (emitter.FallbackCoreRenderer != null)
        {
            emitter.FallbackCoreRenderer.transform.SetParent(emitter.Root, false);
            emitter.FallbackCoreRenderer.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            emitter.FallbackCoreRenderer.transform.localScale = new Vector3(0.32f, 0.55f, 0.32f);
        }

        if (emitter.FallbackGlowRenderer != null)
        {
            emitter.FallbackGlowRenderer.transform.SetParent(emitter.Root, false);
            emitter.FallbackGlowRenderer.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            emitter.FallbackGlowRenderer.transform.localScale = new Vector3(0.75f, 0.22f, 0.75f);
        }
    }

    private void ApplyEmitterFallback(RuntimeFlameEmitter emitter, FireClusterMemberSnapshot member)
    {
        if (!emitter.UsesFallback)
        {
            return;
        }

        float intensity = Mathf.Clamp01(member.Intensity);
        bool visible = intensity > 0.01f;
        if (emitter.FallbackCoreRenderer != null)
        {
            emitter.FallbackCoreRenderer.gameObject.SetActive(visible);
        }

        if (emitter.FallbackGlowRenderer != null)
        {
            emitter.FallbackGlowRenderer.gameObject.SetActive(visible);
        }

        if (!visible)
        {
            return;
        }

        if (emitter.FallbackCoreMaterial != null)
        {
            emitter.FallbackCoreMaterial.color = Color.Lerp(
                new Color(1f, 0.35f, 0.08f, 0.8f),
                fallbackCoreColor,
                intensity);
        }

        if (emitter.FallbackGlowMaterial != null)
        {
            emitter.FallbackGlowMaterial.color = Color.Lerp(
                new Color(1f, 0.12f, 0.02f, 0.2f),
                fallbackGlowColor,
                intensity);
        }
    }

    private void ClearEmitterFallback(RuntimeFlameEmitter emitter)
    {
        if (!emitter.UsesFallback)
        {
            return;
        }

        if (emitter.FallbackCoreRenderer != null)
        {
            Destroy(emitter.FallbackCoreRenderer.gameObject);
            emitter.FallbackCoreRenderer = null;
        }

        if (emitter.FallbackGlowRenderer != null)
        {
            Destroy(emitter.FallbackGlowRenderer.gameObject);
            emitter.FallbackGlowRenderer = null;
        }

        if (emitter.FallbackCoreMaterial != null)
        {
            Destroy(emitter.FallbackCoreMaterial);
            emitter.FallbackCoreMaterial = null;
        }

        if (emitter.FallbackGlowMaterial != null)
        {
            Destroy(emitter.FallbackGlowMaterial);
            emitter.FallbackGlowMaterial = null;
        }

        emitter.UsesFallback = false;
    }

    private void DestroyEmitterResources(RuntimeFlameEmitter emitter)
    {
        if (emitter == null)
        {
            return;
        }

        ClearEmitterFallback(emitter);
        if (emitter.Instance != null)
        {
            Destroy(emitter.Instance);
            emitter.Instance = null;
        }
    }

    private Renderer CreateFallbackPrimitive(string objectName, PrimitiveType primitiveType, Color color, out Material runtimeMaterial)
    {
        runtimeMaterial = null;
        GameObject primitive = GameObject.CreatePrimitive(primitiveType);
        primitive.name = objectName;
        primitive.layer = gameObject.layer;

        Collider primitiveCollider = primitive.GetComponent<Collider>();
        if (primitiveCollider != null)
        {
            Destroy(primitiveCollider);
        }

        Renderer renderer = primitive.GetComponent<Renderer>();
        if (renderer == null)
        {
            return null;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader != null)
        {
            runtimeMaterial = new Material(shader);
            runtimeMaterial.color = color;
            renderer.sharedMaterial = runtimeMaterial;
        }

        return renderer;
    }
}
