using UnityEngine;

public class FPSInteractionSystem : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactMask = ~0;
    [SerializeField] private bool drawDebugRay;

    [Header("Input")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private KeyCode pickupKey = KeyCode.F;
    [SerializeField] private KeyCode useKey = KeyCode.R;

    [Header("References")]
    [SerializeField] private FPSInventorySystem inventory;

    private IInteractable currentInteractable;
    [SerializeField] private GameObject currentTarget;

    public GameObject CurrentTarget => currentTarget;

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
    }

    private void Update()
    {
        if (viewCamera == null)
        {
            return;
        }

        UpdateFocus();

        if (Input.GetKeyDown(pickupKey) && inventory != null)
        {
            if (currentTarget != null)
            {
                inventory.TryPickup(currentTarget, gameObject);
                return;
            }

            if (inventory.HasItem)
            {
                inventory.Drop(gameObject);
                return;
            }
        }

        if (Input.GetKeyDown(interactKey))
        {
            if (currentInteractable != null)
            {
                currentInteractable.Interact(gameObject);
            }
        }

        if (Input.GetKeyDown(useKey) && inventory != null && inventory.HasItem)
        {
            inventory.UseHeld(gameObject);
        }

        if (inventory != null && inventory.ItemCount > 0)
        {
            int maxSelectable = Mathf.Min(9, inventory.MaxSlots);
            for (int i = 0; i < maxSelectable; i++)
            {
                KeyCode key = (KeyCode)((int)KeyCode.Alpha1 + i);
                if (Input.GetKeyDown(key))
                {
                    inventory.TrySelectSlot(i);
                    break;
                }
            }
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
        if (parent != null && parent.TryGetComponent(out IInteractable parentInteractable))
        {
            return parentInteractable;
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
}
