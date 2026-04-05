using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerHazardOverlayUI : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private PlayerHazardExposure exposure;

    [Header("References")]
    [SerializeField] private RectTransform overlayRoot;
    [SerializeField] private RawImage smokeOverlay;
    [SerializeField] private RawImage fireOverlay;

    [Header("Smoke")]
    [SerializeField] private Color smokeColor = new Color(0.36f, 0.37f, 0.38f, 1f);
    [SerializeField, Range(0f, 1f)] private float smokeAlphaMultiplier = 0.72f;
    [SerializeField] private Vector2 smokeUvScale = new Vector2(2.75f, 2.25f);
    [SerializeField] private Vector2 smokeUvScrollSpeed = new Vector2(0.009f, -0.005f);

    [Header("Fire")]
    [SerializeField] private Color fireColor = new Color(1f, 0.72f, 0.28f, 1f);
    [SerializeField, Range(0f, 1f)] private float fireAlphaMultiplier = 0.9f;
    [SerializeField] private Vector2 fireOverlayBaseSize = new Vector2(720f, 720f);
    [SerializeField] private float fireOverlayMinScale = 0.75f;
    [SerializeField] private float fireOverlayMaxScale = 1.45f;

    private Texture2D smokeTexture;
    private Texture2D fireTexture;
    private RectTransform smokeRectTransform;
    private RectTransform fireRectTransform;

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

    private void ResolveReferences()
    {
        if (overlayRoot == null)
        {
            overlayRoot = transform as RectTransform;
        }

        if (exposure == null)
        {
            exposure = FindAnyObjectByType<PlayerHazardExposure>();
        }
    }

    private void EnsureOverlayImages()
    {
        if (overlayRoot == null)
        {
            return;
        }

        if (smokeOverlay == null)
        {
            smokeOverlay = CreateStretchOverlay("SmokeOverlay");
            smokeOverlay.texture = GetSmokeTexture();
            smokeOverlay.color = new Color(smokeColor.r, smokeColor.g, smokeColor.b, 0f);
            smokeRectTransform = smokeOverlay.rectTransform;
        }
        else if (smokeRectTransform == null)
        {
            smokeRectTransform = smokeOverlay.rectTransform;
        }

        if (fireOverlay == null)
        {
            fireOverlay = CreateCenteredOverlay("FireOverlay");
            fireOverlay.texture = GetFireTexture();
            fireOverlay.color = new Color(fireColor.r, fireColor.g, fireColor.b, 0f);
            fireRectTransform = fireOverlay.rectTransform;
        }
        else if (fireRectTransform == null)
        {
            fireRectTransform = fireOverlay.rectTransform;
        }
    }

    private RawImage CreateStretchOverlay(string objectName)
    {
        GameObject overlayObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        overlayObject.layer = gameObject.layer;
        overlayObject.transform.SetParent(overlayRoot, false);
        RectTransform rectTransform = overlayObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.SetAsFirstSibling();

        RawImage image = overlayObject.GetComponent<RawImage>();
        image.raycastTarget = false;
        return image;
    }

    private RawImage CreateCenteredOverlay(string objectName)
    {
        GameObject overlayObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        overlayObject.layer = gameObject.layer;
        overlayObject.transform.SetParent(overlayRoot, false);
        RectTransform rectTransform = overlayObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = fireOverlayBaseSize;
        rectTransform.SetAsFirstSibling();
        if (overlayRoot.childCount > 1)
        {
            rectTransform.SetSiblingIndex(1);
        }

        RawImage image = overlayObject.GetComponent<RawImage>();
        image.raycastTarget = false;
        return image;
    }

    private void RefreshOverlayImmediate()
    {
        UpdateSmokeOverlay();
        UpdateFireOverlay();
    }

    private void UpdateSmokeOverlay()
    {
        if (smokeOverlay == null)
        {
            return;
        }

        float intensity = exposure != null ? exposure.SmokeDensity01 : 0f;
        smokeOverlay.color = new Color(smokeColor.r, smokeColor.g, smokeColor.b, Mathf.Clamp01(intensity * smokeAlphaMultiplier));
        smokeOverlay.uvRect = new Rect(
            Time.unscaledTime * smokeUvScrollSpeed.x,
            Time.unscaledTime * smokeUvScrollSpeed.y,
            Mathf.Max(0.01f, smokeUvScale.x),
            Mathf.Max(0.01f, smokeUvScale.y));
    }

    private void UpdateFireOverlay()
    {
        if (fireOverlay == null || fireRectTransform == null || overlayRoot == null)
        {
            return;
        }

        float intensity = exposure != null ? exposure.FireGlare01 : 0f;
        fireOverlay.color = new Color(fireColor.r, fireColor.g, fireColor.b, Mathf.Clamp01(intensity * fireAlphaMultiplier));

        Vector2 viewport = exposure != null ? exposure.FireGlareViewportPosition : new Vector2(0.5f, 0.5f);
        Vector2 overlaySize = overlayRoot.rect.size;
        fireRectTransform.anchoredPosition = new Vector2(
            (viewport.x - 0.5f) * overlaySize.x,
            (viewport.y - 0.5f) * overlaySize.y);

        float scale = Mathf.Lerp(fireOverlayMinScale, fireOverlayMaxScale, Mathf.Clamp01(intensity));
        fireRectTransform.sizeDelta = fireOverlayBaseSize * scale;
    }

    private Texture2D GetSmokeTexture()
    {
        if (smokeTexture != null)
        {
            return smokeTexture;
        }

        smokeTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false)
        {
            name = "RuntimeSmokeOverlayTexture",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
        };

        for (int y = 0; y < smokeTexture.height; y++)
        {
            for (int x = 0; x < smokeTexture.width; x++)
            {
                float u = (float)x / (smokeTexture.width - 1);
                float v = (float)y / (smokeTexture.height - 1);
                float low = Mathf.PerlinNoise(u * 2.2f + 3.1f, v * 2.2f + 5.7f);
                float mid = Mathf.PerlinNoise(u * 4.8f + 11.4f, v * 4.8f + 2.9f);
                float high = Mathf.PerlinNoise(u * 9.6f + 7.2f, v * 9.6f + 13.3f);
                float noise = Mathf.Clamp01(low * 0.55f + mid * 0.3f + high * 0.15f);
                float alpha = Mathf.SmoothStep(0.2f, 0.95f, noise) * 0.85f;
                smokeTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        smokeTexture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        return smokeTexture;
    }

    private Texture2D GetFireTexture()
    {
        if (fireTexture != null)
        {
            return fireTexture;
        }

        fireTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false)
        {
            name = "RuntimeFireOverlayTexture",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
        };

        Vector2 center = new Vector2(0.5f, 0.5f);
        for (int y = 0; y < fireTexture.height; y++)
        {
            for (int x = 0; x < fireTexture.width; x++)
            {
                Vector2 uv = new Vector2(
                    (float)x / (fireTexture.width - 1),
                    (float)y / (fireTexture.height - 1));
                float radial = 1f - Mathf.Clamp01(Vector2.Distance(uv, center) / 0.7071f);
                float alpha = Mathf.Pow(radial, 2.35f);
                fireTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        fireTexture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        return fireTexture;
    }

    private void OnDestroy()
    {
        if (smokeTexture != null)
        {
            DestroyRuntimeSafe(smokeTexture);
            smokeTexture = null;
        }

        if (fireTexture != null)
        {
            DestroyRuntimeSafe(fireTexture);
            fireTexture = null;
        }
    }

    private static void DestroyRuntimeSafe(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
