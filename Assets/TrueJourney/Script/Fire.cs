using UnityEngine;

public class Fire : MonoBehaviour
{
    [Header("Fire State")]
    [SerializeField] private float maxIntensity = 1f;
    [SerializeField] private float minIntensityToLive = 0.05f;
    [Tooltip("Regrow speed per second (0 = no regrow).")]
    [SerializeField] private float regrowRate = 0f;

    [Tooltip("If true, when enabled and currentIntensity <= 0, set intensity back to max.")]
    [SerializeField] private bool startLitOnEnable = true;

    [Tooltip("If true, regrow still works from 0 intensity.")]
    [SerializeField] private bool allowRegrowFromZero = false;

    [Header("Extinguish")]
    [SerializeField] private float waterExtinguishPerSecond = 0.5f;
    [SerializeField] private string waterTag = "Water";

    [Tooltip("If true, disable the whole GameObject when fully extinguished.")]
    [SerializeField] private bool disableGameObjectOnExtinguish = false;

    [Header("Player Damage")]
    [SerializeField] private float damagePerSecond = 10f;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool damageScalesWithIntensity = true;

    [Header("Visuals")]
    [SerializeField] private ParticleSystem fireParticleSystem;
    [SerializeField] private bool scaleParticleEmissionWithIntensity = true;
    [SerializeField] private float maxEmissionRate = 50f;

    [SerializeField] private Light fireLight;
    [SerializeField] private bool scaleWithIntensity = true;
    [SerializeField] private Vector3 maxScale = Vector3.one;
    [SerializeField] private float maxLightIntensity = 2f;

    [Header("Runtime (Debug)")]
    [SerializeField] private float currentIntensity = 1f;

    private void Awake()
    {
        if (fireParticleSystem == null) fireParticleSystem = GetComponentInChildren<ParticleSystem>();
        if (fireLight == null) fireLight = GetComponentInChildren<Light>();
    }

    private void OnEnable()
    {
        if (startLitOnEnable && currentIntensity <= 0f)
            currentIntensity = maxIntensity;

        currentIntensity = Mathf.Clamp(currentIntensity, 0f, maxIntensity);
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

        if (fireParticleSystem != null)
        {
            if (scaleParticleEmissionWithIntensity)
            {
                var emission = fireParticleSystem.emission;
                emission.rateOverTimeMultiplier = Mathf.Lerp(0f, maxEmissionRate, t01);
            }

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
