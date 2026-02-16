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
    [SerializeField] private float regrowRate = 0f;
    [SerializeField] private bool startLitOnEnable = true;
    [SerializeField] private bool allowRegrowFromZero = false;
    [SerializeField] private float currentIntensity = 1f;

    [Header("Fire Spread")]
    [SerializeField] private bool enableSpread = true;
    [SerializeField] private bool allowRegrow = true;
    [SerializeField] private float minRadius = 0.1f;
    [SerializeField] private float maxRadius = 20f;
    [SerializeField] private float currentRadius = 20f;
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
    private Vector3 particleBaseLocalScale = Vector3.one;
    private bool particleBaseScaleCached;

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
            ApplyWater(waterExtinguishPerSecond * Time.deltaTime);
    }

    private void ApplyVisuals(bool forcePlayState = false)
    {
        float t01 = (maxIntensity <= 0f) ? 0f : Mathf.Clamp01(currentIntensity / maxIntensity);

        if (fireParticleSystem != null)
        {
            UpdateParticleObjectScale(t01);

            bool shouldPlay = currentIntensity > 0f;
            if (shouldPlay)
            {
                if (!fireParticleSystem.isPlaying)
                    fireParticleSystem.Play(true);
            }
            else if (forcePlayState || fireParticleSystem.isPlaying || fireParticleSystem.particleCount > 0)
            {
                fireParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
        if (fireParticleSystem == null) return;

        Transform psTransform = fireParticleSystem.transform;
        if (psTransform == transform) return;

        Vector3 selectedAxis = GetSelectedAxis(psTransform);
        Quaternion correction = Quaternion.FromToRotation(selectedAxis, Vector3.up);
        psTransform.rotation = correction * psTransform.rotation;
    }

    private Vector3 GetSelectedAxis(Transform t)
    {
        switch (particleUpAxis)
        {
            case ParticleUpAxis.Up:
                return t.up;
            case ParticleUpAxis.Right:
                return t.right;
            default:
                return t.forward;
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
            fireParticleSystem = GetComponentInChildren<ParticleSystem>();

        CacheParticleBaseScale();

        if (fireLight == null)
            fireLight = GetComponentInChildren<Light>();
    }

    private void CacheParticleBaseScale()
    {
        if (fireParticleSystem == null) return;
        if (particleBaseScaleCached) return;

        particleBaseLocalScale = fireParticleSystem.transform.localScale;
        particleBaseScaleCached = true;
    }

    private void UpdateParticleObjectScale(float intensity01)
    {
        if (!scaleParticleObjectWithIntensity) return;
        if (fireParticleSystem == null) return;

        CacheParticleBaseScale();
        minParticleObjectScaleMultiplier = Mathf.Max(0f, minParticleObjectScaleMultiplier);
        maxParticleObjectScaleMultiplier = Mathf.Max(minParticleObjectScaleMultiplier, maxParticleObjectScaleMultiplier);

        float scaleMul = Mathf.Lerp(minParticleObjectScaleMultiplier, maxParticleObjectScaleMultiplier, intensity01);
        fireParticleSystem.transform.localScale = particleBaseLocalScale * scaleMul;
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
