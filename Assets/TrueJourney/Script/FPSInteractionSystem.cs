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

        // Input
        [SerializeField] private StarterAssetsInputs input;

        [Header("Physics Grab")]
        [SerializeField] private LayerMask grabMask = ~0;
        [SerializeField] private float grabDistance = 4f;
        [SerializeField] private float holdDistance = 2.2f;
        [SerializeField] private float maxGrabMass = 50f;
        [SerializeField] private float grabSpring = 600f;
        [SerializeField] private float grabDamper = 50f;
        [SerializeField] private float heldDrag = 10f;
        [SerializeField] private float heldAngularDrag = 10f;

        [Header("References")]
        [SerializeField] private FPSInventorySystem inventory;

        private IInteractable currentInteractable;
        [SerializeField] private GameObject currentTarget;

        public GameObject CurrentTarget => currentTarget;

        private Rigidbody grabbedBody;
        private SpringJoint grabJoint;
        private float grabbedOriginalDrag;
        private float grabbedOriginalAngularDrag;
        private bool previousInteract;
        private bool previousPickup;
        private bool previousUse;
        private bool previousDrop;
        private bool previousGrab;

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
        }

        private void Update()
        {
            if (viewCamera == null)
            {
                return;
            }

            UpdateFocus();

        bool currentGrab = input != null && input.grab;
        GetButtonState(currentGrab, ref previousGrab, out bool grabPressed, out bool grabReleased);

        if (grabPressed)
        {
            TryGrab();
        }

        if (grabReleased)
        {
            ReleaseGrab();
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

        private void FixedUpdate()
        {
            if (grabJoint == null)
            {
                return;
            }

            grabJoint.connectedAnchor = GetHoldPoint();
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
            if (parent != null && parent.TryGetComponent(out IInteractable parentInteractable))
            {
                return parentInteractable;
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
            if (parent != null && parent.TryGetComponent(out IGrabbable parentGrabbable))
            {
                return parentGrabbable;
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
        }

        private void ClearFocus()
        {
            if (currentTarget == null && currentInteractable == null)
            {
                return;
            }

            currentTarget = null;
            currentInteractable = null;
        }

        private void TryGrab()
        {
            if (grabJoint != null || viewCamera == null)
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
            grabbedOriginalDrag = grabbedBody.linearDamping;
            grabbedOriginalAngularDrag = grabbedBody.angularDamping;
            grabbedBody.linearDamping = heldDrag;
            grabbedBody.angularDamping = heldAngularDrag;

            grabJoint = grabbedBody.gameObject.AddComponent<SpringJoint>();
            grabJoint.autoConfigureConnectedAnchor = false;
            grabJoint.connectedAnchor = GetHoldPoint();
            grabJoint.spring = grabSpring;
            grabJoint.damper = grabDamper;
            grabJoint.minDistance = 0f;
            grabJoint.maxDistance = 0f;
            grabJoint.tolerance = 0f;
        }

        private void ReleaseGrab()
        {
            if (grabbedBody != null)
            {
                grabbedBody.linearDamping = grabbedOriginalDrag;
                grabbedBody.angularDamping = grabbedOriginalAngularDrag;
            }

            if (grabJoint != null)
            {
                Destroy(grabJoint);
            }

            grabJoint = null;
            grabbedBody = null;
        }

        private Vector3 GetHoldPoint()
        {
            return viewCamera.transform.position + viewCamera.transform.forward * holdDistance;
        }

    private static bool WasPressed(bool current, ref bool previous)
    {
        bool pressed = current && !previous;
        previous = current;
        return pressed;
    }

    private static void GetButtonState(bool current, ref bool previous, out bool pressed, out bool released)
    {
        pressed = current && !previous;
        released = !current && previous;
        previous = current;
    }
}
}
