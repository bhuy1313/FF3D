using UnityEngine;
using UnityEngine.AI;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private void ResetViewPointPitch()
    {
        if (viewPoint == null)
        {
            return;
        }

        viewPoint.localRotation = Quaternion.RotateTowards(viewPoint.localRotation, Quaternion.identity, pitchTurnSpeed * Time.deltaTime);
    }

    private float GetRequiredHorizontalDistanceForAim(IBotExtinguisherItem tool, Vector3 worldPoint)
    {
        if (tool == null || !UsesPreciseAim(tool) || viewPoint == null)
        {
            return 0f;
        }

        Vector3 fromViewPoint = worldPoint - viewPoint.position;
        float horizontalDistance = new Vector2(fromViewPoint.x, fromViewPoint.z).magnitude;
        float verticalOffset = fromViewPoint.y;

        if (horizontalDistance <= 0.001f)
        {
            horizontalDistance = 0.001f;
        }

        float requiredPitch = -Mathf.Atan2(verticalOffset, horizontalDistance) * Mathf.Rad2Deg;
        if (requiredPitch >= minPitchAngle && requiredPitch <= maxPitchAngle)
        {
            return 0f;
        }

        float allowedPitch = requiredPitch < minPitchAngle ? minPitchAngle : maxPitchAngle;
        float allowedPitchAbs = Mathf.Max(1f, Mathf.Abs(allowedPitch));
        return Mathf.Abs(verticalOffset) / Mathf.Tan(allowedPitchAbs * Mathf.Deg2Rad);
    }

    private Vector3 GetAimPoint(IBotExtinguisherItem tool, Vector3 firePosition)
    {
        if (tool == null || !UsesPreciseAim(tool) || viewPoint == null)
        {
            return firePosition;
        }

        if (TryGetBallisticAimDirection(tool, viewPoint.position, firePosition, out Vector3 aimDirection))
        {
            return viewPoint.position + aimDirection * Mathf.Max(5f, tool.MaxSprayDistance);
        }

        return firePosition;
    }

    private void UpdateCurrentExtinguishAimData(IBotExtinguisherItem tool, Vector3 firePosition)
    {
        currentExtinguishAimPoint = firePosition;
        currentExtinguishLaunchDirection = Vector3.zero;
        hasCurrentExtinguishAimPoint = true;
        hasCurrentExtinguishLaunchDirection = false;
        currentExtinguishTrajectoryPointCount = 0;

        if (tool == null || !UsesPreciseAim(tool) || viewPoint == null)
        {
            return;
        }

        if (!TryGetBallisticAimDirection(tool, viewPoint.position, firePosition, out Vector3 launchDirection))
        {
            return;
        }

        currentExtinguishLaunchDirection = launchDirection;
        hasCurrentExtinguishLaunchDirection = true;
        currentExtinguishAimPoint = viewPoint.position + launchDirection * Mathf.Max(5f, tool.MaxSprayDistance);
        BuildBallisticTrajectoryPoints(tool, viewPoint.position, launchDirection, currentExtinguishTrajectoryPoints, out currentExtinguishTrajectoryPointCount);
    }

    private static void BuildBallisticTrajectoryPoints(
        IBotExtinguisherItem tool,
        Vector3 origin,
        Vector3 launchDirection,
        Vector3[] points,
        out int pointCount)
    {
        pointCount = 0;
        if (tool == null || points == null || points.Length == 0 || launchDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float speed = Mathf.Max(0.01f, tool.BallisticLaunchSpeed);
        float maxTravelDistance = Mathf.Max(0.5f, tool.MaxSprayDistance);
        Vector3 gravity = Physics.gravity * Mathf.Max(0f, tool.BallisticGravityMultiplier);
        float estimatedTime = Mathf.Max(0.15f, maxTravelDistance / speed * 1.5f);
        int maxPoints = Mathf.Min(points.Length, 24);
        float travelledDistance = 0f;
        Vector3 previousPoint = origin;

        points[0] = origin;
        pointCount = 1;

        for (int i = 1; i < maxPoints; i++)
        {
            float t = estimatedTime * i / (maxPoints - 1);
            Vector3 point = origin + launchDirection * speed * t + 0.5f * gravity * t * t;
            travelledDistance += Vector3.Distance(previousPoint, point);
            points[pointCount++] = point;
            previousPoint = point;

            if (travelledDistance >= maxTravelDistance)
            {
                break;
            }
        }
    }

    private bool TryGetBallisticAimDirection(IBotExtinguisherItem tool, Vector3 origin, Vector3 target, out Vector3 aimDirection)
    {
        aimDirection = Vector3.zero;
        if (tool == null || tool.BallisticLaunchSpeed <= 0f)
        {
            return false;
        }

        Vector3 toTarget = target - origin;
        Vector3 toTargetXZ = new Vector3(toTarget.x, 0f, toTarget.z);
        float horizontalDistance = toTargetXZ.magnitude;
        if (horizontalDistance <= 0.001f)
        {
            aimDirection = toTarget.normalized;
            return aimDirection.sqrMagnitude > 0.001f;
        }

        float speed = tool.BallisticLaunchSpeed;
        float gravity = Mathf.Abs(Physics.gravity.y) * Mathf.Max(0f, tool.BallisticGravityMultiplier);
        if (gravity <= 0.001f)
        {
            aimDirection = toTarget.normalized;
            return true;
        }

        float speedSquared = speed * speed;
        float speedFourth = speedSquared * speedSquared;
        float verticalOffset = toTarget.y;
        float discriminant = speedFourth - gravity * (gravity * horizontalDistance * horizontalDistance + 2f * verticalOffset * speedSquared);
        if (discriminant < 0f)
        {
            return false;
        }

        float sqrtDiscriminant = Mathf.Sqrt(discriminant);
        float lowAngle = Mathf.Atan2(speedSquared - sqrtDiscriminant, gravity * horizontalDistance);
        Vector3 horizontalDirection = toTargetXZ / horizontalDistance;
        aimDirection = (horizontalDirection * Mathf.Cos(lowAngle) + Vector3.up * Mathf.Sin(lowAngle)).normalized;
        return aimDirection.sqrMagnitude > 0.001f;
    }

    private static bool UsesPreciseAim(IBotExtinguisherItem tool)
    {
        return tool != null && tool.RequiresPreciseAim;
    }

    private float GetVerticalAimPenalty(Vector3 originPosition, Vector3 firePosition)
    {
        float horizontalDistance = new Vector2(firePosition.x - originPosition.x, firePosition.z - originPosition.z).magnitude;
        float verticalOffset = firePosition.y - originPosition.y;
        if (horizontalDistance <= 0.001f)
        {
            horizontalDistance = 0.001f;
        }

        float requiredPitch = -Mathf.Atan2(verticalOffset, horizontalDistance) * Mathf.Rad2Deg;
        if (requiredPitch >= minPitchAngle && requiredPitch <= maxPitchAngle)
        {
            return 0f;
        }

        float deltaToNearestLimit = requiredPitch < minPitchAngle
            ? minPitchAngle - requiredPitch
            : requiredPitch - maxPitchAngle;
        return deltaToNearestLimit * 0.5f;
    }

    private static float GetHorizontalDistance(Vector3 a, Vector3 b)
    {
        Vector2 a2 = new Vector2(a.x, a.z);
        Vector2 b2 = new Vector2(b.x, b.z);
        return Vector2.Distance(a2, b2);
    }

    private static float GetDesiredExtinguisherStandOffDistance(IBotExtinguisherItem tool)
    {
        return tool != null
            ? Mathf.Clamp(tool.PreferredSprayDistance, 0.5f, tool.MaxSprayDistance)
            : 0f;
    }

    private float GetDesiredExtinguisherStandOffDistanceLocked(IBotExtinguisherItem tool, IFireTarget fireTarget)
    {
        float currentStandOff = GetDesiredExtinguisherStandOffDistance(tool);
        if (tool == null || fireTarget == null || !ReferenceEquals(lockedExtinguisherFireTarget, fireTarget))
        {
            return currentStandOff;
        }

        return Mathf.Max(currentStandOff, lockedExtinguisherStandOffDistance);
    }

    private float GetDesiredExtinguisherCenterDistance(IBotExtinguisherItem tool, IFireTarget fireTarget)
    {
        float fireRadius = GetTrackedExtinguisherFireRadius(fireTarget);
        return fireRadius + GetDesiredExtinguisherStandOffDistanceLocked(tool, fireTarget);
    }

    private float GetAllowedExtinguisherEdgeRange(IBotExtinguisherItem tool)
    {
        if (tool == null)
        {
            return 0f;
        }

        return tool.MaxSprayDistance + Mathf.Max(0.25f, extinguisherStandDistanceTolerance);
    }

    private float GetFireEdgeDistance(Vector3 fromPosition, Vector3 firePosition, IFireTarget fireTarget)
    {
        float fireRadius = GetTrackedExtinguisherFireRadius(fireTarget);
        return Mathf.Max(0f, GetHorizontalDistance(fromPosition, firePosition) - fireRadius);
    }

    private float GetTrackedExtinguisherFireRadius(IFireTarget fireTarget)
    {
        float currentRadius = fireTarget != null ? Mathf.Max(0f, fireTarget.GetWorldRadius()) : 0f;
        if (fireTarget == null || !ReferenceEquals(lockedExtinguisherFireTarget, fireTarget))
        {
            return currentRadius;
        }

        return Mathf.Max(currentRadius, lockedExtinguisherFireRadius);
    }

    private bool IsExtinguisherTargetLocked(IFireTarget fireTarget)
    {
        return fireTarget != null && ReferenceEquals(lockedExtinguisherFireTarget, fireTarget);
    }

    private void LockExtinguisherTarget(IFireTarget fireTarget)
    {
        if (fireTarget == null)
        {
            return;
        }

        float currentRadius = Mathf.Max(0f, fireTarget.GetWorldRadius());
        if (!ReferenceEquals(lockedExtinguisherFireTarget, fireTarget))
        {
            lockedExtinguisherFireTarget = fireTarget;
            lockedExtinguisherFireRadius = currentRadius;
            return;
        }

        lockedExtinguisherFireRadius = Mathf.Max(lockedExtinguisherFireRadius, currentRadius);
    }

    private void PrimeExtinguisherTargetLock(IBotExtinguisherItem tool, IFireTarget fireTarget)
    {
        if (tool == null || fireTarget == null)
        {
            return;
        }

        float currentRadius = Mathf.Max(0f, fireTarget.GetWorldRadius());
        float currentStandOff = GetDesiredExtinguisherStandOffDistance(tool);
        if (!ReferenceEquals(lockedExtinguisherFireTarget, fireTarget))
        {
            lockedExtinguisherFireTarget = fireTarget;
            lockedExtinguisherFireRadius = currentRadius;
            lockedExtinguisherStandOffDistance = currentStandOff;
            return;
        }

        lockedExtinguisherFireRadius = Mathf.Max(lockedExtinguisherFireRadius, currentRadius);
        lockedExtinguisherStandOffDistance = Mathf.Max(lockedExtinguisherStandOffDistance, currentStandOff);
    }

    private void ClearExtinguisherTargetLock()
    {
        lockedExtinguisherFireTarget = null;
        lockedExtinguisherFireRadius = 0f;
        lockedExtinguisherStandOffDistance = 0f;
    }

    private IFireTarget GetLockedExtinguisherFireTarget()
    {
        return lockedExtinguisherFireTarget != null && lockedExtinguisherFireTarget.IsBurning
            ? lockedExtinguisherFireTarget
            : null;
    }

    private static float DistanceToSegment2D(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd, out float t)
    {
        Vector2 point2 = new Vector2(point.x, point.z);
        Vector2 start2 = new Vector2(segmentStart.x, segmentStart.z);
        Vector2 end2 = new Vector2(segmentEnd.x, segmentEnd.z);
        Vector2 segment = end2 - start2;
        float segmentLengthSq = segment.sqrMagnitude;
        if (segmentLengthSq <= 0.0001f)
        {
            t = 0f;
            return Vector2.Distance(point2, start2);
        }

        t = Mathf.Clamp01(Vector2.Dot(point2 - start2, segment) / segmentLengthSq);
        Vector2 projection = start2 + segment * t;
        return Vector2.Distance(point2, projection);
    }

    private static string NormalizeExtinguishActivityMessage(ExtinguishDebugStage stage, string detail)
    {
        switch (stage)
        {
            case ExtinguishDebugStage.NoFireGroupFound:
                return "No fire target found.";
            case ExtinguishDebugStage.NoReachableTool:
                return "No usable firefighting tool available.";
            case ExtinguishDebugStage.SearchingExtinguisher:
                return "Searching for firefighting tool.";
            case ExtinguishDebugStage.MovingToExtinguisher:
                return "Moving to firefighting tool.";
            case ExtinguishDebugStage.PickingUpExtinguisher:
                return "Picked up firefighting tool.";
            case ExtinguishDebugStage.MovingToFire:
                return "Moving to firefighting position.";
            case ExtinguishDebugStage.Spraying:
                return "Started extinguishing fire.";
            case ExtinguishDebugStage.OutOfCharge:
                return "Out of charge.";
            case ExtinguishDebugStage.Completed:
                return "Extinguishing completed.";
            default:
                return null;
        }
    }

    private void UpdateExtinguishDebugStage(ExtinguishDebugStage stage, string detail)
    {
        int stageValue = (int)stage;
        if (activityDebug == null || !activityDebug.TryUpdateExtinguishStage(stageValue))
        {
            return;
        }

        string normalizedDetail = NormalizeExtinguishActivityMessage(stage, detail);
        activityDebug.LogExtinguish(this, enableActivityDebug, normalizedDetail);
    }
}
