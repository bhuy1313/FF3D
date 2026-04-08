using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerHazardOverlayUI : MonoBehaviour
{
    [SerializeField] private PlayerHazardExposure exposure;
    [SerializeField] private RawImage smokeOverlay;
    [SerializeField] private CanvasGroup smokeCanvasGroup;
    [SerializeField] private RawImage[] fireOverlays = Array.Empty<RawImage>();
    [SerializeField] private CanvasGroup[] fireCanvasGroups = Array.Empty<CanvasGroup>();
    [SerializeField, Range(0f, 1f)] private float maxSmokeOverlayOpacity = 1f;
    [SerializeField, Range(0f, 1f)] private float maxFireOverlayOpacity = 1f;
    [SerializeField] private Vector2 fireOverlayBaseSize = new Vector2(420f, 420f);
    [SerializeField] private Vector2 fireOverlayExtraSize = new Vector2(320f, 320f);

    private bool hasWarnedMissingSmokeOverlayReferences;
    private bool hasWarnedMissingFireOverlayReferences;
    private bool hasWarnedMissingSmokeTexture;
    private bool hasWarnedMissingFireTexture;
    private float lastAppliedAlpha = -1f;
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
        UpdateSmokeOverlay();
        UpdateFireOverlay();
    }

    private void OnValidate()
    {
        maxSmokeOverlayOpacity = Mathf.Clamp01(maxSmokeOverlayOpacity);
        maxFireOverlayOpacity = Mathf.Clamp01(maxFireOverlayOpacity);
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
        UpdateSmokeOverlay();
        UpdateFireOverlay();
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
                ? Mathf.SmoothStep(0f, 1f, normalizedIntensity) * maxFireOverlayOpacity
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

    private int GetFireOverlaySlotCount()
    {
        if (fireOverlays == null || fireCanvasGroups == null)
        {
            return 0;
        }

        return Mathf.Min(fireOverlays.Length, fireCanvasGroups.Length);
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
