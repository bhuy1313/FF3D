using UnityEngine;

[DisallowMultipleComponent]
public class MinimapDisplayController : MonoBehaviour
{
    [SerializeField] private GameObject minimapSquare;
    [SerializeField] private GameObject minimapCircle;
    [SerializeField] private string minimapSquareObjectName = "MinimapSquare";
    [SerializeField] private string minimapCircleObjectName = "MinimapCircle";

    private void Awake()
    {
        ResolveReferences();
        Apply(MinimapDisplaySettings.GetSavedOrDefaultType());
    }

    private void OnEnable()
    {
        ResolveReferences();
        MinimapDisplayRuntime.DisplayTypeChanged -= HandleDisplayTypeChanged;
        MinimapDisplayRuntime.DisplayTypeChanged += HandleDisplayTypeChanged;
        Apply(MinimapDisplayRuntime.CurrentType);
    }

    private void OnDisable()
    {
        MinimapDisplayRuntime.DisplayTypeChanged -= HandleDisplayTypeChanged;
    }

    private void HandleDisplayTypeChanged(MinimapDisplayType displayType)
    {
        Apply(displayType);
    }

    private void ResolveReferences()
    {
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

    private void Apply(MinimapDisplayType displayType)
    {
        if (minimapSquare != null)
        {
            minimapSquare.SetActive(displayType == MinimapDisplayType.Square);
        }

        if (minimapCircle != null)
        {
            minimapCircle.SetActive(displayType == MinimapDisplayType.Circle);
        }
    }
}
