using UnityEngine;
using UnityEngine.EventSystems;

/*
Usage:
- Attach this script to the draggable header/title bar.
- Assign windowRoot to the full panel RectTransform.
- Assign boundsRoot to the monitor/screen RectTransform that defines drag limits.
- Assign parentCanvas if needed.
*/
[DisallowMultipleComponent]
public class UIDraggableWindow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    [SerializeField] private RectTransform windowRoot;
    [SerializeField] private RectTransform boundsRoot;
    [SerializeField] private Canvas parentCanvas;

    [Header("Behavior")]
    [SerializeField] private bool bringToFrontOnDrag = true;
    [SerializeField] private bool enableDebugLogs = false;

    private Vector2 dragOffsetInBounds;
    private readonly Vector3[] worldCorners = new Vector3[4];

    private void Awake()
    {
        if (parentCanvas == null)
        {
            parentCanvas = GetComponentInParent<Canvas>();
        }

        ValidateReferences(logWarning: true);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!ValidateReferences(logWarning: false))
        {
            return;
        }

        if (!TryGetPointerLocalInBounds(eventData, out Vector2 pointerLocalInBounds))
        {
            return;
        }

        Vector2 windowPivotInBounds = GetWindowPivotInBounds();
        dragOffsetInBounds = windowPivotInBounds - pointerLocalInBounds;

        if (bringToFrontOnDrag)
        {
            windowRoot.SetAsLastSibling();
        }

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(UIDraggableWindow)}: Begin drag on {windowRoot.name}", this);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!ValidateReferences(logWarning: false))
        {
            return;
        }

        if (!TryGetPointerLocalInBounds(eventData, out Vector2 pointerLocalInBounds))
        {
            return;
        }

        Vector2 desiredPivotInBounds = pointerLocalInBounds + dragOffsetInBounds;
        Vector2 clampedPivotInBounds = ClampPivotInsideBounds(desiredPivotInBounds);

        if (!TryApplyAnchoredPosition(clampedPivotInBounds))
        {
            return;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(UIDraggableWindow)}: Dragged {windowRoot.name} to {windowRoot.anchoredPosition}", this);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
    }

    private bool ValidateReferences(bool logWarning)
    {
        if (windowRoot == null || boundsRoot == null)
        {
            if (logWarning)
            {
                Debug.LogWarning($"{nameof(UIDraggableWindow)} on {name}: Missing windowRoot or boundsRoot.", this);
            }

            return false;
        }

        if (windowRoot.parent is not RectTransform)
        {
            if (logWarning)
            {
                Debug.LogWarning($"{nameof(UIDraggableWindow)} on {name}: windowRoot must have a RectTransform parent.", this);
            }

            return false;
        }

        if (parentCanvas == null && logWarning)
        {
            Debug.LogWarning($"{nameof(UIDraggableWindow)} on {name}: parentCanvas is not assigned and was not found in parents.", this);
        }

        return true;
    }

    private bool TryGetPointerLocalInBounds(PointerEventData eventData, out Vector2 localPoint)
    {
        Camera eventCamera = GetEventCamera(eventData);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            boundsRoot,
            eventData.position,
            eventCamera,
            out localPoint);
    }

    private Camera GetEventCamera(PointerEventData eventData)
    {
        if (eventData.pressEventCamera != null)
        {
            return eventData.pressEventCamera;
        }

        if (eventData.enterEventCamera != null)
        {
            return eventData.enterEventCamera;
        }

        if (parentCanvas == null || parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return parentCanvas.worldCamera;
    }

    private Vector2 GetWindowPivotInBounds()
    {
        return boundsRoot.InverseTransformPoint(windowRoot.position);
    }

    private Vector2 ClampPivotInsideBounds(Vector2 desiredPivotInBounds)
    {
        windowRoot.GetWorldCorners(worldCorners);

        Vector2 currentPivotInBounds = GetWindowPivotInBounds();
        Rect boundsRect = boundsRoot.rect;

        float minDeltaX = float.PositiveInfinity;
        float maxDeltaX = float.NegativeInfinity;
        float minDeltaY = float.PositiveInfinity;
        float maxDeltaY = float.NegativeInfinity;

        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector2 cornerInBounds = boundsRoot.InverseTransformPoint(worldCorners[i]);
            Vector2 delta = cornerInBounds - currentPivotInBounds;

            minDeltaX = Mathf.Min(minDeltaX, delta.x);
            maxDeltaX = Mathf.Max(maxDeltaX, delta.x);
            minDeltaY = Mathf.Min(minDeltaY, delta.y);
            maxDeltaY = Mathf.Max(maxDeltaY, delta.y);
        }

        float minPivotX = boundsRect.xMin - minDeltaX;
        float maxPivotX = boundsRect.xMax - maxDeltaX;
        float minPivotY = boundsRect.yMin - minDeltaY;
        float maxPivotY = boundsRect.yMax - maxDeltaY;

        return new Vector2(
            ClampWithFallback(desiredPivotInBounds.x, minPivotX, maxPivotX),
            ClampWithFallback(desiredPivotInBounds.y, minPivotY, maxPivotY));
    }

    private bool TryApplyAnchoredPosition(Vector2 pivotInBounds)
    {
        if (windowRoot.parent is not RectTransform parentRect)
        {
            return false;
        }

        Vector3 pivotWorldPosition = boundsRoot.TransformPoint(pivotInBounds);
        Vector2 pivotInParent = parentRect.InverseTransformPoint(pivotWorldPosition);
        windowRoot.anchoredPosition = ConvertLocalPointToAnchoredPosition(windowRoot, parentRect, pivotInParent);
        return true;
    }

    private static Vector2 ConvertLocalPointToAnchoredPosition(RectTransform rect, RectTransform parentRect, Vector2 localPoint)
    {
        Vector2 anchorReference = new Vector2(
            Mathf.Lerp(parentRect.rect.xMin, parentRect.rect.xMax, Mathf.Lerp(rect.anchorMin.x, rect.anchorMax.x, rect.pivot.x)),
            Mathf.Lerp(parentRect.rect.yMin, parentRect.rect.yMax, Mathf.Lerp(rect.anchorMin.y, rect.anchorMax.y, rect.pivot.y)));

        return localPoint - anchorReference;
    }

    private static float ClampWithFallback(float value, float min, float max)
    {
        if (min <= max)
        {
            return Mathf.Clamp(value, min, max);
        }

        return (min + max) * 0.5f;
    }
}
