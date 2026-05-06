using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RawImage))]
public class MainMenuBackgroundImageController : MonoBehaviour
{
    [SerializeField] private RawImage targetImage;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private Vector2 textureAspect = new Vector2(2f, 1f);
    [SerializeField] [Range(0.65f, 1f)] private float zoomUvScale = 0.9f;
    [SerializeField] private bool randomDrift = true;
    [SerializeField] private Vector2 driftIntervalRange = new Vector2(4f, 8f);
    [SerializeField] private float driftSmoothTime = 2.8f;
    [SerializeField] [Range(0f, 1f)] private float horizontalDriftAmount = 1f;
    [SerializeField] [Range(0f, 1f)] private float verticalDriftAmount = 0.55f;

    private Vector2 currentAnchor = new Vector2(0.5f, 0.5f);
    private Vector2 targetAnchor = new Vector2(0.5f, 0.5f);
    private Vector2 anchorVelocity;
    private float nextDriftTime;

    private void Reset()
    {
        targetImage = GetComponent<RawImage>();
        viewport = transform.parent as RectTransform;
    }

    private void Awake()
    {
        EnsureReferences();
        if (targetImage != null)
        {
            targetImage.raycastTarget = false;
        }
    }

    private void OnEnable()
    {
        currentAnchor = new Vector2(0.5f, 0.5f);
        targetAnchor = currentAnchor;
        ScheduleNextDrift();
        ApplyUvRect();
    }

    private void LateUpdate()
    {
        ApplyUvRect();
    }

    private void EnsureReferences()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<RawImage>();
        }

        if (viewport == null)
        {
            viewport = transform.parent as RectTransform;
        }
    }

    private void ApplyUvRect()
    {
        EnsureReferences();

        if (targetImage == null)
        {
            return;
        }

        RectTransform viewportRect = viewport != null ? viewport : transform as RectTransform;
        Rect rect = viewportRect != null ? viewportRect.rect : new Rect(0f, 0f, Screen.width, Screen.height);
        float viewportAspect = rect.height > 0f ? rect.width / rect.height : 16f / 9f;
        float imageAspect = textureAspect.y > 0f ? textureAspect.x / textureAspect.y : 2f;
        float uvScale = Mathf.Clamp01(zoomUvScale);
        Rect uvRect = new Rect(0f, 0f, 1f, 1f);

        if (viewportAspect < imageAspect)
        {
            uvRect.width = Mathf.Clamp01(viewportAspect / imageAspect);
        }
        else if (viewportAspect > imageAspect)
        {
            uvRect.height = Mathf.Clamp01(imageAspect / viewportAspect);
        }

        uvRect.width = Mathf.Clamp01(uvRect.width * uvScale);
        uvRect.height = Mathf.Clamp01(uvRect.height * uvScale);
        UpdateDriftAnchor();

        float maxXOffset = 1f - uvRect.width;
        float maxYOffset = 1f - uvRect.height;
        uvRect.x = maxXOffset * Mathf.Clamp01(currentAnchor.x);
        uvRect.y = maxYOffset * Mathf.Clamp01(currentAnchor.y);

        targetImage.uvRect = uvRect;
    }

    private void UpdateDriftAnchor()
    {
        if (!randomDrift)
        {
            currentAnchor = new Vector2(0.5f, 0.5f);
            targetAnchor = currentAnchor;
            anchorVelocity = Vector2.zero;
            return;
        }

        if (Time.unscaledTime >= nextDriftTime)
        {
            PickNextDriftTarget();
            ScheduleNextDrift();
        }

        float smoothTime = Mathf.Max(0.01f, driftSmoothTime);
        currentAnchor = Vector2.SmoothDamp(currentAnchor, targetAnchor, ref anchorVelocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
    }

    private void PickNextDriftTarget()
    {
        float horizontalRange = Mathf.Clamp01(horizontalDriftAmount) * 0.5f;
        float verticalRange = Mathf.Clamp01(verticalDriftAmount) * 0.5f;
        targetAnchor = new Vector2(
            Random.Range(0.5f - horizontalRange, 0.5f + horizontalRange),
            Random.Range(0.5f - verticalRange, 0.5f + verticalRange));
    }

    private void ScheduleNextDrift()
    {
        float minInterval = Mathf.Max(0.1f, Mathf.Min(driftIntervalRange.x, driftIntervalRange.y));
        float maxInterval = Mathf.Max(minInterval, Mathf.Max(driftIntervalRange.x, driftIntervalRange.y));
        nextDriftTime = Time.unscaledTime + Random.Range(minInterval, maxInterval);
    }
}
