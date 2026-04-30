using System.Collections;
using RayFire;
using TrueJourney.BotBehavior;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(RayfireRigid))]
[RequireComponent(typeof(RayfireShatter))]
public class Breakable : MonoBehaviour, IInteractable, IBotBreakableTarget
{
    private const float SameSideTolerance = 0.2f;
    private const float LegacyWoodFireAxeTime = 1.25f;
    private const float LegacyWoodChainSawTime = 0.75f;
    private const float LegacyWoodCrowbarTime = 1.9f;
    private const float LegacyStoneSledgeHammerTime = 1.5f;
    private const float LegacyGlassFireAxeTime = 0.1f;
    private const float LegacyGlassSledgeHammerTime = 0.1f;
    private const float LegacyGlassCrowbarTime = 0.18f;

    [System.Serializable]
    private class BreakToolRequirement
    {
        [SerializeField]
        private BreakToolKind toolKind = BreakToolKind.FireAxe;

        [SerializeField]
        private float timeToBreak = 1f;

        public BreakToolKind ToolKind => toolKind;
        public float TimeToBreak => timeToBreak;

        public BreakToolRequirement() { }

        public BreakToolRequirement(BreakToolKind toolKind, float timeToBreak)
        {
            this.toolKind = toolKind;
            this.timeToBreak = timeToBreak;
        }
    }

    public enum BreakableType
    {
        Wood,
        Stone,
        Glass,
    }

    [Header("Type")]
    [SerializeField]
    private BreakableType breakableType = BreakableType.Wood;

    [Header("Requirements")]
    [SerializeField]
    private BreakToolRequirement[] breakRequirements;

    [FormerlySerializedAs("freezePlayerWhileBreaking")]
    [SerializeField]
    private bool lockPlayerWhileBreaking = true;

    [Header("Stand Points")]
    [SerializeField]
    private bool useBreakStandPoints = true;

    [SerializeField]
    private Transform[] breakStandPoints;

    [SerializeField]
    private Transform breakLookTarget;

    [Header("Marker")]
    [SerializeField]
    private bool showMarkerWhileIntact = true;

    [SerializeField]
    private GameObject[] markerObjects;

    [Header("Break Behavior")]
    [SerializeField]
    private bool destroyOnBreak = false;

    [SerializeField]
    private float destroyDelay = 0f;

    [SerializeField]
    private bool deactivateOnBreak = true;

    [Header("Effects")]
    [SerializeField]
    private bool disableCollidersOnBreak = true;

    [SerializeField]
    private bool disableRenderersOnBreak = true;

    [Header("Events")]
    [SerializeField]
    private UnityEvent onBreakStarted;

    [SerializeField]
    private UnityEvent onBroken;

    [Header("Runtime")]
    [SerializeField]
    private bool isBroken;

    [SerializeField]
    private bool isBreakInProgress;

    [SerializeField]
    private GameObject activeBreaker;

    [SerializeField]
    private BreakToolKind activeToolKind = BreakToolKind.None;

    private Coroutine breakRoutine;
    private PlayerActionLock activePlayerLock;

    public BreakableType Type => breakableType;
    public bool IsBroken => isBroken;
    public bool CanBeClearedByBot => !isBroken && HasAnyBreakRequirement();
    public bool IsBreakInProgress => isBreakInProgress;
    public GameObject ActiveBreaker => activeBreaker;
    public event System.Action BreakCompleted;

    private void OnEnable()
    {
        BotRuntimeRegistry.RegisterBreakableTarget(this);
        isBroken = false;
        isBreakInProgress = false;
        activeBreaker = null;
        activeToolKind = BreakToolKind.None;
        breakRoutine = null;
        activePlayerLock = null;
        RefreshMarkerVisibility();
    }

    private void OnDisable()
    {
        CancelActiveBreak();
        BotRuntimeRegistry.UnregisterBreakableTarget(this);
    }

    private void OnValidate()
    {
        destroyDelay = Mathf.Max(0f, destroyDelay);
        RefreshMarkerVisibility();
    }

    public Vector3 GetWorldPosition()
    {
        return transform.position;
    }

    public bool TryGetBreakStandPose(
        Vector3 breakerPosition,
        out Vector3 standPosition,
        out Quaternion standRotation
    )
    {
        standPosition = default;
        standRotation = Quaternion.identity;

        if (!useBreakStandPoints)
        {
            return false;
        }

        Transform selectedStandPoint = GetNearestStandPoint(breakerPosition);
        if (selectedStandPoint == null)
        {
            return false;
        }

        standPosition = selectedStandPoint.position;
        standPosition.y = breakerPosition.y;
        standRotation = ResolveStandRotation(selectedStandPoint);
        return true;
    }

    public void Interact(GameObject interactor)
    {
        if (isBroken)
        {
            return;
        }

        TryForwardInteractionToParent(interactor);
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

        if (
            lockPlayerWhileBreaking
            && breaker != null
            && breaker.GetComponent<BotCommandAgent>() == null
        )
        {
            activePlayerLock = PlayerActionLock.GetOrCreate(breaker);
            activePlayerLock?.AcquireFullLock();
        }

        SnapBreakerToStandPose(breaker);
        onBreakStarted?.Invoke();
        if (breaker != null && breaker.GetComponent<BotCommandAgent>() == null)
        {
            PlayerContinuousActionBus.StartAction();
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

            float progress = 1f - ((endTime - Time.time) / duration);
            if (activeBreaker != null && activeBreaker.GetComponent<BotCommandAgent>() == null)
            {
                PlayerContinuousActionBus.UpdateProgress(progress);
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
        GameObject breaker = activeBreaker;
        EndActiveBreakInteraction();

        RayfireRigid rayfireRigid = GetComponent<RayfireRigid>();
        if (rayfireRigid != null)
        {
            Vector3 impactDirection = breaker != null
                ? transform.position - breaker.transform.position
                : transform.forward;
            RayfireBreakImpact.DemolishWithImpact(
                rayfireRigid,
                breaker,
                transform.position,
                impactDirection,
                false);
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

        RefreshMarkerVisibility();
        onBroken?.Invoke();
        BreakCompleted?.Invoke();

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
        if (isBreakInProgress && activeBreaker != null && activeBreaker.GetComponent<BotCommandAgent>() == null)
        {
            PlayerContinuousActionBus.EndAction(isBroken);
        }

        isBreakInProgress = false;
        activeBreaker = null;
        activeToolKind = BreakToolKind.None;

        if (activePlayerLock != null)
        {
            activePlayerLock.ReleaseFullLock();
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

    private void SnapBreakerToStandPose(GameObject breaker)
    {
        if (
            breaker == null
            || !TryGetBreakStandPose(
                breaker.transform.position,
                out Vector3 standPosition,
                out Quaternion standRotation
            )
        )
        {
            return;
        }

        if (TrySnapBotBreaker(breaker, standPosition, standRotation))
        {
            return;
        }

        if (TrySnapPlayerBreaker(breaker, standPosition, standRotation))
        {
            return;
        }

        breaker.transform.SetPositionAndRotation(standPosition, standRotation);
    }

    private bool TryForwardInteractionToParent(GameObject interactor)
    {
        Transform parent = transform.parent;
        while (parent != null)
        {
            if (
                parent.TryGetComponent(out IInteractable parentInteractable)
                && parentInteractable is not Breakable
            )
            {
                parentInteractable.Interact(interactor);
                return true;
            }

            parent = parent.parent;
        }

        return false;
    }

    private void RefreshMarkerVisibility()
    {
        ResolveMarkerObjects();
        SetMarkerVisibility(showMarkerWhileIntact && !isBroken);
    }

    private void ResolveMarkerObjects()
    {
        if (markerObjects != null && markerObjects.Length > 0)
        {
            return;
        }

        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < childTransforms.Length; i++)
        {
            Transform candidate = childTransforms[i];
            if (candidate == null || candidate == transform)
            {
                continue;
            }

            if (
                candidate.name == "BreakableMarker"
                || candidate.name == "BreakMarker"
                || candidate.name == "CrackMarker"
            )
            {
                markerObjects = new[] { candidate.gameObject };
                return;
            }
        }
    }

    private void SetMarkerVisibility(bool visible)
    {
        if (markerObjects == null)
        {
            return;
        }

        for (int i = 0; i < markerObjects.Length; i++)
        {
            GameObject markerObject = markerObjects[i];
            if (markerObject != null && markerObject.activeSelf != visible)
            {
                markerObject.SetActive(visible);
            }
        }
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
            case BreakableType.Glass:
                return true;
            default:
                return false;
        }
    }

    private bool TryGetLegacyRequirement(
        BreakToolKind toolKind,
        out BreakToolRequirement requirement
    )
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

                if (toolKind == BreakToolKind.Crowbar)
                {
                    requirement = new BreakToolRequirement(toolKind, LegacyWoodCrowbarTime);
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

            case BreakableType.Glass:
                if (toolKind == BreakToolKind.FireAxe)
                {
                    requirement = new BreakToolRequirement(toolKind, LegacyGlassFireAxeTime);
                    return true;
                }

                if (toolKind == BreakToolKind.SledgeHammer)
                {
                    requirement = new BreakToolRequirement(toolKind, LegacyGlassSledgeHammerTime);
                    return true;
                }

                if (toolKind == BreakToolKind.Crowbar)
                {
                    requirement = new BreakToolRequirement(toolKind, LegacyGlassCrowbarTime);
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

    private Transform GetNearestStandPoint(Vector3 breakerPosition)
    {
        if (breakStandPoints == null || breakStandPoints.Length == 0)
        {
            return null;
        }

        Vector3 flattenedBreakerPosition = ProjectToGround(breakerPosition);
        Transform bestStandPoint = null;
        float bestDistanceSq = float.PositiveInfinity;

        for (int i = 0; i < breakStandPoints.Length; i++)
        {
            Transform candidate = breakStandPoints[i];
            if (candidate == null)
            {
                continue;
            }

            float distanceSq = (
                ProjectToGround(candidate.position) - flattenedBreakerPosition
            ).sqrMagnitude;
            if (distanceSq < bestDistanceSq)
            {
                bestDistanceSq = distanceSq;
                bestStandPoint = candidate;
            }
        }

        return bestStandPoint;
    }

    private Quaternion ResolveStandRotation(Transform standPoint)
    {
        if (standPoint == null)
        {
            return Quaternion.identity;
        }

        Vector3 forward = ProjectToGroundDirection(standPoint.forward);
        if (forward.sqrMagnitude > 0.001f)
        {
            return Quaternion.LookRotation(forward, Vector3.up);
        }

        Vector3 lookTargetPosition =
            breakLookTarget != null ? breakLookTarget.position : transform.position;
        Vector3 lookDirection = ProjectToGroundDirection(lookTargetPosition - standPoint.position);
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            return Quaternion.LookRotation(lookDirection, Vector3.up);
        }

        Vector3 fallbackForward = ProjectToGroundDirection(transform.forward);
        return fallbackForward.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(fallbackForward, Vector3.up)
            : Quaternion.identity;
    }

    private bool TrySnapBotBreaker(
        GameObject breaker,
        Vector3 standPosition,
        Quaternion standRotation
    )
    {
        if (
            breaker == null
            || !breaker.TryGetComponent(out NavMeshAgent navMeshAgent)
            || !navMeshAgent.enabled
            || !navMeshAgent.isOnNavMesh
        )
        {
            return false;
        }

        Vector3 resolvedPosition = standPosition;
        float sampleDistance = Mathf.Max(navMeshAgent.radius + 0.5f, 1f);
        if (
            NavMesh.SamplePosition(
                standPosition,
                out NavMeshHit navMeshHit,
                sampleDistance,
                navMeshAgent.areaMask
            )
        )
        {
            resolvedPosition = navMeshHit.position;
        }

        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = true;
        if (!navMeshAgent.Warp(resolvedPosition))
        {
            return false;
        }

        breaker.transform.rotation = standRotation;
        return true;
    }

    private bool TrySnapPlayerBreaker(
        GameObject breaker,
        Vector3 standPosition,
        Quaternion standRotation
    )
    {
        if (
            breaker == null
            || (
                breaker.GetComponent<CharacterController>() == null
                && breaker.GetComponent<StarterAssets.FirstPersonController>() == null
                && breaker.GetComponent<PlayerActionLock>() == null
            )
        )
        {
            return false;
        }

        PlayerActionLock breakActionLock =
            activePlayerLock ?? PlayerActionLock.GetOrCreate(breaker);
        if (breakActionLock == null)
        {
            return false;
        }

        breakActionLock.SnapToPose(standPosition, standRotation);
        return true;
    }

    private static Vector3 ProjectToGround(Vector3 point)
    {
        point.y = 0f;
        return point;
    }

    private static Vector3 ProjectToGroundDirection(Vector3 direction)
    {
        direction.y = 0f;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero;
    }

}
