using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class SmokeVentVfxController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SmokeHazard smokeHazard;
    [SerializeField] private MonoBehaviour ventPointSource;
    [SerializeField] private Transform[] emitPoints = System.Array.Empty<Transform>();
    [SerializeField] private GameObject smokePrefab;

    [Header("Discovery")]
    [SerializeField] private bool autoFindNearestSmokeHazard = true;
    [SerializeField] private float autoFindRadius = 20f;
    [SerializeField] private bool preferMeshShatterAnchorsForWindows = true;

    [Header("Preview")]
    [SerializeField] private bool usePreviewDensityWhenNoHazard;
    [Range(0f, 1f)]
    [SerializeField] private float previewSmokeDensity = 0.65f;

    [Header("Activation")]
    [Range(0f, 1f)]
    [SerializeField] private float emissionStartThreshold = 0.08f;
    [Range(0f, 1f)]
    [SerializeField] private float emissionStopThreshold = 0.04f;
    [SerializeField] private float ventStrengthMultiplier = 2.5f;

    [Header("Direction")]
    [SerializeField] private Vector3 localVentDirection = Vector3.forward;
    [Range(0f, 1f)]
    [SerializeField] private float upwardBias = 0.55f;
    [SerializeField] private float surfaceOffset = 0.08f;

    [Header("Particles")]
    [SerializeField] private bool useBuiltInSmokeWhenNoPrefab = true;
    [SerializeField] private float maxEmissionRate = 18f;
    [SerializeField] private float particleLifetime = 2.4f;
    [SerializeField] private Vector2 startSpeedRange = new Vector2(0.4f, 1.6f);
    [SerializeField] private Vector2 startSizeRange = new Vector2(0.35f, 0.9f);
    [SerializeField] private float noiseStrength = 0.25f;
    [SerializeField] private Color smokeTint = new Color(0.45f, 0.45f, 0.45f, 0.55f);

    [Header("Runtime")]
    [SerializeField] private bool isEmitting;

    private readonly List<AnchorBinding> anchorBindings = new List<AnchorBinding>();
    private readonly List<EmitterInstance> emitters = new List<EmitterInstance>();
    private Transform runtimeRoot;
    private ISmokeVentPoint cachedVentPoint;

    private sealed class AnchorBinding
    {
        public Transform Transform;
        public Renderer Renderer;
    }

    private sealed class EmitterInstance
    {
        public Transform Root;
        public bool UsesBuiltInSmoke;
        public readonly List<ParticleSystemBinding> ParticleSystems = new List<ParticleSystemBinding>();
    }

    private sealed class ParticleSystemBinding
    {
        public ParticleSystem System;
        public float BaseRateOverTimeMultiplier;
        public float BaseRateOverDistanceMultiplier;
    }

    private void Awake()
    {
        ResolveReferences();
        RebuildEmitters();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RebuildEmitters();
        SetEmissionStrength(0f);
    }

    private void OnDisable()
    {
        SetEmissionStrength(0f);
        isEmitting = false;
    }

    private void OnValidate()
    {
        autoFindRadius = Mathf.Max(0f, autoFindRadius);
        previewSmokeDensity = Mathf.Clamp01(previewSmokeDensity);
        emissionStartThreshold = Mathf.Clamp01(emissionStartThreshold);
        emissionStopThreshold = Mathf.Clamp01(emissionStopThreshold);
        if (emissionStopThreshold > emissionStartThreshold)
        {
            emissionStopThreshold = emissionStartThreshold;
        }

        ventStrengthMultiplier = Mathf.Max(0f, ventStrengthMultiplier);
        upwardBias = Mathf.Clamp01(upwardBias);
        surfaceOffset = Mathf.Max(0f, surfaceOffset);
        maxEmissionRate = Mathf.Max(0f, maxEmissionRate);
        particleLifetime = Mathf.Max(0.1f, particleLifetime);
        startSpeedRange.x = Mathf.Max(0f, startSpeedRange.x);
        startSpeedRange.y = Mathf.Max(startSpeedRange.x, startSpeedRange.y);
        startSizeRange.x = Mathf.Max(0.01f, startSizeRange.x);
        startSizeRange.y = Mathf.Max(startSizeRange.x, startSizeRange.y);
        noiseStrength = Mathf.Max(0f, noiseStrength);
        smokeTint.a = Mathf.Clamp01(smokeTint.a);

        if (!Application.isPlaying)
        {
            ResolveReferences();
        }
    }

    private void Update()
    {
        ResolveReferences();
        EnsureEmitterCountMatchesAnchors();
        UpdateEmitterTransforms();

        float smokeDensity = ResolveSmokeDensity();
        float emissionStrength = CalculateEmissionStrength(smokeDensity);
        float threshold = isEmitting ? emissionStopThreshold : emissionStartThreshold;
        bool shouldEmit = emissionStrength > threshold;

        isEmitting = shouldEmit;
        SetEmissionStrength(shouldEmit ? emissionStrength : 0f);
    }

    private void ResolveReferences()
    {
        if (ventPointSource == null)
        {
            Window window = GetComponent<Window>();
            Door door = window == null ? GetComponent<Door>() : null;
            Vent vent = window == null && door == null ? GetComponent<Vent>() : null;

            if (window == null && door == null && vent == null)
            {
                ventPointSource = null;
            }
            else if (window != null)
            {
                ventPointSource = window;
            }
            else if (door != null)
            {
                ventPointSource = door;
            }
            else
            {
                ventPointSource = vent;
            }
        }

        cachedVentPoint = ventPointSource as ISmokeVentPoint;
        if (smokeHazard == null && autoFindNearestSmokeHazard)
        {
            smokeHazard = FindNearestSmokeHazard();
        }
    }

    private SmokeHazard FindNearestSmokeHazard()
    {
        SmokeHazard[] hazards = FindObjectsByType<SmokeHazard>(FindObjectsInactive.Exclude);
        SmokeHazard nearest = null;
        float nearestDistanceSq = autoFindRadius <= 0f ? float.PositiveInfinity : autoFindRadius * autoFindRadius;
        Vector3 origin = transform.position;

        for (int i = 0; i < hazards.Length; i++)
        {
            SmokeHazard candidate = hazards[i];
            if (candidate == null)
            {
                continue;
            }

            float distanceSq = (candidate.transform.position - origin).sqrMagnitude;
            if (distanceSq <= nearestDistanceSq)
            {
                nearestDistanceSq = distanceSq;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private float ResolveSmokeDensity()
    {
        if (smokeHazard != null)
        {
            return Mathf.Clamp01(smokeHazard.CurrentSmokeDensity);
        }

        return usePreviewDensityWhenNoHazard ? previewSmokeDensity : 0f;
    }

    private float CalculateEmissionStrength(float smokeDensity)
    {
        if (cachedVentPoint == null)
        {
            return 0f;
        }

        float ventilationRelief = Mathf.Max(0f, cachedVentPoint.SmokeVentilationRelief);
        if (ventilationRelief <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(smokeDensity * ventilationRelief * ventStrengthMultiplier);
    }

    private void RebuildEmitters()
    {
        RebuildAnchorBindings();
        EnsureRuntimeRoot();
        EnsureEmitterCountMatchesAnchors(forceRebuild: true);
        UpdateEmitterTransforms();
    }

    private void RebuildAnchorBindings()
    {
        anchorBindings.Clear();

        if (emitPoints != null && emitPoints.Length > 0)
        {
            for (int i = 0; i < emitPoints.Length; i++)
            {
                Transform emitPoint = emitPoints[i];
                if (emitPoint == null)
                {
                    continue;
                }

                anchorBindings.Add(new AnchorBinding
                {
                    Transform = emitPoint,
                    Renderer = emitPoint.GetComponent<Renderer>()
                });
            }
        }

        if (anchorBindings.Count == 0 &&
            preferMeshShatterAnchorsForWindows &&
            ventPointSource is Window window)
        {
            MeshShatter[] shatters = window.GetComponentsInChildren<MeshShatter>(true);
            for (int i = 0; i < shatters.Length; i++)
            {
                MeshShatter shatter = shatters[i];
                if (shatter == null)
                {
                    continue;
                }

                Transform anchorTransform = shatter.transform;
                anchorBindings.Add(new AnchorBinding
                {
                    Transform = anchorTransform,
                    Renderer = anchorTransform.GetComponent<Renderer>()
                });
            }
        }

        if (anchorBindings.Count == 0)
        {
            anchorBindings.Add(new AnchorBinding
            {
                Transform = transform,
                Renderer = GetComponent<Renderer>()
            });
        }
    }

    private void EnsureRuntimeRoot()
    {
        if (runtimeRoot != null)
        {
            return;
        }

        GameObject root = new GameObject(name + "_SmokeVentVfx");
        runtimeRoot = root.transform;
        runtimeRoot.SetParent(transform, false);
        runtimeRoot.localPosition = Vector3.zero;
        runtimeRoot.localRotation = Quaternion.identity;
        runtimeRoot.localScale = Vector3.one;
    }

    private void EnsureEmitterCountMatchesAnchors(bool forceRebuild = false)
    {
        if (!forceRebuild && emitters.Count == anchorBindings.Count)
        {
            return;
        }

        for (int i = emitters.Count - 1; i >= 0; i--)
        {
            EmitterInstance emitter = emitters[i];
            if (emitter?.Root != null)
            {
                DestroyEmitterRoot(emitter.Root.gameObject);
            }
        }

        emitters.Clear();
        if (runtimeRoot == null)
        {
            return;
        }

        for (int i = 0; i < anchorBindings.Count; i++)
        {
            EmitterInstance emitter = CreateEmitterInstance(i);
            if (emitter != null)
            {
                emitters.Add(emitter);
            }
        }
    }

    private EmitterInstance CreateEmitterInstance(int index)
    {
        if (smokePrefab != null)
        {
            return CreatePrefabEmitter(index);
        }

        if (useBuiltInSmokeWhenNoPrefab)
        {
            return CreateBuiltInEmitter(index);
        }

        return new EmitterInstance();
    }

    private EmitterInstance CreatePrefabEmitter(int index)
    {
        GameObject emitterObject = Instantiate(smokePrefab, runtimeRoot);
        emitterObject.name = smokePrefab.name + "_SmokeEmitter_" + index;
        emitterObject.transform.localPosition = Vector3.zero;
        emitterObject.transform.localRotation = Quaternion.identity;
        emitterObject.transform.localScale = Vector3.one;

        EmitterInstance emitter = new EmitterInstance
        {
            Root = emitterObject.transform,
            UsesBuiltInSmoke = false
        };

        CacheParticleSystems(emitter, emitterObject.GetComponentsInChildren<ParticleSystem>(true));
        SetPrefabEmissionStrength(emitter, 0f);
        return emitter;
    }

    private EmitterInstance CreateBuiltInEmitter(int index)
    {
        GameObject particleObject = new GameObject("SmokeEmitter_" + index);
        particleObject.transform.SetParent(runtimeRoot, false);

        ParticleSystem particleSystem = particleObject.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        ConfigureParticleSystem(particleSystem);
        particleSystem.Play(true);

        EmitterInstance emitter = new EmitterInstance
        {
            Root = particleObject.transform,
            UsesBuiltInSmoke = true
        };

        CacheParticleSystems(emitter, new[] { particleSystem });
        return emitter;
    }

    private void CacheParticleSystems(EmitterInstance emitter, ParticleSystem[] systems)
    {
        if (emitter == null || systems == null)
        {
            return;
        }

        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem system = systems[i];
            if (system == null)
            {
                continue;
            }

            ParticleSystem.EmissionModule emission = system.emission;
            emitter.ParticleSystems.Add(new ParticleSystemBinding
            {
                System = system,
                BaseRateOverTimeMultiplier = emission.rateOverTimeMultiplier,
                BaseRateOverDistanceMultiplier = emission.rateOverDistanceMultiplier
            });

            if (Application.isPlaying)
            {
                system.Play(true);
            }
        }
    }

    private void ConfigureParticleSystem(ParticleSystem particleSystem)
    {
        ParticleSystem.MainModule main = particleSystem.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 512;
        main.startLifetime = particleLifetime;
        main.startSpeed = startSpeedRange.x;
        main.startSize = startSizeRange.x;
        main.startColor = smokeTint;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.12f;
        shape.length = 0.18f;

        ParticleSystem.NoiseModule noise = particleSystem.noise;
        noise.enabled = true;
        noise.strength = noiseStrength;
        noise.frequency = 0.2f;
        noise.scrollSpeed = 0.25f;
        noise.damping = true;
        noise.separateAxes = false;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.45f),
            new Keyframe(0.35f, 0.9f),
            new Keyframe(1f, 1.3f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.22f, 0.22f, 0.22f), 0f),
                new GradientColorKey(new Color(0.45f, 0.45f, 0.45f), 0.35f),
                new GradientColorKey(new Color(0.65f, 0.65f, 0.65f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(smokeTint.a * 0.85f, 0.1f),
                new GradientAlphaKey(smokeTint.a * 0.55f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private void UpdateEmitterTransforms()
    {
        if (emitters.Count == 0 || anchorBindings.Count == 0)
        {
            return;
        }

        Vector3 direction = ResolveVentDirection();
        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);

        for (int i = 0; i < emitters.Count && i < anchorBindings.Count; i++)
        {
            EmitterInstance emitter = emitters[i];
            AnchorBinding binding = anchorBindings[i];
            if (emitter?.Root == null || binding == null || binding.Transform == null)
            {
                continue;
            }

            Vector3 position = ResolveAnchorPosition(binding, direction);
            emitter.Root.SetPositionAndRotation(position, rotation);
        }
    }

    private Vector3 ResolveAnchorPosition(AnchorBinding binding, Vector3 direction)
    {
        if (binding.Renderer != null)
        {
            return binding.Renderer.bounds.center + direction * surfaceOffset;
        }

        return binding.Transform.position + direction * surfaceOffset;
    }

    private Vector3 ResolveVentDirection()
    {
        Vector3 outwardDirection = transform.TransformDirection(localVentDirection);
        if (outwardDirection.sqrMagnitude <= 0.0001f)
        {
            outwardDirection = transform.forward;
        }

        if (outwardDirection.sqrMagnitude <= 0.0001f)
        {
            outwardDirection = Vector3.forward;
        }

        outwardDirection.Normalize();
        Vector3 direction = Vector3.Lerp(outwardDirection, Vector3.up, upwardBias);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.up;
        }

        return direction.normalized;
    }

    private void SetEmissionStrength(float strength)
    {
        float clampedStrength = Mathf.Clamp01(strength);

        for (int i = 0; i < emitters.Count; i++)
        {
            EmitterInstance emitter = emitters[i];
            if (emitter == null)
            {
                continue;
            }

            if (emitter.UsesBuiltInSmoke)
            {
                SetBuiltInEmissionStrength(emitter, clampedStrength);
            }
            else
            {
                SetPrefabEmissionStrength(emitter, clampedStrength);
            }
        }
    }

    private void SetBuiltInEmissionStrength(EmitterInstance emitter, float strength)
    {
        if (emitter.ParticleSystems.Count == 0)
        {
            return;
        }

        ParticleSystem system = emitter.ParticleSystems[0].System;
        if (system == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = maxEmissionRate * strength;

        ParticleSystem.MainModule main = system.main;
        main.startSpeed = Mathf.Lerp(startSpeedRange.x, startSpeedRange.y, strength);
        main.startSize = Mathf.Lerp(startSizeRange.x, startSizeRange.y, strength);
        main.startColor = Color.Lerp(
            new Color(smokeTint.r, smokeTint.g, smokeTint.b, smokeTint.a * 0.45f),
            smokeTint,
            strength);
    }

    private void SetPrefabEmissionStrength(EmitterInstance emitter, float strength)
    {
        if (emitter.Root == null)
        {
            return;
        }

        bool shouldBeActive = strength > 0.0001f;
        if (emitter.Root.gameObject.activeSelf != shouldBeActive)
        {
            emitter.Root.gameObject.SetActive(shouldBeActive);
        }

        for (int i = 0; i < emitter.ParticleSystems.Count; i++)
        {
            ParticleSystemBinding binding = emitter.ParticleSystems[i];
            ParticleSystem system = binding.System;
            if (system == null)
            {
                continue;
            }

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTimeMultiplier = binding.BaseRateOverTimeMultiplier * strength;
            emission.rateOverDistanceMultiplier = binding.BaseRateOverDistanceMultiplier * strength;

            if (!shouldBeActive)
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            else if (!system.isPlaying)
            {
                system.Play(true);
            }
        }
    }

    private void DestroyEmitterRoot(GameObject emitterObject)
    {
        if (emitterObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(emitterObject);
        }
        else
        {
            DestroyImmediate(emitterObject);
        }
    }
}
