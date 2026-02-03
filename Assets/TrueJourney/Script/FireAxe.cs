using UnityEngine;

public class FireAxe : MonoBehaviour, IInteractable, IPickupable, IUsable
{
    private Rigidbody cachedRigidbody;
    public Rigidbody Rigidbody => cachedRigidbody;

    [Header("Attack")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private LayerMask hitMask = ~0;

    void Start()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
    }

    public void Interact(GameObject interactor)
    {
        Debug.Log("FireAxe Interacted!");
    }

    public void OnPickup(GameObject picker)
    {
        Debug.Log("FireAxe Picked Up!");
    }

    public void OnDrop(GameObject dropper)
    {
        Debug.Log("FireAxe Dropped!");
    }

    public void Use(GameObject user)
    {
        Debug.Log("FireAxe Used!");
        Debug.Log("User: " + (user != null ? user.name : "null"));
        TryDealDamage(user);
    }

    private void TryDealDamage(GameObject user)
    {
        float range = GetInteractRange(user);
        if (damage <= 0f || range <= 0f)
        {
            return;
        }

        GameObject playerCameraRoot = GetPlayerCameraRoot(user);

        if (playerCameraRoot == null)
        {
            return;
        }

        Ray ray = new Ray(playerCameraRoot.transform.position, playerCameraRoot.transform.forward);
        RaycastHit hit;
        bool hitSomething = Physics.Raycast(ray, out hit, range, hitMask, QueryTriggerInteraction.Ignore);

        if (!hitSomething)
        {
            Debug.Log("No hit detected.");
            return;
        }

        IDamageable damageable = FindDamageable(hit.collider);
        if (damageable == null)
        {
            return;
        }

        GameObject source = user != null ? user : gameObject;
        damageable.TakeDamage(damage, source, hit.point, hit.normal);
    }

    private static IDamageable FindDamageable(Collider collider)
    {
        if (collider.TryGetComponent(out IDamageable direct))
        {
            return direct;
        }

        if (collider.attachedRigidbody != null &&
            collider.attachedRigidbody.TryGetComponent(out IDamageable rigidbodyOwner))
        {
            return rigidbodyOwner;
        }

        Transform parent = collider.transform.parent;
        if (parent != null && parent.TryGetComponent(out IDamageable parentDamageable))
        {
            return parentDamageable;
        }

        return null;
    }

    private static float GetInteractRange(GameObject user)
    {
        if (user != null && user.TryGetComponent(out StarterAssets.FPSInteractionSystem interaction))
        {
            return interaction.InteractDistance;
        }

        return 2f;
    }

    private static GameObject GetPlayerCameraRoot(GameObject user)
    {
        if (user == null)
        {
            return null;
        }

        return user.transform.Find("PlayerCameraRoot")?.gameObject;
    }
}
