using UnityEngine;
using System;

public class FireHoseDeployable : MonoBehaviour
{
    public Transform head;
    public bool useInputMovement = true;

    public float moveSpeed = 4f;
    public float groundFollowSpeed = 10f;
    public float heightOffset = 0.3f;

    public float knotSpacing = 1.0f;
    public float normalThreshold = 15f;
    public float heightThreshold = 0.3f;
    public float minDistanceBeforeBreak = 0.3f;
    public float edgeProbeStepDistance = 0.2f;
    public float edgeHeightDeltaThreshold = 0.2f;
    public int edgeBinarySearchIterations = 5;
    public float edgeTransitionInset = 0.08f;

    public float raycastHeight = 2f;
    public LayerMask groundMask;

    public bool useCustomProbeDistances = false;
    public float lookAheadDistance = 0.75f;
    public float farLookAheadDistance = 1.5f;
    public bool useFarLookAheadProbe = true;

    public bool drawDebugRays = true;

    public FireHosePath Path { get; private set; } = new FireHosePath();

    public event Action<Knot> OnKnotAdded;

    private FireHoseHeadPickup cachedHeadPickup;
    private Vector3 lastKnotPos;
    private Vector3 lastNormal;
    private Vector3 lastSamplePoint;
    private Vector3 lastObservedHeadPosition;
    private float distanceSinceLastKnot;
    private bool hasFirstKnot = false;
    private bool hasObservedHeadPosition = false;
    private bool hasForcedStartKnot = false;

    void Start()
    {
        ResolveHeadPickup();
        if (head != null)
        {
            lastObservedHeadPosition = head.position;
            hasObservedHeadPosition = true;
        }
    }

    public void ResetPathFromStartKnot(Vector3 startPosition, Vector3 startNormal)
    {
        Path.Clear();
        hasFirstKnot = false;
        hasForcedStartKnot = false;
        distanceSinceLastKnot = 0f;
        lastKnotPos = startPosition;
        lastSamplePoint = startPosition;
        lastNormal = startNormal.sqrMagnitude > 0.0001f ? startNormal.normalized : Vector3.up;

        AddKnot(startPosition, lastNormal);
        hasFirstKnot = true;
        hasForcedStartKnot = true;
    }

    void Update()
    {
        UpdateHeadForwardFromObservedMotion();
        HandleMovement();
        HandleKnotPlacement();
        DrawDebugVisualization();
        CacheObservedHeadPosition();
    }

    void HandleMovement()
    {
        if (!useInputMovement || head == null)
        {
            return;
        }

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 moveDir = new Vector3(h, 0, v).normalized;

        if (moveDir.sqrMagnitude > 0f)
        {
            head.position += moveDir * moveSpeed * Time.deltaTime;
            head.forward = moveDir;
        }
    }

    void UpdateHeadForwardFromObservedMotion()
    {
        if (head == null)
        {
            return;
        }

        if (!hasObservedHeadPosition)
        {
            lastObservedHeadPosition = head.position;
            hasObservedHeadPosition = true;
            return;
        }

        Vector3 planarDelta = head.position - lastObservedHeadPosition;
        planarDelta.y = 0f;

        if (planarDelta.sqrMagnitude > 0.0001f)
        {
            head.forward = planarDelta.normalized;
        }
    }

    void CacheObservedHeadPosition()
    {
        if (head == null)
        {
            hasObservedHeadPosition = false;
            return;
        }

        lastObservedHeadPosition = head.position;
        hasObservedHeadPosition = true;
    }

    void HandleKnotPlacement()
    {
        if (!TryProbeGround(head.position, out RaycastHit currentHit))
        {
            return;
        }

        Vector3 currentPoint = currentHit.point;
        Vector3 currentNormal = currentHit.normal;

        if (!hasFirstKnot)
        {
            AddKnot(currentPoint, currentNormal);
            hasFirstKnot = true;
            return;
        }

        if (hasForcedStartKnot)
        {
            lastSamplePoint = currentPoint;
            hasForcedStartKnot = false;
            return;
        }

        float dist = Vector3.Distance(lastSamplePoint, currentPoint);
        distanceSinceLastKnot += dist;
        lastSamplePoint = currentPoint;

        float localAngle = Vector3.Angle(lastNormal, currentNormal);
        float localHeightDelta = Mathf.Abs(currentPoint.y - lastKnotPos.y);

        float predictedAngle = localAngle;
        float predictedHeightDelta = localHeightDelta;
        bool sawAheadProbe = false;

        float derivedLookAheadDistance = GetLookAheadDistance();
        float derivedFarLookAheadDistance = GetFarLookAheadDistance();

        if (TryProbeAheadGround(derivedLookAheadDistance, out RaycastHit aheadHit))
        {
            predictedAngle = Mathf.Max(predictedAngle, Vector3.Angle(lastNormal, aheadHit.normal));
            predictedHeightDelta = Mathf.Max(predictedHeightDelta, Mathf.Abs(aheadHit.point.y - lastKnotPos.y));
            sawAheadProbe = true;
        }

        if (useFarLookAheadProbe && TryProbeAheadGround(derivedFarLookAheadDistance, out RaycastHit farAheadHit))
        {
            predictedAngle = Mathf.Max(predictedAngle, Vector3.Angle(lastNormal, farAheadHit.normal));
            predictedHeightDelta = Mathf.Max(predictedHeightDelta, Mathf.Abs(farAheadHit.point.y - lastKnotPos.y));
            sawAheadProbe = true;
        }

        bool spacingRule = distanceSinceLastKnot >= knotSpacing;
        bool breakRule =
            distanceSinceLastKnot > minDistanceBeforeBreak &&
            (localAngle > normalThreshold || localHeightDelta > heightThreshold);

        bool predictiveBreakRule =
            sawAheadProbe &&
            distanceSinceLastKnot > minDistanceBeforeBreak &&
            (predictedAngle > normalThreshold || predictedHeightDelta > heightThreshold);

        bool shouldResolveStepEdge =
            distanceSinceLastKnot > minDistanceBeforeBreak &&
            (localHeightDelta > edgeHeightDeltaThreshold || predictedHeightDelta > edgeHeightDeltaThreshold);

        if (shouldResolveStepEdge &&
            TryResolveStepEdge(
                lastKnotPos,
                currentPoint,
                out Vector3 lowerEdgePoint,
                out Vector3 lowerEdgeNormal,
                out Vector3 upperEdgePoint,
                out Vector3 upperEdgeNormal))
        {
            bool lowerEdgeIsDistinctFromLast = Vector3.Distance(lastKnotPos, lowerEdgePoint) > 0.05f;
            bool lowerEdgeIsDistinctFromCurrent = Vector3.Distance(lowerEdgePoint, currentPoint) > 0.05f;
            bool upperEdgeIsDistinctFromLast = Vector3.Distance(lastKnotPos, upperEdgePoint) > 0.05f;
            bool upperEdgeIsDistinctFromCurrent = Vector3.Distance(upperEdgePoint, currentPoint) > 0.05f;
            bool lowerUpperAreDistinct = Vector3.Distance(lowerEdgePoint, upperEdgePoint) > 0.05f;

            if (lowerEdgeIsDistinctFromLast && lowerEdgeIsDistinctFromCurrent)
            {
                AddKnot(lowerEdgePoint, lowerEdgeNormal);
            }

            if (lowerUpperAreDistinct && upperEdgeIsDistinctFromLast && upperEdgeIsDistinctFromCurrent)
            {
                AddKnot(upperEdgePoint, upperEdgeNormal);
            }
        }

        if (spacingRule || breakRule || predictiveBreakRule)
        {
            AddKnot(currentPoint, currentNormal);
        }
    }

    bool TryProbeGround(Vector3 worldPosition, out RaycastHit hit)
    {
        Vector3 origin = worldPosition;
        return Physics.Raycast(origin, Vector3.down, out hit, raycastHeight, groundMask);
    }

    bool TryProbeAheadGround(float distance, out RaycastHit hit)
    {
        Vector3 forward = head.forward;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = transform.forward;
        }

        Vector3 probePosition = head.position + forward.normalized * Mathf.Max(0f, distance);
        return TryProbeGround(probePosition, out hit);
    }

    void DrawDebugVisualization()
    {
        if (!drawDebugRays || head == null)
        {
            return;
        }

        Vector3 forward = GetProbeForward();
        Vector3 moveDirection = GetMovementDirection();
        Vector3 origin = head.position + Vector3.up * raycastHeight;

        Debug.DrawRay(origin, forward * 1.5f, Color.blue);

        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            Debug.DrawRay(origin + Vector3.up * 0.1f, moveDirection * 1.25f, Color.cyan);
        }

        DrawProbeRay(head.position, Color.green);
        float derivedLookAheadDistance = GetLookAheadDistance();
        float derivedFarLookAheadDistance = GetFarLookAheadDistance();

        DrawProbeRay(head.position + forward * derivedLookAheadDistance, Color.yellow);

        if (useFarLookAheadProbe)
        {
            DrawProbeRay(head.position + forward * derivedFarLookAheadDistance, new Color(1f, 0.5f, 0f));
        }
    }

    void DrawProbeRay(Vector3 probePosition, Color color)
    {
        Debug.DrawRay(probePosition, Vector3.down * raycastHeight, color);
    }

    Vector3 GetProbeForward()
    {
        if (head != null && head.forward.sqrMagnitude > 0.0001f)
        {
            return head.forward.normalized;
        }

        if (transform.forward.sqrMagnitude > 0.0001f)
        {
            return transform.forward.normalized;
        }

        return Vector3.forward;
    }

    Vector3 GetMovementDirection()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 moveDir = new Vector3(h, 0f, v);
        return moveDir.sqrMagnitude > 0.0001f ? moveDir.normalized : Vector3.zero;
    }

    float GetLookAheadDistance()
    {
        if (useCustomProbeDistances)
        {
            return Mathf.Max(0f, lookAheadDistance);
        }

        return Mathf.Max(0f, knotSpacing * 0.75f);
    }

    float GetFarLookAheadDistance()
    {
        if (useCustomProbeDistances)
        {
            return Mathf.Max(0f, farLookAheadDistance);
        }

        return Mathf.Max(0f, knotSpacing * 1.5f);
    }

    bool TryResolveStepEdge(
        Vector3 startPoint,
        Vector3 endPoint,
        out Vector3 lowerEdgePoint,
        out Vector3 lowerEdgeNormal,
        out Vector3 upperEdgePoint,
        out Vector3 upperEdgeNormal)
    {
        lowerEdgePoint = default;
        lowerEdgeNormal = Vector3.up;
        upperEdgePoint = default;
        upperEdgeNormal = Vector3.up;

        Vector3 horizontalDelta = endPoint - startPoint;
        horizontalDelta.y = 0f;
        float horizontalDistance = horizontalDelta.magnitude;
        if (horizontalDistance <= 0.001f)
        {
            return false;
        }

        Vector3 direction = horizontalDelta / horizontalDistance;
        float stepDistance = Mathf.Max(0.05f, edgeProbeStepDistance);
        int sampleCount = Mathf.Max(2, Mathf.CeilToInt(horizontalDistance / stepDistance));

        Vector3 previousSamplePoint = startPoint;
        if (!TryProbeGround(previousSamplePoint, out RaycastHit previousHit))
        {
            return false;
        }

        for (int sampleIndex = 1; sampleIndex <= sampleCount; sampleIndex++)
        {
            float distance = Mathf.Min(horizontalDistance, sampleIndex * stepDistance);
            Vector3 samplePosition = startPoint + direction * distance;

            if (!TryProbeGround(samplePosition, out RaycastHit currentHit))
            {
                continue;
            }

            float heightDelta = Mathf.Abs(currentHit.point.y - previousHit.point.y);
            if (heightDelta > Mathf.Max(0.01f, edgeHeightDeltaThreshold))
            {
                return RefineStepEdge(
                    previousSamplePoint,
                    samplePosition,
                    direction,
                    previousHit,
                    currentHit,
                    out lowerEdgePoint,
                    out lowerEdgeNormal,
                    out upperEdgePoint,
                    out upperEdgeNormal);
            }

            previousSamplePoint = samplePosition;
            previousHit = currentHit;
        }

        return false;
    }

    bool RefineStepEdge(
        Vector3 lowSamplePosition,
        Vector3 highSamplePosition,
        Vector3 direction,
        RaycastHit lowHit,
        RaycastHit highHit,
        out Vector3 lowerEdgePoint,
        out Vector3 lowerEdgeNormal,
        out Vector3 upperEdgePoint,
        out Vector3 upperEdgeNormal)
    {
        lowerEdgePoint = lowHit.point;
        lowerEdgeNormal = lowHit.normal;
        upperEdgePoint = highHit.point;
        upperEdgeNormal = highHit.normal;

        Vector3 lowPosition = lowSamplePosition;
        Vector3 highPosition = highSamplePosition;
        RaycastHit resolvedLowHit = lowHit;
        RaycastHit resolvedHighHit = highHit;

        for (int i = 0; i < Mathf.Max(1, edgeBinarySearchIterations); i++)
        {
            Vector3 midPosition = Vector3.Lerp(lowPosition, highPosition, 0.5f);
            if (!TryProbeGround(midPosition, out RaycastHit midHit))
            {
                break;
            }

            float lowToMidHeightDelta = Mathf.Abs(midHit.point.y - resolvedLowHit.point.y);
            if (lowToMidHeightDelta > Mathf.Max(0.01f, edgeHeightDeltaThreshold))
            {
                highPosition = midPosition;
                resolvedHighHit = midHit;
            }
            else
            {
                lowPosition = midPosition;
                resolvedLowHit = midHit;
            }
        }

        float inset = Mathf.Max(0f, edgeTransitionInset);
        Vector3 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;

        lowerEdgePoint = resolvedLowHit.point - safeDirection * inset;
        lowerEdgeNormal = resolvedLowHit.normal.sqrMagnitude > 0.0001f ? resolvedLowHit.normal.normalized : Vector3.up;
        upperEdgePoint = resolvedHighHit.point + safeDirection * inset;
        upperEdgeNormal = resolvedHighHit.normal.sqrMagnitude > 0.0001f ? resolvedHighHit.normal.normalized : Vector3.up;
        return true;
    }

    void AddKnot(Vector3 pos, Vector3 normal)
    {
        Knot k = Path.AddKnot(pos, normal);
        ApplyLatestKnotRotation(k);
        OnKnotAdded?.Invoke(k);

        lastKnotPos = pos;
        lastNormal = normal;
        lastSamplePoint = pos;
        distanceSinceLastKnot = 0f;
    }

    private void ResolveHeadPickup()
    {
        if (head == null)
        {
            cachedHeadPickup = null;
            return;
        }

        cachedHeadPickup = head.GetComponent<FireHoseHeadPickup>() ?? head.GetComponentInParent<FireHoseHeadPickup>();
    }

    private void ApplyLatestKnotRotation(Knot knot)
    {
        if (head == null)
        {
            return;
        }

        ResolveHeadPickup();
        if (cachedHeadPickup != null && (cachedHeadPickup.IsHeld || cachedHeadPickup.Assembly != null && cachedHeadPickup.Assembly.IsAttached))
        {
            return;
        }

        if (cachedHeadPickup != null && cachedHeadPickup.Assembly != null)
        {
            cachedHeadPickup.Assembly.SnapHeadToLatestKnot();
            return;
        }

        Transform headRoot = cachedHeadPickup != null ? cachedHeadPickup.transform : head;
        headRoot.SetPositionAndRotation(knot.Position, knot.Rotation);
    }
}
