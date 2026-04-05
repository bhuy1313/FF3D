using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class StandardLadder : MonoBehaviour, IGrabbable, ICustomGrabPlacement, IMovementWeightSource
{
    [Header("Placement")]
    [SerializeField] private LayerMask placementMask = ~0;
    [SerializeField] private bool useAimBasedCardinalDirection = true;
    [SerializeField] private Vector3 fallbackPlacementDirection = Vector3.forward;
    [SerializeField] private float wallSearchDistance = 2.5f;
    [SerializeField, Range(0f, 1f)] private float minimumPlatformLookUpDot = 0.2f;
    [SerializeField] private float bottomGroundProbeHeight = 1.5f;
    [SerializeField] private float bottomGroundSearchDistance = 4f;
    [SerializeField] private float bottomOffsetFromWall = 0.35f;
    [SerializeField] private float bottomGroundClearance = 0.03f;
    [SerializeField] private float topSearchDepth = 2f;
    [SerializeField] private float topSearchStep = 0.1f;
    [SerializeField] private float topAnchorOutwardOffset = 0.35f;
    [SerializeField] private float platformTopSurfaceInset = 0.1f;
    [SerializeField, Range(0f, 1f)] private float minimumSurfaceUpDot = 0.65f;

    [Header("Grid Snap")]
    [SerializeField] private bool enableEdgeGridSnap = true;
    [SerializeField] private float edgeGridStep = 0.5f;

    [Header("Shape")]
    [SerializeField] private float topExitOffset = 0.15f;
    [SerializeField] private float placementPitchOffsetDegrees = 8f;
    [SerializeField] private float movementWeightKg = 18f;

    [Header("Navigation")]
    [SerializeField] private bool enableNavMeshLink = true;
    [SerializeField] private float navMeshLinkWidthPadding = 0.2f;
    [SerializeField] private float navMeshLinkBottomOffset = 0.15f;
    [SerializeField] private float navMeshLinkTopPlatformOffset = 0.6f;
    [SerializeField] private bool autoSnapLinkEndpointsToNavMesh = true;
    [SerializeField] private float navMeshEndpointSnapDistance = 1f;
    [SerializeField] private float navMeshEndpointVerticalTolerance = 1f;

    [Header("Debug")]
    [SerializeField] private bool drawPlacementDebug;

    [Header("Runtime")]
    [SerializeField] private Vector3 bottomAnchorWorld;
    [SerializeField] private Vector3 topAnchorWorld;
    [SerializeField] private float ladderHeight;

    private Rigidbody cachedRigidbody;
    private BoxCollider climbCollider;
    private Ladder ladder;
    private NavMeshLink navMeshLink;
    private Transform visualsRoot;
    private bool capturedVisualBaseTransform;
    private Vector3 baseVisualLocalPosition;
    private Quaternion baseVisualLocalRotation = Quaternion.identity;
    private Vector3 baseVisualLocalScale = Vector3.one;
    private bool hasPlacedConfiguration;
    private bool isGrabbed;
    private bool hasPendingPlacement;
    private Vector3 pendingBottomPoint;
    private Vector3 pendingTopAnchor;
    private Vector3 pendingTopSurfacePoint;
    private Vector3 pendingOutward;

    public Rigidbody Rigidbody => cachedRigidbody;
    public float LadderHeight => ladderHeight;
    public float MovementWeightKg => Mathf.Max(0f, movementWeightKg);

    private void Awake()
    {
        EnsureReferences();
        ApplyPlacedState(hasPlacedConfiguration);
    }

    private void OnValidate()
    {
        topExitOffset = Mathf.Max(0f, topExitOffset);
        movementWeightKg = Mathf.Max(0f, movementWeightKg);
        minimumPlatformLookUpDot = Mathf.Clamp01(minimumPlatformLookUpDot);
        platformTopSurfaceInset = Mathf.Max(0.01f, platformTopSurfaceInset);
        edgeGridStep = Mathf.Max(0.05f, edgeGridStep);
        bottomGroundClearance = Mathf.Max(0f, bottomGroundClearance);
    }

    public bool TryGetGrabPlacementPose(Transform aimTransform, LayerMask ignoredPlacementMask, float ignoredMaxDistance, out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;

        if (!TryResolvePlacement(
                aimTransform,
                out Vector3 bottomPoint,
                out Vector3 topAnchor,
                out Vector3 topSurfacePoint,
                out Vector3 outward))
        {
            hasPendingPlacement = false;
            return false;
        }

        pendingBottomPoint = bottomPoint;
        pendingTopAnchor = topAnchor;
        pendingTopSurfacePoint = topSurfacePoint;
        pendingOutward = outward;
        hasPendingPlacement = true;

        position = Vector3.Lerp(topAnchor, bottomPoint, 0.5f);
        rotation = BuildPlacementRotation(outward);
        return true;
    }

    public void OnGrabStarted()
    {
        EnsureReferences();
        isGrabbed = true;
        hasPendingPlacement = false;
        pendingTopSurfacePoint = default;
        ApplyPlacedState(hasPlacedConfiguration);
    }

    public void OnGrabCancelled()
    {
        isGrabbed = false;
        hasPendingPlacement = false;
        pendingTopSurfacePoint = default;
        ApplyPlacedState(hasPlacedConfiguration);
    }

    public void OnGrabPlaced(Vector3 position, Quaternion rotation)
    {
        isGrabbed = false;
        if (hasPendingPlacement)
        {
            ApplyResolvedPlacement(pendingBottomPoint, pendingTopAnchor, pendingTopSurfacePoint, pendingOutward);
        }
        else
        {
            transform.SetPositionAndRotation(position, rotation);
            ApplyPlacedState(hasPlacedConfiguration);
        }

        hasPendingPlacement = false;
        pendingTopSurfacePoint = default;
    }

    public void ApplyPlacement(Vector3 bottomPoint, Vector3 topAnchor, Vector3 outward)
    {
        ApplyResolvedPlacement(bottomPoint, topAnchor, topAnchor, outward);
    }

    private void ApplyResolvedPlacement(Vector3 bottomPoint, Vector3 topAnchor, Vector3 topSurfacePoint, Vector3 outward)
    {
        EnsureReferences();

        Vector3 flatOutward = Vector3.ProjectOnPlane(outward, Vector3.up);
        if (flatOutward.sqrMagnitude <= 0.0001f)
        {
            flatOutward = Vector3.forward;
        }

        flatOutward.Normalize();
        Quaternion placementRotation = BuildPlacementRotation(flatOutward);

        AlignColliderToPlacement(bottomPoint, placementRotation);
        LiftPlacedColliderAboveGround(bottomPoint.y);

        bottomAnchorWorld = GetColliderEndpointWorld(isTop: false);
        topAnchorWorld = GetColliderEndpointWorld(isTop: true);
        ladderHeight = GetTargetPlacementHeight();

        ladder.SetTopHeightOffset(topExitOffset);
        Vector3 resolvedTopSurfacePoint = ResolveTopSurfacePoint(topAnchorWorld, topSurfacePoint);
        ConfigureNavMeshLink(resolvedTopSurfacePoint, bottomAnchorWorld, flatOutward);
        ApplyVisualLayout();
        hasPlacedConfiguration = true;
        ApplyPlacedState(true);

        if (cachedRigidbody != null)
        {
            cachedRigidbody.linearVelocity = Vector3.zero;
            cachedRigidbody.angularVelocity = Vector3.zero;
            cachedRigidbody.Sleep();
        }
    }

    private void EnsureReferences()
    {
        cachedRigidbody ??= GetComponent<Rigidbody>();

        if (climbCollider == null)
        {
            climbCollider = GetComponent<BoxCollider>();
        }

        if (ladder == null)
        {
            ladder = GetComponent<Ladder>();
            if (ladder == null)
            {
                ladder = gameObject.AddComponent<Ladder>();
            }
        }

        if (navMeshLink == null)
        {
            navMeshLink = GetComponent<NavMeshLink>();
            if (navMeshLink == null)
            {
                navMeshLink = gameObject.AddComponent<NavMeshLink>();
            }
        }

        PrepareVisualsRoot();
    }

    private void PrepareVisualsRoot()
    {
        if (visualsRoot == null)
        {
            visualsRoot = transform.Find("Visuals");
            if (visualsRoot == null)
            {
                GameObject visuals = new GameObject("Visuals");
                visualsRoot = visuals.transform;
                visualsRoot.SetParent(transform, false);

                List<Transform> childrenToMove = new List<Transform>();
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    if (child == visualsRoot)
                    {
                        continue;
                    }

                    childrenToMove.Add(child);
                }

                for (int i = 0; i < childrenToMove.Count; i++)
                {
                    childrenToMove[i].SetParent(visualsRoot, true);
                }
            }
        }

        if (!capturedVisualBaseTransform && visualsRoot != null)
        {
            baseVisualLocalPosition = visualsRoot.localPosition;
            baseVisualLocalRotation = visualsRoot.localRotation;
            baseVisualLocalScale = visualsRoot.localScale;
            capturedVisualBaseTransform = true;
        }

        SanitizeVisualChildren();
    }

    private void SanitizeVisualChildren()
    {
        if (visualsRoot == null)
        {
            return;
        }

        Collider[] colliders = visualsRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            DestroyRuntimeSafe(colliders[i]);
        }

        Rigidbody[] rigidbodies = visualsRoot.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            DestroyRuntimeSafe(rigidbodies[i]);
        }
    }

    private void ApplyVisualLayout()
    {
        PrepareVisualsRoot();
        if (visualsRoot == null)
        {
            return;
        }

        visualsRoot.localPosition = baseVisualLocalPosition;
        visualsRoot.localRotation = baseVisualLocalRotation;
        visualsRoot.localScale = baseVisualLocalScale;
    }

    private void ApplyPlacedState(bool enabled)
    {
        if (climbCollider != null)
        {
            climbCollider.enabled = !isGrabbed;
        }

        if (ladder != null)
        {
            ladder.enabled = enabled && !isGrabbed;
        }

        if (navMeshLink != null)
        {
            navMeshLink.activated = enabled && enableNavMeshLink && !isGrabbed;
        }
    }

    private Quaternion BuildPlacementRotation(Vector3 outward)
    {
        Vector3 flatOutward = Vector3.ProjectOnPlane(outward, Vector3.up);
        if (flatOutward.sqrMagnitude <= 0.0001f)
        {
            flatOutward = Vector3.back;
        }

        Quaternion baseRotation = Quaternion.LookRotation(-flatOutward.normalized, Vector3.up);
        return baseRotation * Quaternion.Euler(placementPitchOffsetDegrees, 0f, 0f);
    }

    private void AlignColliderToPlacement(Vector3 bottomPoint, Quaternion placementRotation)
    {
        Vector3 localBottomPoint = GetColliderLocalEndpoint(isTop: false);
        Vector3 worldPosition = bottomPoint - (placementRotation * localBottomPoint);
        transform.SetPositionAndRotation(worldPosition, placementRotation);
    }

    private void LiftPlacedColliderAboveGround(float minimumBottomY)
    {
        if (climbCollider == null)
        {
            return;
        }

        float lift = minimumBottomY - climbCollider.bounds.min.y;
        if (lift > 0f)
        {
            transform.position += Vector3.up * lift;
        }
    }

    private Vector3 GetColliderEndpointWorld(bool isTop)
    {
        return transform.TransformPoint(GetColliderLocalEndpoint(isTop));
    }

    private Vector3 GetColliderLocalEndpoint(bool isTop)
    {
        if (climbCollider == null)
        {
            float fallbackHalfHeight = Mathf.Max(0.25f, GetTargetPlacementHeight() * 0.5f);
            return Vector3.up * (isTop ? fallbackHalfHeight : -fallbackHalfHeight);
        }

        float verticalSign = isTop ? 0.5f : -0.5f;
        return climbCollider.center + Vector3.up * (climbCollider.size.y * verticalSign);
    }

    private bool TryResolvePlacement(
        Transform aimTransform,
        out Vector3 bottomPoint,
        out Vector3 topAnchor,
        out Vector3 topSurfacePoint,
        out Vector3 outward)
    {
        bottomPoint = default;
        topAnchor = default;
        topSurfacePoint = default;
        outward = Vector3.zero;

        if (aimTransform == null)
        {
            return false;
        }

        Vector3 towardWall = ResolvePlacementForward(aimTransform);
        if (towardWall.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        if (!TryFindPlacementReference(
                aimTransform,
                towardWall,
                out Vector3 wallReference,
                out float probeBaseY,
                out float preferredTopSurfaceY,
                out outward))
        {
            return false;
        }

        wallReference = SnapAlongEdge(wallReference, outward);
        if (!TryFindBottomPoint(probeBaseY, wallReference, outward, out bottomPoint))
        {
            return false;
        }

        if (!TryResolveTopAnchorPlacement(
                wallReference,
                outward,
                bottomPoint.y,
                preferredTopSurfaceY,
                out topAnchor,
                out topSurfacePoint))
        {
            return false;
        }

        float height = topAnchor.y - bottomPoint.y;
        return IsPlacementHeightWithinLimits(height);
    }

    private Vector3 ResolvePlacementForward(Transform aimTransform)
    {
        Vector3 forward = useAimBasedCardinalDirection && aimTransform != null
            ? aimTransform.forward
            : fallbackPlacementDirection;

        forward = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(fallbackPlacementDirection, Vector3.up);
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        return QuantizeToCardinalDirection(forward.normalized);
    }

    private static Vector3 QuantizeToCardinalDirection(Vector3 forward)
    {
        if (Mathf.Abs(forward.z) >= Mathf.Abs(forward.x))
        {
            return forward.z >= 0f ? Vector3.forward : Vector3.back;
        }

        return forward.x >= 0f ? Vector3.right : Vector3.left;
    }

    private bool TryFindPlacementReference(
        Transform aimTransform,
        Vector3 towardWall,
        out Vector3 wallReference,
        out float probeBaseY,
        out float preferredTopSurfaceY,
        out Vector3 outward)
    {
        if (TryFindPlatformPlacementReference(
                aimTransform,
                out wallReference,
                out probeBaseY,
                out preferredTopSurfaceY,
                out outward))
        {
            return true;
        }

        return TryFindWallPlacementReference(
            aimTransform,
            towardWall,
            out wallReference,
            out probeBaseY,
            out preferredTopSurfaceY,
            out outward);
    }

    private bool TryFindPlatformPlacementReference(
        Transform aimTransform,
        out Vector3 wallReference,
        out float probeBaseY,
        out float preferredTopSurfaceY,
        out Vector3 outward)
    {
        wallReference = default;
        probeBaseY = 0f;
        preferredTopSurfaceY = 0f;
        outward = Vector3.zero;

        if (aimTransform == null)
        {
            return false;
        }

        Vector3 aimForward = aimTransform.forward;
        if (aimForward.sqrMagnitude <= 0.0001f || aimForward.normalized.y < minimumPlatformLookUpDot)
        {
            return false;
        }

        RaycastHit[] hits = Physics.RaycastAll(
            aimTransform.position,
            aimForward.normalized,
            GetPlatformSearchDistance(),
            placementMask,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null)
            {
                continue;
            }

            outward = ResolvePlatformOutward(aimTransform, hit.point);
            if (outward.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            preferredTopSurfaceY = hit.collider.bounds.max.y;
            Vector3 topHitPoint = new Vector3(hit.point.x, preferredTopSurfaceY, hit.point.z);
            wallReference = ResolvePlacementReferencePoint(hit.collider.bounds, outward, topHitPoint);
            if (!TryResolvePlatformTopSurfaceY(hit.collider, wallReference, outward, out preferredTopSurfaceY))
            {
                continue;
            }

            if (preferredTopSurfaceY <= aimTransform.position.y + 0.05f)
            {
                continue;
            }

            probeBaseY = preferredTopSurfaceY;
            return true;
        }

        return false;
    }

    private bool TryFindWallPlacementReference(
        Transform aimTransform,
        Vector3 towardWall,
        out Vector3 wallReference,
        out float probeBaseY,
        out float preferredTopSurfaceY,
        out Vector3 outward)
    {
        wallReference = default;
        probeBaseY = 0f;
        preferredTopSurfaceY = 0f;
        outward = Vector3.zero;

        Vector3 origin = aimTransform.position;
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            towardWall,
            wallSearchDistance,
            placementMask,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null)
            {
                continue;
            }

            outward = ResolvePlacementOutward(hit.normal, towardWall);
            wallReference = ResolvePlacementReferencePoint(hit, outward);
            probeBaseY = hit.point.y;
            preferredTopSurfaceY = hit.collider.bounds.max.y;
            return true;
        }

        return false;
    }

    private Vector3 ResolvePlatformOutward(Transform aimTransform, Vector3 platformPoint)
    {
        Vector3 outward = aimTransform != null
            ? Vector3.ProjectOnPlane(aimTransform.position - platformPoint, Vector3.up)
            : Vector3.zero;

        if (outward.sqrMagnitude <= 0.0001f)
        {
            outward = aimTransform != null
                ? -ResolvePlacementForward(aimTransform)
                : Vector3.back;
        }

        outward = Vector3.ProjectOnPlane(outward, Vector3.up);
        if (outward.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        return QuantizeToCardinalDirection(outward.normalized);
    }

    private float GetPlatformSearchDistance()
    {
        return Mathf.Max(wallSearchDistance, GetTargetPlacementHeight() + 1f);
    }

    private bool TryResolvePlatformTopSurfaceY(Collider collider, Vector3 wallReference, Vector3 outward, out float topSurfaceY)
    {
        topSurfaceY = 0f;
        if (collider == null)
        {
            return false;
        }

        Vector3 flatOutward = Vector3.ProjectOnPlane(outward, Vector3.up);
        if (flatOutward.sqrMagnitude <= 0.0001f)
        {
            flatOutward = Vector3.back;
        }

        flatOutward.Normalize();
        float inset = Mathf.Max(0.01f, platformTopSurfaceInset);
        Vector3 probePoint = wallReference - flatOutward * inset;
        float probeLift = Mathf.Max(0.25f, bottomGroundClearance + 0.15f);
        Vector3 probeOrigin = new Vector3(probePoint.x, collider.bounds.max.y + probeLift, probePoint.z);
        float probeDistance = collider.bounds.size.y + probeLift * 2f + 0.5f;

        if (!collider.Raycast(new Ray(probeOrigin, Vector3.down), out RaycastHit topHit, probeDistance))
        {
            return false;
        }

        if (!IsSurfaceValid(topHit))
        {
            return false;
        }

        topSurfaceY = topHit.point.y;
        return true;
    }

    private static Vector3 ResolvePlacementOutward(Vector3 surfaceNormal, Vector3 towardWall)
    {
        Vector3 outward = Vector3.ProjectOnPlane(surfaceNormal, Vector3.up);
        if (outward.sqrMagnitude > 0.0001f)
        {
            outward.Normalize();
            if (Vector3.Dot(outward, -towardWall) >= 0.1f)
            {
                return outward;
            }
        }

        return -towardWall;
    }

    private static Vector3 ResolvePlacementReferencePoint(RaycastHit hit, Vector3 outward)
    {
        if (hit.collider == null)
        {
            return hit.point;
        }

        Vector3 flatOutward = Vector3.ProjectOnPlane(outward, Vector3.up);
        if (flatOutward.sqrMagnitude <= 0.0001f)
        {
            return hit.point;
        }

        return ResolvePlacementReferencePoint(hit.collider.bounds, flatOutward.normalized, hit.point);
    }

    private static Vector3 ResolvePlacementReferencePoint(Bounds bounds, Vector3 outward, Vector3 hitPoint)
    {
        Vector3 flatOutward = Vector3.ProjectOnPlane(outward, Vector3.up);
        if (flatOutward.sqrMagnitude <= 0.0001f)
        {
            return new Vector3(hitPoint.x, hitPoint.y, hitPoint.z);
        }

        flatOutward.Normalize();
        Vector3 edgeTangent = Vector3.Cross(Vector3.up, flatOutward);
        if (edgeTangent.sqrMagnitude <= 0.0001f)
        {
            return new Vector3(hitPoint.x, hitPoint.y, hitPoint.z);
        }

        edgeTangent.Normalize();

        Vector3 centerFlat = Vector3.ProjectOnPlane(bounds.center, Vector3.up);
        Vector3 hitFlat = Vector3.ProjectOnPlane(hitPoint, Vector3.up);
        Vector3 hitOffset = hitFlat - centerFlat;

        float outwardExtent =
            Mathf.Abs(flatOutward.x) * bounds.extents.x +
            Mathf.Abs(flatOutward.z) * bounds.extents.z;
        float tangentExtent =
            Mathf.Abs(edgeTangent.x) * bounds.extents.x +
            Mathf.Abs(edgeTangent.z) * bounds.extents.z;
        float tangentCoordinate = Mathf.Clamp(
            Vector3.Dot(hitOffset, edgeTangent),
            -tangentExtent,
            tangentExtent);

        Vector3 edgeFlatPoint =
            centerFlat +
            edgeTangent * tangentCoordinate +
            flatOutward * outwardExtent;

        return new Vector3(edgeFlatPoint.x, hitPoint.y, edgeFlatPoint.z);
    }

    private bool TryFindBottomPoint(float probeBaseY, Vector3 wallReference, Vector3 outward, out Vector3 bottomPoint)
    {
        bottomPoint = default;

        Vector3 probeBase = wallReference + outward * bottomOffsetFromWall;
        float targetHeight = GetTargetPlacementHeight();
        float verticalSearchDistance = Mathf.Max(bottomGroundSearchDistance, targetHeight + bottomGroundClearance + 0.25f);
        Vector3 probeOrigin = new Vector3(
            probeBase.x,
            probeBaseY + Mathf.Max(0.1f, bottomGroundProbeHeight),
            probeBase.z);
        float probeDistance = Mathf.Max(0.1f, bottomGroundProbeHeight + verticalSearchDistance);

        if (!Physics.Raycast(
                probeOrigin,
                Vector3.down,
                out RaycastHit hit,
                probeDistance,
                placementMask,
                QueryTriggerInteraction.Ignore) ||
            !IsSurfaceValid(hit))
        {
            return false;
        }

        bottomPoint = hit.point + hit.normal.normalized * bottomGroundClearance;
        return true;
    }

    private bool TryFindTopAnchor(
        Vector3 wallReference,
        Vector3 outward,
        float bottomY,
        float preferredTopSurfaceY,
        out Vector3 topAnchor)
    {
        return TryResolveTopAnchorPlacement(
            wallReference,
            outward,
            bottomY,
            preferredTopSurfaceY,
            out topAnchor,
            out _);
    }

    private bool TryResolveTopAnchorPlacement(
        Vector3 wallReference,
        Vector3 outward,
        float bottomY,
        float preferredTopSurfaceY,
        out Vector3 topAnchor,
        out Vector3 topSurfacePoint)
    {
        topAnchor = default;
        topSurfacePoint = default;

        float targetHeight = GetTargetPlacementHeight();
        Vector3 snappedWallReference = SnapAlongEdge(wallReference, outward);
        if (IsResolvedHeightValid(preferredTopSurfaceY - bottomY, targetHeight))
        {
            topAnchor = BuildTopPlacementPoint(snappedWallReference, outward, bottomY + targetHeight);
            topSurfacePoint = BuildTopPlacementPoint(snappedWallReference, outward, preferredTopSurfaceY);
            return true;
        }

        float topProbeLift = Mathf.Max(0.15f, bottomGroundClearance + 0.05f);
        float searchHeight = bottomY + targetHeight + topProbeLift;
        float downwardDistance = Mathf.Max(0.5f, targetHeight + topProbeLift + 1f);
        float step = Mathf.Max(0.05f, topSearchStep);

        for (float depth = 0f; depth <= topSearchDepth + 0.001f; depth += step)
        {
            Vector3 probeOrigin = new Vector3(snappedWallReference.x, searchHeight, snappedWallReference.z) +
                                  outward * topAnchorOutwardOffset -
                                  outward * depth;

            if (!Physics.Raycast(
                    probeOrigin,
                    Vector3.down,
                    out RaycastHit hit,
                    downwardDistance,
                    placementMask,
                    QueryTriggerInteraction.Ignore) ||
                !IsSurfaceValid(hit))
            {
                continue;
            }

            float resolvedHeight = hit.point.y - bottomY;
            if (!IsResolvedHeightValid(resolvedHeight, targetHeight))
            {
                continue;
            }

            topAnchor = BuildTopPlacementPoint(snappedWallReference, outward, bottomY + targetHeight);
            topSurfacePoint = BuildTopPlacementPoint(snappedWallReference, outward, hit.point.y);
            return true;
        }

        return false;
    }

    private Vector3 BuildTopPlacementPoint(Vector3 wallReference, Vector3 outward, float surfaceY)
    {
        return new Vector3(wallReference.x, surfaceY, wallReference.z) + outward * topAnchorOutwardOffset;
    }

    private bool IsResolvedHeightValid(float resolvedHeight, float targetHeight)
    {
        return resolvedHeight >= 0.1f &&
               resolvedHeight <= targetHeight + 0.001f;
    }

    private float GetTargetPlacementHeight()
    {
        return GetClimbColliderSize().y;
    }

    private bool IsPlacementHeightWithinLimits(float height)
    {
        return height >= 0.1f && height <= GetTargetPlacementHeight() + 0.001f;
    }

    private Vector3 GetClimbColliderSize()
    {
        Vector3 size = climbCollider != null ? climbCollider.size : Vector3.one;
        return new Vector3(
            Mathf.Max(0.1f, size.x),
            Mathf.Max(0.5f, size.y),
            Mathf.Max(0.05f, size.z));
    }

    private Vector3 SnapAlongEdge(Vector3 point, Vector3 outward)
    {
        if (!enableEdgeGridSnap || edgeGridStep <= 0.01f)
        {
            return point;
        }

        Vector3 flatOutward = Vector3.ProjectOnPlane(outward, Vector3.up);
        if (flatOutward.sqrMagnitude <= 0.0001f)
        {
            return point;
        }

        flatOutward.Normalize();
        Vector3 edgeTangent = Vector3.Cross(Vector3.up, flatOutward);
        if (edgeTangent.sqrMagnitude <= 0.0001f)
        {
            return point;
        }

        edgeTangent.Normalize();

        Vector3 flatPoint = Vector3.ProjectOnPlane(point, Vector3.up);
        float tangentCoordinate = Vector3.Dot(flatPoint, edgeTangent);
        float outwardCoordinate = Vector3.Dot(flatPoint, flatOutward);
        float snappedTangentCoordinate = Mathf.Round(tangentCoordinate / edgeGridStep) * edgeGridStep;
        Vector3 snappedFlatPoint =
            edgeTangent * snappedTangentCoordinate +
            flatOutward * outwardCoordinate;

        return new Vector3(snappedFlatPoint.x, point.y, snappedFlatPoint.z);
    }

    private bool IsSurfaceValid(RaycastHit hit)
    {
        return hit.collider != null && hit.normal.y >= minimumSurfaceUpDot;
    }

    private void ConfigureNavMeshLink(Vector3 topSurfacePoint, Vector3 bottomPoint, Vector3 outward)
    {
        if (navMeshLink == null)
        {
            return;
        }

        Vector3 horizontalOutward = Vector3.ProjectOnPlane(outward, Vector3.up);
        if (horizontalOutward.sqrMagnitude <= 0.0001f)
        {
            horizontalOutward = transform.forward;
        }

        horizontalOutward.Normalize();

        Vector3 colliderSize = GetClimbColliderSize();
        float topPlatformOffset = Mathf.Max(colliderSize.z * 0.5f, navMeshLinkTopPlatformOffset);
        float bottomOffset = Mathf.Max(0f, navMeshLinkBottomOffset);

        Vector3 bottomWorld = bottomPoint + horizontalOutward * bottomOffset;
        Vector3 topWorld = topSurfacePoint - horizontalOutward * topPlatformOffset;
        bottomWorld = ResolveClosestNavMeshEndpoint(bottomWorld, bottomPoint.y);
        topWorld = ResolveClosestNavMeshEndpoint(topWorld, topSurfacePoint.y);

        navMeshLink.agentTypeID = 0;
        navMeshLink.bidirectional = true;
        navMeshLink.width = colliderSize.x + Mathf.Max(0f, navMeshLinkWidthPadding);
        navMeshLink.autoUpdate = true;
        navMeshLink.startTransform = null;
        navMeshLink.endTransform = null;
        navMeshLink.startPoint = transform.InverseTransformPoint(bottomWorld);
        navMeshLink.endPoint = transform.InverseTransformPoint(topWorld);
        navMeshLink.activated = enableNavMeshLink && !isGrabbed;
        navMeshLink.UpdateLink();
    }

    private static Vector3 ResolveTopSurfacePoint(Vector3 ladderTopPoint, Vector3 requestedTopSurfacePoint)
    {
        Vector3 resolvedTopSurfacePoint = requestedTopSurfacePoint;
        resolvedTopSurfacePoint.y = Mathf.Min(ladderTopPoint.y, requestedTopSurfacePoint.y);
        return resolvedTopSurfacePoint;
    }

    private Vector3 ResolveClosestNavMeshEndpoint(Vector3 fallbackWorldPoint, float expectedSurfaceY)
    {
        if (!enableNavMeshLink || !autoSnapLinkEndpointsToNavMesh)
        {
            return fallbackWorldPoint;
        }

        if (!NavMesh.SamplePosition(fallbackWorldPoint, out NavMeshHit navMeshHit, navMeshEndpointSnapDistance, NavMesh.AllAreas))
        {
            return fallbackWorldPoint;
        }

        if (Mathf.Abs(navMeshHit.position.y - expectedSurfaceY) > navMeshEndpointVerticalTolerance)
        {
            return fallbackWorldPoint;
        }

        return navMeshHit.position;
    }

    private static void DestroyRuntimeSafe(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawPlacementDebug || !hasPlacedConfiguration)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(bottomAnchorWorld, 0.06f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(topAnchorWorld, 0.06f);
        Gizmos.DrawLine(bottomAnchorWorld, topAnchorWorld);
    }
}
