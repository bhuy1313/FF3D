using UnityEngine;
using UnityEngine.Serialization;

public class FireParticleSystem : MonoBehaviour
{
    [Header("Fire State")]
    [FormerlySerializedAs("maxIntensity")]
    [SerializeField] private float maxHp = 1f;
    [Tooltip("Regrow rate per second (0 = no regrow).")]
    [FormerlySerializedAs("regrowRate")]
    [SerializeField] private float regrowHpPerSecond = 0f;
    [Tooltip("If true, when enabled and currentHp <= 0, set to maxHp.")]
    [SerializeField] private bool startLitOnEnable = true;
    [Tooltip("If true, regrow can start from 0 even after fully extinguished.")]
    [SerializeField] private bool allowRegrowFromZero = false;
    [SerializeField] private float regrowResumeDelay = 1.5f;

    [Header("Extinguish")]
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
    [FormerlySerializedAs("currentIntensity")]
    [SerializeField] private float currentHp = 1f;

    private bool particleDefaultsCached;
    private float cachedMaxEmissionRate;
    private float cachedMaxStartSize;
    private float lastWaterAppliedTime = float.NegativeInfinity;

    private void Awake()
    {
        if (firePs == null) firePs = GetComponentInChildren<ParticleSystem>();
        if (fireLight == null) fireLight = GetComponentInChildren<Light>();
        CacheParticleDefaults();
    }

    private void OnEnable()
    {
        if (startLitOnEnable && currentHp <= 0f)
            currentHp = maxHp;

        currentHp = Mathf.Clamp(currentHp, 0f, maxHp);
        lastWaterAppliedTime = float.NegativeInfinity;

        CacheParticleDefaults();
        ApplyVisuals(forcePlayState: true);
    }

    private void Update()
    {
        if (regrowHpPerSecond <= 0f) return;
        if (!allowRegrowFromZero && currentHp <= 0f) return;
        if (currentHp >= maxHp) return;
        if (Time.time < lastWaterAppliedTime + Mathf.Max(0f, regrowResumeDelay)) return;

        currentHp = Mathf.Min(maxHp, currentHp + regrowHpPerSecond * Time.deltaTime);
        ApplyVisuals();
    }

    public void ApplyWater(float amount)
    {
        if (amount <= 0f) return;
        if (currentHp <= 0f) return;

        float previousHp = currentHp;
        currentHp = Mathf.Max(0f, currentHp - amount);
        if (currentHp < previousHp)
        {
            lastWaterAppliedTime = Time.time;
        }

        ApplyVisuals();

        if (currentHp <= 0f)
            Extinguish();
    }

    public void Ignite(float amount)
    {
        if (amount <= 0f) return;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        currentHp = Mathf.Clamp(currentHp + amount, 0f, maxHp);
        ApplyVisuals(forcePlayState: true);
    }

    private void Extinguish()
    {
        currentHp = 0f;
        ApplyVisuals(forcePlayState: true);

        if (disableGameObjectOnExtinguish)
            gameObject.SetActive(false);
    }

    private void ApplyVisuals(bool forcePlayState = false)
    {
        float t01 = GetNormalizedHp();

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
                if (currentHp > 0f) firePs.Play();
                else StopParticles();
            }
            else if (currentHp <= 0f && firePs.isPlaying)
            {
                StopParticles();
            }
        }

        if (fireLight != null)
        {
            fireLight.intensity = Mathf.Lerp(0f, maxLightIntensity, t01);
            fireLight.enabled = currentHp > 0f;
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
        if (damagePerSecond <= 0f || currentHp <= 0f) return;
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;

        PlayerVitals vitals = other.GetComponentInParent<PlayerVitals>();
        if (vitals == null || !vitals.IsAlive) return;

        float t01 = GetNormalizedHp();
        float scale = damageScalesWithIntensity ? t01 : 1f;
        if (scale <= 0f) return;

        vitals.TakeDamage(damagePerSecond * scale * Time.deltaTime);
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
                // FireHose applies water through its arc logic, so its particles remain visual-only.
                return;
            }
        }
    }

    private float GetNormalizedHp()
    {
        return maxHp <= 0f ? 0f : Mathf.Clamp01(currentHp / maxHp);
    }
}
