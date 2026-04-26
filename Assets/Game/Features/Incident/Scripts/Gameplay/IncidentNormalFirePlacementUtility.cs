using UnityEngine;

public static class IncidentNormalFirePlacementUtility
{
    private const int SampleCount = 24;
    private const float RaycastDistancePadding = 1f;
    private const float UpFacingSurfaceDotThreshold = 0.6f;
    private const float DownFacingSurfaceDotThreshold = -0.35f;

    public static bool TryResolvePlacement(IncidentOriginArea area, out Vector3 position, out Quaternion rotation)
    {
        position = area != null ? area.transform.position : Vector3.zero;
        rotation = area != null ? area.transform.rotation : Quaternion.identity;
        if (area == null)
        {
            return false;
        }

        Bounds bounds = area.GetAreaBounds();
        LayerMask mask = area.SurfacePlacementMask;
        QueryTriggerInteraction triggerInteraction = area.SurfacePlacementTriggerInteraction;
        Vector3 center = bounds.center;
        float rayDistance = bounds.extents.magnitude + RaycastDistancePadding;

        for (int i = 0; i < SampleCount; i++)
        {
            Vector3 direction = Random.onUnitSphere;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            if (TryResolveHit(area, bounds, center, direction.normalized, rayDistance, mask, triggerInteraction, out position, out rotation))
            {
                return true;
            }
        }

        Vector3 fallbackDirection = area.transform.forward.sqrMagnitude > 0.0001f
            ? area.transform.forward.normalized
            : Vector3.forward;
        if (TryResolveHit(area, bounds, center, fallbackDirection, rayDistance, mask, triggerInteraction, out position, out rotation))
        {
            return true;
        }

        position = area.transform.position;
        rotation = area.transform.rotation;
        return true;
    }

    private static bool TryResolveHit(
        IncidentOriginArea area,
        Bounds bounds,
        Vector3 start,
        Vector3 direction,
        float distance,
        LayerMask mask,
        QueryTriggerInteraction triggerInteraction,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = start;
        rotation = area.transform.rotation;
        if (!Physics.Raycast(start, direction, out RaycastHit hit, distance, mask, triggerInteraction))
        {
            return false;
        }

        if (!bounds.Contains(hit.point))
        {
            return false;
        }

        if (IsRejectedPrimarySurface(area, hit))
        {
            return false;
        }

        position = hit.point + (hit.normal * area.SurfaceOffset);
        if (!bounds.Contains(position))
        {
            position = hit.point;
        }

        rotation = ResolveSurfaceRotation(area.transform, hit.normal);
        return true;
    }

    private static bool IsRejectedPrimarySurface(IncidentOriginArea area, RaycastHit hit)
    {
        if (area == null || hit.collider == null)
        {
            return true;
        }

        Vector3 normalizedNormal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
        float upDot = Vector3.Dot(normalizedNormal, Vector3.up);
        if (upDot <= DownFacingSurfaceDotThreshold)
        {
            return true;
        }

        bool isFloor = upDot >= UpFacingSurfaceDotThreshold;
        bool isWall = !isFloor;
        switch (area.PrimarySurfaceMode)
        {
            case IncidentOriginArea.NormalRoomFirePrimarySurfaceMode.FloorOnly:
                return !isFloor;

            case IncidentOriginArea.NormalRoomFirePrimarySurfaceMode.WallOnly:
                return !isWall;

            case IncidentOriginArea.NormalRoomFirePrimarySurfaceMode.FloorOrWall:
            default:
                return false;
        }
    }

    private static Quaternion ResolveSurfaceRotation(Transform areaTransform, Vector3 surfaceNormal)
    {
        Vector3 up = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        Vector3 projectedForward = Vector3.ProjectOnPlane(areaTransform.forward, up);
        if (projectedForward.sqrMagnitude < 0.0001f)
        {
            projectedForward = Vector3.ProjectOnPlane(Vector3.forward, up);
        }

        if (projectedForward.sqrMagnitude < 0.0001f)
        {
            projectedForward = Vector3.Cross(up, Vector3.right);
        }

        if (projectedForward.sqrMagnitude < 0.0001f)
        {
            projectedForward = Vector3.Cross(up, Vector3.up);
        }

        return Quaternion.LookRotation(projectedForward.normalized, up);
    }
}
