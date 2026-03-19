using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class Fire : MonoBehaviour
{
    private enum ParticleUpAxis
    {
        Up,
        Forward,
        Right
    }

    [Header("Fire State")]
    [SerializeField] private float maxIntensity = 1f;
    [SerializeField] private float minIntensityToLive = 0.05f;
    [SerializeField] private float regrowRate = 0.05f;
    [SerializeField] private bool startLitOnEnable = false;
    [SerializeField] private bool allowRegrowFromZero = false;
    [SerializeField] private float currentIntensity = 0f;

    [Header("Fire Spread")]
    [SerializeField] private bool enableSpread = true;
    [SerializeField] private bool allowRegrow = true;
    [SerializeField] private float minRadius = 0.1f;
    [SerializeField] private float maxRadius = 1f;
    [SerializeField] private float currentRadius = 0f;
    [SerializeField] private float spreadInterval = 1f;
    [SerializeField] private float spreadIgniteAmount = 0.2f;
    [Range(0f, 1f)]
    [SerializeField] private float spreadMinNormalizedIntensity = 0.3f;
    [SerializeField] private bool spreadOnlyToUnlitTargets = true;
    [SerializeField] private int spreadMaxOverlaps = 16;
    [SerializeField] private LayerMask spreadLayerMask = ~0;
    [SerializeField] private QueryTriggerInteraction spreadTriggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Extinguish")]
    [SerializeField] private float waterExtinguishPerSecond = 0.5f;
    [SerializeField] private string waterTag = "Water";
    [SerializeField] private bool disableGameObjectOnExtinguish = false;

    [Header("Player Damage")]
    [SerializeField] private float damagePerSecond = 10f;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool damageScalesWithIntensity = true;

    [Header("Visuals")]
    [SerializeField] private ParticleSystem fireParticleSystem;
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

    private int lastWateredFrame = -1;

    private void Reset()
    {
        CacheReferences();
        EnsureCollider();
        EnsureSpreadBuffer();
        SyncRadiusAndCollider();
        ApplyVisuals(forcePlayState: true);
    }

    private void Awake()
    {
        CacheReferences();
        EnsureCollider();
        EnsureSpreadBuffer();
    }

    private void OnEnable()
    {
        if (startLitOnEnable && currentIntensity <= 0f)
            currentIntensity = maxIntensity;

        currentIntensity = Mathf.Clamp(currentIntensity, 0f, Mathf.Max(0f, maxIntensity));
        spreadTimer = 0f;
        SyncRadiusAndCollider();
        ApplyVisuals(forcePlayState: true);
    }

    private void Update()
    {
        RegrowIntensity();
        SyncRadiusAndCollider();
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

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        currentIntensity = Mathf.Clamp(currentIntensity + amount, 0f, Mathf.Max(0f, maxIntensity));
    }

    public void ApplyWater(float amount)
    {
        if (Time.frameCount == lastWateredFrame) return;
        lastWateredFrame = Time.frameCount;

        if (amount <= 0f) return;
        if (currentIntensity <= 0f) return;

        currentIntensity = Mathf.Max(0f, currentIntensity - amount);
        if (currentIntensity <= minIntensityToLive)
            Extinguish();
    }

    private void Extinguish()
    {
        currentIntensity = 0f;
        if (disableGameObjectOnExtinguish)
            gameObject.SetActive(false);
    }

    private void RegrowIntensity()
    {
        if (regrowRate <= 0f) return;
        if (!allowRegrowFromZero && currentIntensity <= 0f) return;
        if (currentIntensity >= maxIntensity) return;

        currentIntensity = Mathf.Min(maxIntensity, currentIntensity + regrowRate * Time.deltaTime);
    }

    private void TrySpreadFire()
    {
        if (!enableSpread) return;
        if (spreadIgniteAmount <= 0f) return;
        if (currentIntensity <= 0f || maxIntensity <= 0f) return;

        float normalizedIntensity = Mathf.Clamp01(currentIntensity / maxIntensity);
        if (normalizedIntensity < spreadMinNormalizedIntensity) return;

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
            if (spreadOnlyToUnlitTargets && target.currentIntensity > target.minIntensityToLive) continue;
            if (!spreadTargets.Add(target)) continue;

            target.Ignite(spreadIgniteAmount);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!string.IsNullOrEmpty(waterTag) && other.CompareTag(waterTag))
            ApplyWater(waterExtinguishPerSecond * Time.deltaTime);

        if (damagePerSecond <= 0f || currentIntensity <= 0f) return;
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;

        PlayerVitals vitals = other.GetComponentInParent<PlayerVitals>();
        if (vitals == null || !vitals.IsAlive) return;

        float t01 = (maxIntensity <= 0f) ? 0f : Mathf.Clamp01(currentIntensity / maxIntensity);
        float scale = damageScalesWithIntensity ? t01 : 1f;
        if (scale <= 0f) return;

        vitals.TakeDamage(damagePerSecond * scale * Time.deltaTime);
    }

    private void OnParticleCollision(GameObject other)
    {
        if (!string.IsNullOrEmpty(waterTag) && other.CompareTag(waterTag))
        {
            FireHose hose = other.GetComponentInParent<FireHose>();
            float amount = hose != null 
                ? hose.CurrentApplyWaterRate * Time.deltaTime 
                : waterExtinguishPerSecond * Time.deltaTime;
                
            ApplyWater(amount);
        }
    }

    private void ApplyVisuals(bool forcePlayState = false)
    {
        float t01 = (maxIntensity <= 0f) ? 0f : Mathf.Clamp01(currentIntensity / maxIntensity);

        if (managedParticleSystems.Count > 0)
        {
            UpdateParticleObjectScale(t01);

            bool shouldPlay = currentIntensity > 0f;
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
            fireLight.enabled = currentIntensity > 0f;
        }

        if (scaleWithIntensity)
            transform.localScale = Vector3.Lerp(Vector3.zero, maxScale, t01);
        else
            transform.localScale = maxScale;
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

        float t01 = (maxIntensity <= 0f) ? 0f : Mathf.Clamp01(currentIntensity / maxIntensity);
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

    private void CacheReferences()
    {
        if (fireParticleSystem == null)
        {
            fireParticleSystem = GetComponentInChildren<ParticleSystem>(true);
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

    private void EnsureSpreadBuffer()
    {
        if (spreadMaxOverlaps < 1)
            spreadMaxOverlaps = 1;

        if (spreadBuffer == null || spreadBuffer.Length != spreadMaxOverlaps)
            spreadBuffer = new Collider[spreadMaxOverlaps];
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
}
