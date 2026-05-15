using UnityEngine;

[DisallowMultipleComponent]
public class MinimapDisplayController : MonoBehaviour
{
    [SerializeField] private GameObject minimapSquare;
    [SerializeField] private GameObject minimapCircle;
    [SerializeField] private MinimapCompassController minimapCompassController;
    [SerializeField] private string minimapSquareObjectName = "MinimapSquare";
    [SerializeField] private string minimapCircleObjectName = "MinimapCircle";

    private void Awake()
    {
        ResolveReferences();
        Apply(MinimapDisplaySettings.GetSavedOrDefaultType(), MinimapDisplaySettings.GetSavedOrDefaultEnabled());
    }

    private void OnEnable()
    {
        ResolveReferences();
        MinimapDisplayRuntime.DisplayTypeChanged -= HandleDisplayTypeChanged;
        MinimapDisplayRuntime.DisplayTypeChanged += HandleDisplayTypeChanged;
        MinimapDisplayRuntime.EnabledChanged -= HandleEnabledChanged;
        MinimapDisplayRuntime.EnabledChanged += HandleEnabledChanged;
        Apply(MinimapDisplayRuntime.CurrentType, MinimapDisplayRuntime.CurrentEnabled);
    }

    private void OnDisable()
    {
        MinimapDisplayRuntime.DisplayTypeChanged -= HandleDisplayTypeChanged;
        MinimapDisplayRuntime.EnabledChanged -= HandleEnabledChanged;
    }

    private void HandleDisplayTypeChanged(MinimapDisplayType displayType)
    {
        Apply(displayType, MinimapDisplayRuntime.CurrentEnabled);
    }

    private void HandleEnabledChanged(bool enabled)
    {
        Apply(MinimapDisplayRuntime.CurrentType, enabled);
    }

    private void ResolveReferences()
    {
        if (minimapCompassController == null)
        {
            minimapCompassController = GetComponent<MinimapCompassController>();
        }

        if (minimapSquare == null)
        {
            Transform squareTransform = transform.Find(minimapSquareObjectName);
            if (squareTransform != null)
            {
                minimapSquare = squareTransform.gameObject;
            }
        }

        if (minimapCircle == null)
        {
            Transform circleTransform = transform.Find(minimapCircleObjectName);
            if (circleTransform != null)
            {
                minimapCircle = circleTransform.gameObject;
            }
        }
    }

    private void Apply(MinimapDisplayType displayType, bool enabled)
    {
        if (minimapCompassController != null)
        {
            minimapCompassController.enabled = enabled;
        }

        if (minimapSquare != null)
        {
            minimapSquare.SetActive(enabled && displayType == MinimapDisplayType.Square);
        }

        if (minimapCircle != null)
        {
            minimapCircle.SetActive(enabled && displayType == MinimapDisplayType.Circle);
        }
    }
}
