using UnityEngine;

[DisallowMultipleComponent]
public class RopeLadderPlaceable : PlaceableItem
{
    [Header("Placement")]
    [SerializeField] private LayerMask placementMask = ~0;
    [SerializeField] private bool useAimBasedCardinalDirection = true;
    [SerializeField] private Vector3 fallbackPlacementDirection = Vector3.forward;
    [SerializeField] private float topProbeHeight = 1.2f;
    [SerializeField] private float topProbeDepth = 2f;
    [SerializeField] private float edgeSearchDistance = 1.25f;
    [SerializeField] private float edgeSearchStep = 0.1f;
    [SerializeField] private float ladderOutwardOffset = 0.35f;
    [SerializeField] private float maxDropDistance = 20f;
    [SerializeField] private float minDropHeight = 2f;
    [SerializeField, Range(0f, 1f)] private float minimumSurfaceUpDot = 0.65f;

    [Header("Grid Snap")]
    [SerializeField] private bool enableEdgeGridSnap = true;
    [SerializeField] private float edgeGridStep = 0.5f;

    [Header("Ladder")]
    [SerializeField] private RopeLadderDeployed deployedLadderPrefab;
    [SerializeField] private float ladderWidth = 0.45f;
    [SerializeField] private float rungSpacing = 0.35f;
    [SerializeField] private float railThickness = 0.04f;
    [SerializeField] private float rungThickness = 0.03f;
    [SerializeField] private float colliderDepth = 0.2f;
    [SerializeField] private float topExitOffset = 0.15f;

    [Header("Navigation")]
    [SerializeField] private bool enableNavMeshLink = true;
    [SerializeField] private float navMeshLinkWidthPadding = 0.2f;
    [SerializeField] private float navMeshLinkBottomOffset = 0.15f;
    [SerializeField] private float navMeshLinkTopPlatformOffset = 0.6f;
    [SerializeField] private bool autoSnapLinkEndpointsToNavMesh = true;
    [SerializeField] private float navMeshEndpointSnapDistance = 1f;
    [SerializeField] private float navMeshEndpointVerticalTolerance = 1f;

    [Header("Preview")]
    [SerializeField] private bool enableGhostPreview = true;
    [SerializeField] private Color ghostPreviewColor = new Color(0.45f, 0.95f, 0.65f, 0.4f);

    [Header("Debug")]
    [SerializeField] private bool drawPlacementDebug;

    private Vector3 lastDebugTopAnchor;
    private Vector3 lastDebugBottomPoint;
    private bool hadLastPlacement;
    private RopeLadderDeployed ghostPreview;

    private void Update()
    {
        UpdateGhostPreview();
    }

    protected override bool TryPlace(GameObject user)
    {
        if (!TryResolvePlacement(user, out Vector3 topAnchor, out Vector3 bottomPoint, out Vector3 forward))
        {
            hadLastPlacement = false;
            return false;
        }

        RopeLadderDeployed deployed = CreateDeployedLadder(preview: false);
        deployed.ApplyDimensions(ladderWidth, rungSpacing, railThickness, rungThickness, colliderDepth, topExitOffset);
        deployed.ApplyNavigationSettings(
            enableNavMeshLink,
            navMeshLinkWidthPadding,
            navMeshLinkBottomOffset,
            navMeshLinkTopPlatformOffset,
            autoSnapLinkEndpointsToNavMesh,
            navMeshEndpointSnapDistance,
            navMeshEndpointVerticalTolerance);
        deployed.Configure(topAnchor, bottomPoint, forward);

        lastDebugTopAnchor = topAnchor;
        lastDebugBottomPoint = bottomPoint;
        hadLastPlacement = true;
        HideGhostPreview(destroyPreview: true);
        return true;
    }

    private Vector3 ResolvePlacementForward(GameObject user)
    {
        Vector3 forward = fallbackPlacementDirection;
        if (useAimBasedCardinalDirection && TryResolveAimForward(user, out Vector3 aimForward))
        {
            forward = aimForward;
        }

        forward = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (forward.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        return QuantizeToCardinalDirection(forward.normalized);
    }

    private bool TryResolveAimForward(GameObject user, out Vector3 forward)
    {
        forward = Vector3.zero;
        if (user == null)
        {
            return false;
        }

        Transform cameraRoot = user.transform.Find("PlayerCameraRoot");
        if (cameraRoot != null)
        {
            forward = cameraRoot.forward;
            return true;
        }

        Camera cameraComponent = user.GetComponentInChildren<Camera>();
        if (cameraComponent != null)
        {
            forward = cameraComponent.transform.forward;
            return true;
        }

        forward = user.transform.forward;
        return forward.sqrMagnitude > 0.0001f;
    }

    private static Vector3 QuantizeToCardinalDirection(Vector3 forward)
    {
        if (Mathf.Abs(forward.z) >= Mathf.Abs(forward.x))
        {
            return forward.z >= 0f ? Vector3.forward : Vector3.back;
        }

        return forward.x >= 0f ? Vector3.right : Vector3.left;
    }

    private bool TryFindTopAnchor(Transform userTransform, Vector3 forward, out Vector3 topAnchor)
    {
        topAnchor = default;
        if (userTransform == null)
        {
            return false;
        }

        RaycastHit lastGroundHit = default;
        bool hadGround = false;
        Vector3 probeBase = userTransform.position + Vector3.up * Mathf.Max(0.1f, topProbeHeight);

        for (float distance = 0f; distance <= edgeSearchDistance + 0.001f; distance += Mathf.Max(0.05f, edgeSearchStep))
        {
            Vector3 probeOrigin = probeBase + forward * distance;
            if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit hit, topProbeDepth, placementMask, QueryTriggerInteraction.Ignore) &&
                IsSurfaceValid(hit))
            {
                hadGround = true;
                lastGroundHit = hit;
                continue;
            }

            if (!hadGround)
            {
                continue;
            }

            topAnchor = lastGroundHit.point + forward * ladderOutwardOffset;
            topAnchor = SnapTopAnchorAlongEdge(topAnchor, forward);
            return true;
        }

        return false;
    }

    private Vector3 SnapTopAnchorAlongEdge(Vector3 topAnchor, Vector3 forward)
    {
        if (!enableEdgeGridSnap || edgeGridStep <= 0.01f)
        {
            return topAnchor;
        }

        Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (flatForward.sqrMagnitude <= 0.0001f)
        {
            return topAnchor;
        }

        flatForward.Normalize();
        Vector3 edgeTangent = Vector3.Cross(Vector3.up, flatForward);
        if (edgeTangent.sqrMagnitude <= 0.0001f)
        {
            return topAnchor;
        }

        edgeTangent.Normalize();

        Vector3 flatAnchor = Vector3.ProjectOnPlane(topAnchor, Vector3.up);
        float tangentCoordinate = Vector3.Dot(flatAnchor, edgeTangent);
        float forwardCoordinate = Vector3.Dot(flatAnchor, flatForward);
        float snappedTangentCoordinate = Mathf.Round(tangentCoordinate / edgeGridStep) * edgeGridStep;

        Vector3 snappedFlatAnchor =
            edgeTangent * snappedTangentCoordinate +
            flatForward * forwardCoordinate;

        return new Vector3(snappedFlatAnchor.x, topAnchor.y, snappedFlatAnchor.z);
    }

    private bool TryFindBottomPoint(Vector3 topAnchor, out Vector3 bottomPoint)
    {
        bottomPoint = default;
        RaycastHit[] hits = Physics.RaycastAll(
            topAnchor + Vector3.up * 0.05f,
            Vector3.down,
            maxDropDistance,
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
            if (!IsSurfaceValid(hit))
            {
                continue;
            }

            if (topAnchor.y - hit.point.y < minDropHeight)
            {
                continue;
            }

            bottomPoint = hit.point;
            return true;
        }

        return false;
    }

    private bool IsSurfaceValid(RaycastHit hit)
    {
        return hit.collider != null && hit.normal.y >= minimumSurfaceUpDot;
    }

    private bool TryResolvePlacement(GameObject user, out Vector3 topAnchor, out Vector3 bottomPoint, out Vector3 forward)
    {
        topAnchor = default;
        bottomPoint = default;
        forward = Vector3.zero;

        if (user == null)
        {
            return false;
        }

        forward = ResolvePlacementForward(user);
        if (forward.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        if (!TryFindTopAnchor(user.transform, forward, out topAnchor))
        {
            return false;
        }

        if (!TryFindBottomPoint(topAnchor, out bottomPoint))
        {
            return false;
        }

        return topAnchor.y - bottomPoint.y >= minDropHeight;
    }

    private RopeLadderDeployed CreateDeployedLadder(bool preview)
    {
        RopeLadderDeployed deployed = deployedLadderPrefab != null
            ? Instantiate(deployedLadderPrefab)
            : new GameObject("RopeLadder_Deployed").AddComponent<RopeLadderDeployed>();

        deployed.name = preview
            ? "RopeLadder_Preview"
            : deployedLadderPrefab != null ? deployedLadderPrefab.name : "RopeLadder_Deployed";

        if (preview)
        {
            deployed.SetPreviewMode(true, ghostPreviewColor);
        }

        return deployed;
    }

    private void UpdateGhostPreview()
    {
        if (!enableGhostPreview || !ShouldShowGhostPreview())
        {
            HideGhostPreview(destroyPreview: false);
            return;
        }

        if (!TryResolvePlacement(CurrentHolder, out Vector3 topAnchor, out Vector3 bottomPoint, out Vector3 forward))
        {
            HideGhostPreview(destroyPreview: false);
            return;
        }

        RopeLadderDeployed preview = EnsureGhostPreview();
        preview.ApplyDimensions(ladderWidth, rungSpacing, railThickness, rungThickness, colliderDepth, topExitOffset);
        preview.ApplyNavigationSettings(
            enableNavMeshLink,
            navMeshLinkWidthPadding,
            navMeshLinkBottomOffset,
            navMeshLinkTopPlatformOffset,
            autoSnapLinkEndpointsToNavMesh,
            navMeshEndpointSnapDistance,
            navMeshEndpointVerticalTolerance);
        preview.SetPreviewMode(true, ghostPreviewColor);
        preview.Configure(topAnchor, bottomPoint, forward);
        if (!preview.gameObject.activeSelf)
        {
            preview.gameObject.SetActive(true);
        }
    }

    private bool ShouldShowGhostPreview()
    {
        if (CurrentHolder == null)
        {
            return false;
        }

        if (!CurrentHolder.TryGetComponent(out FPSInventorySystem inventory))
        {
            return false;
        }

        return inventory.HeldObject == gameObject;
    }

    private RopeLadderDeployed EnsureGhostPreview()
    {
        if (ghostPreview != null)
        {
            return ghostPreview;
        }

        ghostPreview = CreateDeployedLadder(preview: true);
        ghostPreview.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        return ghostPreview;
    }

    private void HideGhostPreview(bool destroyPreview)
    {
        if (ghostPreview == null)
        {
            return;
        }

        if (destroyPreview)
        {
            DestroyRuntimeSafe(ghostPreview.gameObject);
            ghostPreview = null;
            return;
        }

        if (ghostPreview.gameObject.activeSelf)
        {
            ghostPreview.gameObject.SetActive(false);
        }
    }

    public override void OnDrop(GameObject dropper)
    {
        HideGhostPreview(destroyPreview: true);
        base.OnDrop(dropper);
    }

    protected override void ConsumePlacedItem(GameObject user)
    {
        HideGhostPreview(destroyPreview: true);
        base.ConsumePlacedItem(user);
    }

    private void OnDestroy()
    {
        HideGhostPreview(destroyPreview: true);
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
        if (!drawPlacementDebug || !hadLastPlacement)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(lastDebugTopAnchor, 0.06f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(lastDebugBottomPoint, 0.06f);
        Gizmos.DrawLine(lastDebugTopAnchor, lastDebugBottomPoint);
    }
}
