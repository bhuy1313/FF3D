using UnityEngine;

/*
Usage:
- Attach this script to a Call Phase UI controller/manager object.
- viewportRoot should be the visible masked area (full-screen viewport for Call Phase).
- contentRoot should contain all Call Phase UI content that needs zoom + edge pan.
- Popup systems can pause/resume this effect via SetSuppressed(true/false).
*/
[DisallowMultipleComponent]
public class CallPhaseScreenFocus : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform viewportRoot;
    [SerializeField] private RectTransform contentRoot;

    [Header("Zoom")]
    [SerializeField] private bool applyZoomOnEnable = true;
    [SerializeField] private float zoomScale = 1.1f;

    [Header("Scroll Zoom (Optional)")]
    [SerializeField] private bool enableCtrlScrollZoom = true;
    [SerializeField] private float ctrlScrollZoomStep = 0.03f;
    [SerializeField] private float minZoomScale = 1f;
    [SerializeField] private float maxZoomScale = 1.25f;

    [Header("Edge Pan")]
    [SerializeField] private bool effectEnabled = true;
    [SerializeField] private bool suppressWhilePopupOpen = true;
    [SerializeField] private bool isSuppressed = false;
    [SerializeField] private float edgeThresholdPercent = 0.12f;
    [SerializeField] private bool ignoreOutOfBoundsMouse = true;

    [Header("Smoothing")]
    [SerializeField] private float panSmoothTime = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private Vector2 defaultAnchoredPosition;
    private Vector3 defaultLocalScale;
    private Vector2 panVelocity;
    private bool smoothResetRequested;
    private bool isInitialized;

    private void Awake()
    {
        if (!ValidateReferences())
        {
            return;
        }

        defaultAnchoredPosition = contentRoot.anchoredPosition;
        defaultLocalScale = contentRoot.localScale;
        isInitialized = true;
        zoomScale = ClampZoomScale(zoomScale);

        if (applyZoomOnEnable)
        {
            ApplyZoom();
        }

        contentRoot.anchoredPosition = Vector2.zero;
        panVelocity = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (!isInitialized || contentRoot == null)
        {
            return;
        }

        // Restore the original authoring values when this component is removed/destroyed.
        contentRoot.anchoredPosition = defaultAnchoredPosition;
        contentRoot.localScale = defaultLocalScale;
    }

    private void Update()
    {
        if (!isInitialized || !ValidateReferences())
        {
            return;
        }

        HandleCtrlScrollZoomInput();

        bool shouldReturnToCenter = smoothResetRequested ||
                                    !effectEnabled ||
                                    (suppressWhilePopupOpen && isSuppressed);

        Vector2 targetPan = Vector2.zero;

        if (!shouldReturnToCenter)
        {
            Vector3 mousePosition = Input.mousePosition;

            if (ignoreOutOfBoundsMouse && IsOutOfBounds(mousePosition))
            {
                shouldReturnToCenter = true;
            }
            else
            {
                Vector2 edgeInfluence = CalculateEdgeInfluence(mousePosition);
                Vector2 maxPan = CalculateMaxPan();

                // Invert direction so edge hover reveals cropped content on that side.
                targetPan = new Vector2(-edgeInfluence.x * maxPan.x, -edgeInfluence.y * maxPan.y);
            }
        }

        if (shouldReturnToCenter)
        {
            targetPan = Vector2.zero;
        }

        Vector2 maxPanLimits = CalculateMaxPan();
        targetPan.x = Mathf.Clamp(targetPan.x, -maxPanLimits.x, maxPanLimits.x);
        targetPan.y = Mathf.Clamp(targetPan.y, -maxPanLimits.y, maxPanLimits.y);

        float safeSmoothTime = Mathf.Max(0.0001f, panSmoothTime);
        Vector2 current = contentRoot.anchoredPosition;

        contentRoot.anchoredPosition = Vector2.SmoothDamp(
            current,
            targetPan,
            ref panVelocity,
            safeSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);

        if (smoothResetRequested && contentRoot.anchoredPosition.sqrMagnitude <= 0.000001f && panVelocity.sqrMagnitude <= 0.000001f)
        {
            smoothResetRequested = false;
        }
    }

    public void SetEffectEnabled(bool value)
    {
        effectEnabled = value;
        if (!effectEnabled)
        {
            smoothResetRequested = false;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(CallPhaseScreenFocus)}: effectEnabled = {effectEnabled}", this);
        }
    }

    public void SetSuppressed(bool value)
    {
        isSuppressed = value;

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(CallPhaseScreenFocus)}: isSuppressed = {isSuppressed}", this);
        }
    }

    public void SetZoomScale(float value, bool recenter = true)
    {
        zoomScale = ClampZoomScale(value);

        if (!isInitialized || !ValidateReferences())
        {
            return;
        }

        ApplyZoom();

        if (recenter)
        {
            contentRoot.anchoredPosition = Vector2.zero;
            panVelocity = Vector2.zero;
            smoothResetRequested = false;
        }
    }

    public void ResetFocusInstant()
    {
        if (!ValidateReferences())
        {
            return;
        }

        smoothResetRequested = false;
        panVelocity = Vector2.zero;
        contentRoot.anchoredPosition = Vector2.zero;
    }

    public void ResetFocusSmooth()
    {
        if (!ValidateReferences())
        {
            return;
        }

        smoothResetRequested = true;
    }

    private void ApplyZoom()
    {
        if (contentRoot == null)
        {
            return;
        }

        float safeZoom = ClampZoomScale(zoomScale);
        zoomScale = safeZoom;
        contentRoot.localScale = defaultLocalScale * safeZoom;
    }

    private void HandleCtrlScrollZoomInput()
    {
        if (!enableCtrlScrollZoom || !effectEnabled)
        {
            return;
        }

        if (suppressWhilePopupOpen && isSuppressed)
        {
            return;
        }

        if (ignoreOutOfBoundsMouse && IsOutOfBounds(Input.mousePosition))
        {
            return;
        }

        bool isCtrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (!isCtrlHeld)
        {
            return;
        }

        float scrollDelta = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scrollDelta) <= Mathf.Epsilon)
        {
            return;
        }

        float newZoom = ClampZoomScale(zoomScale + (scrollDelta * Mathf.Max(0.001f, ctrlScrollZoomStep)));
        if (Mathf.Approximately(newZoom, zoomScale))
        {
            return;
        }

        SetZoomScale(newZoom, false);

        // Keep current pan, but clamp to new zoom limits after changing scale.
        Vector2 maxPan = CalculateMaxPan();
        Vector2 clampedPan = contentRoot.anchoredPosition;
        clampedPan.x = Mathf.Clamp(clampedPan.x, -maxPan.x, maxPan.x);
        clampedPan.y = Mathf.Clamp(clampedPan.y, -maxPan.y, maxPan.y);
        contentRoot.anchoredPosition = clampedPan;

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(CallPhaseScreenFocus)}: zoomScale = {zoomScale:0.###}", this);
        }
    }

    private Vector2 CalculateMaxPan()
    {
        float zoomX = GetCurrentZoomAxis(contentRoot.localScale.x, defaultLocalScale.x);
        float zoomY = GetCurrentZoomAxis(contentRoot.localScale.y, defaultLocalScale.y);
        float maxOffsetX = viewportRoot.rect.width * (zoomX - 1f) * 0.5f;
        float maxOffsetY = viewportRoot.rect.height * (zoomY - 1f) * 0.5f;
        return new Vector2(Mathf.Max(0f, maxOffsetX), Mathf.Max(0f, maxOffsetY));
    }

    private static float GetCurrentZoomAxis(float currentScale, float defaultScale)
    {
        if (Mathf.Approximately(defaultScale, 0f))
        {
            return 1f;
        }

        return Mathf.Max(1f, currentScale / defaultScale);
    }

    private float ClampZoomScale(float value)
    {
        float safeMin = Mathf.Max(1f, minZoomScale);
        float safeMax = Mathf.Max(safeMin, maxZoomScale);
        return Mathf.Clamp(value, safeMin, safeMax);
    }

    private Vector2 CalculateEdgeInfluence(Vector3 mousePosition)
    {
        float width = Mathf.Max(1f, Screen.width);
        float height = Mathf.Max(1f, Screen.height);

        float threshold = Mathf.Clamp(edgeThresholdPercent, 0.001f, 0.49f);
        float thresholdX = width * threshold;
        float thresholdY = height * threshold;

        float influenceX = 0f;
        if (mousePosition.x <= thresholdX)
        {
            float t = Mathf.Clamp01(mousePosition.x / thresholdX);
            influenceX = -(1f - t);
        }
        else if (mousePosition.x >= width - thresholdX)
        {
            float t = Mathf.Clamp01((mousePosition.x - (width - thresholdX)) / thresholdX);
            influenceX = t;
        }

        float influenceY = 0f;
        if (mousePosition.y <= thresholdY)
        {
            float t = Mathf.Clamp01(mousePosition.y / thresholdY);
            influenceY = -(1f - t);
        }
        else if (mousePosition.y >= height - thresholdY)
        {
            float t = Mathf.Clamp01((mousePosition.y - (height - thresholdY)) / thresholdY);
            influenceY = t;
        }

        return new Vector2(Mathf.Clamp(influenceX, -1f, 1f), Mathf.Clamp(influenceY, -1f, 1f));
    }

    private bool ValidateReferences()
    {
        if (viewportRoot != null && contentRoot != null)
        {
            return true;
        }

        if (enableDebugLogs)
        {
            Debug.LogWarning($"{nameof(CallPhaseScreenFocus)} on {name}: Missing viewportRoot or contentRoot reference.", this);
        }

        return false;
    }

    private static bool IsOutOfBounds(Vector3 mousePosition)
    {
        return mousePosition.x < 0f ||
               mousePosition.y < 0f ||
               mousePosition.x > Screen.width ||
               mousePosition.y > Screen.height;
    }
}
