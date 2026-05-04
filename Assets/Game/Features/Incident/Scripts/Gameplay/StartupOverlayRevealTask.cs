using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StartupOverlayRevealTask : SceneStartupTask
{
    [Header("Timing")]
    [SerializeField] private float blackHoldDuration = 0.1f;
    [SerializeField] private float revealDuration = 1.15f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private AnimationCurve revealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Overlay")]
    [SerializeField] private RawImage overlayImage;
    [SerializeField] private Color overlayColor = Color.black;
    [SerializeField] [Range(0.01f, 0.35f)] private float edgeSoftness = 0.18f;
    [SerializeField] [Range(64, 512)] private int textureSize = 256;
    [SerializeField] private int sortingOrder = 32760;
    [SerializeField] private bool destroyRuntimeOverlayWhenDone = true;

    private Texture2D runtimeMaskTexture;
    private Color32[] maskPixels;
    private Coroutine playRoutine;

    public bool IsPlaying => playRoutine != null;

    protected override IEnumerator Execute(SceneStartupFlow startupFlow)
    {
        Play();
        while (IsPlaying)
        {
            yield return null;
        }
    }

    public void Play()
    {
        if (playRoutine != null)
        {
            return;
        }

        playRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        RawImage image = ResolveOverlayImage();
        if (image == null)
        {
            playRoutine = null;
            yield break;
        }

        image.gameObject.SetActive(true);
        image.color = overlayColor;

        ApplyMask(image, -edgeSoftness);

        float holdDuration = Mathf.Max(0f, blackHoldDuration);
        float holdElapsed = 0f;
        while (holdElapsed < holdDuration)
        {
            holdElapsed += ResolveDeltaTime();
            yield return null;
        }

        float duration = Mathf.Max(0.01f, revealDuration);
        float elapsed = 0f;
        float maxRadius = 0.7072f + edgeSoftness;

        while (elapsed < duration)
        {
            elapsed += ResolveDeltaTime();
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float curvedTime = EvaluateReveal(normalizedTime);
            float revealRadius = Mathf.Lerp(-edgeSoftness, maxRadius, curvedTime);
            ApplyMask(image, revealRadius);
            yield return null;
        }

        ApplyMask(image, maxRadius);

        if (destroyRuntimeOverlayWhenDone && overlayImage == null && image != null)
        {
            Destroy(image.transform.root.gameObject);
        }
        else if (image != null)
        {
            image.gameObject.SetActive(false);
        }

        playRoutine = null;
    }

    private RawImage ResolveOverlayImage()
    {
        if (overlayImage != null)
        {
            return overlayImage;
        }

        return CreateRuntimeOverlayImage();
    }

    private RawImage CreateRuntimeOverlayImage()
    {
        GameObject canvasObject = new GameObject("Startup Overlay Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject imageObject = new GameObject("Radial Reveal Overlay");
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rectTransform = imageObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        RawImage image = imageObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        return image;
    }

    private void ApplyMask(RawImage image, float revealRadius)
    {
        if (image == null)
        {
            return;
        }

        Texture2D maskTexture = ResolveMaskTexture();
        if (maskTexture == null)
        {
            return;
        }

        float softness = Mathf.Max(0.001f, edgeSoftness);
        int size = maskTexture.width;
        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            float normalizedY = (y - center) / size;
            for (int x = 0; x < size; x++)
            {
                float normalizedX = (x - center) / size;
                float distance = Mathf.Sqrt((normalizedX * normalizedX) + (normalizedY * normalizedY));
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(revealRadius, revealRadius + softness, distance));
                byte alphaByte = (byte)Mathf.RoundToInt(alpha * 255f);
                maskPixels[(y * size) + x] = new Color32(255, 255, 255, alphaByte);
            }
        }

        maskTexture.SetPixels32(maskPixels);
        maskTexture.Apply(false, false);
        image.texture = maskTexture;
    }

    private Texture2D ResolveMaskTexture()
    {
        int size = Mathf.Clamp(textureSize, 64, 512);
        if (runtimeMaskTexture != null && runtimeMaskTexture.width == size)
        {
            return runtimeMaskTexture;
        }

        if (runtimeMaskTexture != null)
        {
            Destroy(runtimeMaskTexture);
        }

        runtimeMaskTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "StartupRadialRevealMask",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        maskPixels = new Color32[size * size];
        return runtimeMaskTexture;
    }

    private float ResolveDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private float EvaluateReveal(float normalizedTime)
    {
        if (revealCurve == null || revealCurve.length == 0)
        {
            return normalizedTime;
        }

        return Mathf.Clamp01(revealCurve.Evaluate(normalizedTime));
    }

    private void OnDestroy()
    {
        playRoutine = null;

        if (runtimeMaskTexture != null)
        {
            Destroy(runtimeMaskTexture);
            runtimeMaskTexture = null;
        }
    }
}
