using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class VictimStabilizationKit : Item, IInteractable, IPickupable, IUsable, IMovementWeightSource
{
    [Header("Stabilization Kit")]
    [SerializeField] private float movementWeightKg = 1.5f;
    [SerializeField] private float useRange = 3f;
    [SerializeField] private float stabilizeDurationMultiplier = 0.6f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Runtime")]
    [SerializeField] private GameObject currentHolder;

    private Rigidbody cachedRigidbody;

    public Rigidbody Rigidbody => cachedRigidbody;
    public float MovementWeightKg => Mathf.Max(0f, movementWeightKg);

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
    }

    private void OnValidate()
    {
        movementWeightKg = Mathf.Max(0f, movementWeightKg);
        useRange = Mathf.Max(0.5f, useRange);
        stabilizeDurationMultiplier = Mathf.Max(0.05f, stabilizeDurationMultiplier);
    }

    public void Interact(GameObject interactor)
    {
    }

    public void OnPickup(GameObject picker)
    {
        currentHolder = picker;
    }

    public void OnDrop(GameObject dropper)
    {
        if (currentHolder == dropper)
        {
            currentHolder = null;
        }
    }

    public void Use(GameObject user)
    {
        if (!TryGetUseHit(user, out RaycastHit hit))
        {
            return;
        }

        Rescuable rescuable = FindRescuable(hit.collider);
        if (rescuable == null)
        {
            return;
        }

        rescuable.TryStabilize(user != null ? user : currentHolder, stabilizeDurationMultiplier);
    }

    private bool TryGetUseHit(GameObject user, out RaycastHit hit)
    {
        hit = default;

        Transform aim = GetAimTransform(user);
        if (aim == null)
        {
            return false;
        }

        float range = GetUseRange(user);
        Ray ray = new Ray(aim.position, aim.forward);
        return Physics.Raycast(ray, out hit, range, hitMask, QueryTriggerInteraction.Ignore);
    }

    private float GetUseRange(GameObject user)
    {
        if (user != null && user.TryGetComponent(out StarterAssets.FPSInteractionSystem interaction))
        {
            return interaction.InteractDistance;
        }

        return Mathf.Max(0.5f, useRange);
    }

    private Transform GetAimTransform(GameObject user)
    {
        if (user != null)
        {
            Transform cameraRoot = user.transform.Find("PlayerCameraRoot");
            if (cameraRoot != null)
            {
                return cameraRoot;
            }
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            return cam.transform;
        }

        return transform;
    }

    private static Rescuable FindRescuable(Collider collider)
    {
        if (collider == null)
        {
            return null;
        }

        if (collider.TryGetComponent(out Rescuable direct))
        {
            return direct;
        }

        if (collider.attachedRigidbody != null &&
            collider.attachedRigidbody.TryGetComponent(out Rescuable rigidbodyOwner))
        {
            return rigidbodyOwner;
        }

        Transform parent = collider.transform.parent;
        while (parent != null)
        {
            if (parent.TryGetComponent(out Rescuable parentRescuable))
            {
                return parentRescuable;
            }

            parent = parent.parent;
        }

        return null;
    }
}
