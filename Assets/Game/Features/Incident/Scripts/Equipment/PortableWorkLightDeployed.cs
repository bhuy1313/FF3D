using UnityEngine;

[DisallowMultipleComponent]
public class PortableWorkLightDeployed : MonoBehaviour, IInteractable
{
    public enum PortableWorkLightMode
    {
        Omni = 0,
        Directional = 1
    }

    [Header("Light")]
    [SerializeField] private PortableWorkLightMode lightMode = PortableWorkLightMode.Omni;
    [SerializeField] private Light[] controlledLights = System.Array.Empty<Light>();
    [SerializeField] private bool autoSyncChildLights = true;
    [SerializeField] private Light[] synchronizedLights = System.Array.Empty<Light>();
    [SerializeField] private float omniRange = 8f;
    [SerializeField] private float omniIntensity = 4f;
    [SerializeField] private float directionalRange = 12f;
    [SerializeField] private float directionalIntensity = 5.5f;
    [SerializeField, Range(1f, 179f)] private float directionalSpotAngle = 70f;
    [SerializeField, Range(0f, 179f)] private float directionalInnerSpotAngle = 45f;
    [SerializeField] private Vector3 directionalLocalEulerAngles = new Vector3(12f, 0f, 0f);
    [SerializeField] private Renderer[] indicatorRenderers = System.Array.Empty<Renderer>();
    [SerializeField] private string indicatorColorProperty = "_BaseColor";
    [SerializeField] private Color enabledColor = new Color(1f, 0.92f, 0.68f, 1f);
    [SerializeField] private Color disabledColor = new Color(0.18f, 0.18f, 0.18f, 1f);
    [SerializeField] private bool startsEnabled = true;
    [SerializeField] private bool allowPlayerToggle = true;

    [Header("Runtime")]
    [SerializeField] private bool isLightEnabled;

    private Light[] resolvedSynchronizedLights = System.Array.Empty<Light>();

    public bool IsLightEnabled => isLightEnabled;
    public PortableWorkLightMode LightMode => lightMode;

    private void Awake()
    {
        ResolveSynchronizedLights();
        ApplyLightModeConfiguration();
        ApplyLightState(startsEnabled);
    }

    private void OnEnable()
    {
        ResolveSynchronizedLights();
        ApplyLightModeConfiguration();
        ApplyLightState(startsEnabled);
    }

    private void LateUpdate()
    {
        EnsureSynchronizedLightsMatchState();
    }

    private void OnValidate()
    {
        omniRange = Mathf.Max(0f, omniRange);
        omniIntensity = Mathf.Max(0f, omniIntensity);
        directionalRange = Mathf.Max(0f, directionalRange);
        directionalIntensity = Mathf.Max(0f, directionalIntensity);
        directionalSpotAngle = Mathf.Clamp(directionalSpotAngle, 1f, 179f);
        directionalInnerSpotAngle = Mathf.Clamp(directionalInnerSpotAngle, 0f, directionalSpotAngle);
        ResolveSynchronizedLights();
        ApplyLightModeConfiguration();
        ApplyLightState(isLightEnabled);
    }

    private void OnTransformChildrenChanged()
    {
        ResolveSynchronizedLights();
        ApplyLightState(isLightEnabled);
    }

    public void Interact(GameObject interactor)
    {
        if (!allowPlayerToggle)
        {
            return;
        }

        ApplyLightState(!isLightEnabled);
    }

    public void SetLightEnabled(bool enabled)
    {
        ApplyLightState(enabled);
    }

    public void SetLightMode(PortableWorkLightMode mode)
    {
        lightMode = mode;
        ApplyLightModeConfiguration();
        ApplyLightState(isLightEnabled);
    }

    private void ResolveSynchronizedLights()
    {
        System.Collections.Generic.List<Light> resolvedLights = new System.Collections.Generic.List<Light>();

        if (synchronizedLights != null)
        {
            for (int i = 0; i < synchronizedLights.Length; i++)
            {
                TryAddSynchronizedLight(resolvedLights, synchronizedLights[i]);
            }
        }

        if (autoSyncChildLights)
        {
            Light[] childLights = GetComponentsInChildren<Light>(true);
            for (int i = 0; i < childLights.Length; i++)
            {
                TryAddSynchronizedLight(resolvedLights, childLights[i]);
            }
        }

        resolvedSynchronizedLights = resolvedLights.ToArray();
    }

    private void TryAddSynchronizedLight(System.Collections.Generic.List<Light> destination, Light candidate)
    {
        if (candidate == null || IsControlledLight(candidate) || destination.Contains(candidate))
        {
            return;
        }

        destination.Add(candidate);
    }

    private bool IsControlledLight(Light candidate)
    {
        if (candidate == null || controlledLights == null)
        {
            return false;
        }

        for (int i = 0; i < controlledLights.Length; i++)
        {
            if (controlledLights[i] == candidate)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyLightModeConfiguration()
    {
        Light configuredLight = ResolveConfiguredLight();
        if (configuredLight == null)
        {
            return;
        }

        if (lightMode == PortableWorkLightMode.Directional)
        {
            configuredLight.type = LightType.Spot;
            configuredLight.range = directionalRange;
            configuredLight.intensity = directionalIntensity;
            configuredLight.spotAngle = directionalSpotAngle;
#if UNITY_2022_2_OR_NEWER
            configuredLight.innerSpotAngle = directionalInnerSpotAngle;
#endif
            configuredLight.transform.localRotation = Quaternion.Euler(directionalLocalEulerAngles);
            return;
        }

        configuredLight.type = LightType.Point;
        configuredLight.range = omniRange;
        configuredLight.intensity = omniIntensity;
        configuredLight.transform.localRotation = Quaternion.identity;
    }

    private void ApplyLightState(bool enabled)
    {
        isLightEnabled = enabled;

        for (int i = 0; i < controlledLights.Length; i++)
        {
            Light controlledLight = controlledLights[i];
            if (controlledLight != null)
            {
                controlledLight.enabled = enabled;
            }
        }

        for (int i = 0; i < resolvedSynchronizedLights.Length; i++)
        {
            Light synchronizedLight = resolvedSynchronizedLights[i];
            if (synchronizedLight != null)
            {
                synchronizedLight.enabled = enabled;
            }
        }

        Color targetColor = enabled ? enabledColor : disabledColor;
        for (int i = 0; i < indicatorRenderers.Length; i++)
        {
            Renderer indicator = indicatorRenderers[i];
            if (indicator == null)
            {
                continue;
            }

            Material material = Application.isPlaying ? indicator.material : indicator.sharedMaterial;
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty(indicatorColorProperty))
            {
                material.SetColor(indicatorColorProperty, targetColor);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", targetColor);
            }
        }
    }

    private void EnsureSynchronizedLightsMatchState()
    {
        for (int i = 0; i < controlledLights.Length; i++)
        {
            Light controlledLight = controlledLights[i];
            if (controlledLight != null && controlledLight.enabled != isLightEnabled)
            {
                controlledLight.enabled = isLightEnabled;
            }
        }

        for (int i = 0; i < resolvedSynchronizedLights.Length; i++)
        {
            Light synchronizedLight = resolvedSynchronizedLights[i];
            if (synchronizedLight != null && synchronizedLight.enabled != isLightEnabled)
            {
                synchronizedLight.enabled = isLightEnabled;
            }
        }
    }

    private Light ResolveConfiguredLight()
    {
        if (controlledLights == null)
        {
            return null;
        }

        for (int i = 0; i < controlledLights.Length; i++)
        {
            if (controlledLights[i] != null)
            {
                return controlledLights[i];
            }
        }

        return null;
    }
}
