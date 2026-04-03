using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

[System.Serializable]
public enum StandardLadderHeightMode
{
    StretchToFit = 0,
    UseModelHeight = 1
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class StandardLadder : MonoBehaviour, IGrabbable, ICustomGrabPlacement
{
    [Header("Placement")]
    [SerializeField] private LayerMask placementMask = ~0;
    [SerializeField] private bool useAimBasedCardinalDirection = true;
    [SerializeField] private Vector3 fallbackPlacementDirection = Vector3.forward;
    [SerializeField] private float wallSearchDistance = 2.5f;
    [SerializeField, Range(0f, 1f)] private float minimumWallNormalDot = 0.7f;
    [SerializeField, Range(0f, 1f)] private float maximumWallUpDot = 0.35f;
    [SerializeField] private float bottomGroundProbeHeight = 1.5f;
    [SerializeField] private float bottomGroundSearchDistance = 4f;
    [SerializeField] private float bottomOffsetFromWall = 0.35f;
    [SerializeField] private float topSearchDepth = 2f;
    [SerializeField] private float topSearchStep = 0.1f;
    [SerializeField] private float topAnchorOutwardOffset = 0.35f;
    [SerializeField] private float maxLadderHeight = 4f;
    [SerializeField] private float minLadderHeight = 1.5f;
    [SerializeField, Range(0f, 1f)] private float minimumSurfaceUpDot = 0.65f;

    [Header("Grid Snap")]
    [SerializeField] private bool enableEdgeGridSnap = true;
    [SerializeField] private float edgeGridStep = 0.5f;

    [Header("Height Mode")]
    [SerializeField] private StandardLadderHeightMode heightMode = StandardLadderHeightMode.StretchToFit;
    [SerializeField] private float modelHeightPlacementTolerance = 0.25f;

    [Header("Shape")]
    [SerializeField] private float ladderWidth = 0.55f;
    [SerializeField] private float colliderDepth = 0.3f;
    [SerializeField] private float topExitOffset = 0.15f;
    [SerializeField] private float placementPitchOffsetDegrees = 8f;

    [Header("Visual")]
    [SerializeField] private float visualReferenceWidth = 0.55f;
    [SerializeField] private float visualReferenceHeight = 3f;
    [SerializeField] private float visualReferenceDepth = 0.25f;
    [SerializeField] private float proceduralRailThickness = 0.05f;
    [SerializeField] private float proceduralRungThickness = 0.04f;
    [SerializeField] private float proceduralRungSpacing = 0.35f;

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
    [SerializeField] private float detectedModelHeight;

    private Rigidbody cachedRigidbody;
    private BoxCollider climbCollider;
    private Ladder ladder;
    private NavMeshLink navMeshLink;
    private Transform visualsRoot;
    private bool generatedProceduralVisuals;
    private bool capturedVisualBaseTransform;
    private Vector3 baseVisualLocalPosition;
    private Quaternion baseVisualLocalRotation = Quaternion.identity;
    private Vector3 baseVisualLocalScale = Vector3.one;
    private bool hasPlacedConfiguration;
    private bool isGrabbed;
    private bool hasPendingPlacement;
    private Vector3 pendingBottomPoint;
    private Vector3 pendingTopAnchor;
    private Vector3 pendingOutward;

    public Rigidbody Rigidbody => cachedRigidbody;
    public float LadderHeight => ladderHeight;

    private void Awake()
    {
        EnsureReferences();
        ApplyPlacedState(hasPlacedConfiguration);
    }

    private void OnValidate()
    {
        ladderWidth = Mathf.Max(0.1f, ladderWidth);
        colliderDepth = Mathf.Max(0.05f, colliderDepth);
        topExitOffset = Mathf.Max(0f, topExitOffset);
        visualReferenceWidth = Mathf.Max(0.05f, visualReferenceWidth);
        visualReferenceHeight = Mathf.Max(0.25f, visualReferenceHeight);
        visualReferenceDepth = Mathf.Max(0.05f, visualReferenceDepth);
        proceduralRailThickness = Mathf.Max(0.01f, proceduralRailThickness);
        proceduralRungThickness = Mathf.Max(0.01f, proceduralRungThickness);
        proceduralRungSpacing = Mathf.Max(0.1f, proceduralRungSpacing);
        maxLadderHeight = Mathf.Max(0.5f, maxLadderHeight);
        minLadderHeight = Mathf.Clamp(minLadderHeight, 0.5f, maxLadderHeight);
        edgeGridStep = Mathf.Max(0.05f, edgeGridStep);
        modelHeightPlacementTolerance = Mathf.Max(0.01f, modelHeightPlacementTolerance);
    }

    public bool TryGetGrabPlacementPose(Transform aimTransform, LayerMask ignoredPlacementMask, float ignoredMaxDistance, out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;

        if (!TryResolvePlacement(aimTransform, out Vector3 bottomPoint, out Vector3 topAnchor, out Vector3 outward))
        {
            hasPendingPlacement = false;
            return false;
        }

        pendingBottomPoint = bottomPoint;
        pendingTopAnchor = topAnchor;
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
        ApplyPlacedState(hasPlacedConfiguration);
    }

    public void OnGrabCancelled()
    {
        isGrabbed = false;
        hasPendingPlacement = false;
        ApplyPlacedState(hasPlacedConfiguration);
    }

    public void OnGrabPlaced(Vector3 position, Quaternion rotation)
    {
        isGrabbed = false;
        if (hasPendingPlacement)
        {
            ApplyPlacement(pendingBottomPoint, pendingTopAnchor, pendingOutward);
        }
        else
        {
            transform.SetPositionAndRotation(position, rotation);
            ApplyPlacedState(hasPlacedConfiguration);
        }

        hasPendingPlacement = false;
    }

    public void ConfigureDimensions(float width, float depth, float exitOffset)
    {
        ladderWidth = Mathf.Max(0.1f, width);
        colliderDepth = Mathf.Max(0.05f, depth);
        topExitOffset = Mathf.Max(0f, exitOffset);
    }

    public void ConfigureVisualReference(float width, float height, float depth)
    {
        visualReferenceWidth = Mathf.Max(0.05f, width);
        visualReferenceHeight = Mathf.Max(0.25f, height);
        visualReferenceDepth = Mathf.Max(0.05f, depth);
    }

    public void ApplyPlacement(Vector3 bottomPoint, Vector3 topAnchor, Vector3 outward)
    {
        EnsureReferences();

        Vector3 flatOutward = Vector3.ProjectOnPlane(outward, Vector3.up);
        if (flatOutward.sqrMagnitude <= 0.0001f)
        {
            flatOutward = Vector3.forward;
        }

        flatOutward.Normalize();
        bottomAnchorWorld = bottomPoint;
        topAnchorWorld = topAnchor;
        ladderHeight = Mathf.Max(0.5f, topAnchor.y - bottomPoint.y);

        transform.position = Vector3.Lerp(topAnchor, bottomPoint, 0.5f);
        transform.rotation = BuildPlacementRotation(flatOutward);

        climbCollider.center = Vector3.zero;
        climbCollider.size = new Vector3(ladderWidth, ladderHeight, colliderDepth);

        ladder.SetTopHeightOffset(topExitOffset);
        ConfigureNavMeshLink(topAnchor, bottomPoint, flatOutward);
        ApplyVisualLayout();
        hasPlacedConfiguration = true;
        ApplyPlacedState(true);
    }

    private void EnsureReferences()
    {
        cachedRigidbody ??= GetComponent<Rigidbody>();

        if (climbCollider == null)
        {
            climbCollider = GetComponent<BoxCollider>();
            if (climbCollider == null)
            {
                climbCollider = gameObject.AddComponent<BoxCollider>();
            }
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
        CacheDetectedModelHeight();
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

        if (visualsRoot != null && visualsRoot.childCount == 0 && !generatedProceduralVisuals)
        {
            BuildProceduralVisuals();
            generatedProceduralVisuals = true;
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

    private void BuildProceduralVisuals()
    {
        if (visualsRoot == null)
        {
            return;
        }

        float referenceHeight = Mathf.Max(0.5f, visualReferenceHeight);
        float referenceWidth = Mathf.Max(0.1f, visualReferenceWidth);
        float halfHeight = referenceHeight * 0.5f;
        float railInset = Mathf.Max(
            proceduralRailThickness * 0.5f,
            referenceWidth * 0.5f - proceduralRailThickness * 0.5f);

        CreateVisualBlock(
            "RailLeft",
            new Vector3(-railInset, 0f, 0f),
            new Vector3(proceduralRailThickness, referenceHeight, proceduralRailThickness));
        CreateVisualBlock(
            "RailRight",
            new Vector3(railInset, 0f, 0f),
            new Vector3(proceduralRailThickness, referenceHeight, proceduralRailThickness));

        int rungCount = Mathf.Max(2, Mathf.FloorToInt(referenceHeight / proceduralRungSpacing) + 1);
        float usableHeight = Mathf.Max(0f, referenceHeight - proceduralRungThickness);
        float step = rungCount > 1 ? usableHeight / (rungCount - 1) : 0f;
        float startY = halfHeight - proceduralRungThickness * 0.5f;

        for (int i = 0; i < rungCount; i++)
        {
            float y = startY - step * i;
            CreateVisualBlock(
                $"Rung_{i}",
                new Vector3(0f, y, 0f),
                new Vector3(referenceWidth - proceduralRailThickness, proceduralRungThickness, proceduralRungThickness));
        }
    }

    private void CreateVisualBlock(string objectName, Vector3 localPosition, Vector3 localScale)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = objectName;
        block.transform.SetParent(visualsRoot, false);
        block.transform.localPosition = localPosition;
        block.transform.localRotation = Quaternion.identity;
        block.transform.localScale = localScale;
    }

    private void ApplyVisualLayout()
    {
        PrepareVisualsRoot();
        if (visualsRoot == null)
        {
            return;
        }

        float widthScale = ladderWidth / Mathf.Max(0.05f, visualReferenceWidth);
        float heightScale = heightMode == StandardLadderHeightMode.UseModelHeight
            ? 1f
            : ladderHeight / Mathf.Max(0.25f, visualReferenceHeight);
        float depthScale = colliderDepth / Mathf.Max(0.05f, visualReferenceDepth);

        visualsRoot.localPosition = baseVisualLocalPosition;
        visualsRoot.localRotation = baseVisualLocalRotation;
        visualsRoot.localScale = new Vector3(
            baseVisualLocalScale.x * widthScale,
            baseVisualLocalScale.y * heightScale,
            baseVisualLocalScale.z * depthScale);
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

    private bool TryResolvePlacement(Transform aimTransform, out Vector3 bottomPoint, out Vector3 topAnchor, out Vector3 outward)
    {
        bottomPoint = default;
        topAnchor = default;
        outward = Vector3.zero;

        if (aimTransform == null)
        {
            return false;
        }

        float targetHeight = GetTargetPlacementHeight();
        if (heightMode == StandardLadderHeightMode.UseModelHeight &&
            targetHeight > maxLadderHeight)
        {
            return false;
        }

        Vector3 towardWall = ResolvePlacementForward(aimTransform);
        if (towardWall.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        if (!TryFindWall(aimTransform, towardWall, out RaycastHit wallHit, out outward))
        {
            return false;
        }

        Vector3 wallReference = SnapAlongEdge(wallHit.point, outward);
        if (!TryFindBottomPoint(wallHit.point.y, wallReference, outward, out bottomPoint))
        {
            return false;
        }

        if (!TryFindTopAnchor(wallReference, outward, bottomPoint.y, out topAnchor))
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

    private bool TryFindWall(Transform aimTransform, Vector3 towardWall, out RaycastHit wallHit, out Vector3 outward)
    {
        wallHit = default;
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
            if (!IsWallValid(hit, towardWall))
            {
                continue;
            }

            wallHit = hit;
            outward = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
            if (outward.sqrMagnitude <= 0.0001f)
            {
                outward = -towardWall;
            }

            outward.Normalize();
            return true;
        }

        return false;
    }

    private bool TryFindBottomPoint(float probeBaseY, Vector3 wallReference, Vector3 outward, out Vector3 bottomPoint)
    {
        bottomPoint = default;

        Vector3 probeBase = wallReference + outward * bottomOffsetFromWall;
        Vector3 probeOrigin = new Vector3(
            probeBase.x,
            probeBaseY + Mathf.Max(0.1f, bottomGroundProbeHeight),
            probeBase.z);
        float probeDistance = Mathf.Max(0.1f, bottomGroundProbeHeight + bottomGroundSearchDistance);

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

        bottomPoint = hit.point;
        return true;
    }

    private bool TryFindTopAnchor(Vector3 wallReference, Vector3 outward, float bottomY, out Vector3 topAnchor)
    {
        topAnchor = default;

        float targetHeight = GetTargetPlacementHeight();
        float searchHeight = heightMode == StandardLadderHeightMode.UseModelHeight
            ? bottomY + targetHeight + modelHeightPlacementTolerance
            : bottomY + maxLadderHeight;
        float downwardDistance = heightMode == StandardLadderHeightMode.UseModelHeight
            ? Mathf.Max(0.5f, targetHeight + modelHeightPlacementTolerance + 1f)
            : Mathf.Max(0.5f, maxLadderHeight + 1f);
        float step = Mathf.Max(0.05f, topSearchStep);
        Vector3 snappedWallReference = SnapAlongEdge(wallReference, outward);

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

            float anchoredTopY = heightMode == StandardLadderHeightMode.UseModelHeight
                ? bottomY + targetHeight
                : hit.point.y;

            topAnchor = new Vector3(snappedWallReference.x, anchoredTopY, snappedWallReference.z) +
                        outward * topAnchorOutwardOffset;
            return true;
        }

        return false;
    }

    private bool IsResolvedHeightValid(float resolvedHeight, float targetHeight)
    {
        if (!IsPlacementHeightWithinLimits(resolvedHeight))
        {
            return false;
        }

        if (heightMode != StandardLadderHeightMode.UseModelHeight)
        {
            return true;
        }

        return Mathf.Abs(resolvedHeight - targetHeight) <= modelHeightPlacementTolerance;
    }

    private float GetTargetPlacementHeight()
    {
        if (heightMode != StandardLadderHeightMode.UseModelHeight)
        {
            return maxLadderHeight;
        }

        CacheDetectedModelHeight();
        return Mathf.Max(0.1f, detectedModelHeight > 0.05f ? detectedModelHeight : visualReferenceHeight);
    }

    private bool IsPlacementHeightWithinLimits(float height)
    {
        if (heightMode == StandardLadderHeightMode.UseModelHeight)
        {
            return height >= 0.1f && height <= maxLadderHeight;
        }

        return height >= minLadderHeight && height <= maxLadderHeight;
    }

    private void CacheDetectedModelHeight()
    {
        if (visualsRoot == null)
        {
            return;
        }

        float maxHeight = 0f;
        bool foundSource = false;

        Renderer[] renderers = visualsRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            float scaleY = Mathf.Max(0.0001f, visualsRoot.lossyScale.y);
            float candidateHeight = renderer.bounds.size.y / scaleY;
            if (candidateHeight <= 0.01f)
            {
                continue;
            }

            maxHeight = Mathf.Max(maxHeight, candidateHeight);
            foundSource = true;
        }

        if (!foundSource)
        {
            maxHeight = Mathf.Max(maxHeight, visualReferenceHeight);
        }

        detectedModelHeight = Mathf.Max(0.05f, maxHeight);
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

    private bool IsWallValid(RaycastHit hit, Vector3 towardWall)
    {
        if (hit.collider == null)
        {
            return false;
        }

        Vector3 flatNormal = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
        if (flatNormal.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        flatNormal.Normalize();
        return Mathf.Abs(hit.normal.y) <= maximumWallUpDot &&
               Vector3.Dot(flatNormal, -towardWall) >= minimumWallNormalDot;
    }

    private bool IsSurfaceValid(RaycastHit hit)
    {
        return hit.collider != null && hit.normal.y >= minimumSurfaceUpDot;
    }

    private void ConfigureNavMeshLink(Vector3 topAnchor, Vector3 bottomPoint, Vector3 outward)
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

        float topPlatformOffset = Mathf.Max(colliderDepth * 0.5f, navMeshLinkTopPlatformOffset);
        float bottomOffset = Mathf.Max(0f, navMeshLinkBottomOffset);

        Vector3 bottomWorld = bottomPoint + horizontalOutward * bottomOffset;
        Vector3 topWorld = topAnchor - horizontalOutward * topPlatformOffset;
        bottomWorld = ResolveClosestNavMeshEndpoint(bottomWorld, bottomPoint.y);
        topWorld = ResolveClosestNavMeshEndpoint(topWorld, topAnchor.y);

        navMeshLink.agentTypeID = 0;
        navMeshLink.bidirectional = true;
        navMeshLink.width = ladderWidth + Mathf.Max(0f, navMeshLinkWidthPadding);
        navMeshLink.autoUpdate = true;
        navMeshLink.startTransform = null;
        navMeshLink.endTransform = null;
        navMeshLink.startPoint = transform.InverseTransformPoint(bottomWorld);
        navMeshLink.endPoint = transform.InverseTransformPoint(topWorld);
        navMeshLink.activated = enableNavMeshLink && !isGrabbed;
        navMeshLink.UpdateLink();
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
