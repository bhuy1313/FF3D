using UnityEngine;

[DisallowMultipleComponent]
public class MinimapZoomController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera minimapCamera;
    [SerializeField] private FullscreenMinimapController fullscreenMinimapController;

    [Header("Auto Resolve")]
    [SerializeField] private string minimapCameraObjectName = "MinimapCamera";

    [Header("Input")]
    [SerializeField] private KeyCode zoomInKey = KeyCode.I;
    [SerializeField] private KeyCode zoomOutKey = KeyCode.O;

    [Header("Zoom")]
    [SerializeField] private float zoomStep = 3f;
    [SerializeField] private float minOrthographicSize = 12f;
    [SerializeField] private float maxOrthographicSize = 50f;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();
        if (minimapCamera == null || !minimapCamera.orthographic)
        {
            return;
        }

        if (fullscreenMinimapController != null && fullscreenMinimapController.IsFullscreenOpen)
        {
            return;
        }

        bool zoomInPressed = Input.GetKeyDown(zoomInKey);
        bool zoomOutPressed = Input.GetKeyDown(zoomOutKey);
        if (!zoomInPressed && !zoomOutPressed)
        {
            return;
        }

        float direction = 0f;
        if (zoomInPressed)
        {
            direction -= 1f;
        }

        if (zoomOutPressed)
        {
            direction += 1f;
        }

        float nextSize = minimapCamera.orthographicSize + (direction * zoomStep);
        minimapCamera.orthographicSize = Mathf.Clamp(nextSize, minOrthographicSize, maxOrthographicSize);
    }

    private void ResolveReferences()
    {
        if (fullscreenMinimapController == null)
        {
            fullscreenMinimapController = GetComponent<FullscreenMinimapController>();
        }

        if (minimapCamera == null)
        {
            GameObject minimapCameraObject = GameObject.Find(minimapCameraObjectName);
            if (minimapCameraObject != null)
            {
                minimapCamera = minimapCameraObject.GetComponent<Camera>();
            }
        }
    }
}
