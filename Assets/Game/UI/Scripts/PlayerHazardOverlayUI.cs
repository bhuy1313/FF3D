using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerHazardOverlayUI : MonoBehaviour
{
    private static readonly string[] HelmetOverlayCandidateNames = { "OverlayMask", "MaskOxygenOverlay" };
    private static readonly string[] VisorOverlayCandidateNames = { "VisorOverlay", "HelmetVisorOverlay" };
    private static readonly string[] VignetteOverlayCandidateNames = { "VignetteMask", "VignetteOverlay" };
    private static readonly string[] DamageVignetteCandidateNames = { "DamageVignette", "LowHealthVignette", "BloodVignette" };

    [SerializeField] private PlayerHazardExposure exposure;
    [SerializeField] private RawImage smokeOverlay;
    [SerializeField] private CanvasGroup smokeCanvasGroup;
    [SerializeField] private RawImage[] fireOverlays = Array.Empty<RawImage>();
    [SerializeField] private CanvasGroup[] fireCanvasGroups = Array.Empty<CanvasGroup>();
    [SerializeField] private Image helmetOverlay;
    [SerializeField] private Image visorOverlay;
    [SerializeField] private Image vignetteOverlay;
    [SerializeField] private Image damageVignetteOverlay;
    [SerializeField] private PlayerVitals playerVitals;
    [SerializeField] private RectTransform statusBarRectTransform;
    [SerializeField] private CanvasGroup diegeticOverlayCanvasGroup;
    [SerializeField, Range(0f, 1f)] private float maxSmokeOverlayOpacity = 1f;
    [SerializeField, Range(0f, 1f)] private float maxFireOverlayOpacity = 1f;
    [SerializeField, Range(0f, 1f)] private float screenMaskOpacityMultiplier = 1f;
    [SerializeField, Range(0f, 1f)] private float helmetOverlayOpacity = 1f;
    [SerializeField, Range(0f, 1f)] private float visorOverlayOpacity = 0.85f;
    [SerializeField, Range(0f, 1f)] private float vignetteMaskIntensity;
    [SerializeField] private bool helmetOverlayVisible;
    [SerializeField] private bool visorOverlayVisible;
    [SerializeField] private bool diegeticOverlayVisible = true;
    [SerializeField, Range(0f, 1f)] private float diegeticOverlayOpacity = 1f;
    [SerializeField] private Vector2 statusBarAnchoredPositionWhenOverlayVisible = new Vector2(410f, 100f);
    [SerializeField] private Vector2 statusBarAnchoredPositionWhenOverlayHidden = new Vector2(300f, 100f);
    [SerializeField] private Vector3 statusBarScaleWhenOverlayVisible = new Vector3(1.5f, 1.5f, 1.5f);
    [SerializeField] private Vector3 statusBarScaleWhenOverlayHidden = new Vector3(1.5f, 1.5f, 1.5f);
    [Header("Low Health Damage Vignette")]
    [SerializeField, Range(0f, 1f)] private float damageVignetteStartHealthPercent = 0.45f;
    [SerializeField, Range(0f, 1f)] private float damageVignetteCriticalHealthPercent = 0.1f;
    [SerializeField, Range(0f, 1f)] private float damageVignetteMaxOpacity = 0.9f;
    [SerializeField, Min(0f)] private float damageVignetteFadeSpeed = 6f;
    [SerializeField] private Vector2 fireOverlayBaseSize = new Vector2(420f, 420f);
    [SerializeField] private Vector2 fireOverlayExtraSize = new Vector2(320f, 320f);
    [SerializeField] private ThermalVisionController thermalVisionController;

    private bool hasWarnedMissingSmokeOverlayReferences;
    private bool hasWarnedMissingFireOverlayReferences;
    private bool hasWarnedMissingSmokeTexture;
    private bool hasWarnedMissingFireTexture;
    private bool hasWarnedMissingStatusBarReference;
    private bool hasWarnedMissingDamageVignetteReference;
    private bool hasInitializedOverlayDefaults;
    private bool hasInitializedDamageVignetteDefaults;
    private float lastAppliedAlpha = -1f;
    private float currentDamageVignetteAlpha;
    private float[] lastAppliedFireAlphas = Array.Empty<float>();

    private void Awake()
    {
        ResolveReferences();
        EnsureOverlayImages();
        RefreshOverlayImmediate();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureOverlayImages();
        RefreshOverlayImmediate();
    }

    private void Update()
    {
        ResolveReferences();
        EnsureOverlayImages();
        UpdateMaskLayers();
        UpdateSmokeOverlay();
        UpdateFireOverlay();
        UpdateDamageVignetteOverlay();
    }

    private void OnValidate()
    {
        maxSmokeOverlayOpacity = Mathf.Clamp01(maxSmokeOverlayOpacity);
        maxFireOverlayOpacity = Mathf.Clamp01(maxFireOverlayOpacity);
        screenMaskOpacityMultiplier = Mathf.Clamp01(screenMaskOpacityMultiplier);
        helmetOverlayOpacity = Mathf.Clamp01(helmetOverlayOpacity);
        visorOverlayOpacity = Mathf.Clamp01(visorOverlayOpacity);
        vignetteMaskIntensity = Mathf.Clamp01(vignetteMaskIntensity);
        diegeticOverlayOpacity = Mathf.Clamp01(diegeticOverlayOpacity);
        damageVignetteStartHealthPercent = Mathf.Clamp01(damageVignetteStartHealthPercent);
        damageVignetteCriticalHealthPercent = Mathf.Clamp01(damageVignetteCriticalHealthPercent);
        if (damageVignetteCriticalHealthPercent > damageVignetteStartHealthPercent)
        {
            damageVignetteCriticalHealthPercent = damageVignetteStartHealthPercent;
        }
        damageVignetteMaxOpacity = Mathf.Clamp01(damageVignetteMaxOpacity);
        damageVignetteFadeSpeed = Mathf.Max(0f, damageVignetteFadeSpeed);
        fireOverlayBaseSize.x = Mathf.Max(0f, fireOverlayBaseSize.x);
        fireOverlayBaseSize.y = Mathf.Max(0f, fireOverlayBaseSize.y);
        fireOverlayExtraSize.x = Mathf.Max(0f, fireOverlayExtraSize.x);
        fireOverlayExtraSize.y = Mathf.Max(0f, fireOverlayExtraSize.y);
    }

    private void ResolveReferences()
    {
        if (exposure == null)
        {
            exposure = FindAnyObjectByType<PlayerHazardExposure>();
        }

        if (thermalVisionController == null)
        {
            thermalVisionController = FindAnyObjectByType<ThermalVisionController>();
        }

        if (playerVitals == null)
        {
            playerVitals = FindAnyObjectByType<PlayerVitals>(FindObjectsInactive.Exclude);
        }

        if (helmetOverlay == null)
        {
            helmetOverlay = FindImageByNames(HelmetOverlayCandidateNames);
        }

        if (visorOverlay == null)
        {
            visorOverlay = FindImageByNames(VisorOverlayCandidateNames);
        }

        if (vignetteOverlay == null)
        {
            vignetteOverlay = FindImageByNames(VignetteOverlayCandidateNames);
        }

        if (damageVignetteOverlay == null)
        {
            damageVignetteOverlay = FindImageByNames(DamageVignetteCandidateNames);
        }

        if (statusBarRectTransform == null &&
            TryFindDescendantByName(transform, "StatusBar", out Transform statusBarTransform))
        {
            statusBarRectTransform = statusBarTransform as RectTransform;
        }
    }

    private void EnsureOverlayImages()
    {
        if (!HasSmokeOverlayReferences())
        {
            WarnMissingSmokeOverlayReferences();
        }
        else if (smokeOverlay.texture == null)
        {
            WarnMissingSmokeTexture();
        }

        if (!HasFireOverlayReferences())
        {
            WarnMissingFireOverlayReferences();
        }

        if (smokeCanvasGroup != null)
        {
            smokeCanvasGroup.interactable = false;
            smokeCanvasGroup.blocksRaycasts = false;
        }

        InitializeOverlayDefaultsIfNeeded();
        ConfigureMaskLayer(helmetOverlay);
        ConfigureMaskLayer(visorOverlay);
        ConfigureMaskLayer(vignetteOverlay);
        ConfigureMaskLayer(damageVignetteOverlay);
        EnsureDamageVignetteDefaults();

        int fireSlotCount = GetFireOverlaySlotCount();
        EnsureFireAlphaCacheSize(fireSlotCount);

        for (int i = 0; i < fireSlotCount; i++)
        {
            RawImage fireOverlay = fireOverlays[i];
            CanvasGroup fireCanvasGroup = fireCanvasGroups[i];

            fireCanvasGroup.interactable = false;
            fireCanvasGroup.blocksRaycasts = false;

            if (fireOverlay.texture == null)
            {
                WarnMissingFireTexture();
            }
        }
    }

    private void RefreshOverlayImmediate()
    {
        UpdateMaskLayers();
        UpdateSmokeOverlay();
        UpdateFireOverlay();
        UpdateDamageVignetteOverlay();
    }

    public void SetHelmetOverlayVisible(bool visible, float opacity = 1f)
    {
        helmetOverlayVisible = visible;
        helmetOverlayOpacity = Mathf.Clamp01(opacity);
    }

    public void SetVisorOverlayVisible(bool visible, float opacity = 0.85f)
    {
        visorOverlayVisible = visible;
        visorOverlayOpacity = Mathf.Clamp01(opacity);
    }

    public void SetVignetteMaskIntensity(float intensity01)
    {
        vignetteMaskIntensity = Mathf.Clamp01(intensity01);
    }

    public void SetScreenMaskOpacityMultiplier(float multiplier01)
    {
        screenMaskOpacityMultiplier = Mathf.Clamp01(multiplier01);
    }

    public void SetDiegeticOverlayVisible(bool visible, float opacity = 1f)
    {
        diegeticOverlayVisible = visible;
        diegeticOverlayOpacity = Mathf.Clamp01(opacity);
    }

    private void UpdateMaskLayers()
    {
        ApplyMaskLayer(helmetOverlay, helmetOverlayVisible, helmetOverlayOpacity);
        ApplyMaskLayer(visorOverlay, visorOverlayVisible, visorOverlayOpacity);
        ApplyMaskLayer(vignetteOverlay, vignetteMaskIntensity > 0.001f, vignetteMaskIntensity);

        if (diegeticOverlayCanvasGroup != null)
        {
            diegeticOverlayCanvasGroup.interactable = false;
            diegeticOverlayCanvasGroup.blocksRaycasts = false;
            diegeticOverlayCanvasGroup.alpha = diegeticOverlayVisible ? diegeticOverlayOpacity : 0f;
        }

        UpdateStatusBarPlacement(IsOverlayMaskVisible());
    }

    private void UpdateSmokeOverlay()
    {
        if (smokeOverlay == null || smokeCanvasGroup == null)
        {
            return;
        }

        float intensity = exposure != null ? exposure.SmokeDensity01 : 0f;
        if (thermalVisionController != null && thermalVisionController.IsThermalVisionActive)
        {
            intensity *= thermalVisionController.SmokeOverlayMultiplier;
        }

        bool hasTexture = smokeOverlay.texture != null;
        float alpha = hasTexture
            ? Mathf.Clamp01(intensity) * maxSmokeOverlayOpacity * screenMaskOpacityMultiplier
            : 0f;

        smokeOverlay.enabled = hasTexture;
        smokeCanvasGroup.alpha = alpha;

        if (!Mathf.Approximately(lastAppliedAlpha, alpha))
        {
            smokeOverlay.canvasRenderer.SetAlpha(alpha);
            smokeOverlay.SetVerticesDirty();
            smokeOverlay.SetMaterialDirty();
            lastAppliedAlpha = alpha;
        }
    }

    private void UpdateFireOverlay()
    {
        int fireSlotCount = GetFireOverlaySlotCount();
        if (fireSlotCount <= 0)
        {
            return;
        }

        EnsureFireAlphaCacheSize(fireSlotCount);
        int visibleSlotCount = exposure != null ? exposure.SimultaneousFireOverlayCount : 0;

        for (int i = 0; i < fireSlotCount; i++)
        {
            RawImage fireOverlay = fireOverlays[i];
            CanvasGroup fireCanvasGroup = fireCanvasGroups[i];
            bool hasTexture = fireOverlay.texture != null;
            float intensity = exposure != null && i < visibleSlotCount
                ? exposure.GetFireGlare01(i)
                : 0f;
            float normalizedIntensity = Mathf.Clamp01(intensity);
            float alpha = hasTexture
                ? Mathf.SmoothStep(0f, 1f, normalizedIntensity) * maxFireOverlayOpacity * screenMaskOpacityMultiplier
                : 0f;

            fireOverlay.enabled = hasTexture;
            fireCanvasGroup.alpha = alpha;

            RectTransform fireRectTransform = fireOverlay.rectTransform;
            Vector2 viewportPosition = exposure != null && i < visibleSlotCount
                ? exposure.GetFireGlareViewportPosition(i)
                : new Vector2(0.5f, 0.5f);
            fireRectTransform.anchorMin = viewportPosition;
            fireRectTransform.anchorMax = viewportPosition;
            fireRectTransform.anchoredPosition = Vector2.zero;
            fireRectTransform.pivot = new Vector2(0.5f, 0.5f);
            fireRectTransform.sizeDelta = fireOverlayBaseSize + fireOverlayExtraSize * Mathf.Sqrt(normalizedIntensity);

            if (Mathf.Approximately(lastAppliedFireAlphas[i], alpha))
            {
                continue;
            }

            fireOverlay.canvasRenderer.SetAlpha(alpha);
            fireOverlay.SetVerticesDirty();
            fireOverlay.SetMaterialDirty();
            lastAppliedFireAlphas[i] = alpha;
        }
    }

    private bool HasSmokeOverlayReferences()
    {
        return smokeOverlay != null && smokeCanvasGroup != null;
    }

    private bool HasFireOverlayReferences()
    {
        if (fireOverlays == null ||
            fireCanvasGroups == null ||
            fireOverlays.Length == 0 ||
            fireCanvasGroups.Length == 0 ||
            fireOverlays.Length != fireCanvasGroups.Length)
        {
            return false;
        }

        int fireSlotCount = GetFireOverlaySlotCount();
        if (fireSlotCount <= 0)
        {
            return false;
        }

        for (int i = 0; i < fireSlotCount; i++)
        {
            if (fireOverlays[i] == null || fireCanvasGroups[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    private void WarnMissingSmokeOverlayReferences()
    {
        if (hasWarnedMissingSmokeOverlayReferences)
        {
            return;
        }

        hasWarnedMissingSmokeOverlayReferences = true;
        Debug.LogWarning(
            $"[{nameof(PlayerHazardOverlayUI)}] Missing overlay references on '{name}'. Assign SmokeOverlay and SmokeCanvasGroup in the inspector.",
            this);
    }

    private void WarnMissingFireOverlayReferences()
    {
        if (hasWarnedMissingFireOverlayReferences)
        {
            return;
        }

        hasWarnedMissingFireOverlayReferences = true;
        Debug.LogWarning(
            $"[{nameof(PlayerHazardOverlayUI)}] Missing fire overlay references on '{name}'. Assign matching FireOverlays and FireCanvasGroups in the inspector.",
            this);
    }

    private void WarnMissingSmokeTexture()
    {
        if (hasWarnedMissingSmokeTexture)
        {
            return;
        }

        hasWarnedMissingSmokeTexture = true;
        Debug.LogWarning($"[{nameof(PlayerHazardOverlayUI)}] Smoke texture is not assigned on '{name}'.", this);
    }

    private void WarnMissingFireTexture()
    {
        if (hasWarnedMissingFireTexture)
        {
            return;
        }

        hasWarnedMissingFireTexture = true;
        Debug.LogWarning($"[{nameof(PlayerHazardOverlayUI)}] Fire texture is not assigned on '{name}'.", this);
    }

    private bool IsOverlayMaskVisible()
    {
        if (helmetOverlay == null)
        {
            return false;
        }

        return helmetOverlay.gameObject.activeSelf && helmetOverlay.color.a > 0.001f;
    }

    private void UpdateStatusBarPlacement(bool overlayVisible)
    {
        if (statusBarRectTransform == null)
        {
            WarnMissingStatusBarReference();
            return;
        }

        Vector2 targetAnchoredPosition = overlayVisible
            ? statusBarAnchoredPositionWhenOverlayVisible
            : statusBarAnchoredPositionWhenOverlayHidden;
        Vector3 targetScale = overlayVisible
            ? statusBarScaleWhenOverlayVisible
            : statusBarScaleWhenOverlayHidden;

        if (statusBarRectTransform.anchoredPosition != targetAnchoredPosition)
        {
            statusBarRectTransform.anchoredPosition = targetAnchoredPosition;
        }

        if (statusBarRectTransform.localRotation != Quaternion.identity)
        {
            statusBarRectTransform.localRotation = Quaternion.identity;
        }

        if (statusBarRectTransform.localScale != targetScale)
        {
            statusBarRectTransform.localScale = targetScale;
        }
    }

    private void UpdateDamageVignetteOverlay()
    {
        if (damageVignetteOverlay == null)
        {
            WarnMissingDamageVignetteReference();
            return;
        }

        float healthPercent = playerVitals != null ? Mathf.Clamp01(playerVitals.HealthPercent) : 1f;
        float targetAlpha = ResolveDamageVignetteTargetAlpha(healthPercent);
        float blend = damageVignetteFadeSpeed > 0f ? Time.unscaledDeltaTime * damageVignetteFadeSpeed : 1f;
        currentDamageVignetteAlpha = Mathf.Lerp(currentDamageVignetteAlpha, targetAlpha, Mathf.Clamp01(blend));

        bool shouldBeVisible = currentDamageVignetteAlpha > 0.001f;
        if (damageVignetteOverlay.gameObject.activeSelf != shouldBeVisible)
        {
            damageVignetteOverlay.gameObject.SetActive(shouldBeVisible);
        }

        Color color = damageVignetteOverlay.color;
        if (!Mathf.Approximately(color.a, currentDamageVignetteAlpha))
        {
            color.a = currentDamageVignetteAlpha;
            damageVignetteOverlay.color = color;
        }
    }

    private float ResolveDamageVignetteTargetAlpha(float healthPercent)
    {
        if (healthPercent >= damageVignetteStartHealthPercent)
        {
            return 0f;
        }

        float normalizedRange = Mathf.Max(0.0001f, damageVignetteStartHealthPercent - damageVignetteCriticalHealthPercent);
        float t = (damageVignetteStartHealthPercent - healthPercent) / normalizedRange;
        float curved = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
        return curved * damageVignetteMaxOpacity;
    }

    private void WarnMissingStatusBarReference()
    {
        if (hasWarnedMissingStatusBarReference)
        {
            return;
        }

        hasWarnedMissingStatusBarReference = true;
        Debug.LogWarning(
            $"[{nameof(PlayerHazardOverlayUI)}] Missing StatusBar reference on '{name}'. Assign StatusBar RectTransform in the inspector.",
            this);
    }

    private void WarnMissingDamageVignetteReference()
    {
        if (hasWarnedMissingDamageVignetteReference)
        {
            return;
        }

        hasWarnedMissingDamageVignetteReference = true;
        Debug.LogWarning(
            $"[{nameof(PlayerHazardOverlayUI)}] Missing DamageVignette reference on '{name}'. Assign DamageVignette Image in the inspector.",
            this);
    }

    private int GetFireOverlaySlotCount()
    {
        if (fireOverlays == null || fireCanvasGroups == null)
        {
            return 0;
        }

        return Mathf.Min(fireOverlays.Length, fireCanvasGroups.Length);
    }

    private void InitializeOverlayDefaultsIfNeeded()
    {
        if (hasInitializedOverlayDefaults)
        {
            return;
        }

        if (helmetOverlay != null)
        {
            helmetOverlayVisible = helmetOverlay.gameObject.activeSelf;
            helmetOverlayOpacity = Mathf.Clamp01(helmetOverlay.color.a);
        }

        if (visorOverlay != null)
        {
            visorOverlayVisible = visorOverlay.gameObject.activeSelf;
            visorOverlayOpacity = Mathf.Clamp01(visorOverlay.color.a);
        }

        if (vignetteOverlay != null)
        {
            vignetteMaskIntensity = vignetteOverlay.gameObject.activeSelf
                ? Mathf.Clamp01(vignetteOverlay.color.a)
                : 0f;
        }

        hasInitializedOverlayDefaults = true;
    }

    private void EnsureDamageVignetteDefaults()
    {
        if (damageVignetteOverlay == null)
        {
            hasInitializedDamageVignetteDefaults = false;
            return;
        }

        if (hasInitializedDamageVignetteDefaults)
        {
            return;
        }

        Color color = damageVignetteOverlay.color;
        if (!Mathf.Approximately(color.a, 0f))
        {
            color.a = 0f;
            damageVignetteOverlay.color = color;
        }

        if (damageVignetteOverlay.gameObject.activeSelf)
        {
            damageVignetteOverlay.gameObject.SetActive(false);
        }

        currentDamageVignetteAlpha = 0f;
        hasInitializedDamageVignetteDefaults = true;
    }

    private void ConfigureMaskLayer(Image layer)
    {
        if (layer == null)
        {
            return;
        }

        layer.raycastTarget = false;
    }

    private static void ApplyMaskLayer(Image layer, bool visible, float alpha)
    {
        if (layer == null)
        {
            return;
        }

        bool shouldBeActive = visible && alpha > 0.001f;
        if (layer.gameObject.activeSelf != shouldBeActive)
        {
            layer.gameObject.SetActive(shouldBeActive);
        }

        Color color = layer.color;
        float clampedAlpha = Mathf.Clamp01(alpha);
        if (!Mathf.Approximately(color.a, clampedAlpha))
        {
            color.a = clampedAlpha;
            layer.color = color;
        }
    }

    private Image FindImageByNames(string[] candidateNames)
    {
        if (candidateNames == null || candidateNames.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < candidateNames.Length; i++)
        {
            if (TryFindDescendantByName(transform, candidateNames[i], out Transform found))
            {
                return found.GetComponent<Image>();
            }
        }

        return null;
    }

    private static bool TryFindDescendantByName(Transform root, string targetName, out Transform result)
    {
        result = null;
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return false;
        }

        int childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                result = child;
                return true;
            }

            if (TryFindDescendantByName(child, targetName, out result))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureFireAlphaCacheSize(int size)
    {
        if (size <= 0)
        {
            lastAppliedFireAlphas = Array.Empty<float>();
            return;
        }

        if (lastAppliedFireAlphas.Length == size)
        {
            return;
        }

        float[] resized = new float[size];

        for (int i = 0; i < resized.Length; i++)
        {
            resized[i] = -1f;
        }

        if (lastAppliedFireAlphas != null)
        {
            int count = Mathf.Min(lastAppliedFireAlphas.Length, resized.Length);
            Array.Copy(lastAppliedFireAlphas, resized, count);
        }

        lastAppliedFireAlphas = resized;
    }
}
