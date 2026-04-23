using UnityEngine;

public static class IncidentNormalFirePlacementUtility
{
    private const int SampleCount = 12;
    private const float VerticalMargin = 0.35f;

    public static bool TryResolvePlacement(IncidentOriginArea area, out Vector3 position, out Quaternion rotation)
    {
        position = area != null ? area.transform.position : Vector3.zero;
        rotation = area != null ? area.transform.rotation : Quaternion.identity;
        if (area == null)
        {
            return false;
        }

        Bounds bounds = area.GetAreaBounds();
        float halfHeight = Mathf.Max(0.5f, bounds.extents.y + VerticalMargin);
        LayerMask mask = area.SurfacePlacementMask;
        QueryTriggerInteraction triggerInteraction = area.SurfacePlacementTriggerInteraction;

        for (int i = 0; i < SampleCount; i++)
        {
            Vector3 start = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                bounds.center.y + halfHeight,
                Random.Range(bounds.min.z, bounds.max.z));
            if (TryResolveHit(area, start, (halfHeight * 2f) + 1f, mask, triggerInteraction, out position, out rotation))
            {
                return true;
            }
        }

        Vector3 centerStart = new Vector3(bounds.center.x, bounds.center.y + halfHeight, bounds.center.z);
        if (TryResolveHit(area, centerStart, (halfHeight * 2f) + 1f, mask, triggerInteraction, out position, out rotation))
        {
            return true;
        }

        position = area.transform.position;
        rotation = area.transform.rotation;
        return true;
    }

    private static bool TryResolveHit(
        IncidentOriginArea area,
        Vector3 start,
        float distance,
        LayerMask mask,
        QueryTriggerInteraction triggerInteraction,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = start;
        rotation = area.transform.rotation;

        if (!Physics.Raycast(start, Vector3.down, out RaycastHit hit, distance, mask, triggerInteraction))
        {
            return false;
        }

        position = hit.point + (hit.normal * area.SurfaceOffset);
        Vector3 projectedForward = Vector3.ProjectOnPlane(area.transform.forward, hit.normal);
        if (projectedForward.sqrMagnitude < 0.0001f)
        {
            projectedForward = Vector3.ProjectOnPlane(Vector3.forward, hit.normal);
        }

        if (projectedForward.sqrMagnitude < 0.0001f)
        {
            projectedForward = Vector3.Cross(hit.normal, Vector3.right);
        }

        if (projectedForward.sqrMagnitude < 0.0001f)
        {
            projectedForward = Vector3.Cross(hit.normal, Vector3.up);
        }

        rotation = Quaternion.LookRotation(projectedForward.normalized, hit.normal);
        return true;
    }
}
