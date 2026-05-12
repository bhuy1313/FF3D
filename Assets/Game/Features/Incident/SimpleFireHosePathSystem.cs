using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class SimpleFireHosePathSystem : MonoBehaviour
{
    [Header("Session")]
    [Tooltip("Require the interactor to have empty hands before starting a hose path session.")]
    [SerializeField] private bool requireEmptyHands = true;
    [Tooltip("Only the player who started the session can finish it at a connect point.")]
    [SerializeField] private bool onlyOwningInteractorCanComplete = true;
    [Tooltip("Allow canceling the current recording session with a keyboard key.")]
    [SerializeField] private bool allowCancelInput = true;
    [Tooltip("Keyboard key used to cancel/drop the current recording session.")]
    [SerializeField] private KeyCode cancelSessionKey = KeyCode.Q;

    [Header("Ground Probe")]
    [Tooltip("Layers considered valid ground for pickup, preview, and committed hose projection.")]
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("Height above a sample point used when raycasting downward to find the ground.")]
    [SerializeField] private float raycastHeight = 2f;
    [Tooltip("Maximum downward ray distance used to find the ground.")]
    [SerializeField] private float raycastDistance = 6f;

    [Header("Knot Recording")]
    [Tooltip("Minimum travel distance before a new knot is added under normal conditions.")]
    [SerializeField] private float knotSpacing = 1f;
    [Tooltip("Force a knot when the ground normal changes more than this angle.")]
    [SerializeField] private float normalThreshold = 15f;
    [Tooltip("Force a knot when the sampled ground height changes more than this amount.")]
    [SerializeField] private float heightThreshold = 0.3f;
    [Tooltip("Minimum movement required before slope/height changes are allowed to create a break knot.")]
    [SerializeField] private float minDistanceBeforeBreak = 0.3f;

    [Header("Held Head")]
    [Tooltip("Optional visual shown near the player's hands while the path is being recorded.")]
    [SerializeField] private GameObject carriedHeadVisualPrefab;
    [Tooltip("Local-style offset from the interactor used for the carried head visual.")]
    [SerializeField] private Vector3 carriedHeadOffset = new Vector3(0.35f, 1.15f, 0.55f);
    [Tooltip("Leave a dropped head visual behind when the recording is canceled at the pickup point.")]
    [SerializeField] private bool spawnDroppedHeadVisual = true;

    [Header("Preview Markers")]
    [Tooltip("Spacing used when smoothing the recorded path for preview markers.")]
    [SerializeField] private float previewSampleSpacing = 0.4f;
    [Tooltip("Prefab spawned repeatedly along the recorded preview path.")]
    [SerializeField] private GameObject previewMarkerPrefab;
    [Tooltip("World spacing between preview arrow markers.")]
    [SerializeField] private float previewMarkerSpacing = 1.2f;
    [Tooltip("Small offset applied after projecting preview markers to the ground.")]
    [SerializeField] private Vector3 previewMarkerOffset = new Vector3(0f, 0.02f, 0f);
    [Tooltip("Prefer runtime URP DecalProjector preview markers over the legacy mesh prefab when a decal base material is assigned.")]
    [SerializeField] private bool preferDecalPreviewMarkers = true;
    [Tooltip("URP Decal material used as the base for runtime hose path preview markers.")]
    [SerializeField] private Material previewDecalBaseMaterial;
    [Tooltip("Projected width/length of each runtime preview decal marker.")]
    [SerializeField] private Vector2 previewDecalSize = new Vector2(0.55f, 1.35f);
    [Tooltip("Extra rotation applied around the surface normal so the decal art aligns with path direction.")]
    [SerializeField] private float previewDecalRotationDegrees = -90f;
    [Tooltip("Projection depth used by each runtime preview decal marker.")]
    [SerializeField] private float previewDecalDepth = 0.3f;
    [Tooltip("How far runtime preview decals should render.")]
    [SerializeField] private float previewDecalDrawDistance = 40f;
    [Tooltip("Fade blend used by runtime preview decals near the screen edges.")]
    [SerializeField] private float previewDecalFadeScale = 0.35f;
    [Tooltip("Surface offset used by runtime preview decals to avoid z-fighting.")]
    [SerializeField] private float previewDecalSurfaceOffset = 0.015f;
    [Tooltip("Start angle fade used by runtime preview decals on steep edges.")]
    [SerializeField] [Range(0f, 180f)] private float previewDecalStartAngleFade = 45f;
    [Tooltip("End angle fade used by runtime preview decals on steep edges.")]
    [SerializeField] [Range(0f, 180f)] private float previewDecalEndAngleFade = 70f;

    [Header("Committed Hose")]
    [Tooltip("Material used by the final generated hose mesh after connection is completed.")]
    [SerializeField] private Material hoseMaterial;
    [Tooltip("Spacing used when resampling the final hose path before mesh generation.")]
    [SerializeField] private float hoseSampleSpacing = 0.5f;
    [Tooltip("Radius of the generated hose mesh.")]
    [SerializeField] private float hoseRadius = 0.12f;
    [Tooltip("Number of radial sides used by the generated hose mesh.")]
    [SerializeField] private int hoseRadialSegments = 12;
    [Tooltip("Lift applied from the ground normal so the committed hose does not z-fight with the floor.")]
    [SerializeField] private float hoseSurfaceOffset = 0.03f;
    [Tooltip("How far from each endpoint anchor the transition control point should be placed.")]
    [SerializeField] private float endpointTransitionDistance = 0.45f;
    [Tooltip("How much the endpoint transition control point should be lifted along the ground normal.")]
    [SerializeField] private float endpointTransitionLift = 0.18f;

    [Header("Runtime")]
    [SerializeField] private bool sessionActive;
    [SerializeField] private GameObject activeInteractor;
    [SerializeField] private SimpleFireHosePickupPoint activePickupPoint;

    private readonly List<Knot> recordedKnots = new List<Knot>(128);
    private readonly List<Vector3> previewPoints = new List<Vector3>(256);
    private readonly List<GameObject> previewMarkers = new List<GameObject>(128);
    private readonly List<GameObject> droppedHeadVisuals = new List<GameObject>(16);

    private Transform previewRoot;
    private Transform committedRoot;
    private GameObject carriedHeadVisualInstance;
    private bool warnedMissingPreviewDecalMaterial;

    private Vector3 lastKnotPosition;
    private Vector3 lastKnotNormal = Vector3.up;
    private Vector3 lastSamplePoint;
    private float distanceSinceLastKnot;
    private bool hasFirstKnot;

    public bool HasActiveSession => sessionActive;
    public GameObject ActiveInteractor => activeInteractor;

    private void Awake()
    {
        ClampPreviewSettings();
        EnsureRuntimeRoots();
    }

    private void OnValidate()
    {
        ClampPreviewSettings();
    }

    private void OnDestroy()
    {
    }

    private void Update()
    {
        if (!sessionActive)
        {
            return;
        }

        if (activeInteractor == null)
        {
            CancelActiveSession();
            return;
        }

        if (ShouldConsumeCancelInput())
        {
            TryDropSession(activeInteractor);
            return;
        }

        UpdateCarriedHeadVisual();
        RecordCurrentGroundPoint();
        RenderPreview();
    }

    public bool TryBeginSession(SimpleFireHosePickupPoint pickupPoint, GameObject interactor)
    {
        if (sessionActive || pickupPoint == null || interactor == null)
        {
            return false;
        }

        if (requireEmptyHands &&
            interactor.TryGetComponent(out FPSInventorySystem inventory) &&
            inventory.HeldObject != null)
        {
            return false;
        }

        if (!pickupPoint.TryResolveStartPose(out Vector3 startPosition, out Vector3 startNormal))
        {
            return false;
        }

        EnsureRuntimeRoots();
        ClearPreviewMarkers();

        activePickupPoint = pickupPoint;
        activeInteractor = interactor;
        sessionActive = true;

        recordedKnots.Clear();
        previewPoints.Clear();
        distanceSinceLastKnot = 0f;
        hasFirstKnot = false;

        AddKnot(startPosition, startNormal);
        hasFirstKnot = true;
        lastSamplePoint = startPosition;

        CreateCarriedHeadVisual();
        RenderPreview();
        return true;
    }

    public bool TryCompleteSession(SimpleFireHoseConnectPoint connectPoint, GameObject interactor)
    {
        if (!sessionActive || connectPoint == null || interactor == null)
        {
            return false;
        }

        if (onlyOwningInteractorCanComplete && interactor != activeInteractor)
        {
            return false;
        }

        if (!connectPoint.TryResolveConnectPose(out Vector3 connectPosition, out Vector3 connectNormal))
        {
            return false;
        }

        TryAppendTerminalKnot(connectPosition, connectNormal);
        BuildCommittedHose(connectPoint);
        connectPoint.SetOccupied(true);
        EndActiveSession();
        return true;
    }

    [ContextMenu("Cancel Active Session")]
    public void CancelActiveSession()
    {
        EndActiveSession();
        recordedKnots.Clear();
    }

    public bool TryDropSession(GameObject interactor)
    {
        if (!sessionActive || interactor == null || interactor != activeInteractor)
        {
            return false;
        }

        SpawnDroppedHeadVisualAtLastKnot();
        EndActiveSession();
        recordedKnots.Clear();
        return true;
    }

    private bool ShouldConsumeCancelInput()
    {
        if (!allowCancelInput || activeInteractor == null || cancelSessionKey == KeyCode.None)
        {
            return false;
        }

        if (activeInteractor.GetComponent("BotCommandAgent") != null)
        {
            return false;
        }

        return Input.GetKeyDown(cancelSessionKey);
    }

    private void RecordCurrentGroundPoint()
    {
        if (!TryProbeGround(activeInteractor.transform.position, out RaycastHit hit))
        {
            return;
        }

        Vector3 currentPoint = hit.point;
        Vector3 currentNormal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;

        if (!hasFirstKnot)
        {
            AddKnot(currentPoint, currentNormal);
            hasFirstKnot = true;
            return;
        }

        float distance = Vector3.Distance(lastSamplePoint, currentPoint);
        distanceSinceLastKnot += distance;
        lastSamplePoint = currentPoint;

        float localAngle = Vector3.Angle(lastKnotNormal, currentNormal);
        float localHeightDelta = Mathf.Abs(currentPoint.y - lastKnotPosition.y);

        bool spacingRule = distanceSinceLastKnot >= Mathf.Max(0.05f, knotSpacing);
        bool breakRule =
            distanceSinceLastKnot > Mathf.Max(0.01f, minDistanceBeforeBreak) &&
            (localAngle > normalThreshold || localHeightDelta > heightThreshold);

        if (spacingRule || breakRule)
        {
            AddKnot(currentPoint, currentNormal);
        }
    }

    private void TryAppendTerminalKnot(Vector3 worldPosition, Vector3 worldNormal)
    {
        if (recordedKnots.Count == 0)
        {
            AddKnot(worldPosition, worldNormal);
            return;
        }

        Knot lastKnot = recordedKnots[recordedKnots.Count - 1];
        if (Vector3.Distance(lastKnot.Position, worldPosition) <= 0.05f)
        {
            return;
        }

        AddKnot(worldPosition, worldNormal);
    }

    private void AddKnot(Vector3 worldPosition, Vector3 worldNormal)
    {
        Vector3 normalizedNormal = worldNormal.sqrMagnitude > 0.0001f ? worldNormal.normalized : Vector3.up;
        Knot knot = new Knot(worldPosition, normalizedNormal);
        recordedKnots.Add(knot);
        lastKnotPosition = worldPosition;
        lastKnotNormal = normalizedNormal;
        lastSamplePoint = worldPosition;
        distanceSinceLastKnot = 0f;
    }

    private bool TryProbeGround(Vector3 nearWorldPosition, out RaycastHit hit)
    {
        Vector3 origin = nearWorldPosition + Vector3.up * Mathf.Max(0.1f, raycastHeight);
        return Physics.Raycast(origin, Vector3.down, out hit, Mathf.Max(0.1f, raycastDistance), groundMask, QueryTriggerInteraction.Ignore);
    }

    public bool TryProjectPointToGround(Vector3 worldPosition, out RaycastHit hit)
    {
        return TryProbeGround(worldPosition, out hit);
    }

    private void EnsureRuntimeRoots()
    {
        if (previewRoot == null)
        {
            GameObject previewObject = new GameObject("SimpleFireHosePreview");
            previewObject.transform.SetParent(transform, false);
            previewRoot = previewObject.transform;
        }

        if (committedRoot == null)
        {
            GameObject committedObject = new GameObject("SimpleFireHoseCommitted");
            committedObject.transform.SetParent(transform, false);
            committedRoot = committedObject.transform;
        }
    }

    private void RenderPreview()
    {
        BuildPreviewPoints();
        if (previewPoints.Count <= 1)
        {
            ClearPreviewMarkers();
            return;
        }

        UpdatePreviewMarkers();
    }

    private void BuildPreviewPoints()
    {
        previewPoints.Clear();
        if (recordedKnots.Count < 2)
        {
            return;
        }

        List<Vector3> resampled = FireHosePathSampler.Resample(recordedKnots, Mathf.Max(0.02f, previewSampleSpacing));
        List<Vector3> smoothed = FireHosePathSampler.CatmullRom(resampled);
        if (smoothed.Count == 0)
        {
            smoothed = resampled;
        }

        for (int i = 0; i < smoothed.Count; i++)
        {
            previewPoints.Add(ProjectPointToGround(smoothed[i]));
        }
    }

    private void UpdatePreviewMarkers()
    {
        if (previewMarkerPrefab == null || previewPoints.Count <= 1)
        {
            ClearPreviewMarkers();
            return;
        }

        int markerIndex = 0;
        float spacing = Mathf.Max(0.1f, previewMarkerSpacing);
        float carryDistance = 0f;

        for (int i = 1; i < previewPoints.Count; i++)
        {
            Vector3 from = previewPoints[i - 1];
            Vector3 to = previewPoints[i];
            Vector3 segment = to - from;
            float length = segment.magnitude;
            if (length <= 0.001f)
            {
                continue;
            }

            Vector3 forward = segment / length;
            float distanceAlongSegment = spacing - carryDistance;
            while (distanceAlongSegment <= length)
            {
                Vector3 markerPosition = from + forward * distanceAlongSegment;
                PlacePreviewMarker(markerIndex, markerPosition, forward);
                markerIndex++;
                distanceAlongSegment += spacing;
            }

            carryDistance = length - (distanceAlongSegment - spacing);
            if (carryDistance >= spacing)
            {
                carryDistance = 0f;
            }
        }

        for (int i = markerIndex; i < previewMarkers.Count; i++)
        {
            if (previewMarkers[i] != null)
            {
                previewMarkers[i].SetActive(false);
            }
        }
    }

    private void PlacePreviewMarker(int index, Vector3 worldPosition, Vector3 forward)
    {
        GameObject marker = GetOrCreatePreviewMarker(index);
        if (marker == null)
        {
            return;
        }

        marker.SetActive(true);
        Transform markerTransform = marker.transform;

        Vector3 projectedPosition = worldPosition;
        Vector3 projectedUp = Vector3.up;
        if (TryProbeGround(worldPosition, out RaycastHit hit))
        {
            projectedPosition = hit.point;
            projectedUp = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
        }

        Vector3 surfacePosition = projectedPosition + previewMarkerOffset;
        Vector3 flattenedForward = Vector3.ProjectOnPlane(forward, projectedUp);
        if (flattenedForward.sqrMagnitude <= 0.001f)
        {
            flattenedForward = ResolvePreviewTangent(projectedUp);
        }

        if (marker.TryGetComponent(out DecalProjector decalProjector))
        {
            PlacePreviewDecalMarker(decalProjector, surfacePosition, projectedUp, flattenedForward.normalized);
            return;
        }

        markerTransform.position = surfacePosition;
        markerTransform.rotation = Quaternion.LookRotation(flattenedForward.normalized, projectedUp);
    }

    private GameObject GetOrCreatePreviewMarker(int index)
    {
        while (previewMarkers.Count <= index)
        {
            GameObject marker = CreatePreviewMarker();
            if (marker == null)
            {
                return null;
            }

            marker.SetActive(false);
            previewMarkers.Add(marker);
        }

        return previewMarkers[index];
    }

    private GameObject CreatePreviewMarker()
    {
        int markerIndex = previewMarkers.Count;
        if (ShouldUseRuntimeDecalPreview())
        {
            GameObject marker = new GameObject($"SimpleFireHosePathDecal_{markerIndex}");
            marker.transform.SetParent(previewRoot, false);

            DecalProjector projector = marker.AddComponent<DecalProjector>();
            ConfigurePreviewDecalProjector(projector);
            return marker;
        }

        if (previewMarkerPrefab == null)
        {
            return null;
        }

        GameObject markerInstance = Instantiate(previewMarkerPrefab, previewRoot);
        markerInstance.name = $"{previewMarkerPrefab.name}_{markerIndex}";
        if (markerInstance.TryGetComponent(out DecalProjector projectorComponent))
        {
            ConfigurePreviewDecalProjector(projectorComponent);
        }

        return markerInstance;
    }

    private void ClearPreviewMarkers()
    {
        for (int i = 0; i < previewMarkers.Count; i++)
        {
            if (previewMarkers[i] != null)
            {
                previewMarkers[i].SetActive(false);
            }
        }
    }

    private void CreateCarriedHeadVisual()
    {
        if (carriedHeadVisualPrefab == null || activeInteractor == null)
        {
            return;
        }

        if (carriedHeadVisualInstance == null)
        {
            carriedHeadVisualInstance = Instantiate(carriedHeadVisualPrefab, previewRoot);
        }

        carriedHeadVisualInstance.SetActive(true);
        UpdateCarriedHeadVisual();
    }

    private void SpawnDroppedHeadVisualAtLastKnot()
    {
        if (!spawnDroppedHeadVisual ||
            carriedHeadVisualPrefab == null ||
            previewRoot == null ||
            recordedKnots.Count == 0)
        {
            return;
        }

        Knot lastKnot = recordedKnots[recordedKnots.Count - 1];
        Vector3 dropPosition = lastKnot.Position;
        Vector3 up = lastKnot.Normal.sqrMagnitude > 0.0001f ? lastKnot.Normal.normalized : Vector3.up;
        if (TryProbeGround(lastKnot.Position, out RaycastHit hit))
        {
            dropPosition = hit.point;
            up = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
        }

        GameObject droppedHead = Instantiate(carriedHeadVisualPrefab, previewRoot);
        droppedHead.name = $"{carriedHeadVisualPrefab.name}_Dropped_{droppedHeadVisuals.Count}";

        Vector3 forward = transform.forward.sqrMagnitude > 0.001f
            ? Vector3.ProjectOnPlane(transform.forward, up).normalized
            : Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
        if (forward.sqrMagnitude <= 0.001f)
        {
            forward = Vector3.Cross(up, Vector3.right).normalized;
        }

        droppedHead.transform.SetPositionAndRotation(
            dropPosition + up * 0.03f,
            Quaternion.LookRotation(forward, up));
        droppedHeadVisuals.Add(droppedHead);
    }

    private void UpdateCarriedHeadVisual()
    {
        if (carriedHeadVisualInstance == null || activeInteractor == null)
        {
            return;
        }

        Transform interactorTransform = activeInteractor.transform;
        Vector3 targetPosition =
            interactorTransform.position +
            interactorTransform.right * carriedHeadOffset.x +
            Vector3.up * carriedHeadOffset.y +
            interactorTransform.forward * carriedHeadOffset.z;

        carriedHeadVisualInstance.transform.SetPositionAndRotation(
            targetPosition,
            Quaternion.LookRotation(interactorTransform.forward, Vector3.up));
    }

    private void BuildCommittedHose(SimpleFireHoseConnectPoint connectPoint)
    {
        if (recordedKnots.Count < 2)
        {
            return;
        }

        EnsureRuntimeRoots();

        GameObject hoseObject = new GameObject($"SimpleFireHose_{committedRoot.childCount}");
        hoseObject.transform.SetParent(committedRoot, false);

        MeshFilter meshFilter = hoseObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = hoseObject.AddComponent<MeshRenderer>();
        if (hoseMaterial != null)
        {
            meshRenderer.sharedMaterial = hoseMaterial;
        }

        List<Knot> groundedKnots = GetGroundProjectedKnots(recordedKnots);
        List<Vector3> committedWorldPoints = BuildCommittedWorldPoints(groundedKnots, activePickupPoint, connectPoint);
        List<Vector3> resampled = ResampleWorldPoints(committedWorldPoints, Mathf.Max(0.02f, hoseSampleSpacing));
        List<Vector3> spline = FireHosePathSampler.CatmullRom(resampled);
        if (spline.Count == 0)
        {
            spline = resampled;
        }

        List<Vector3> liftedSpline = LiftWorldPoints(spline, Mathf.Max(0f, hoseSurfaceOffset));
        List<Vector3> localSpline = ConvertWorldPointsToLocal(hoseObject.transform, liftedSpline);
        Mesh hoseMesh = FireHoseMeshBuilder.Build(
            localSpline,
            null,
            Vector3.up,
            out _,
            Mathf.Max(0.001f, hoseRadius),
            Mathf.Max(3, hoseRadialSegments));
        meshFilter.sharedMesh = hoseMesh;
    }

    private List<Vector3> BuildCommittedWorldPoints(
        List<Knot> groundedKnots,
        SimpleFireHosePickupPoint pickupPoint,
        SimpleFireHoseConnectPoint connectPoint)
    {
        List<Vector3> points = new List<Vector3>(groundedKnots.Count + 6);
        if (groundedKnots == null || groundedKnots.Count == 0)
        {
            return points;
        }

        Transform startAnchor = pickupPoint != null ? pickupPoint.ResolveAnchorTransform() : null;
        Transform endAnchor = connectPoint != null ? connectPoint.ResolveAnchorTransform() : null;

        Vector3 firstGroundPoint = groundedKnots[0].Position;
        Vector3 firstGroundNormal = groundedKnots[0].Normal.sqrMagnitude > 0.0001f ? groundedKnots[0].Normal.normalized : Vector3.up;
        Vector3 lastGroundPoint = groundedKnots[groundedKnots.Count - 1].Position;
        Vector3 lastGroundNormal = groundedKnots[groundedKnots.Count - 1].Normal.sqrMagnitude > 0.0001f ? groundedKnots[groundedKnots.Count - 1].Normal.normalized : Vector3.up;

        if (startAnchor != null)
        {
            points.Add(startAnchor.position);
            points.Add(CreateEndpointTransitionPoint(
                startAnchor.position,
                firstGroundPoint,
                firstGroundNormal,
                true));
        }
        else
        {
            points.Add(firstGroundPoint);
        }

        for (int i = 0; i < groundedKnots.Count; i++)
        {
            points.Add(groundedKnots[i].Position);
        }

        if (endAnchor != null)
        {
            points.Add(CreateEndpointTransitionPoint(
                endAnchor.position,
                lastGroundPoint,
                lastGroundNormal,
                false));
            points.Add(endAnchor.position);
        }

        if (points.Count == 0)
        {
            points.Add(firstGroundPoint);
            if (firstGroundPoint != lastGroundPoint)
            {
                points.Add(lastGroundPoint);
            }
        }

        return points;
    }

    private Vector3 CreateEndpointTransitionPoint(
        Vector3 anchorPosition,
        Vector3 groundPoint,
        Vector3 groundNormal,
        bool isStart)
    {
        Vector3 normal = groundNormal.sqrMagnitude > 0.0001f ? groundNormal.normalized : Vector3.up;
        Vector3 horizontalDirection = groundPoint - anchorPosition;
        horizontalDirection = Vector3.ProjectOnPlane(horizontalDirection, normal);

        if (horizontalDirection.sqrMagnitude <= 0.0001f)
        {
            horizontalDirection = isStart ? Vector3.forward : Vector3.back;
        }

        horizontalDirection.Normalize();
        float transitionDistance = Mathf.Max(0.01f, endpointTransitionDistance);
        float transitionLift = Mathf.Max(0f, endpointTransitionLift);

        Vector3 point = anchorPosition + horizontalDirection * transitionDistance;
        point += normal * transitionLift;
        return point;
    }

    private static List<Vector3> ResampleWorldPoints(List<Vector3> worldPoints, float spacing)
    {
        List<Vector3> results = new List<Vector3>();
        if (worldPoints == null || worldPoints.Count == 0)
        {
            return results;
        }

        if (worldPoints.Count == 1 || spacing <= 0f)
        {
            results.AddRange(worldPoints);
            return results;
        }

        float accumulated = 0f;
        results.Add(worldPoints[0]);

        for (int i = 1; i < worldPoints.Count; i++)
        {
            Vector3 a = worldPoints[i - 1];
            Vector3 b = worldPoints[i];
            float distance = Vector3.Distance(a, b);
            if (distance <= 0.0001f)
            {
                continue;
            }

            while (accumulated + distance >= spacing)
            {
                float t = (spacing - accumulated) / distance;
                Vector3 point = Vector3.Lerp(a, b, t);
                results.Add(point);
                a = point;
                distance = Vector3.Distance(a, b);
                accumulated = 0f;
                if (distance <= 0.0001f)
                {
                    break;
                }
            }

            accumulated += distance;
        }

        Vector3 finalPoint = worldPoints[worldPoints.Count - 1];
        if (results.Count == 0 || Vector3.Distance(results[results.Count - 1], finalPoint) > 0.001f)
        {
            results.Add(finalPoint);
        }

        return results;
    }

    private List<Knot> GetGroundProjectedKnots(List<Knot> sourceKnots)
    {
        List<Knot> grounded = new List<Knot>(sourceKnots.Count);
        for (int i = 0; i < sourceKnots.Count; i++)
        {
            Knot knot = sourceKnots[i];
            if (TryProbeGround(knot.Position, out RaycastHit hit))
            {
                Vector3 normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
                grounded.Add(new Knot(hit.point, normal));
                continue;
            }

            Vector3 fallbackNormal = knot.Normal.sqrMagnitude > 0.0001f ? knot.Normal.normalized : Vector3.up;
            grounded.Add(new Knot(knot.Position, fallbackNormal));
        }

        return grounded;
    }

    private static List<Vector3> LiftWorldPoints(List<Vector3> worldPoints, float lift)
    {
        List<Vector3> lifted = new List<Vector3>(worldPoints.Count);
        for (int i = 0; i < worldPoints.Count; i++)
        {
            lifted.Add(worldPoints[i] + Vector3.up * lift);
        }

        return lifted;
    }

    private Vector3 ProjectPointToGround(Vector3 worldPoint)
    {
        if (TryProbeGround(worldPoint, out RaycastHit hit))
        {
            return hit.point;
        }

        return worldPoint;
    }

    private static List<Vector3> ConvertWorldPointsToLocal(Transform targetTransform, List<Vector3> worldPoints)
    {
        List<Vector3> localPoints = new List<Vector3>(worldPoints.Count);
        for (int i = 0; i < worldPoints.Count; i++)
        {
            localPoints.Add(targetTransform.InverseTransformPoint(worldPoints[i]));
        }

        return localPoints;
    }

    private void EndActiveSession()
    {
        sessionActive = false;
        activeInteractor = null;
        activePickupPoint = null;
        distanceSinceLastKnot = 0f;
        hasFirstKnot = false;

        ClearPreviewMarkers();
        if (carriedHeadVisualInstance != null)
        {
            carriedHeadVisualInstance.SetActive(false);
        }
    }

    private void ClampPreviewSettings()
    {
        previewSampleSpacing = Mathf.Max(0.02f, previewSampleSpacing);
        previewMarkerSpacing = Mathf.Max(0.1f, previewMarkerSpacing);
        previewDecalSize.x = Mathf.Max(0.05f, previewDecalSize.x);
        previewDecalSize.y = Mathf.Max(0.05f, previewDecalSize.y);
        previewDecalDepth = Mathf.Max(0.05f, previewDecalDepth);
        previewDecalDrawDistance = Mathf.Max(1f, previewDecalDrawDistance);
        previewDecalFadeScale = Mathf.Clamp01(previewDecalFadeScale);
        previewDecalSurfaceOffset = Mathf.Max(0f, previewDecalSurfaceOffset);
        previewDecalStartAngleFade = Mathf.Clamp(previewDecalStartAngleFade, 0f, 180f);
        previewDecalEndAngleFade = Mathf.Clamp(previewDecalEndAngleFade, previewDecalStartAngleFade, 180f);
    }

    private bool ShouldUseRuntimeDecalPreview()
    {
        return preferDecalPreviewMarkers && ResolvePreviewDecalMaterial() != null;
    }

    private void ConfigurePreviewDecalProjector(DecalProjector projector)
    {
        if (projector == null)
        {
            return;
        }

        projector.material = ResolvePreviewDecalMaterial();
        projector.drawDistance = previewDecalDrawDistance;
        projector.fadeScale = previewDecalFadeScale;
        projector.startAngleFade = previewDecalStartAngleFade;
        projector.endAngleFade = previewDecalEndAngleFade;
        projector.size = new Vector3(previewDecalSize.x, previewDecalSize.y, previewDecalDepth);
        projector.pivot = new Vector3(0f, 0f, previewDecalDepth * 0.5f);
        projector.fadeFactor = 1f;
        projector.uvScale = Vector2.one;
        projector.uvBias = Vector2.zero;
    }

    private void PlacePreviewDecalMarker(
        DecalProjector projector,
        Vector3 surfacePosition,
        Vector3 surfaceNormal,
        Vector3 surfaceForward)
    {
        if (projector == null)
        {
            return;
        }

        Vector3 normal = surfaceNormal.sqrMagnitude > 0.001f ? surfaceNormal.normalized : Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(surfaceForward, normal);
        if (forward.sqrMagnitude <= 0.001f)
        {
            forward = ResolvePreviewTangent(normal);
        }

        projector.transform.position = surfacePosition + normal * previewDecalSurfaceOffset;
        Quaternion projectorRotation = Quaternion.LookRotation(-normal, forward.normalized);
        projector.transform.rotation = Quaternion.AngleAxis(previewDecalRotationDegrees, normal) * projectorRotation;
        projector.size = new Vector3(previewDecalSize.x, previewDecalSize.y, previewDecalDepth);
        projector.pivot = new Vector3(0f, 0f, previewDecalDepth * 0.5f);
        projector.fadeFactor = 1f;
    }

    private Material ResolvePreviewDecalMaterial()
    {
        if (previewDecalBaseMaterial == null)
        {
            if (!warnedMissingPreviewDecalMaterial && preferDecalPreviewMarkers)
            {
                warnedMissingPreviewDecalMaterial = true;
                Debug.LogWarning($"{nameof(SimpleFireHosePathSystem)} needs a URP Decal material assigned to use runtime preview decals.", this);
            }

            return null;
        }

        return previewDecalBaseMaterial;
    }

    private static Vector3 ResolvePreviewTangent(Vector3 normal)
    {
        Vector3 tangent = Vector3.ProjectOnPlane(Vector3.forward, normal);
        if (tangent.sqrMagnitude <= 0.001f)
        {
            tangent = Vector3.ProjectOnPlane(Vector3.right, normal);
        }

        return tangent.sqrMagnitude > 0.001f ? tangent.normalized : Vector3.forward;
    }

}
