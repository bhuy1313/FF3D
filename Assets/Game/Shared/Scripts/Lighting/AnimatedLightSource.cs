using UnityEngine;

public class AnimatedLightSource : MonoBehaviour
{
    public enum MotionMode
    {
        Constant = 0,
        Pulse = 1,
        Flicker = 2
    }

    [SerializeField] private Light targetLight;
    [SerializeField] private Renderer[] emissiveRenderers = new Renderer[0];
    [SerializeField] private MotionMode motionMode = MotionMode.Constant;
    [SerializeField] private float baseLightIntensity = 1f;
    [SerializeField] private float baseEmissionIntensity = 1f;
    [SerializeField] private Color emissionColor = Color.white;
    [SerializeField] private float intensityAmplitude = 0.2f;
    [SerializeField] private float animationSpeed = 2f;
    [SerializeField] private float flickerSmoothing = 12f;
    [SerializeField] private Transform rotatingVisual;
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    [SerializeField] private float rotationSpeed = 0f;

    private MaterialPropertyBlock propertyBlock;
    private float currentMultiplier = 1f;
    private float targetFlickerMultiplier = 1f;
    private float nextFlickerUpdateTime;

    public void Configure(
        Light configuredLight,
        Renderer[] configuredRenderers,
        MotionMode configuredMotionMode,
        float configuredLightIntensity,
        float configuredEmissionIntensity,
        Color configuredEmissionColor,
        float configuredAmplitude,
        float configuredAnimationSpeed,
        Transform configuredRotatingVisual,
        Vector3 configuredRotationAxis,
        float configuredRotationSpeed)
    {
        targetLight = configuredLight;
        emissiveRenderers = configuredRenderers ?? new Renderer[0];
        motionMode = configuredMotionMode;
        baseLightIntensity = configuredLightIntensity;
        baseEmissionIntensity = configuredEmissionIntensity;
        emissionColor = configuredEmissionColor;
        intensityAmplitude = configuredAmplitude;
        animationSpeed = configuredAnimationSpeed;
        rotatingVisual = configuredRotatingVisual;
        rotationAxis = configuredRotationAxis;
        rotationSpeed = configuredRotationSpeed;

        ApplyMultiplier(1f);
    }

    private void Reset()
    {
        targetLight = GetComponentInChildren<Light>();
        emissiveRenderers = GetComponentsInChildren<Renderer>();
        if (targetLight != null)
        {
            baseLightIntensity = targetLight.intensity;
        }
    }

    private void OnEnable()
    {
        EnsurePropertyBlock();
        currentMultiplier = 1f;
        targetFlickerMultiplier = 1f;
        nextFlickerUpdateTime = 0f;
        ApplyMultiplier(1f);
    }

    private void Update()
    {
        float multiplier = EvaluateMultiplier();
        ApplyMultiplier(multiplier);

        if (rotatingVisual != null && rotationSpeed != 0f)
        {
            Vector3 axis = rotationAxis.sqrMagnitude > 0f ? rotationAxis.normalized : Vector3.up;
            rotatingVisual.Rotate(axis, rotationSpeed * Time.deltaTime, Space.Self);
        }
    }

    private void OnValidate()
    {
        baseLightIntensity = Mathf.Max(0f, baseLightIntensity);
        baseEmissionIntensity = Mathf.Max(0f, baseEmissionIntensity);
        intensityAmplitude = Mathf.Clamp(intensityAmplitude, 0f, 1f);
        animationSpeed = Mathf.Max(0f, animationSpeed);
        flickerSmoothing = Mathf.Max(0.1f, flickerSmoothing);

        if (!Application.isPlaying)
        {
            EnsurePropertyBlock();
            ApplyMultiplier(1f);
        }
    }

    private float EvaluateMultiplier()
    {
        switch (motionMode)
        {
            case MotionMode.Pulse:
                return Mathf.Max(0f, 1f + Mathf.Sin(Time.time * animationSpeed) * intensityAmplitude);

            case MotionMode.Flicker:
                if (Time.time >= nextFlickerUpdateTime)
                {
                    targetFlickerMultiplier = Random.Range(1f - intensityAmplitude, 1f + intensityAmplitude);
                    float updateInterval = 1f / Mathf.Max(8f, animationSpeed * 12f);
                    nextFlickerUpdateTime = Time.time + updateInterval;
                }

                float blend = 1f - Mathf.Exp(-flickerSmoothing * Time.deltaTime);
                currentMultiplier = Mathf.Lerp(currentMultiplier, targetFlickerMultiplier, blend);
                return Mathf.Max(0f, currentMultiplier);

            default:
                return 1f;
        }
    }

    private void ApplyMultiplier(float multiplier)
    {
        EnsurePropertyBlock();

        if (targetLight != null)
        {
            targetLight.intensity = baseLightIntensity * multiplier;
        }

        if (emissiveRenderers == null || emissiveRenderers.Length == 0)
        {
            return;
        }

        Color finalEmission = emissionColor * (baseEmissionIntensity * multiplier);
        for (int i = 0; i < emissiveRenderers.Length; i++)
        {
            Renderer rendererComponent = emissiveRenderers[i];
            if (rendererComponent == null)
            {
                continue;
            }

            rendererComponent.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_EmissionColor", finalEmission);
            rendererComponent.SetPropertyBlock(propertyBlock);
        }
    }

    private void EnsurePropertyBlock()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }
}
