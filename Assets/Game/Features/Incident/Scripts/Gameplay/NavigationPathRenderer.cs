using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class NavigationPathRenderer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Path")]
    [SerializeField] private bool updateContinuously = true;
    [SerializeField] [Min(0.05f)] private float refreshInterval = 0.2f;
    [SerializeField] [Min(0.1f)] private float sampleDistance = 2f;
    [SerializeField] private int navMeshAreaMask = NavMesh.AllAreas;
    [SerializeField] [Min(0.1f)] private float pathPointSpacing = 0.75f;

    [Header("Projection")]
    [SerializeField] private Vector3 pointOffset = new Vector3(0f, 0.08f, 0f);
    [SerializeField] [Min(0.1f)] private float groundProbeHeight = 4f;
    [SerializeField] [Min(0.1f)] private float groundProbeDistance = 12f;
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Markers")]
    [SerializeField] private Transform markerRoot;
    [SerializeField] private GameObject markerPrefab;
    [SerializeField] [Min(0.1f)] private float markerSpacing = 1.5f;
    [SerializeField] private Vector3 markerOffset = new Vector3(0f, 0.02f, 0f);
    [SerializeField] private bool orientMarkersAlongPath = true;
    [SerializeField] private bool alignMarkersToGround = true;

    private NavMeshPath navMeshPath;
    private readonly List<Vector3> renderedPoints = new List<Vector3>(128);
    private readonly List<GameObject> markerPool = new List<GameObject>(64);
    private float nextRefreshTime;

    private void Awake()
    {
        navMeshPath = new NavMeshPath();

        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        if (markerRoot == null)
        {
            markerRoot = transform;
        }
    }

    private void OnEnable()
    {
        RefreshPathImmediate();
    }

    private void Update()
    {
        if (!updateContinuously)
        {
            return;
        }

        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.unscaledTime + refreshInterval;
        RefreshPathImmediate();
    }

    private void RefreshPathImmediate()
    {
        if (startPoint == null || endPoint == null)
        {
            HidePath();
            return;
        }

        if (!TryCalculatePath(startPoint.position, endPoint.position))
        {
            HidePath();
            return;
        }

        RenderPath();
    }

    [ContextMenu("Refresh Path")]
    public void RefreshPath()
    {
        RefreshPathImmediate();
    }

    private bool TryCalculatePath(Vector3 sourcePosition, Vector3 destinationPosition)
    {
        if (navMeshPath == null)
        {
            navMeshPath = new NavMeshPath();
        }

        if (!TrySampleNavMeshPosition(sourcePosition, out NavMeshHit sourceHit))
        {
            return false;
        }

        if (!TrySampleNavMeshPosition(destinationPosition, out NavMeshHit destinationHit))
        {
            return false;
        }

        if (!NavMesh.CalculatePath(sourceHit.position, destinationHit.position, navMeshAreaMask, navMeshPath))
        {
            return false;
        }

        if (navMeshPath.status != NavMeshPathStatus.PathComplete || navMeshPath.corners == null || navMeshPath.corners.Length < 2)
        {
            return false;
        }

        return true;
    }

    private bool TrySampleNavMeshPosition(Vector3 worldPosition, out NavMeshHit navMeshHit)
    {
        return NavMesh.SamplePosition(worldPosition, out navMeshHit, sampleDistance, navMeshAreaMask);
    }

    private void RenderPath()
    {
        if (lineRenderer == null)
        {
            return;
        }

        BuildRenderedPoints();
        if (renderedPoints.Count <= 1)
        {
            HidePath();
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.positionCount = renderedPoints.Count;
        lineRenderer.SetPositions(renderedPoints.ToArray());
        UpdateMarkers();
    }

    private void BuildRenderedPoints()
    {
        renderedPoints.Clear();

        Vector3[] corners = navMeshPath.corners;
        if (corners == null || corners.Length == 0)
        {
            return;
        }

        AppendProjectedPoint(corners[0]);
        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 previous = corners[i - 1];
            Vector3 current = corners[i];
            float distance = Vector3.Distance(previous, current);
            int subdivisions = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(0.1f, pathPointSpacing)));

            for (int step = 1; step <= subdivisions; step++)
            {
                float t = (float)step / subdivisions;
                Vector3 sampledPoint = Vector3.Lerp(previous, current, t);
                AppendProjectedPoint(sampledPoint);
            }
        }
    }

    private void AppendProjectedPoint(Vector3 worldPoint)
    {
        renderedPoints.Add(ResolveProjectedPoint(worldPoint) + pointOffset);
    }

    private Vector3 ResolveProjectedPoint(Vector3 worldPoint)
    {
        Vector3 probeOrigin = worldPoint + Vector3.up * groundProbeHeight;
        if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit hit, groundProbeDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return worldPoint;
    }

    private void HidePath()
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.positionCount = 0;
        lineRenderer.enabled = false;
        HideMarkers();
    }

    private void UpdateMarkers()
    {
        if (markerPrefab == null || renderedPoints.Count <= 1)
        {
            HideMarkers();
            return;
        }

        int markerIndex = 0;
        float spacing = Mathf.Max(0.1f, markerSpacing);
        float carryDistance = 0f;

        for (int i = 1; i < renderedPoints.Count; i++)
        {
            Vector3 from = renderedPoints[i - 1];
            Vector3 to = renderedPoints[i];
            Vector3 segment = to - from;
            float segmentLength = segment.magnitude;
            if (segmentLength <= 0.001f)
            {
                continue;
            }

            Vector3 direction = segment / segmentLength;
            float distanceAlongSegment = spacing - carryDistance;
            while (distanceAlongSegment <= segmentLength)
            {
                Vector3 position = from + direction * distanceAlongSegment;
                PlaceMarker(markerIndex, position, direction);
                markerIndex++;
                distanceAlongSegment += spacing;
            }

            carryDistance = segmentLength - (distanceAlongSegment - spacing);
            if (carryDistance >= spacing)
            {
                carryDistance = 0f;
            }
        }

        for (int i = markerIndex; i < markerPool.Count; i++)
        {
            if (markerPool[i] != null)
            {
                markerPool[i].SetActive(false);
            }
        }
    }

    private void PlaceMarker(int markerIndex, Vector3 position, Vector3 forward)
    {
        GameObject marker = GetOrCreateMarker(markerIndex);
        if (marker == null)
        {
            return;
        }

        marker.SetActive(true);
        Transform markerTransform = marker.transform;
        markerTransform.position = position + markerOffset;

        Vector3 up = Vector3.up;
        if (alignMarkersToGround)
        {
            Vector3 probeOrigin = position + Vector3.up * groundProbeHeight;
            if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit hit, groundProbeDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                up = hit.normal.sqrMagnitude > 0.001f ? hit.normal.normalized : Vector3.up;
                markerTransform.position = hit.point + markerOffset;
            }
        }

        if (orientMarkersAlongPath && forward.sqrMagnitude > 0.001f)
        {
            Vector3 flattenedForward = Vector3.ProjectOnPlane(forward, up);
            if (flattenedForward.sqrMagnitude <= 0.001f)
            {
                flattenedForward = Vector3.Cross(markerTransform.right, up);
            }

            markerTransform.rotation = Quaternion.LookRotation(flattenedForward.normalized, up);
        }
        else
        {
            markerTransform.rotation = Quaternion.FromToRotation(Vector3.up, up);
        }
    }

    private GameObject GetOrCreateMarker(int index)
    {
        while (markerPool.Count <= index)
        {
            if (markerPrefab == null)
            {
                return null;
            }

            GameObject marker = Instantiate(markerPrefab, markerRoot);
            marker.name = $"{markerPrefab.name}_{markerPool.Count}";
            marker.SetActive(false);
            markerPool.Add(marker);
        }

        return markerPool[index];
    }

    private void HideMarkers()
    {
        for (int i = 0; i < markerPool.Count; i++)
        {
            if (markerPool[i] != null)
            {
                markerPool[i].SetActive(false);
            }
        }
    }
}
