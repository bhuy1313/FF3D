using UnityEngine;
using UnityEngine.VFX;

public class Fire : MonoBehaviour
{
    [Header("Fire State")]
    [SerializeField] private float maxIntensity = 1f;
    [SerializeField] private float minIntensityToLive = 0.05f;
    [Tooltip("Tốc độ hồi lửa mỗi giây (0 = không hồi).")]
    [SerializeField] private float regrowRate = 0f;

    [Tooltip("Nếu true, khi Enable mà currentIntensity <= 0 thì set lên maxIntensity (lửa bật sẵn).")]
    [SerializeField] private bool startLitOnEnable = true;

    [Tooltip("Nếu true, regrow vẫn chạy từ 0 (dù đã tắt).")]
    [SerializeField] private bool allowRegrowFromZero = false;

    [Header("Extinguish")]
    [SerializeField] private float waterExtinguishPerSecond = 0.5f;
    [SerializeField] private string waterTag = "Water";

    [Tooltip("Nếu true, khi tắt hẳn sẽ disable cả GameObject (cẩn thận vì sẽ không regrow/trigger nữa).")]
    [SerializeField] private bool disableGameObjectOnExtinguish = false;

    [Header("Visuals")]
    [SerializeField] private VisualEffect fireVfx;
    [SerializeField] private string vfxIntensityParam = "Intensity";
    [SerializeField] private bool invertVfxIntensity = false;   // <-- bật nếu graph của bạn đang ngược (0 mạnh, 1 tắt)
    [SerializeField] private bool logMissingVfxParam = true;

    [SerializeField] private Light fireLight;
    [SerializeField] private bool scaleWithIntensity = true;
    [SerializeField] private Vector3 maxScale = Vector3.one;
    [SerializeField] private float maxLightIntensity = 2f;

    [Header("Runtime (Debug)")]
    [SerializeField] private float currentIntensity = 1f;

    private bool missingParamLogged;
    private bool vfxHasParamCached;
    private bool vfxParamChecked;

    private void Awake()
    {
        if (fireVfx == null) fireVfx = GetComponentInChildren<VisualEffect>();
        if (fireLight == null) fireLight = GetComponentInChildren<Light>();
    }

    private void OnEnable()
    {
        if (startLitOnEnable && currentIntensity <= 0f)
            currentIntensity = maxIntensity;

        currentIntensity = Mathf.Clamp(currentIntensity, 0f, maxIntensity);

        CheckVfxParamOnce();
        ApplyVisuals(forcePlayState: true);
    }

    private void Update()
    {
        if (regrowRate <= 0f) return;

        // Regrow logic
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

        // Nếu object đã bị disable bởi disableGameObjectOnExtinguish,
        // cần bật lại trước khi ApplyVisuals.
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
        float t01 = (maxIntensity <= 0f) ? 0f : (currentIntensity / maxIntensity);
        t01 = Mathf.Clamp01(t01);

        // Nếu VFX graph đang ngược (0 mạnh, 1 tắt) thì bật invert
        float vfxT = invertVfxIntensity ? (1f - t01) : t01;

        if (fireVfx != null)
        {
            if (!string.IsNullOrEmpty(vfxIntensityParam))
            {
                CheckVfxParamOnce();

                if (vfxHasParamCached)
                {
                    fireVfx.SetFloat(vfxIntensityParam, vfxT);
                }
                else if (logMissingVfxParam && !missingParamLogged)
                {
                    Debug.LogWarning($"Fire VFX param not found (Float): {vfxIntensityParam}", this);
                    missingParamLogged = true;
                }
            }

            // Chỉ play khi còn lửa, stop khi hết
            if (forcePlayState)
            {
                if (currentIntensity > 0f) fireVfx.Play();
                else fireVfx.Stop();
            }
            else
            {
                // Avoid spam: chỉ đổi trạng thái khi cần (đủ tốt trong đa số trường hợp)
                if (currentIntensity > 0f) fireVfx.Play();
                else fireVfx.Stop();
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

    private void CheckVfxParamOnce()
    {
        if (vfxParamChecked) return;
        vfxParamChecked = true;

        if (fireVfx == null || string.IsNullOrEmpty(vfxIntensityParam))
        {
            vfxHasParamCached = false;
            return;
        }

        vfxHasParamCached = fireVfx.HasFloat(vfxIntensityParam);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!string.IsNullOrEmpty(waterTag) && other.CompareTag(waterTag))
            ApplyWater(waterExtinguishPerSecond * Time.deltaTime);
    }

    private void OnParticleCollision(GameObject other)
    {
        if (!string.IsNullOrEmpty(waterTag) && other.CompareTag(waterTag))
            ApplyWater(waterExtinguishPerSecond * Time.deltaTime);
    }
}
