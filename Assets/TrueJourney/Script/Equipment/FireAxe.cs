using TrueJourney.BotBehavior;
using UnityEngine;

public class FireAxe : MonoBehaviour, IInteractable, IPickupable, IUsable, IBotBreakTool
{
    private Rigidbody cachedRigidbody;
    public Rigidbody Rigidbody => cachedRigidbody;

    [Header("Attack")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private float botUseRange = 1.75f;
    [SerializeField] private float botUseCooldown = 0.6f;

    [Header("Stamina")]
    [SerializeField] private float staminaCost = 10f;

    [Header("Runtime (Debug)")]
    [SerializeField] private GameObject currentHolder;
    [SerializeField] private GameObject claimOwner;

    public float PreferredBreakDistance => Mathf.Max(0.5f, botUseRange * 0.8f);
    public float MaxBreakDistance => Mathf.Max(0.5f, botUseRange);
    public float UseCooldown => Mathf.Max(0.05f, botUseCooldown);
    public bool IsHeld => currentHolder != null;
    public bool IsHeldBy(GameObject requester) => requester != null && currentHolder == requester;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        BotRuntimeRegistry.RegisterBreakTool(this);
    }

    private void OnDisable()
    {
        BotRuntimeRegistry.UnregisterBreakTool(this);
    }

    public void Interact(GameObject interactor)
    {
    }

    public void OnPickup(GameObject picker)
    {
        currentHolder = picker;
        claimOwner = picker;
    }

    public void OnDrop(GameObject dropper)
    {
        currentHolder = null;
        if (claimOwner == dropper)
        {
            claimOwner = null;
        }
    }

    public bool IsAvailableTo(GameObject requester)
    {
        if (requester == null)
        {
            return false;
        }

        return claimOwner == null || claimOwner == requester || currentHolder == requester;
    }

    public bool TryClaim(GameObject requester)
    {
        if (!IsAvailableTo(requester))
        {
            return false;
        }

        claimOwner = requester;
        return true;
    }

    public void ReleaseClaim(GameObject requester)
    {
        if (requester != null && claimOwner == requester && currentHolder != requester)
        {
            claimOwner = null;
        }
    }

    public void Use(GameObject user)
    {
        if (!TryConsumeStamina(user))
        {
            Debug.LogWarning("Not enough stamina to use FireAxe.");
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
        RaycastHit hit;
        bool hitSomething = Physics.Raycast(ray, out hit, range, hitMask, QueryTriggerInteraction.Ignore);

        if (!hitSomething)
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

    public void UseOnTarget(GameObject user, IBotBreakableTarget target)
    {
        if (target == null || target.IsBroken || !target.CanBeClearedByBot || damage <= 0f)
        {
            return;
        }

        Vector3 targetPosition = target.GetWorldPosition();
        Vector3 hitNormal = (targetPosition - transform.position).normalized;
        if (hitNormal.sqrMagnitude <= 0.001f)
        {
            hitNormal = transform.forward;
        }

        GameObject source = user != null ? user : gameObject;
        target.TakeBreakDamage(damage, source, targetPosition, hitNormal);
    }

    private static bool CanDamageTarget(IDamageable damageable)
    {
        if (damageable is Breakable breakable)
        {
            return breakable.Type == Breakable.BreakableType.Wood;
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
