using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif


namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class FPSInteractionSystem : MonoBehaviour
    {
        [Header("Raycast")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float interactDistance = 3f;
        [SerializeField] private LayerMask interactMask = ~0;
        [SerializeField] private bool drawDebugRay;
        [SerializeField] private bool enableOutlineHighlight = true;
        [SerializeField, Range(0, 31)] private int outlineRenderingLayer = 6;

        // Input
        [SerializeField] private StarterAssetsInputs input;

        [Header("Grab")]
        [SerializeField] private LayerMask grabMask = ~0;
        [SerializeField] private float grabDistance = 3f;
        [SerializeField] private float maxGrabMass = 50f;
        [SerializeField] private Transform grabPoint;
        [SerializeField] private Vector3 grabPointLocalPosition = Vector3.zero;

        [Header("References")]
        [SerializeField] private FPSInventorySystem inventory;

        private IInteractable currentInteractable;
        [SerializeField] private GameObject currentTarget;

        public GameObject CurrentTarget => currentTarget;
        public float InteractDistance => interactDistance;
        public bool IsGrabActive => grabbedBody != null;

        private Rigidbody grabbedBody;
        private Transform grabbedOriginalParent;
        private bool grabbedOriginalIsKinematic;
        private bool grabbedOriginalDetectCollisions;
        private bool previousInteract;
        private bool previousPickup;
        private bool previousUse;
        private bool previousDrop;
        private bool previousGrab;
        private GameObject outlinedTargetRoot;
        private Renderer[] outlinedRenderers;
        private bool[] outlinedRendererHadBit;

        private void Awake()
        {
            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }

            if (inventory == null)
            {
                inventory = GetComponent<FPSInventorySystem>();
            }

            if (input == null)
            {
                input = GetComponent<StarterAssetsInputs>();
            }

            grabDistance = interactDistance;
            ResolveGrabPoint();
        }

        private void Update()
        {
            if (viewCamera == null)
            {
                return;
            }

            if (IsGrabActive)
            {
                ClearFocus();
            }
            else
            {
                UpdateFocus();
            }

            bool currentGrab = input != null && input.grab;
            bool grabPressed = WasPressed(currentGrab, ref previousGrab);

            if (grabPressed)
            {
                ToggleGrab();
            }

            if (IsGrabActive)
            {
                BlockGameplayActionsWhileGrabbed();
                return;
            }

            if (WasPressed(input != null && input.pickup, ref previousPickup) && inventory != null)
            {
                if (currentTarget != null)
                {
                    inventory.TryPickup(currentTarget, gameObject);
                    return;
                }
            }

            if (WasPressed(input != null && input.interact, ref previousInteract))
            {
                if (currentInteractable != null)
                {
                    currentInteractable.Interact(gameObject);
                }
            }

            if (WasPressed(input != null && input.use, ref previousUse) && inventory != null && inventory.HasItem)
            {
                inventory.UseHeld(gameObject);
            }

            if (WasPressed(input != null && input.drop, ref previousDrop) && inventory != null && inventory.HasItem)
            {
                inventory.Drop(gameObject);
            }

            if (inventory != null && inventory.ItemCount > 0 && input != null && input.slot >= 0)
            {
                int maxSelectable = Mathf.Min(6, inventory.MaxSlots);
                int slotIndex = Mathf.Clamp(input.slot, 0, maxSelectable - 1);
                inventory.TrySelectSlot(slotIndex);
                input.slot = -1;
            }
        }

        private void UpdateFocus()
        {
            Ray ray = viewCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask, QueryTriggerInteraction.Ignore))
            {
                IInteractable interactable = FindInteractable(hit.collider);

                if (interactable != null)
                {
                    SetFocus(hit.collider.gameObject, interactable);
                    if (drawDebugRay)
                    {
                        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.green);
                    }
                    return;
                }
            }

            ClearFocus();

            if (drawDebugRay)
            {
                Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.red);
            }
        }

        private static IInteractable FindInteractable(Collider collider)
        {
            if (collider.TryGetComponent(out IInteractable direct))
            {
                return direct;
            }

            if (collider.attachedRigidbody != null &&
                collider.attachedRigidbody.TryGetComponent(out IInteractable rigidbodyOwner))
            {
                return rigidbodyOwner;
            }

            Transform parent = collider.transform.parent;
            while (parent != null)
            {
                if (parent.TryGetComponent(out IInteractable parentInteractable))
                {
                    return parentInteractable;
                }
                parent = parent.parent;
            }

            return null;
        }

        private static IGrabbable FindGrabbable(Collider collider)
        {
            if (collider.TryGetComponent(out IGrabbable direct))
            {
                return direct;
            }

            if (collider.attachedRigidbody != null &&
                collider.attachedRigidbody.TryGetComponent(out IGrabbable rigidbodyOwner))
            {
                return rigidbodyOwner;
            }

            Transform parent = collider.transform.parent;
            while (parent != null)
            {
                if (parent.TryGetComponent(out IGrabbable parentGrabbable))
                {
                    return parentGrabbable;
                }
                parent = parent.parent;
            }

            return null;
        }

        private void SetFocus(GameObject target, IInteractable interactable)
        {
            if (currentTarget == target && currentInteractable == interactable)
            {
                return;
            }

            currentTarget = target;
            currentInteractable = interactable;
            UpdateOutlineHighlight(target, interactable);
        }

        private void ClearFocus()
        {
            if (currentTarget == null && currentInteractable == null)
            {
                return;
            }

            currentTarget = null;
            currentInteractable = null;
            ClearOutlineHighlight();
        }

        private void TryGrab()
        {
            if (grabbedBody != null || viewCamera == null)
            {
                return;
            }

            if (inventory != null && inventory.HasItem)
            {
                return;
            }

            ResolveGrabPoint();
            if (grabPoint == null)
            {
                return;
            }

            Ray ray = viewCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, grabDistance, grabMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            if (FindGrabbable(hit.collider) == null)
            {
                return;
            }

            Rigidbody targetBody = hit.rigidbody;
            if (targetBody == null || targetBody.isKinematic)
            {
                return;
            }

            if (targetBody.mass > maxGrabMass)
            {
                return;
            }

            grabbedBody = targetBody;
            grabbedOriginalParent = grabbedBody.transform.parent;
            grabbedOriginalIsKinematic = grabbedBody.isKinematic;
            grabbedOriginalDetectCollisions = grabbedBody.detectCollisions;

            grabbedBody.linearVelocity = Vector3.zero;
            grabbedBody.angularVelocity = Vector3.zero;
            grabbedBody.isKinematic = true;
            grabbedBody.detectCollisions = false;

            Transform grabbedTransform = grabbedBody.transform;
            grabbedTransform.SetParent(grabPoint, false);
            grabbedTransform.localPosition = Vector3.zero;
            grabbedTransform.localRotation = Quaternion.identity;
        }

        private void ToggleGrab()
        {
            if (IsGrabActive)
            {
                ReleaseGrab();
                return;
            }

            TryGrab();
        }

        private void ReleaseGrab()
        {
            if (grabbedBody != null)
            {
                Transform grabbedTransform = grabbedBody.transform;
                grabbedTransform.SetParent(grabbedOriginalParent, true);

                grabbedBody.isKinematic = grabbedOriginalIsKinematic;
                grabbedBody.detectCollisions = grabbedOriginalDetectCollisions;
                grabbedBody.linearVelocity = Vector3.zero;
                grabbedBody.angularVelocity = Vector3.zero;
            }

            grabbedBody = null;
            grabbedOriginalParent = null;
        }

        private void ResolveGrabPoint()
        {
            if (grabPoint != null)
            {
                return;
            }

            grabPoint = FindChildTransformByName("GrabPoint");
            if (grabPoint != null || viewCamera == null)
            {
                ApplyGrabPointPosition();
                return;
            }

            GameObject runtimeGrabPoint = new GameObject("RuntimeGrabPoint");
            grabPoint = runtimeGrabPoint.transform;
            grabPoint.SetParent(viewCamera.transform, false);
            ApplyGrabPointPosition();
            grabPoint.localRotation = Quaternion.identity;
        }

        private Transform FindChildTransformByName(string childName)
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null && candidate.name == childName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void ApplyGrabPointPosition()
        {
            if (grabPoint == null)
            {
                return;
            }

            grabPoint.localPosition = grabPointLocalPosition;
        }

        private void BlockGameplayActionsWhileGrabbed()
        {
            if (input != null)
            {
                input.ClearGameplayActionInputs();
            }

            previousInteract = false;
            previousPickup = false;
            previousUse = false;
            previousDrop = false;
        }

        private static bool WasPressed(bool current, ref bool previous)
        {
            bool pressed = current && !previous;
            previous = current;
            return pressed;
        }

        private void OnDisable()
        {
            ReleaseGrab();
            ClearOutlineHighlight();
        }

        private void UpdateOutlineHighlight(GameObject target, IInteractable interactable)
        {
            if (!enableOutlineHighlight)
            {
                ClearOutlineHighlight();
                return;
            }

            GameObject highlightRoot = ResolveHighlightRoot(target, interactable);
            if (highlightRoot == outlinedTargetRoot)
            {
                return;
            }

            ClearOutlineHighlight();
            if (highlightRoot == null)
            {
                return;
            }

            outlinedTargetRoot = highlightRoot;
            outlinedRenderers = highlightRoot.GetComponentsInChildren<Renderer>(true);
            outlinedRendererHadBit = new bool[outlinedRenderers.Length];

            uint outlineBit = 1u << outlineRenderingLayer;
            for (int i = 0; i < outlinedRenderers.Length; i++)
            {
                Renderer renderer = outlinedRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                outlinedRendererHadBit[i] = (renderer.renderingLayerMask & outlineBit) != 0;
                renderer.renderingLayerMask |= outlineBit;
            }
        }

        private void ClearOutlineHighlight()
        {
            if (outlinedRenderers != null && outlinedRendererHadBit != null)
            {
                uint outlineBit = 1u << outlineRenderingLayer;
                int count = Mathf.Min(outlinedRenderers.Length, outlinedRendererHadBit.Length);
                for (int i = 0; i < count; i++)
                {
                    Renderer renderer = outlinedRenderers[i];
                    if (renderer == null || outlinedRendererHadBit[i])
                    {
                        continue;
                    }

                    renderer.renderingLayerMask &= ~outlineBit;
                }
            }

            outlinedTargetRoot = null;
            outlinedRenderers = null;
            outlinedRendererHadBit = null;
        }

        private static GameObject ResolveHighlightRoot(GameObject target, IInteractable interactable)
        {
            if (interactable is Component interactableComponent)
            {
                return interactableComponent.gameObject;
            }

            return target;
        }
    }
}
