using UnityEngine;

[DisallowMultipleComponent]
public class MinimapIconTarget : MonoBehaviour
{
    [Header("Icon")]
    [SerializeField] private Sprite iconSprite;
    [SerializeField] private Color iconColor = Color.white;
    [SerializeField] [Min(0.1f)] private float iconScale = 1f;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 4f, 0f);

    [Header("Rotation")]
    [SerializeField] private bool rotateWithTargetYaw;
    [SerializeField] private float yawOffset;

    [Header("Render")]
    [SerializeField] private bool visibleOnMinimap = true;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 0;

    public Sprite IconSprite => iconSprite;
    public Color IconColor => iconColor;
    public float IconScale => iconScale;
    public Vector3 WorldOffset => worldOffset;
    public bool RotateWithTargetYaw => rotateWithTargetYaw;
    public float YawOffset => yawOffset;
    public bool VisibleOnMinimap => visibleOnMinimap;
    public string SortingLayerName => sortingLayerName;
    public int SortingOrder => sortingOrder;

    private void OnEnable()
    {
        if (MinimapIconManager.Instance != null)
        {
            MinimapIconManager.Instance.RegisterTarget(this);
        }
    }

    private void OnDisable()
    {
        if (MinimapIconManager.Instance != null)
        {
            MinimapIconManager.Instance.UnregisterTarget(this);
        }
    }

    private void OnDestroy()
    {
        if (MinimapIconManager.Instance != null)
        {
            MinimapIconManager.Instance.UnregisterTarget(this);
        }
    }
}
