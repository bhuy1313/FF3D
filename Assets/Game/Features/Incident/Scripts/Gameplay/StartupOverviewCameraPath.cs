using System;
using UnityEngine;

[DisallowMultipleComponent]
public class StartupOverviewCameraPath : MonoBehaviour
{
    [Header("Shot")]
    [SerializeField] private Transform lookAtTarget;
    [SerializeField] private Transform[] waypoints = Array.Empty<Transform>();
    [SerializeField] private bool useWaypointRotations;

    [Header("Timing")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float holdDuration = 1.6f;

    [Header("Lens")]
    [SerializeField, Range(20f, 90f)] private float fieldOfView = 38f;

    public Transform LookAtTarget => lookAtTarget;
    public bool UseWaypointRotations => useWaypointRotations;
    public float Speed => Mathf.Max(0.01f, speed);
    public float HoldDuration => Mathf.Max(0f, holdDuration);
    public float FieldOfView => Mathf.Clamp(fieldOfView, 20f, 90f);

    public int ValidWaypointCount
    {
        get
        {
            int count = 0;
            if (waypoints == null)
            {
                return count;
            }

            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] != null)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public bool TryGetWaypoint(int validIndex, out Transform waypoint)
    {
        waypoint = null;
        if (validIndex < 0 || waypoints == null)
        {
            return false;
        }

        int currentValidIndex = 0;
        for (int i = 0; i < waypoints.Length; i++)
        {
            Transform candidate = waypoints[i];
            if (candidate == null)
            {
                continue;
            }

            if (currentValidIndex == validIndex)
            {
                waypoint = candidate;
                return true;
            }

            currentValidIndex++;
        }

        return false;
    }
}
