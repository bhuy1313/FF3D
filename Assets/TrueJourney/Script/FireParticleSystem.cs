using UnityEngine;

public class FireParticleSystem : MonoBehaviour
{
    [Header("Fire State")]
    [SerializeField] private float maxIntensity = 1f;
    [SerializeField] private float minIntensityToLive = 0.05f;
    [Tooltip("Regrow rate per second (0 = no regrow).")]
    [SerializeField] private float regrowRate = 0f;
    [Tooltip("If true, when enabled and currentIntensity <= 0, set to maxIntensity.")]
    [SerializeField] private bool startLitOnEnable = true;
    [Tooltip("If true, regrow can start from 0 even after fully extinguished.")]
    [SerializeField] private bool allowRegrowFromZero = false;

    [Header("Extinguish")]
    [SerializeField] private float waterExtinguishPerSecond = 0.5f;
    [SerializeField] private string waterTag = "Water";
    [Tooltip("If true, disables GameObject when fully extinguished.")]
    [SerializeField] private bool disableGameObjectOnExtinguish = false;

    [Header("Player Damage")]
    [SerializeField] private float damagePerSecond = 10f;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool damageScalesWithIntensity = true;

    [Header("Visuals - Particle System")]
    [SerializeField] private ParticleSystem firePs;
    [Tooltip("Max emission rate at intensity = 1. If 0, uses current PS emission rate as max.")]
    [SerializeField] private float maxEmissionRate = 0f;
    [Tooltip("If true, scales ParticleSystem start size with intensity.")]
    [SerializeField] private bool scaleParticleSizeWithIntensity = false;
    [Tooltip("Max start size at intensity = 1. If 0, uses current PS start size as max.")]
    [SerializeField] private float maxStartSize = 0f;
    [SerializeField] private bool clearOnStop = false;

    [Header("Light/Scale")]
    [SerializeField] private Light fireLight;
    [SerializeField] private bool scaleWithIntensity = true;
    [SerializeField] private Vector3 maxScale = Vector3.one;
    [SerializeField] private float maxLightIntensity = 2f;

    [Header("Runtime (Debug)")]
    [SerializeField] private float currentIntensity = 1f;

    private bool particleDefaultsCached;
    private float cachedMaxEmissionRate;
    private float cachedMaxStartSize;

    private void Awake()
    {
        if (firePs == null) firePs = GetComponentInChildren<ParticleSystem>();
        if (fireLight == null) fireLight = GetComponentInChildren<Light>();
        CacheParticleDefaults();
    }

    private void OnEnable()
    {
        if (startLitOnEnable && currentIntensity <= 0f)
            currentIntensity = maxIntensity;

        currentIntensity = Mathf.Clamp(currentIntensity, 0f, maxIntensity);

        CacheParticleDefaults();
        ApplyVisuals(forcePlayState: true);
    }

    private void Update()
    {
        if (regrowRate <= 0f) return;
        if (!allowRegrowFromZero && currentIntensity <= 0f) return;
        if (currentIntensity >= maxIntensity) return;

        currentIntensity = Mathf.Min(maxIntensity, currentIntensity + regrowRate * Time.deltaTime);
        ApplyVisuals();
    }

    public void ApplyWater(float amount)
    {
        if (amount <= 0f) return;
        if (currentIntensity <= 0f) return;

        currentIntensity = Mathf.Max(0f, currentIntensity - amount);
        ApplyVisuals();

        if (currentIntensity <= minIntensityToLive)
            Extinguish();
    }

    public void Ignite(float amount)
    {
        if (amount <= 0f) return;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        currentIntensity = Mathf.Clamp(currentIntensity + amount, 0f, maxIntensity);
        ApplyVisuals(forcePlayState: true);
    }

    private void Extinguish()
    {
        currentIntensity = 0f;
        ApplyVisuals(forcePlayState: true);

        if (disableGameObjectOnExtinguish)
            gameObject.SetActive(false);
    }

    private void ApplyVisuals(bool forcePlayState = false)
    {
        float t01 = (maxIntensity <= 0f) ? 0f : Mathf.Clamp01(currentIntensity / maxIntensity);

        if (firePs != null)
        {
            var emission = firePs.emission;
            float emissionMax = maxEmissionRate > 0f ? maxEmissionRate : cachedMaxEmissionRate;
            if (emissionMax > 0f)
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(emissionMax * t01);

            if (scaleParticleSizeWithIntensity)
            {
                float sizeMax = maxStartSize > 0f ? maxStartSize : cachedMaxStartSize;
                if (sizeMax > 0f)
                {
                    var main = firePs.main;
                    main.startSize = new ParticleSystem.MinMaxCurve(sizeMax * t01);
                }
            }

            if (forcePlayState || !firePs.isPlaying)
            {
                if (currentIntensity > 0f) firePs.Play();
                else StopParticles();
            }
            else if (currentIntensity <= 0f && firePs.isPlaying)
            {
                StopParticles();
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

    private void StopParticles()
    {
        if (firePs == null) return;
        var behavior = clearOnStop
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;
        firePs.Stop(true, behavior);
    }

    private void CacheParticleDefaults()
    {
        if (particleDefaultsCached || firePs == null) return;
        particleDefaultsCached = true;

        var emission = firePs.emission;
        cachedMaxEmissionRate = maxEmissionRate > 0f ? maxEmissionRate : GetCurveMaxValue(emission.rateOverTime);

        var main = firePs.main;
        cachedMaxStartSize = maxStartSize > 0f ? maxStartSize : GetCurveMaxValue(main.startSize);
    }

    private static float GetCurveMaxValue(ParticleSystem.MinMaxCurve curve)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return curve.constant;
            case ParticleSystemCurveMode.TwoConstants:
                return curve.constantMax;
            case ParticleSystemCurveMode.Curve:
            case ParticleSystemCurveMode.TwoCurves:
                return curve.curveMultiplier;
            default:
                return 0f;
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
}
