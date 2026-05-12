using UnityEngine;

[DisallowMultipleComponent]
public sealed class FireNodeEffectView : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Light nodeLight;
    [SerializeField] private ParticleSystem[] particleSystems;
    [SerializeField] private float maxLightIntensity = 2f;
    [SerializeField] private Vector2 flameScaleRange = new Vector2(0.7f, 1.35f);
    [SerializeField] private Vector3 visualOffset = Vector3.zero;
    [SerializeField] private bool scaleVisualOffsetWithEffectScale = true;
    [Header("Visual Variation")]
    [SerializeField] private bool randomizeScalePerNode = true;
    [SerializeField] private Vector2 randomScaleMultiplierRange = new Vector2(0.4f, 1f);
    [SerializeField] private int randomScaleSeed = 9173;
    [Header("Retire")]
    [SerializeField] [Min(0f)] private float retireShrinkDuration = 0.45f;
    [SerializeField] [Min(0f)] private float retireEndScaleMultiplier = 0.05f;
    [SerializeField] private bool stopParticlesWhenRetiring = true;
    [Header("Fallback Visual")]
    [SerializeField] private bool createFallbackVisualWhenNoParticles = true;
    [SerializeField] private Color fallbackCoreColor = new Color(1f, 0.45f, 0.1f, 0.95f);
    [SerializeField] private Color fallbackGlowColor = new Color(1f, 0.2f, 0.02f, 0.55f);

    private int boundNodeIndex = -1;
    private FireHazardType boundHazardType = FireHazardType.OrdinaryCombustibles;
    private Transform fallbackVisualRoot;
    private Renderer fallbackCoreRenderer;
    private Renderer fallbackGlowRenderer;
    private Material fallbackCoreMaterial;
    private Material fallbackGlowMaterial;
    private bool fallbackVisualInitialized;
    private float nodeScaleMultiplier = 1f;
    private Vector3 lastNodePosition;
    private Vector3 lastResolvedOffset;
    private float lastFinalScale = 1f;
    private float retireElapsed;
    private float retireStartScale = 1f;
    private bool retiring;

    public int BoundNodeIndex => boundNodeIndex;
    public FireHazardType BoundHazardType => boundHazardType;
    public bool IsBound => boundNodeIndex >= 0;
    public bool IsRetiring => retiring;

    private void Awake()
    {
        EnsureFallbackVisual();
    }

    private void OnDestroy()
    {
        if (fallbackCoreMaterial != null)
        {
            Destroy(fallbackCoreMaterial);
        }

        if (fallbackGlowMaterial != null)
        {
            Destroy(fallbackGlowMaterial);
        }
    }

    public void Bind(int nodeIndex, FireHazardType hazardType)
    {
        boundNodeIndex = nodeIndex;
        boundHazardType = hazardType;
        retiring = false;
        retireElapsed = 0f;
        nodeScaleMultiplier = ResolveScaleMultiplier(nodeIndex);
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    public void Unbind()
    {
        boundNodeIndex = -1;
        retiring = false;
        retireElapsed = 0f;
        nodeScaleMultiplier = 1f;
        SetParticlesActive(false);

        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    public void ApplySnapshot(FireNodeSnapshot snapshot)
    {
        ApplySnapshot(snapshot, Vector3.one);
    }

    public void ApplySnapshot(FireNodeSnapshot snapshot, float scaleMultiplier)
    {
        ApplySnapshot(snapshot, Vector3.one * Mathf.Max(0.01f, scaleMultiplier));
    }

    public void ApplySnapshot(FireNodeSnapshot snapshot, Vector3 scaleMultiplier)
    {
        EnsureFallbackVisual();

        float intensity01 = Mathf.Clamp01(snapshot.Intensity);
        float scale = Mathf.Lerp(flameScaleRange.x, flameScaleRange.y, intensity01);
        float baseScale = scale * nodeScaleMultiplier;
        Vector3 finalScale = new Vector3(
            baseScale * Mathf.Max(0.01f, scaleMultiplier.x),
            baseScale * Mathf.Max(0.01f, scaleMultiplier.y),
            baseScale * Mathf.Max(0.01f, scaleMultiplier.z));

        Vector3 resolvedOffset = scaleVisualOffsetWithEffectScale
            ? visualOffset * baseScale
            : visualOffset;
        lastNodePosition = snapshot.Position;
        lastResolvedOffset = resolvedOffset;
        lastFinalScale = Mathf.Max(finalScale.x, Mathf.Max(finalScale.y, finalScale.z));
        transform.position = snapshot.Position + resolvedOffset;
        transform.rotation = ResolveVisualRotation(snapshot.SurfaceNormal);
        transform.localScale = finalScale;

        if (visualRoot != null)
        {
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;
        }

        bool visible = intensity01 > 0.01f;

        if (nodeLight != null)
        {
            nodeLight.enabled = visible;
            nodeLight.intensity = intensity01 * maxLightIntensity;
        }

        ApplyFallbackVisual(intensity01, visible);
        SetParticlesActive(visible);
    }

    public void BeginRetire()
    {
        if (retiring)
        {
            return;
        }

        retiring = true;
        retireElapsed = 0f;
        retireStartScale = Mathf.Max(0.0001f, lastFinalScale);
        boundNodeIndex = -1;

        if (nodeLight != null)
        {
            nodeLight.enabled = false;
        }

        ApplyFallbackVisual(0f, false);

        if (stopParticlesWhenRetiring)
        {
            SetParticlesActive(false);
        }
    }

    public bool TickRetire(float deltaTime)
    {
        if (!retiring)
        {
            return true;
        }

        float duration = Mathf.Max(0.0001f, retireShrinkDuration);
        retireElapsed += Mathf.Max(0f, deltaTime);
        float t01 = Mathf.Clamp01(retireElapsed / duration);
        float eased = 1f - (1f - t01) * (1f - t01);
        float targetScale = retireStartScale * Mathf.Max(0f, retireEndScaleMultiplier);
        float currentScale = Mathf.Lerp(retireStartScale, targetScale, eased);
        Vector3 currentOffset = scaleVisualOffsetWithEffectScale
            ? lastResolvedOffset * (currentScale / retireStartScale)
            : lastResolvedOffset;

        transform.position = lastNodePosition + currentOffset;
        transform.localScale = Vector3.one * currentScale;

        return t01 >= 1f;
    }

    private void OnValidate()
    {
        flameScaleRange.x = Mathf.Max(0.01f, flameScaleRange.x);
        flameScaleRange.y = Mathf.Max(0.01f, flameScaleRange.y);
        if (flameScaleRange.y < flameScaleRange.x)
        {
            flameScaleRange.y = flameScaleRange.x;
        }

        randomScaleMultiplierRange.x = Mathf.Max(0.01f, randomScaleMultiplierRange.x);
        randomScaleMultiplierRange.y = Mathf.Max(0.01f, randomScaleMultiplierRange.y);
        if (randomScaleMultiplierRange.y < randomScaleMultiplierRange.x)
        {
            randomScaleMultiplierRange.y = randomScaleMultiplierRange.x;
        }

        retireShrinkDuration = Mathf.Max(0f, retireShrinkDuration);
        retireEndScaleMultiplier = Mathf.Max(0f, retireEndScaleMultiplier);
    }

    private float ResolveScaleMultiplier(int nodeIndex)
    {
        if (!randomizeScalePerNode)
        {
            return 1f;
        }

        float min = Mathf.Min(randomScaleMultiplierRange.x, randomScaleMultiplierRange.y);
        float max = Mathf.Max(randomScaleMultiplierRange.x, randomScaleMultiplierRange.y);
        if (Mathf.Approximately(min, max))
        {
            return min;
        }

        return Mathf.Lerp(min, max, GetStableUnitValue(nodeIndex));
    }

    private float GetStableUnitValue(int nodeIndex)
    {
        unchecked
        {
            uint hash = (uint)randomScaleSeed;
            hash ^= (uint)(nodeIndex + 0x9E3779B9);
            hash *= 0x85EBCA6B;
            hash ^= hash >> 13;
            hash *= 0xC2B2AE35;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFF) / 16777215f;
        }
    }

    private static Quaternion ResolveVisualRotation(Vector3 surfaceNormal)
    {
        Vector3 up = surfaceNormal.sqrMagnitude > 0.001f ? surfaceNormal.normalized : Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, up);
        if (forward.sqrMagnitude <= 0.001f)
        {
            forward = Vector3.ProjectOnPlane(Vector3.right, up);
        }

        if (forward.sqrMagnitude <= 0.001f)
        {
            return Quaternion.identity;
        }

        return Quaternion.LookRotation(forward.normalized, up);
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

    private void EnsureFallbackVisual()
    {
        if (fallbackVisualInitialized)
        {
            return;
        }

        fallbackVisualInitialized = true;
        if (!createFallbackVisualWhenNoParticles || HasConfiguredParticles())
        {
            return;
        }

        Transform root = visualRoot != null ? visualRoot : transform;
        fallbackVisualRoot = new GameObject("FallbackVisual").transform;
        fallbackVisualRoot.SetParent(root, false);

        fallbackCoreRenderer = CreateFallbackPrimitive("Core", fallbackCoreColor, out fallbackCoreMaterial);
        fallbackGlowRenderer = CreateFallbackPrimitive("Glow", fallbackGlowColor, out fallbackGlowMaterial);

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

    private void ApplyFallbackVisual(float intensity01, bool visible)
    {
        if (fallbackVisualRoot == null)
        {
            return;
        }

        fallbackVisualRoot.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        if (fallbackCoreMaterial != null)
        {
            fallbackCoreMaterial.color = Color.Lerp(
                new Color(1f, 0.35f, 0.08f, 0.8f),
                fallbackCoreColor,
                intensity01);
        }

        if (fallbackGlowMaterial != null)
        {
            fallbackGlowMaterial.color = Color.Lerp(
                new Color(1f, 0.12f, 0.02f, 0.2f),
                fallbackGlowColor,
                intensity01);
        }
    }

    private void SetParticlesActive(bool shouldPlay)
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

    private Renderer CreateFallbackPrimitive(string objectName, Color color, out Material runtimeMaterial)
    {
        runtimeMaterial = null;
        GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
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
