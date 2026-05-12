using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [DisallowMultipleComponent]
    public class PlayerFlashlightController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FirstPersonController firstPersonController;
        [SerializeField] private StarterAssetsInputs starterAssetsInputs;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private Transform flashlightAnchor;
        [SerializeField] private Light flashlightLight;

        [Header("Input")]
        [SerializeField] private string toggleActionName = "ToggleFlashlight";
        [SerializeField] private KeyCode fallbackToggleKey = KeyCode.R;

        [Header("Default State")]
        [SerializeField] private bool startsEnabled;

        [Header("Light")]
        [SerializeField] private Color lightColor = Color.white;
        [SerializeField] private float intensity = 3.2f;
        [SerializeField] private float range = 28f;
        [SerializeField] private float spotAngle = 72f;
        [SerializeField] private float innerSpotAngle = 54f;
        [SerializeField] private LightShadows shadows = LightShadows.Soft;
        [SerializeField] private Vector3 localPosition = new Vector3(0.08f, -0.06f, 0.18f);
        [SerializeField] private Vector3 localEulerAngles = Vector3.zero;

        [Header("Rotation Lag")]
        [SerializeField] private bool enableRotationLag = true;
        [SerializeField, Min(0.01f)] private float rotationLagFollowSpeed = 12f;
        [SerializeField, Range(0f, 45f)] private float rotationLagMaxAngle = 10f;

        private bool isFlashlightEnabled;
        private Quaternion defaultLocalRotation = Quaternion.identity;
        private Quaternion currentWorldRotation = Quaternion.identity;

        public bool IsFlashlightEnabled => isFlashlightEnabled;

        private void Awake()
        {
            ResolveReferences();
            EnsureFlashlightExists();
            ApplyFlashlightState(startsEnabled);
        }

        private void Update()
        {
            if (!WasToggleRequested())
            {
                return;
            }

            ToggleFlashlight();
        }

        private void LateUpdate()
        {
            UpdateRotationLag();
        }

        private void ResolveReferences()
        {
            if (firstPersonController == null)
            {
                firstPersonController = GetComponent<FirstPersonController>();
            }

            if (starterAssetsInputs == null)
            {
                starterAssetsInputs = GetComponent<StarterAssetsInputs>();
            }

#if ENABLE_INPUT_SYSTEM
            if (playerInput == null)
            {
                playerInput = GetComponent<PlayerInput>();
            }
#endif

            if (flashlightAnchor == null && firstPersonController != null && firstPersonController.CinemachineCameraTarget != null)
            {
                flashlightAnchor = firstPersonController.CinemachineCameraTarget.transform;
            }

            if (flashlightAnchor == null && Camera.main != null)
            {
                flashlightAnchor = Camera.main.transform;
            }
        }

        private void EnsureFlashlightExists()
        {
            if (flashlightLight != null)
            {
                ConfigureFlashlight(flashlightLight);
                return;
            }

            if (flashlightAnchor == null)
            {
                return;
            }

            Transform existingChild = flashlightAnchor.Find("PlayerFlashlight");
            if (existingChild != null)
            {
                flashlightLight = existingChild.GetComponent<Light>();
            }

            if (flashlightLight == null)
            {
                GameObject flashlightObject = new GameObject("PlayerFlashlight");
                Transform flashlightTransform = flashlightObject.transform;
                flashlightTransform.SetParent(flashlightAnchor, false);
                flashlightLight = flashlightObject.AddComponent<Light>();
            }

            ConfigureFlashlight(flashlightLight);
        }

        private void ConfigureFlashlight(Light targetLight)
        {
            if (targetLight == null)
            {
                return;
            }

            targetLight.transform.localPosition = localPosition;
            defaultLocalRotation = Quaternion.Euler(localEulerAngles);
            targetLight.transform.localRotation = defaultLocalRotation;
            targetLight.type = LightType.Spot;
            targetLight.color = lightColor;
            targetLight.intensity = Mathf.Max(0f, intensity);
            targetLight.range = Mathf.Max(0.1f, range);
            targetLight.spotAngle = Mathf.Clamp(spotAngle, 1f, 179f);
            targetLight.innerSpotAngle = Mathf.Clamp(innerSpotAngle, 0f, targetLight.spotAngle);
            targetLight.shadows = shadows;
            targetLight.renderMode = LightRenderMode.ForcePixel;

            if (flashlightAnchor != null)
            {
                currentWorldRotation = flashlightAnchor.rotation * defaultLocalRotation;
            }
            else
            {
                currentWorldRotation = targetLight.transform.rotation;
            }
        }

        private void UpdateRotationLag()
        {
            if (flashlightLight == null || flashlightAnchor == null)
            {
                return;
            }

            if (!enableRotationLag)
            {
                flashlightLight.transform.localRotation = defaultLocalRotation;
                currentWorldRotation = flashlightAnchor.rotation * defaultLocalRotation;
                return;
            }

            Quaternion targetWorldRotation = flashlightAnchor.rotation * defaultLocalRotation;
            float interpolation = 1f - Mathf.Exp(-rotationLagFollowSpeed * Time.deltaTime);
            currentWorldRotation = Quaternion.Slerp(currentWorldRotation, targetWorldRotation, interpolation);

            float angle = Quaternion.Angle(targetWorldRotation, currentWorldRotation);
            if (angle > rotationLagMaxAngle && angle > 0.001f)
            {
                currentWorldRotation = Quaternion.Slerp(targetWorldRotation, currentWorldRotation, rotationLagMaxAngle / angle);
            }

            flashlightLight.transform.localRotation = Quaternion.Inverse(flashlightAnchor.rotation) * currentWorldRotation;
        }

        private bool WasToggleRequested()
        {
            if (starterAssetsInputs != null && starterAssetsInputs.flashlightToggle)
            {
                return true;
            }

#if ENABLE_INPUT_SYSTEM
            if (playerInput != null && playerInput.actions != null)
            {
                InputAction action = playerInput.actions.FindAction(toggleActionName, throwIfNotFound: false);
                if (action != null && action.WasPressedThisFrame())
                {
                    return true;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(fallbackToggleKey);
#else
            return false;
#endif
        }

        public void ToggleFlashlight()
        {
            ApplyFlashlightState(!isFlashlightEnabled);
        }

        public void SetFlashlightEnabled(bool enabled)
        {
            ApplyFlashlightState(enabled);
        }

        private void ApplyFlashlightState(bool enabled)
        {
            isFlashlightEnabled = enabled;

            if (flashlightLight == null)
            {
                EnsureFlashlightExists();
            }

            if (flashlightLight != null)
            {
                flashlightLight.enabled = enabled;
            }
        }

        private void OnValidate()
        {
            intensity = Mathf.Max(0f, intensity);
            range = Mathf.Max(0.1f, range);
            spotAngle = Mathf.Clamp(spotAngle, 1f, 179f);
            innerSpotAngle = Mathf.Clamp(innerSpotAngle, 0f, spotAngle);
            rotationLagFollowSpeed = Mathf.Max(0.01f, rotationLagFollowSpeed);
            rotationLagMaxAngle = Mathf.Clamp(rotationLagMaxAngle, 0f, 45f);

            if (flashlightLight != null)
            {
                ConfigureFlashlight(flashlightLight);
                flashlightLight.enabled = startsEnabled;
            }
        }
    }
}
