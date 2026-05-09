using TrueJourney.BotBehavior;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class Tool : MonoBehaviour, IInteractable, IPickupable, IUsable, IBotBreakTool, IMovementWeightSource
{
    protected struct UseHit
    {
        public Collider Collider;
        public Vector3 Point;
        public Vector3 Normal;
        public float Distance;
    }

    [Header("Tool")]
    [SerializeField] private BreakToolKind toolKind = BreakToolKind.None;
    [SerializeField] private float movementWeightKg = 5f;

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
    public float MovementWeightKg => Mathf.Max(0f, movementWeightKg);
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
        movementWeightKg = Mathf.Max(0f, movementWeightKg);
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

    public virtual void Use(GameObject user)
    {
        if (toolKind == BreakToolKind.None)
        {
            return;
        }

        if (toolKind == BreakToolKind.Crowbar &&
            TryGetUseHit(user, out UseHit pryHit) &&
            TryFindPryOpenable(pryHit.Collider, out IPryOpenable pryOpenable) &&
            pryOpenable.TryPryOpen(user != null ? user : gameObject))
        {
            return;
        }

        IBotBreakableTarget breakableTarget = FindBreakableTargetInView(user);
        if (breakableTarget == null)
        {
            UseDamageableInView(user);
            return;
        }

        GameObject source = user != null ? user : gameObject;
        breakableTarget.TryStartBreak(source, toolKind);
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

    public bool UseOnTarget(GameObject user, IBotPryTarget target)
    {
        if (toolKind != BreakToolKind.Crowbar || target == null || target.IsBreached)
        {
            return false;
        }

        GameObject source = user != null ? user : gameObject;
        return target.TryPryOpen(source);
    }

    private void ApplyDefaultToolKind()
    {
        if (DefaultToolKind != BreakToolKind.None)
        {
            toolKind = DefaultToolKind;
        }
    }

    private IBotBreakableTarget FindBreakableTargetInView(GameObject user)
    {
        if (!TryGetUseHit(user, out UseHit hit))
        {
            return null;
        }

        return FindBreakableTarget(hit.Collider);
    }

    private void UseDamageableInView(GameObject user)
    {
        if (damagePerUse <= 0f)
        {
            return;
        }

        if (!TryGetUseHit(user, out UseHit hit))
        {
            return;
        }

        IDamageable damageable = FindDamageable(hit.Collider);
        if (damageable == null)
        {
            return;
        }

        GameObject source = user != null ? user : gameObject;
        damageable.TakeDamage(damagePerUse, source, hit.Point, hit.Normal);
    }

    protected bool TryGetUseHit(GameObject user, out UseHit hit)
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

        Ray ray = BuildUseRay(aim);
        if (Physics.Raycast(ray, out RaycastHit raycastHit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            hit = new UseHit
            {
                Collider = raycastHit.collider,
                Point = raycastHit.point,
                Normal = raycastHit.normal,
                Distance = raycastHit.distance
            };
            return true;
        }

        return TryGetCloseRangeUseHit(ray, range, out hit);
    }

    protected float GetUseRange(GameObject user)
    {
        if (user != null && user.TryGetComponent(out StarterAssets.FPSInteractionSystem interaction))
        {
            return interaction.InteractDistance;
        }

        return useRange;
    }

    protected Transform GetAimTransform(GameObject user)
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

    private static Ray BuildUseRay(Transform fallbackAim)
    {
        Camera activeCamera = Camera.main;
        if (activeCamera != null)
        {
            return activeCamera.ScreenPointToRay(
                new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        }

        return new Ray(fallbackAim.position, fallbackAim.forward);
    }

    private bool TryGetCloseRangeUseHit(Ray ray, float range, out UseHit hit)
    {
        hit = default;

        const float closeProbeRadius = 0.3f;
        const float forwardTolerance = 0.2f;
        const float lateralTolerance = 0.45f;

        Collider[] overlaps = Physics.OverlapSphere(
            ray.origin,
            closeProbeRadius,
            hitMask,
            QueryTriggerInteraction.Ignore);
        if (overlaps == null || overlaps.Length == 0)
        {
            return false;
        }

        bool foundHit = false;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider collider = overlaps[i];
            if (collider == null)
            {
                continue;
            }

            Vector3 closestPoint = collider.ClosestPoint(ray.origin);
            Vector3 toPoint = closestPoint - ray.origin;
            float projectedDistance = Vector3.Dot(toPoint, ray.direction);
            if (projectedDistance < -forwardTolerance || projectedDistance > range)
            {
                continue;
            }

            Vector3 projectedPoint = ray.origin + (ray.direction * Mathf.Max(0f, projectedDistance));
            float lateralDistance = (closestPoint - projectedPoint).magnitude;
            if (lateralDistance > lateralTolerance)
            {
                continue;
            }

            if (projectedDistance >= bestDistance)
            {
                continue;
            }

            Vector3 normal = ray.origin - closestPoint;
            if (normal.sqrMagnitude <= 0.0001f)
            {
                normal = -ray.direction;
            }

            hit = new UseHit
            {
                Collider = collider,
                Point = closestPoint,
                Normal = normal.normalized,
                Distance = Mathf.Max(0f, projectedDistance)
            };

            bestDistance = projectedDistance;
            foundHit = true;
        }

        return foundHit;
    }

    private static IBotBreakableTarget FindBreakableTarget(Collider collider)
    {
        if (collider == null)
        {
            return null;
        }

        if (collider.TryGetComponent(out IBotBreakableTarget direct))
        {
            return direct;
        }

        if (collider.attachedRigidbody != null &&
            collider.attachedRigidbody.TryGetComponent(out IBotBreakableTarget rigidbodyOwner))
        {
            return rigidbodyOwner;
        }

        Transform parent = collider.transform.parent;
        while (parent != null)
        {
            if (parent.TryGetComponent(out IBotBreakableTarget parentBreakable))
            {
                return parentBreakable;
            }

            parent = parent.parent;
        }

        return null;
    }

    private static bool TryFindPryOpenable(Collider collider, out IPryOpenable pryOpenable)
    {
        pryOpenable = null;
        if (collider == null)
        {
            return false;
        }

        if (collider.TryGetComponent(out pryOpenable))
        {
            return true;
        }

        if (collider.attachedRigidbody != null &&
            collider.attachedRigidbody.TryGetComponent(out pryOpenable))
        {
            return true;
        }

        Transform parent = collider.transform.parent;
        while (parent != null)
        {
            if (parent.TryGetComponent(out pryOpenable))
            {
                return true;
            }

            parent = parent.parent;
        }

        return false;
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
