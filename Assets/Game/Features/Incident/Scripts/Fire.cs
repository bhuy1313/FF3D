using System.Collections.Generic;
using TrueJourney.BotBehavior;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(NavMeshModifier))]
public class Fire : MonoBehaviour, IFireTarget
    , IThermalSignatureSource
{
    private enum ParticleUpAxis
    {
        Up,
        Forward,
        Right
    }

    [Header("Fire State")]
    [FormerlySerializedAs("maxIntensity")]
    [SerializeField] private float maxHp = 1f;
    [FormerlySerializedAs("minIntensityToLive")]
    [SerializeField] private float minHpToLive = 0.05f;
    [FormerlySerializedAs("regrowRate")]
    [SerializeField] private float regrowHpPerSecond = 0.05f;
    [SerializeField] private bool startLitOnEnable = false;
    [SerializeField] private bool allowRegrowFromZero = false;
    [SerializeField] private float regrowResumeDelay = 1.5f;
    [FormerlySerializedAs("currentIntensity")]
    [SerializeField] private float currentHp = 0f;

    [Header("Fire Spread")]
    [SerializeField] private bool enableSpread = true;
    [SerializeField] private bool allowRegrow = true;
    [SerializeField] private float minRadius = 0.1f;
    [SerializeField] private float maxRadius = 1f;
    [SerializeField] private float currentRadius = 0f;
    [SerializeField] private float spreadInterval = 1f;
    [SerializeField] private float spreadIgniteAmount = 0.2f;
    [Range(0f, 1f)]
    [FormerlySerializedAs("spreadMinNormalizedIntensity")]
    [SerializeField] private float spreadMinNormalizedHp = 0.3f;
    [SerializeField] private bool spreadOnlyToUnlitTargets = true;
    [SerializeField] private int spreadMaxOverlaps = 16;
    [SerializeField] private LayerMask spreadLayerMask = ~0;
    [SerializeField] private QueryTriggerInteraction spreadTriggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Extinguish")]
    [SerializeField] private bool disableGameObjectOnExtinguish = false;

    [Header("Hazard Type")]
    [SerializeField] private FireHazardType fireType = FireHazardType.OrdinaryCombustibles;
    [SerializeField] private bool startHazardIsolated = false;
    [SerializeField] private bool requiresIsolationToFullyExtinguish = false;
    [SerializeField] private float hazardActiveRegrowMultiplier = 2f;
    [Range(0f, 1f)]
    [SerializeField] private float hazardActiveMinimumHpNormalized = 0.18f;

    [Header("Player Damage")]
    [SerializeField] private float damagePerSecond = 10f;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool damageScalesWithIntensity = true;

    [Header("Navigation Blocking")]
    [SerializeField] private bool removeFromNavMeshWhileBurning = true;
    [SerializeField] private bool navMeshModifierApplyToChildren = true;

    [Header("Visuals")]
    [SerializeField] private ParticleSystem fireParticleSystem;
    [SerializeField] private Transform particleVisualRoot;
    [SerializeField] private bool includeChildParticleSystems = true;
    [SerializeField] private bool keepParticleWorldUp = false;
    [SerializeField] private ParticleUpAxis particleUpAxis = ParticleUpAxis.Forward;
    [SerializeField] private bool scaleParticleObjectWithIntensity = true;
    [SerializeField] private float minParticleObjectScaleMultiplier = 0.1f;
    [SerializeField] private float maxParticleObjectScaleMultiplier = 1f;
    [SerializeField] private Light fireLight;
    [SerializeField] private bool scaleWithIntensity = true;
    [SerializeField] private Vector3 maxScale = Vector3.one;
    [SerializeField] private float maxLightIntensity = 2f;

    private SphereCollider sphereCollider;
    private NavMeshModifier navMeshModifier;
    private float spreadTimer;
    private Collider[] spreadBuffer;
    private readonly HashSet<Fire> spreadTargets = new HashSet<Fire>();
    private readonly List<ParticleSystem> managedParticleSystems = new List<ParticleSystem>();
    private readonly List<Transform> particleRootTransforms = new List<Transform>();
    private readonly List<Vector3> particleRootBaseLocalScales = new List<Vector3>();
    private readonly List<Quaternion> particleRootBaseLocalRotations = new List<Quaternion>();
    private readonly List<Vector3> managedParticleBaseLocalScales = new List<Vector3>();
    private readonly List<float> managedParticleScaleExponents = new List<float>();
    private readonly List<ParticleSystemScalingMode> particleScalingModes = new List<ParticleSystemScalingMode>();
    private readonly List<bool> particleUses3DStartSize = new List<bool>();
    private readonly List<float> particleBaseStartSizeMultipliers = new List<float>();
    private readonly List<Vector3> particleBaseStartSize3DMultipliers = new List<Vector3>();
    private Vector3 particleVisualRootBaseLocalScale = Vector3.one;
    private float lastWaterAppliedTime = float.NegativeInfinity;
    private FireAudioEmitter fireAudioEmitter;
    [SerializeField] private bool hazardSourceIsolated;

    public bool IsBurning => currentHp > 0f;
    public bool AllowRegrowFromZero => allowRegrowFromZero;
    public float CurrentHp => currentHp;
    public float NormalizedHp => GetNormalizedHp();
    public FireHazardType FireType => fireType;
    public bool IsHazardSourceIsolated => hazardSourceIsolated;
    public bool HasThermalSignature => IsBurning;
    public ThermalSignatureCategory ThermalSignatureCategory => ThermalSignatureCategory.Fire;

    public float CurrentContactDamagePerSecond
    {
        get
        {
            if (damagePerSecond <= 0f || currentHp <= 0f)
            {
                return 0f;
            }

            float scale = damageScalesWithIntensity ? GetNormalizedHp() : 1f;
            return scale > 0f ? damagePerSecond * scale : 0f;
        }
    }

    public event System.Action<bool> BurningStateChanged;
    public event System.Action Ignited;
    public event System.Action Extinguished;

    public Vector3 GetWorldPosition()
    {
        return transform.position;
    }

    public float GetWorldRadius()
    {
        return GetSphereRadiusWorld();
    }

    private void Reset()
    {
        CacheReferences();
        EnsureCollider();
        EnsureNavMeshModifier();
        EnsureSpreadBuffer();
        SyncRadiusAndCollider();
        SyncNavMeshModifier();
        ApplyVisuals(forcePlayState: true);
    }

    private void Awake()
    {
        CacheReferences();
        EnsureCollider();
        EnsureNavMeshModifier();
        EnsureSpreadBuffer();
        ApplyHazardDefaults();
    }

    private void OnEnable()
    {
        BotRuntimeRegistry.RegisterFireTarget(this);
        BotRuntimeRegistry.RegisterThermalSignatureSource(this);

        if (startLitOnEnable && currentHp <= 0f)
            currentHp = maxHp;

        currentHp = Mathf.Clamp(currentHp, 0f, Mathf.Max(0f, maxHp));
        spreadTimer = 0f;
        lastWaterAppliedTime = float.NegativeInfinity;
        hazardSourceIsolated = startHazardIsolated;
        SyncRadiusAndCollider();
        SyncNavMeshModifier();
        ApplyVisuals(forcePlayState: true);
        EnsureFireAudioEmitter();
        fireAudioEmitter?.Initialize(this);
    }

    private void OnDisable()
    {
        BotRuntimeRegistry.UnregisterFireTarget(this);
        BotRuntimeRegistry.UnregisterThermalSignatureSource(this);
        if (navMeshModifier != null)
        {
            navMeshModifier.enabled = false;
        }

        fireAudioEmitter?.HandleFireDisabled();
    }

    public Vector3 GetThermalSignatureWorldPosition()
    {
        return GetWorldPosition();
    }

    public float GetThermalSignatureStrength()
    {
        if (!IsBurning)
        {
            return 0f;
        }

        return Mathf.Lerp(0.45f, 1f, GetNormalizedHp());
    }

    private void Update()
    {
        RegrowHp();
        SyncRadiusAndCollider();
        SyncNavMeshModifier();
        ApplyVisuals();
        TrySpreadFire();
    }

    private void LateUpdate()
    {
        KeepParticlesWorldUp();
    }

    public void Ignite(float amount)
    {
        if (amount <= 0f) return;

        bool wasBurning = IsBurning;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        currentHp = Mathf.Clamp(currentHp + amount, 0f, Mathf.Max(0f, maxHp));
        NotifyBurningStateChangeIfNeeded(wasBurning);
    }

    public void ApplyWater(float amount)
    {
        ApplySuppression(amount, FireSuppressionAgent.Water);
    }

    public void ApplySuppression(float amount, FireSuppressionAgent agent)
    {
        if (amount <= 0f) return;
        if (currentHp <= 0f) return;

        float effectiveAmount = amount * GetSuppressionEffectiveness(agent);
        if (effectiveAmount <= 0f)
        {
            return;
        }

        bool wasBurning = IsBurning;
        float previousHp = currentHp;
        currentHp = Mathf.Max(GetMinimumRemainingHpWhileHazardActive(), currentHp - effectiveAmount);
        if (currentHp < previousHp)
        {
            lastWaterAppliedTime = Time.time;
        }

        if (currentHp <= 0f)
        {
            Extinguish(wasBurning);
            return;
        }

        NotifyBurningStateChangeIfNeeded(wasBurning);
    }

    public void SetAllowRegrowFromZero(bool allow)
    {
        allowRegrowFromZero = allow;
    }

    public void SetRequiresIsolationToFullyExtinguish(bool required)
    {
        requiresIsolationToFullyExtinguish = required;
        ApplyHazardDefaults();
    }

    public void SetHazardSourceIsolated(bool isolated)
    {
        hazardSourceIsolated = isolated;
    }

    public void SetFireHazardType(FireHazardType type)
    {
        fireType = type;
        ApplyHazardDefaults();
    }

    public void SetSpreadEnabled(bool enabled)
    {
        enableSpread = enabled;
        spreadTimer = 0f;
    }

    public void ConfigureSpreadProfile(float intervalSeconds, float igniteAmount, float minNormalizedHp)
    {
        spreadInterval = Mathf.Max(0.05f, intervalSeconds);
        spreadIgniteAmount = Mathf.Max(0f, igniteAmount);
        spreadMinNormalizedHp = Mathf.Clamp01(minNormalizedHp);
        spreadTimer = 0f;
    }

    public void SetBurningLevel01(float intensity01)
    {
        bool wasBurning = IsBurning;

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        currentHp = Mathf.Clamp01(intensity01) * Mathf.Max(0f, maxHp);
        NotifyBurningStateChangeIfNeeded(wasBurning);
        SyncRadiusAndCollider();
        SyncNavMeshModifier();
        ApplyVisuals(forcePlayState: true);
    }

    public void ToggleHazardSourceIsolation()
    {
        SetHazardSourceIsolated(!hazardSourceIsolated);
    }

    private void Extinguish()
    {
        Extinguish(IsBurning);
    }

    private void Extinguish(bool wasBurning)
    {
        currentHp = 0f;
        NotifyBurningStateChangeIfNeeded(wasBurning);
        if (disableGameObjectOnExtinguish)
            gameObject.SetActive(false);
    }

    private void RegrowHp()
    {
        bool wasBurning = IsBurning;
        if (regrowHpPerSecond <= 0f) return;
        if (!allowRegrowFromZero && currentHp <= 0f) return;
        if (currentHp >= maxHp) return;
        if (Time.time < lastWaterAppliedTime + Mathf.Max(0f, regrowResumeDelay)) return;

        currentHp = Mathf.Min(maxHp, currentHp + regrowHpPerSecond * GetHazardRegrowMultiplier() * Time.deltaTime);
        NotifyBurningStateChangeIfNeeded(wasBurning);
    }

    private void NotifyBurningStateChangeIfNeeded(bool wasBurning)
    {
        bool isBurning = IsBurning;
        if (isBurning == wasBurning)
        {
            return;
        }

        BurningStateChanged?.Invoke(isBurning);
        if (isBurning)
        {
            Ignited?.Invoke();
        }
        else
        {
            Extinguished?.Invoke();
        }
    }

    private void TrySpreadFire()
    {
        if (!enableSpread) return;
        if (spreadIgniteAmount <= 0f) return;
        if (currentHp <= 0f || maxHp <= 0f) return;

        float normalizedHp = GetNormalizedHp();
        if (normalizedHp < spreadMinNormalizedHp) return;

        spreadTimer -= Time.deltaTime;
        if (spreadTimer > 0f) return;
        spreadTimer = spreadInterval;

        float spreadRadiusWorld = GetSphereRadiusWorld();
        if (spreadRadiusWorld <= 0f) return;

        int hitCount = Physics.OverlapSphereNonAlloc(
            GetSpreadCenterWorld(),
            spreadRadiusWorld,
            spreadBuffer,
            spreadLayerMask,
            spreadTriggerInteraction);

        spreadTargets.Clear();
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = spreadBuffer[i];
            if (hit == null) continue;

            Fire target = hit.GetComponentInParent<Fire>();
            if (target == null || target == this) continue;
            if (spreadOnlyToUnlitTargets && target.currentHp > target.minHpToLive) continue;
            if (!spreadTargets.Add(target)) continue;

            target.Ignite(spreadIgniteAmount);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (CurrentContactDamagePerSecond <= 0f) return;
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;

        PlayerVitals vitals = other.GetComponentInParent<PlayerVitals>();
        if (vitals == null || !vitals.IsAlive) return;

        vitals.TakeDamage(CurrentContactDamagePerSecond * Time.deltaTime);
    }

    private void OnParticleCollision(GameObject other)
    {
        if (other != null)
        {
            FireExtinguisher extinguisher = other.GetComponentInParent<FireExtinguisher>();
            if (extinguisher != null)
            {
                // FireExtinguisher now applies water through its own cone-cast pipeline.
                return;
            }

            FireHose hose = other.GetComponentInParent<FireHose>();
            if (hose != null)
            {
                // FireHose uses its own arc/sphere-cast pipeline as the single source of truth.
                return;
            }
        }
    }

    private void ApplyVisuals(bool forcePlayState = false)
    {
        float t01 = GetNormalizedHp();

        if (managedParticleSystems.Count > 0)
        {
            UpdateParticleObjectScale(t01);

            bool shouldPlay = currentHp > 0f;
            for (int i = 0; i < managedParticleSystems.Count; i++)
            {
                ParticleSystem ps = managedParticleSystems[i];
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
                else if (forcePlayState || ps.isPlaying || ps.particleCount > 0)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        if (fireLight != null)
        {
            fireLight.intensity = Mathf.Lerp(0f, maxLightIntensity, t01);
            fireLight.enabled = currentHp > 0f;
        }

        UpdateParticleVisualRootScale(t01);
    }

    private void EnsureFireAudioEmitter()
    {
        if (fireAudioEmitter == null)
        {
            fireAudioEmitter = GetComponent<FireAudioEmitter>();
        }

        if (fireAudioEmitter == null)
        {
            fireAudioEmitter = gameObject.AddComponent<FireAudioEmitter>();
        }
    }

    private void KeepParticlesWorldUp()
    {
        if (!keepParticleWorldUp) return;
        if (particleRootTransforms.Count == 0) return;

        int count = Mathf.Min(particleRootTransforms.Count, particleRootBaseLocalRotations.Count);
        for (int i = 0; i < count; i++)
        {
            Transform psTransform = particleRootTransforms[i];
            if (psTransform == null)
            {
                continue;
            }

            Quaternion parentRotation = psTransform.parent != null
                ? psTransform.parent.rotation
                : Quaternion.identity;
            Quaternion baseWorldRotation = parentRotation * particleRootBaseLocalRotations[i];
            Vector3 selectedAxis = GetSelectedAxis(baseWorldRotation);
            if (selectedAxis.sqrMagnitude <= Mathf.Epsilon)
            {
                continue;
            }

            Quaternion correction = Quaternion.FromToRotation(selectedAxis, Vector3.up);
            psTransform.rotation = correction * baseWorldRotation;
        }
    }

    private Vector3 GetSelectedAxis(Quaternion rotation)
    {
        switch (particleUpAxis)
        {
            case ParticleUpAxis.Up:
                return rotation * Vector3.up;
            case ParticleUpAxis.Right:
                return rotation * Vector3.right;
            default:
                return rotation * Vector3.forward;
        }
    }

    private void SyncRadiusAndCollider()
    {
        EnsureCollider();
        if (sphereCollider == null) return;

        minRadius = Mathf.Max(0.1f, minRadius);
        maxRadius = Mathf.Max(minRadius, maxRadius);

        float t01 = GetNormalizedHp();
        float targetRadius = Mathf.Lerp(minRadius, maxRadius, t01);

        if (allowRegrow)
        {
            currentRadius = targetRadius;
        }
        else
        {
            currentRadius = (currentRadius <= 0f) ? targetRadius : Mathf.Min(currentRadius, targetRadius);
        }

        currentRadius = Mathf.Clamp(currentRadius, minRadius, maxRadius);
        sphereCollider.radius = currentRadius;
    }

    private void OnValidate()
    {
        maxHp = Mathf.Max(0.01f, maxHp);
        minHpToLive = Mathf.Clamp(minHpToLive, 0f, maxHp);
        regrowHpPerSecond = Mathf.Max(0f, regrowHpPerSecond);
        regrowResumeDelay = Mathf.Max(0f, regrowResumeDelay);
        hazardActiveRegrowMultiplier = Mathf.Max(0f, hazardActiveRegrowMultiplier);
        hazardActiveMinimumHpNormalized = Mathf.Clamp01(hazardActiveMinimumHpNormalized);
        ApplyHazardDefaults();
    }

    private void CacheReferences()
    {
        ResolveParticleVisualRoot();
        if (navMeshModifier == null)
        {
            navMeshModifier = GetComponent<NavMeshModifier>();
        }

        if (fireParticleSystem == null)
        {
            fireParticleSystem = ResolvePrimaryParticleSystem();
        }

        if (particleVisualRoot == null && fireParticleSystem != null)
        {
            particleVisualRoot = ResolveTopLevelParticleBranch(fireParticleSystem.transform);
        }

        CacheManagedParticleSystems();
        CacheParticleRootsAndBaseScales();

        if (fireLight == null)
            fireLight = GetComponentInChildren<Light>();
    }

    private void CacheManagedParticleSystems()
    {
        managedParticleSystems.Clear();
        managedParticleBaseLocalScales.Clear();
        managedParticleScaleExponents.Clear();
        particleScalingModes.Clear();
        particleUses3DStartSize.Clear();
        particleBaseStartSizeMultipliers.Clear();
        particleBaseStartSize3DMultipliers.Clear();

        if (fireParticleSystem == null)
        {
            return;
        }

        if (!includeChildParticleSystems)
        {
            managedParticleSystems.Add(fireParticleSystem);
            CacheParticleSizeData(fireParticleSystem);
            CacheManagedTransformScaleData();
            return;
        }

        ParticleSystem[] allSystems = fireParticleSystem.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < allSystems.Length; i++)
        {
            ParticleSystem ps = allSystems[i];
            if (ps != null)
            {
                managedParticleSystems.Add(ps);
                CacheParticleSizeData(ps);
            }
        }

        CacheManagedTransformScaleData();
    }

    private void CacheParticleSizeData(ParticleSystem ps)
    {
        ParticleSystem.MainModule main = ps.main;
        particleScalingModes.Add(main.scalingMode);
        if (main.startSize3D)
        {
            particleUses3DStartSize.Add(true);
            particleBaseStartSizeMultipliers.Add(1f);
            particleBaseStartSize3DMultipliers.Add(new Vector3(
                main.startSizeXMultiplier,
                main.startSizeYMultiplier,
                main.startSizeZMultiplier));
        }
        else
        {
            particleUses3DStartSize.Add(false);
            particleBaseStartSizeMultipliers.Add(main.startSizeMultiplier);
            particleBaseStartSize3DMultipliers.Add(Vector3.one);
        }
    }

    private void CacheManagedTransformScaleData()
    {
        managedParticleBaseLocalScales.Clear();
        managedParticleScaleExponents.Clear();
        if (managedParticleSystems.Count == 0)
        {
            return;
        }

        for (int i = 0; i < managedParticleSystems.Count; i++)
        {
            ParticleSystem ps = managedParticleSystems[i];
            Transform t = ps != null ? ps.transform : null;
            if (t == null)
            {
                managedParticleBaseLocalScales.Add(Vector3.one);
                managedParticleScaleExponents.Add(1f);
                continue;
            }

            managedParticleBaseLocalScales.Add(t.localScale);
            managedParticleScaleExponents.Add(1f);
        }
    }

    private void CacheParticleRootsAndBaseScales()
    {
        particleRootTransforms.Clear();
        particleRootBaseLocalScales.Clear();
        particleRootBaseLocalRotations.Clear();
        particleVisualRootBaseLocalScale = particleVisualRoot != null
            ? particleVisualRoot.localScale
            : Vector3.one;
        if (managedParticleSystems.Count == 0)
        {
            return;
        }

        HashSet<Transform> systemTransforms = new HashSet<Transform>();
        for (int i = 0; i < managedParticleSystems.Count; i++)
        {
            ParticleSystem ps = managedParticleSystems[i];
            if (ps != null)
            {
                systemTransforms.Add(ps.transform);
            }
        }

        for (int i = 0; i < managedParticleSystems.Count; i++)
        {
            ParticleSystem ps = managedParticleSystems[i];
            if (ps == null)
            {
                continue;
            }

            Transform candidate = ps.transform;
            if (HasAncestorInSet(candidate.parent, systemTransforms))
            {
                continue;
            }

            if (particleRootTransforms.Contains(candidate))
            {
                continue;
            }

            particleRootTransforms.Add(candidate);
            particleRootBaseLocalScales.Add(candidate.localScale);
            particleRootBaseLocalRotations.Add(candidate.localRotation);
        }
    }

    private void UpdateParticleObjectScale(float intensity01)
    {
        if (!scaleParticleObjectWithIntensity) return;
        if (managedParticleSystems.Count == 0) return;
        minParticleObjectScaleMultiplier = Mathf.Max(0f, minParticleObjectScaleMultiplier);
        maxParticleObjectScaleMultiplier = Mathf.Max(minParticleObjectScaleMultiplier, maxParticleObjectScaleMultiplier);

        float scaleMul = intensity01 <= 0f
            ? 0f
            : Mathf.Lerp(minParticleObjectScaleMultiplier, maxParticleObjectScaleMultiplier, intensity01);
        int count = Mathf.Min(
            managedParticleSystems.Count,
            Mathf.Min(managedParticleBaseLocalScales.Count, managedParticleScaleExponents.Count));

        for (int i = 0; i < count; i++)
        {
            ParticleSystem ps = managedParticleSystems[i];
            if (ps == null)
            {
                continue;
            }

            float localScaleMul = Mathf.Pow(scaleMul, managedParticleScaleExponents[i]);
            ps.transform.localScale = managedParticleBaseLocalScales[i] * localScaleMul;
        }

        UpdateShapeModeParticleSize(scaleMul);
    }

    private void UpdateParticleVisualRootScale(float intensity01)
    {
        if (particleVisualRoot == null)
        {
            return;
        }

        if (scaleWithIntensity)
        {
            Vector3 visualScale = Vector3.Lerp(Vector3.zero, maxScale, intensity01);
            particleVisualRoot.localScale = Vector3.Scale(particleVisualRootBaseLocalScale, visualScale);
            return;
        }

        particleVisualRoot.localScale = Vector3.Scale(particleVisualRootBaseLocalScale, maxScale);
    }

    private void UpdateShapeModeParticleSize(float scaleMul)
    {
        int count = Mathf.Min(
            managedParticleSystems.Count,
            Mathf.Min(
                particleScalingModes.Count,
                Mathf.Min(
                    particleUses3DStartSize.Count,
                    Mathf.Min(
                        particleBaseStartSizeMultipliers.Count,
                        particleBaseStartSize3DMultipliers.Count))));

        for (int i = 0; i < count; i++)
        {
            if (particleScalingModes[i] != ParticleSystemScalingMode.Shape)
            {
                continue;
            }

            ParticleSystem ps = managedParticleSystems[i];
            if (ps == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = ps.main;
            if (particleUses3DStartSize[i])
            {
                Vector3 baseSize = particleBaseStartSize3DMultipliers[i];
                main.startSizeXMultiplier = baseSize.x * scaleMul;
                main.startSizeYMultiplier = baseSize.y * scaleMul;
                main.startSizeZMultiplier = baseSize.z * scaleMul;
            }
            else
            {
                main.startSizeMultiplier = particleBaseStartSizeMultipliers[i] * scaleMul;
            }
        }
    }

    private static bool HasAncestorInSet(Transform current, HashSet<Transform> set)
    {
        while (current != null)
        {
            if (set.Contains(current))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void EnsureCollider()
    {
        if (sphereCollider == null)
            sphereCollider = GetComponent<SphereCollider>();

        if (sphereCollider == null)
            sphereCollider = gameObject.AddComponent<SphereCollider>();

        sphereCollider.isTrigger = true;
    }

    private void EnsureNavMeshModifier()
    {
        if (navMeshModifier == null)
        {
            navMeshModifier = GetComponent<NavMeshModifier>();
        }

        if (navMeshModifier == null)
        {
            navMeshModifier = gameObject.AddComponent<NavMeshModifier>();
        }

        navMeshModifier.overrideArea = false;
        navMeshModifier.ignoreFromBuild = true;
        navMeshModifier.applyToChildren = navMeshModifierApplyToChildren;
    }

    private void EnsureSpreadBuffer()
    {
        if (spreadMaxOverlaps < 1)
            spreadMaxOverlaps = 1;

        if (spreadBuffer == null || spreadBuffer.Length != spreadMaxOverlaps)
            spreadBuffer = new Collider[spreadMaxOverlaps];
    }

    private void ApplyHazardDefaults()
    {
        if ((fireType == FireHazardType.GasFed || fireType == FireHazardType.Electrical) &&
            !requiresIsolationToFullyExtinguish)
        {
            requiresIsolationToFullyExtinguish = true;
        }
    }

    private float GetSuppressionEffectiveness(FireSuppressionAgent agent)
    {
        switch (fireType)
        {
            case FireHazardType.Electrical:
                switch (agent)
                {
                    case FireSuppressionAgent.Water:
                        return hazardSourceIsolated ? 0.8f : 0f;
                    case FireSuppressionAgent.CO2:
                        return hazardSourceIsolated ? 1f : 1.35f;
                    default:
                        return hazardSourceIsolated ? 1.05f : 1.25f;
                }

            case FireHazardType.FlammableLiquid:
                switch (agent)
                {
                    case FireSuppressionAgent.Water:
                        return 0.2f;
                    case FireSuppressionAgent.CO2:
                        return 1f;
                    default:
                        return 1.2f;
                }

            case FireHazardType.GasFed:
                switch (agent)
                {
                    case FireSuppressionAgent.Water:
                        return hazardSourceIsolated ? 0.85f : 0.3f;
                    case FireSuppressionAgent.CO2:
                        return hazardSourceIsolated ? 1f : 0.4f;
                    default:
                        return hazardSourceIsolated ? 1.1f : 0.45f;
                }

            default:
                switch (agent)
                {
                    case FireSuppressionAgent.Water:
                        return 1f;
                    case FireSuppressionAgent.CO2:
                        return 0.55f;
                    default:
                        return 0.8f;
                }
        }
    }

    private float GetHazardRegrowMultiplier()
    {
        if (hazardSourceIsolated)
        {
            return 1f;
        }

        switch (fireType)
        {
            case FireHazardType.Electrical:
            case FireHazardType.GasFed:
                return hazardActiveRegrowMultiplier;
            default:
                return 1f;
        }
    }

    private float GetMinimumRemainingHpWhileHazardActive()
    {
        if (hazardSourceIsolated || !requiresIsolationToFullyExtinguish || currentHp <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(hazardActiveMinimumHpNormalized) * maxHp;
    }

    private Vector3 GetSpreadCenterWorld()
    {
        if (sphereCollider == null) return transform.position;
        return transform.TransformPoint(sphereCollider.center);
    }

    private float GetSphereRadiusWorld()
    {
        if (sphereCollider == null) return 0f;

        float maxAxisScale = Mathf.Max(
            Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.y),
            Mathf.Abs(transform.lossyScale.z));

        return Mathf.Max(0f, sphereCollider.radius * maxAxisScale);
    }

    private void SyncNavMeshModifier()
    {
        EnsureNavMeshModifier();
        if (navMeshModifier == null)
        {
            return;
        }

        navMeshModifier.enabled = removeFromNavMeshWhileBurning && IsBurning;
        navMeshModifier.overrideArea = false;
        navMeshModifier.ignoreFromBuild = true;
        navMeshModifier.applyToChildren = navMeshModifierApplyToChildren;
    }

    private float GetNormalizedHp()
    {
        return maxHp <= 0f ? 0f : Mathf.Clamp01(currentHp / maxHp);
    }

    private void ResolveParticleVisualRoot()
    {
        if (particleVisualRoot != null)
        {
            return;
        }

        Transform namedParticleChild = transform.Find("Particle");
        if (namedParticleChild != null)
        {
            particleVisualRoot = namedParticleChild;
        }
    }

    private ParticleSystem ResolvePrimaryParticleSystem()
    {
        if (particleVisualRoot != null)
        {
            ParticleSystem particleInVisualRoot = particleVisualRoot.GetComponentInChildren<ParticleSystem>(true);
            if (particleInVisualRoot != null)
            {
                return particleInVisualRoot;
            }
        }

        Transform namedParticleChild = transform.Find("Particle");
        if (namedParticleChild != null)
        {
            ParticleSystem particleInNamedChild = namedParticleChild.GetComponentInChildren<ParticleSystem>(true);
            if (particleInNamedChild != null)
            {
                return particleInNamedChild;
            }
        }

        return GetComponentInChildren<ParticleSystem>(true);
    }

    private Transform ResolveTopLevelParticleBranch(Transform particleTransform)
    {
        if (particleTransform == null)
        {
            return null;
        }

        Transform current = particleTransform;
        while (current.parent != null && current.parent != transform)
        {
            current = current.parent;
        }

        return current;
    }
}
