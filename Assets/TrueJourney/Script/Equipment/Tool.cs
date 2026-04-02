using TrueJourney.BotBehavior;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class Tool : MonoBehaviour, IInteractable, IPickupable, IUsable, IBotBreakTool
{
    [Header("Tool")]
    [SerializeField] private BreakToolKind toolKind = BreakToolKind.None;

    [Header("Use")]
    [SerializeField] private float useRange = 2f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private float damagePerUse = 1f;

    [Header("Bot")]
    [SerializeField] private float botUseRange = 1.75f;
    [SerializeField] private float botUseCooldown = 0.6f;

    [Header("Runtime")]
    [SerializeField] private GameObject currentHolder;
    [SerializeField] private GameObject claimOwner;

    private Rigidbody cachedRigidbody;

    protected virtual BreakToolKind DefaultToolKind => BreakToolKind.None;

    public BreakToolKind ToolKind => toolKind;
    public Rigidbody Rigidbody => cachedRigidbody;
    public float PreferredBreakDistance => Mathf.Max(0.5f, botUseRange * 0.8f);
    public float MaxBreakDistance => Mathf.Max(0.5f, botUseRange);
    public float UseCooldown => Mathf.Max(0.05f, botUseCooldown);
    public bool IsHeld => currentHolder != null;
    public bool IsHeldBy(GameObject requester) => requester != null && currentHolder == requester;

    protected virtual void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        ApplyDefaultToolKind();
    }

    protected virtual void OnValidate()
    {
        ApplyDefaultToolKind();
    }

    protected virtual void OnEnable()
    {
        if (toolKind != BreakToolKind.None)
        {
            BotRuntimeRegistry.RegisterBreakTool(this);
        }
    }

    protected virtual void OnDisable()
    {
        if (toolKind != BreakToolKind.None)
        {
            BotRuntimeRegistry.UnregisterBreakTool(this);
        }
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
        if (toolKind == BreakToolKind.None)
        {
            return;
        }

        Breakable breakable = FindBreakableInView(user);
        if (breakable == null)
        {
            UseDamageableInView(user);
            return;
        }

        GameObject source = user != null ? user : gameObject;
        breakable.TryStartBreak(source, toolKind);
    }

    public bool UseOnTarget(GameObject user, IBotBreakableTarget target)
    {
        if (toolKind == BreakToolKind.None || target == null || target.IsBroken || !target.CanBeClearedByBot)
        {
            return false;
        }

        GameObject source = user != null ? user : gameObject;
        return target.TryStartBreak(source, toolKind);
    }

    private void ApplyDefaultToolKind()
    {
        if (DefaultToolKind != BreakToolKind.None)
        {
            toolKind = DefaultToolKind;
        }
    }

    private Breakable FindBreakableInView(GameObject user)
    {
        if (!TryGetUseHit(user, out RaycastHit hit))
        {
            return null;
        }

        return FindBreakable(hit.collider);
    }

    private void UseDamageableInView(GameObject user)
    {
        if (damagePerUse <= 0f)
        {
            return;
        }

        if (!TryGetUseHit(user, out RaycastHit hit))
        {
            return;
        }

        IDamageable damageable = FindDamageable(hit.collider);
        if (damageable == null)
        {
            return;
        }

        GameObject source = user != null ? user : gameObject;
        damageable.TakeDamage(damagePerUse, source, hit.point, hit.normal);
    }

    private bool TryGetUseHit(GameObject user, out RaycastHit hit)
    {
        hit = default;

        float range = GetUseRange(user);
        if (range <= 0f)
        {
            return false;
        }

        Transform aim = GetAimTransform(user);
        if (aim == null)
        {
            return false;
        }

        Ray ray = new Ray(aim.position, aim.forward);
        return Physics.Raycast(ray, out hit, range, hitMask, QueryTriggerInteraction.Ignore);
    }

    private float GetUseRange(GameObject user)
    {
        if (user != null && user.TryGetComponent(out StarterAssets.FPSInteractionSystem interaction))
        {
            return interaction.InteractDistance;
        }

        return useRange;
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

    private static Breakable FindBreakable(Collider collider)
    {
        if (collider == null)
        {
            return null;
        }

        if (collider.TryGetComponent(out Breakable direct))
        {
            return direct;
        }

        if (collider.attachedRigidbody != null &&
            collider.attachedRigidbody.TryGetComponent(out Breakable rigidbodyOwner))
        {
            return rigidbodyOwner;
        }

        Transform parent = collider.transform.parent;
        while (parent != null)
        {
            if (parent.TryGetComponent(out Breakable parentBreakable))
            {
                return parentBreakable;
            }

            parent = parent.parent;
        }

        return null;
    }

    private static IDamageable FindDamageable(Collider collider)
    {
        if (collider == null)
        {
            return null;
        }

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
        while (parent != null)
        {
            if (parent.TryGetComponent(out IDamageable parentDamageable))
            {
                return parentDamageable;
            }

            parent = parent.parent;
        }

        return null;
    }
}
