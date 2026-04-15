using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public class TimeOfDayPresetController : MonoBehaviour
{
    public enum TimeOfDayPreset
    {
        Morning = 0,
        Noon = 1,
        Evening = 2,
        Night = 3
    }

    private struct PresetValues
    {
        public Material skybox;
        public Color fogColor;
        public float fogStart;
        public float fogEnd;
        public Color ambientSkyColor;
        public Color ambientEquatorColor;
        public Color ambientGroundColor;
        public float ambientIntensity;
        public float reflectionIntensity;
        public Color sunColor;
        public float sunIntensity;
        public Vector3 sunEulerAngles;
    }

    [Header("Preset")]
    [SerializeField] private TimeOfDayPreset selectedPreset = TimeOfDayPreset.Evening;
    [SerializeField] private bool applyOnEnable = true;
    [SerializeField, Tooltip("Allow overriding RenderSettings.fogEndDistance")] private bool applyFogEnd = true;

    [Header("References")]
    [SerializeField] private Light sunLight;

    [Header("Skyboxes")]
    [SerializeField] private Material morningSkybox;
    [SerializeField] private Material noonSkybox;
    [SerializeField] private Material eveningSkybox;
    [SerializeField] private Material nightSkybox;

    public TimeOfDayPreset SelectedPreset => selectedPreset;

    private void Reset()
    {
        sunLight = GetComponent<Light>();
    }

    private void OnEnable()
    {
        EnsureReferences();

        if (applyOnEnable)
        {
            ApplySelectedPreset();
        }
    }

    private void OnValidate()
    {
        EnsureReferences();

        if (!isActiveAndEnabled)
        {
            return;
        }

        ApplySelectedPreset();
    }

    public void SetPreset(TimeOfDayPreset preset)
    {
        selectedPreset = preset;
        ApplySelectedPreset();
    }

    public void ApplyPresetFromIndex(int presetIndex)
    {
        presetIndex = Mathf.Clamp(presetIndex, 0, 3);
        SetPreset((TimeOfDayPreset)presetIndex);
    }

    public void ApplySelectedPreset()
    {
        EnsureReferences();

        if (sunLight == null)
        {
            return;
        }

        ApplyPreset(GetPresetValues(selectedPreset));
    }

    private void EnsureReferences()
    {
        if (sunLight == null)
        {
            sunLight = GetComponent<Light>();
        }
    }

    private void ApplyPreset(PresetValues preset)
    {
        if (preset.skybox != null)
        {
            RenderSettings.skybox = preset.skybox;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = preset.fogColor;
        RenderSettings.fogStartDistance = preset.fogStart;
        
        if (applyFogEnd)
        {
            RenderSettings.fogEndDistance = preset.fogEnd;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = preset.ambientSkyColor;
        RenderSettings.ambientEquatorColor = preset.ambientEquatorColor;
        RenderSettings.ambientGroundColor = preset.ambientGroundColor;
        RenderSettings.ambientIntensity = preset.ambientIntensity;
        RenderSettings.reflectionIntensity = preset.reflectionIntensity;
        RenderSettings.sun = sunLight;

        sunLight.color = preset.sunColor;
        sunLight.intensity = preset.sunIntensity;
        transform.rotation = Quaternion.Euler(preset.sunEulerAngles);

        DynamicGI.UpdateEnvironment();
    }

    private PresetValues GetPresetValues(TimeOfDayPreset preset)
    {
        switch (preset)
        {
            case TimeOfDayPreset.Morning:
                return new PresetValues
                {
                    skybox = morningSkybox,
                    fogColor = new Color(0.84f, 0.71f, 0.67f),
                    fogStart = 0f,
                    fogEnd = 240f,
                    ambientSkyColor = new Color(0.80f, 0.62f, 0.56f),
                    ambientEquatorColor = new Color(0.57f, 0.49f, 0.46f),
                    ambientGroundColor = new Color(0.22f, 0.18f, 0.17f),
                    ambientIntensity = 0.82f,
                    reflectionIntensity = 0.18f,
                    sunColor = new Color(1f, 0.82f, 0.68f),
                    sunIntensity = 0.95f,
                    sunEulerAngles = new Vector3(18f, -30f, 0f)
                };

            case TimeOfDayPreset.Noon:
                return new PresetValues
                {
                    skybox = noonSkybox,
                    fogColor = new Color(0.72f, 0.84f, 0.94f),
                    fogStart = 0f,
                    fogEnd = 420f,
                    ambientSkyColor = new Color(0.56f, 0.73f, 0.92f),
                    ambientEquatorColor = new Color(0.36f, 0.50f, 0.61f),
                    ambientGroundColor = new Color(0.21f, 0.25f, 0.27f),
                    ambientIntensity = 1f,
                    reflectionIntensity = 0.24f,
                    sunColor = new Color(0.91f, 0.82f, 0.64f),
                    sunIntensity = 1.35f,
                    sunEulerAngles = new Vector3(70f, -10f, 0f)
                };

            case TimeOfDayPreset.Night:
                return new PresetValues
                {
                    skybox = nightSkybox,
                    fogColor = new Color(0.07f, 0.09f, 0.15f),
                    fogStart = 0f,
                    fogEnd = 160f,
                    ambientSkyColor = new Color(0.08f, 0.11f, 0.18f),
                    ambientEquatorColor = new Color(0.05f, 0.07f, 0.10f),
                    ambientGroundColor = new Color(0.02f, 0.03f, 0.04f),
                    ambientIntensity = 0.42f,
                    reflectionIntensity = 0.07f,
                    sunColor = new Color(0.62f, 0.73f, 0.95f),
                    sunIntensity = 0.22f,
                    sunEulerAngles = new Vector3(340f, -24f, 0f)
                };

            default:
                return new PresetValues
                {
                    skybox = eveningSkybox,
                    fogColor = new Color(0.30f, 0.25f, 0.30f),
                    fogStart = 0f,
                    fogEnd = 210f,
                    ambientSkyColor = new Color(0.25f, 0.22f, 0.28f),
                    ambientEquatorColor = new Color(0.18f, 0.15f, 0.18f),
                    ambientGroundColor = new Color(0.08f, 0.07f, 0.08f),
                    ambientIntensity = 0.7f,
                    reflectionIntensity = 0.12f,
                    sunColor = new Color(1f, 0.72f, 0.52f),
                    sunIntensity = 0.88f,
                    sunEulerAngles = new Vector3(12f, -32f, 0f)
                };
        }
    }
}
