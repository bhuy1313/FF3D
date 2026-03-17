using UnityEngine;

public class SledgeHammer : MonoBehaviour, IInteractable, IPickupable, IUsable
{
    private Rigidbody cachedRigidbody;
    public Rigidbody Rigidbody => cachedRigidbody;

    [Header("Attack")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Stamina")]
    [SerializeField] private float staminaCost = 10f;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
    }

    public void Interact(GameObject interactor)
    {
    }

    public void OnPickup(GameObject picker)
    {
    }

    public void OnDrop(GameObject dropper)
    {
    }

    public void Use(GameObject user)
    {
        if (!TryConsumeStamina(user))
        {
            Debug.LogWarning("Not enough stamina to use SledgeHammer.");
            return;
        }

        TryDealDamage(user);
    }

    private bool TryConsumeStamina(GameObject user)
    {
        if (staminaCost <= 0f || user == null)
        {
            return true;
        }

        if (!user.TryGetComponent(out PlayerVitals vitals))
        {
            return true;
        }

        return vitals.TryUseStamina(staminaCost);
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
        if (!Physics.Raycast(ray, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        IDamageable damageable = FindDamageable(hit.collider);
        if (damageable == null)
        {
            return;
        }

        if (!CanDamageTarget(damageable))
        {
            return;
        }

        GameObject source = user != null ? user : gameObject;
        damageable.TakeDamage(damage, source, hit.point, hit.normal);
    }

    private static bool CanDamageTarget(IDamageable damageable)
    {
        if (damageable is Breakable breakable)
        {
            return breakable.Type == Breakable.BreakableType.Stone;
        }

        return true;
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
