using TrueJourney.BotBehavior;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class Breakable : MonoBehaviour, IInteractable, IBotBreakableTarget
{
    private const float SameSideTolerance = 0.2f;
    private const float LegacyWoodFireAxeTime = 1.25f;
    private const float LegacyWoodChainSawTime = 0.75f;
    private const float LegacyStoneSledgeHammerTime = 1.5f;

    [System.Serializable]
    private class BreakToolRequirement
    {
        [SerializeField] private BreakToolKind toolKind = BreakToolKind.FireAxe;
        [SerializeField] private float timeToBreak = 1f;

        public BreakToolKind ToolKind => toolKind;
        public float TimeToBreak => timeToBreak;

        public BreakToolRequirement()
        {
        }

        public BreakToolRequirement(BreakToolKind toolKind, float timeToBreak)
        {
            this.toolKind = toolKind;
            this.timeToBreak = timeToBreak;
        }
    }

    public enum BreakableType
    {
        Wood,
        Stone
    }

    [Header("Type")]
    [SerializeField] private BreakableType breakableType = BreakableType.Wood;

    [Header("Requirements")]
    [SerializeField] private BreakToolRequirement[] breakRequirements;
    [FormerlySerializedAs("freezePlayerWhileBreaking")]
    [SerializeField] private bool lockPlayerWhileBreaking = true;

    [Header("Break Behavior")]
    [SerializeField] private bool destroyOnBreak = false;
    [SerializeField] private float destroyDelay = 0f;
    [SerializeField] private bool deactivateOnBreak = true;

    [Header("Effects")]
    [SerializeField] private bool disableCollidersOnBreak = true;
    [SerializeField] private bool disableRenderersOnBreak = true;
    [SerializeField] private GameObject brokenPrefab;
    [SerializeField] private Transform brokenSpawnPoint;

    [Header("Events")]
    [SerializeField] private UnityEvent onBreakStarted;
    [SerializeField] private UnityEvent onBroken;

    [Header("Runtime")]
    [SerializeField] private bool isBroken;
    [SerializeField] private bool isBreakInProgress;
    [SerializeField] private GameObject activeBreaker;
    [SerializeField] private BreakToolKind activeToolKind = BreakToolKind.None;

    private Coroutine breakRoutine;
    private BreakActionLock activePlayerLock;

    public BreakableType Type => breakableType;
    public bool IsBroken => isBroken;
    public bool CanBeClearedByBot => !isBroken && HasAnyBreakRequirement();
    public bool IsBreakInProgress => isBreakInProgress;
    public GameObject ActiveBreaker => activeBreaker;

    private void OnEnable()
    {
        BotRuntimeRegistry.RegisterBreakableTarget(this);
        isBroken = false;
        isBreakInProgress = false;
        activeBreaker = null;
        activeToolKind = BreakToolKind.None;
        breakRoutine = null;
        activePlayerLock = null;
    }

    private void OnDisable()
    {
        CancelActiveBreak();
        BotRuntimeRegistry.UnregisterBreakableTarget(this);
    }

    public Vector3 GetWorldPosition()
    {
        return transform.position;
    }

    public void Interact(GameObject interactor)
    {
        if (interactor == null || isBroken)
        {
            return;
        }

        if (!TryResolveHeldBreakTool(interactor, out BreakToolKind toolKind))
        {
            return;
        }

        TryStartBreak(interactor, toolKind);
    }

    public bool IsOnSameSide(Vector3 pointA, Vector3 pointB)
    {
        if (!TryResolveSeparationPlane(out Vector3 planePoint, out Vector3 planeNormal))
        {
            return true;
        }

        float sideA = Vector3.Dot(ProjectToGround(pointA) - planePoint, planeNormal);
        float sideB = Vector3.Dot(ProjectToGround(pointB) - planePoint, planeNormal);

        if (Mathf.Abs(sideA) <= SameSideTolerance || Mathf.Abs(sideB) <= SameSideTolerance)
        {
            return true;
        }

        return sideA * sideB >= 0f;
    }

    public bool SupportsBreakTool(BreakToolKind toolKind)
    {
        return TryGetRequirement(toolKind, out _);
    }

    public bool TryStartBreak(GameObject breaker, BreakToolKind toolKind)
    {
        if (isBroken || !TryGetRequirement(toolKind, out BreakToolRequirement requirement))
        {
            return false;
        }

        if (isBreakInProgress)
        {
            return activeBreaker == breaker && activeToolKind == toolKind;
        }

        isBreakInProgress = true;
        activeBreaker = breaker;
        activeToolKind = toolKind;
        onBreakStarted?.Invoke();

        if (lockPlayerWhileBreaking && breaker != null && breaker.GetComponent<BotCommandAgent>() == null)
        {
            activePlayerLock = BreakActionLock.GetOrCreate(breaker);
            activePlayerLock?.Acquire();
        }

        breakRoutine = StartCoroutine(BreakAfterDelay(Mathf.Max(0.01f, requirement.TimeToBreak)));
        return true;
    }

    private IEnumerator BreakAfterDelay(float duration)
    {
        float endTime = Time.time + duration;
        while (Time.time < endTime)
        {
            if (!isActiveAndEnabled || isBroken)
            {
                CancelActiveBreak();
                yield break;
            }

            yield return null;
        }

        CompleteBreak();
    }

    private void CompleteBreak()
    {
        if (isBroken)
        {
            CancelActiveBreak();
            return;
        }

        isBroken = true;
        EndActiveBreakInteraction();

        if (brokenPrefab != null)
        {
            Transform spawn = brokenSpawnPoint != null ? brokenSpawnPoint : transform;
            Instantiate(brokenPrefab, spawn.position, spawn.rotation);
        }

        if (disableCollidersOnBreak)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        if (disableRenderersOnBreak)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
        }

        onBroken?.Invoke();

        if (destroyOnBreak)
        {
            Destroy(gameObject, Mathf.Max(0f, destroyDelay));
        }
        else if (deactivateOnBreak)
        {
            gameObject.SetActive(false);
        }
    }

    private void CancelActiveBreak()
    {
        if (breakRoutine != null)
        {
            StopCoroutine(breakRoutine);
            breakRoutine = null;
        }

        EndActiveBreakInteraction();
    }

    private void EndActiveBreakInteraction()
    {
        isBreakInProgress = false;
        activeBreaker = null;
        activeToolKind = BreakToolKind.None;

        if (activePlayerLock != null)
        {
            activePlayerLock.Release();
            activePlayerLock = null;
        }
    }

    private bool TryGetRequirement(BreakToolKind toolKind, out BreakToolRequirement requirement)
    {
        requirement = null;
        if (breakRequirements != null)
        {
            for (int i = 0; i < breakRequirements.Length; i++)
            {
                BreakToolRequirement candidate = breakRequirements[i];
                if (candidate != null && candidate.ToolKind == toolKind)
                {
                    requirement = candidate;
                    return true;
                }
            }
        }

        return TryGetLegacyRequirement(toolKind, out requirement);
    }

    private static bool TryResolveHeldBreakTool(GameObject interactor, out BreakToolKind toolKind)
    {
        toolKind = BreakToolKind.None;
        if (interactor == null || !interactor.TryGetComponent(out FPSInventorySystem inventory))
        {
            return false;
        }

        GameObject heldObject = inventory.HeldObject;
        if (heldObject == null)
        {
            return false;
        }

        if (heldObject.TryGetComponent(out Tool tool))
        {
            toolKind = tool.ToolKind;
            return toolKind != BreakToolKind.None;
        }

        return false;
    }

    private bool HasAnyBreakRequirement()
    {
        if (breakRequirements != null)
        {
            for (int i = 0; i < breakRequirements.Length; i++)
            {
                BreakToolRequirement candidate = breakRequirements[i];
                if (candidate != null && candidate.ToolKind != BreakToolKind.None)
                {
                    return true;
                }
            }
        }

        switch (breakableType)
        {
            case BreakableType.Wood:
            case BreakableType.Stone:
                return true;
            default:
                return false;
        }
    }

    private bool TryGetLegacyRequirement(BreakToolKind toolKind, out BreakToolRequirement requirement)
    {
        requirement = null;
        switch (breakableType)
        {
            case BreakableType.Wood:
                if (toolKind == BreakToolKind.FireAxe)
                {
                    requirement = new BreakToolRequirement(toolKind, LegacyWoodFireAxeTime);
                    return true;
                }

                if (toolKind == BreakToolKind.ChainSaw)
                {
                    requirement = new BreakToolRequirement(toolKind, LegacyWoodChainSawTime);
                    return true;
                }

                return false;

            case BreakableType.Stone:
                if (toolKind == BreakToolKind.SledgeHammer)
                {
                    requirement = new BreakToolRequirement(toolKind, LegacyStoneSledgeHammerTime);
                    return true;
                }

                return false;

            default:
                return false;
        }
    }

    private bool TryResolveSeparationPlane(out Vector3 planePoint, out Vector3 planeNormal)
    {
        planePoint = ProjectToGround(transform.position);
        planeNormal = Vector3.zero;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            planePoint = ProjectToGround(collider.bounds.center);
            planeNormal = ResolveHorizontalPlaneNormal(collider);
            if (planeNormal.sqrMagnitude > 0.001f)
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 ResolveHorizontalPlaneNormal(Collider collider)
    {
        if (collider == null)
        {
            return Vector3.zero;
        }

        Transform colliderTransform = collider.transform;
        Vector3 right = colliderTransform.right;
        Vector3 forward = colliderTransform.forward;
        right.y = 0f;
        forward.y = 0f;
        right.Normalize();
        forward.Normalize();

        if (collider is BoxCollider boxCollider)
        {
            float widthX = Mathf.Abs(boxCollider.size.x * colliderTransform.lossyScale.x);
            float widthZ = Mathf.Abs(boxCollider.size.z * colliderTransform.lossyScale.z);
            return widthX <= widthZ ? right : forward;
        }

        Bounds bounds = collider.bounds;
        return bounds.size.x <= bounds.size.z ? right : forward;
    }

    private static Vector3 ProjectToGround(Vector3 point)
    {
        point.y = 0f;
        return point;
    }
}
