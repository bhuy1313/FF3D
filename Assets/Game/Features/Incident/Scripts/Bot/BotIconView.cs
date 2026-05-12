using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BotIconView : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Image iconImage;

    private static Sprite fallbackSprite;

    private int boundInstanceId = -1;

    public int BoundInstanceId => boundInstanceId;

    private void Awake()
    {
        EnsureComponents();
    }

    public void Bind(int instanceId)
    {
        boundInstanceId = instanceId;
        gameObject.SetActive(true);
    }

    public void Unbind()
    {
        boundInstanceId = -1;
        gameObject.SetActive(false);
    }

    public void Apply(Camera camera, Vector3 worldPosition, float alpha, float sizePixels, Sprite sprite, Color color)
    {
        EnsureComponents();

        Camera targetCamera = camera != null ? camera : Camera.main;
        if (targetCamera == null)
        {
            iconImage.enabled = false;
            return;
        }

        Vector3 screenPoint = targetCamera.WorldToScreenPoint(worldPosition);
        if (screenPoint.z <= 0f)
        {
            iconImage.enabled = false;
            return;
        }

        if (screenPoint.x < 0f || screenPoint.x > Screen.width || screenPoint.y < 0f || screenPoint.y > Screen.height)
        {
            iconImage.enabled = false;
            return;
        }

        rectTransform.position = screenPoint;
        rectTransform.sizeDelta = Vector2.one * Mathf.Max(8f, sizePixels);
        iconImage.sprite = sprite != null ? sprite : GetFallbackSprite();

        Color resolvedColor = color;
        resolvedColor.a *= Mathf.Clamp01(alpha);
        iconImage.color = resolvedColor;
        iconImage.enabled = resolvedColor.a > 0.001f;
    }

    private void EnsureComponents()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (iconImage == null)
        {
            iconImage = GetComponent<Image>();
        }

        if (iconImage == null)
        {
            iconImage = gameObject.AddComponent<Image>();
        }

        iconImage.raycastTarget = false;
        iconImage.preserveAspect = true;
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return fallbackSprite;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Bot Icon Texture",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float v = (y + 0.5f) / size;
                float dx = (u - 0.5f) / 0.34f;
                float dy = (v - 0.52f) / 0.34f;
                float helmet = 1f - Mathf.Clamp01(dx * dx + dy * dy);

                float bodyX = Mathf.Abs(u - 0.5f) / 0.18f;
                float bodyY = Mathf.Abs(v - 0.2f) / 0.2f;
                float body = 1f - Mathf.Clamp01(bodyX + bodyY);

                float silhouette = Mathf.Clamp01(Mathf.Max(helmet, body));
                float alpha = Mathf.SmoothStep(0f, 1f, silhouette);
                Color pixel = Color.Lerp(
                    new Color(0.15f, 0.72f, 1f, alpha),
                    new Color(0.92f, 0.98f, 1f, alpha),
                    Mathf.Clamp01(v));
                texture.SetPixel(x, y, new Color(pixel.r, pixel.g, pixel.b, alpha));
            }
        }

        texture.Apply(false, true);
        fallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.15f), 100f);
        fallbackSprite.name = "Runtime Bot Icon Sprite";
        return fallbackSprite;
    }
}
