using UnityEngine;
using StarterAssets;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class MinimapZoomController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera minimapCamera;
    [SerializeField] private FullscreenMinimapController fullscreenMinimapController;
    [SerializeField] private StarterAssetsInputs starterAssetsInputs;
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private PlayerInput playerInput;
#endif

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

        bool zoomInPressed = WasZoomInPressed();
        bool zoomOutPressed = WasZoomOutPressed();
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

        if (starterAssetsInputs == null)
        {
            starterAssetsInputs = FindAnyObjectByType<StarterAssetsInputs>();
        }

#if ENABLE_INPUT_SYSTEM
        if (playerInput == null && starterAssetsInputs != null)
        {
            playerInput = starterAssetsInputs.GetComponent<PlayerInput>();
        }

        if (playerInput == null)
        {
            playerInput = FindAnyObjectByType<PlayerInput>();
        }
#endif

        if (minimapCamera == null)
        {
            GameObject minimapCameraObject = GameObject.Find(minimapCameraObjectName);
            if (minimapCameraObject != null)
            {
                minimapCamera = minimapCameraObject.GetComponent<Camera>();
            }
        }
    }

    private bool WasZoomInPressed()
    {
        if (starterAssetsInputs != null && starterAssetsInputs.minimapZoomIn)
        {
            starterAssetsInputs.minimapZoomIn = false;
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        if (TryGetPlayerAction("MinimapZoomIn", out InputAction action) && action.WasPressedThisFrame())
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(zoomInKey);
#else
        return false;
#endif
    }

    private bool WasZoomOutPressed()
    {
        if (starterAssetsInputs != null && starterAssetsInputs.minimapZoomOut)
        {
            starterAssetsInputs.minimapZoomOut = false;
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        if (TryGetPlayerAction("MinimapZoomOut", out InputAction action) && action.WasPressedThisFrame())
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(zoomOutKey);
#else
        return false;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private bool TryGetPlayerAction(string actionName, out InputAction action)
    {
        action = null;
        if (playerInput == null || playerInput.actions == null)
        {
            return false;
        }

        action = playerInput.actions.FindAction(actionName, throwIfNotFound: false);
        return action != null;
    }
#endif
}
