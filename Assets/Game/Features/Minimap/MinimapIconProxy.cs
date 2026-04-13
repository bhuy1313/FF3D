using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class MinimapIconProxy : MonoBehaviour
{
    private const float TopDownPitch = 90f;

    private Transform followTarget;
    private SpriteRenderer spriteRenderer;
    private Sprite iconSprite;
    private Color iconColor = Color.white;
    private float iconScale = 1f;
    private Vector3 worldOffset;
    private bool rotateWithTargetYaw;
    private float yawOffset;
    private bool visibleOnMinimap = true;
    private string sortingLayerName = "Default";
    private int sortingOrder;
    private int iconLayer = -1;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (followTarget == null)
        {
            Destroy(gameObject);
            return;
        }

        ApplyTargetState();
    }

    public void Bind(MinimapIconTarget iconTarget, int resolvedLayer)
    {
        if (iconTarget == null)
        {
            return;
        }

        Bind(
            iconTarget.transform,
            iconTarget.IconSprite,
            iconTarget.IconColor,
            iconTarget.IconScale,
            iconTarget.WorldOffset,
            iconTarget.RotateWithTargetYaw,
            iconTarget.YawOffset,
            iconTarget.VisibleOnMinimap,
            iconTarget.SortingLayerName,
            iconTarget.SortingOrder,
            resolvedLayer);
    }

    public void Bind(
        Transform targetTransform,
        Sprite sprite,
        Color color,
        float scale,
        Vector3 offset,
        bool followYaw,
        float additionalYawOffset,
        bool visible,
        string targetSortingLayerName,
        int targetSortingOrder,
        int resolvedLayer)
    {
        followTarget = targetTransform;
        iconSprite = sprite;
        iconColor = color;
        iconScale = scale;
        worldOffset = offset;
        rotateWithTargetYaw = followYaw;
        yawOffset = additionalYawOffset;
        visibleOnMinimap = visible;
        sortingLayerName = targetSortingLayerName;
        sortingOrder = targetSortingOrder;
        iconLayer = resolvedLayer;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        ApplyTargetState();
    }

    private void ApplyTargetState()
    {
        if (followTarget == null || spriteRenderer == null)
        {
            return;
        }

        transform.position = followTarget.position + worldOffset;

        float yaw = rotateWithTargetYaw ? followTarget.eulerAngles.y : 0f;
        transform.rotation = Quaternion.Euler(TopDownPitch, yaw + yawOffset, 0f);
        transform.localScale = Vector3.one * iconScale;

        spriteRenderer.sprite = iconSprite;
        spriteRenderer.color = iconColor;
        spriteRenderer.enabled = visibleOnMinimap && iconSprite != null;

        if (!string.IsNullOrWhiteSpace(sortingLayerName))
        {
            spriteRenderer.sortingLayerName = sortingLayerName;
        }

        spriteRenderer.sortingOrder = sortingOrder;

        if (iconLayer >= 0)
        {
            gameObject.layer = iconLayer;
        }
    }
}
