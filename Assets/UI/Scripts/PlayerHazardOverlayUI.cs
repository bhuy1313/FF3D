using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerHazardOverlayUI : MonoBehaviour
{
    [SerializeField] private PlayerHazardExposure exposure;
    [SerializeField] private RawImage smokeOverlay;
    [SerializeField] private CanvasGroup smokeCanvasGroup;
    [SerializeField, Range(0f, 1f)] private float maxSmokeOverlayOpacity = 1f;

    private bool hasWarnedMissingOverlayReferences;
    private bool hasWarnedMissingSmokeTexture;
    private float lastAppliedAlpha = -1f;

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
        UpdateSmokeOverlay();
    }

    private void OnValidate()
    {
        maxSmokeOverlayOpacity = Mathf.Clamp01(maxSmokeOverlayOpacity);
    }

    private void ResolveReferences()
    {
        if (exposure == null)
        {
            exposure = FindAnyObjectByType<PlayerHazardExposure>();
        }
    }

    private void EnsureOverlayImages()
    {
        if (!HasOverlayReferences())
        {
            WarnMissingOverlayReferences();
            return;
        }

        if (smokeOverlay.texture == null)
        {
            WarnMissingSmokeTexture();
        }

        smokeCanvasGroup.interactable = false;
        smokeCanvasGroup.blocksRaycasts = false;
    }

    private void RefreshOverlayImmediate()
    {
        UpdateSmokeOverlay();
    }

    private void UpdateSmokeOverlay()
    {
        if (smokeOverlay == null || smokeCanvasGroup == null)
        {
            return;
        }

        float intensity = exposure != null ? exposure.SmokeDensity01 : 0f;
        bool hasTexture = smokeOverlay.texture != null;
        float alpha = hasTexture
            ? Mathf.Clamp01(intensity) * maxSmokeOverlayOpacity
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

    private bool HasOverlayReferences()
    {
        return smokeOverlay != null &&
               smokeCanvasGroup != null;
    }

    private void WarnMissingOverlayReferences()
    {
        if (hasWarnedMissingOverlayReferences)
        {
            return;
        }

        hasWarnedMissingOverlayReferences = true;
        Debug.LogWarning(
            $"[{nameof(PlayerHazardOverlayUI)}] Missing overlay references on '{name}'. Assign SmokeOverlay and SmokeCanvasGroup in the inspector.",
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
}
