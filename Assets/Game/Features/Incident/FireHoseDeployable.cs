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

    public float raycastHeight = 2f;
    public LayerMask groundMask;

    public bool useCustomProbeDistances = false;
    public float lookAheadDistance = 0.75f;
    public float farLookAheadDistance = 1.5f;
    public bool useFarLookAheadProbe = true;

    public bool drawDebugRays = true;

    public FireHosePath Path { get; private set; } = new FireHosePath();

    public event Action<Knot> OnKnotAdded;

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

        if (spacingRule || breakRule || predictiveBreakRule)
        {
            AddKnot(currentPoint, currentNormal);
        }
    }

    bool TryProbeGround(Vector3 worldPosition, out RaycastHit hit)
    {
        Vector3 origin = worldPosition + Vector3.up * raycastHeight;
        return Physics.Raycast(origin, Vector3.down, out hit, raycastHeight * 2f, groundMask);
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
        Vector3 origin = head.position + Vector3.up * 0.2f;

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
        Vector3 rayOrigin = probePosition + Vector3.up * raycastHeight;
        Debug.DrawRay(rayOrigin, Vector3.down * (raycastHeight * 2f), color);
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

    void AddKnot(Vector3 pos, Vector3 normal)
    {
        Knot k = new Knot(pos, normal);

        Path.AddKnot(pos, normal);
        OnKnotAdded?.Invoke(k);

        lastKnotPos = pos;
        lastNormal = normal;
        lastSamplePoint = pos;
        distanceSinceLastKnot = 0f;
    }
}
