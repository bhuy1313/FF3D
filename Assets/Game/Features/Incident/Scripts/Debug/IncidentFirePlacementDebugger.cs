using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class IncidentFirePlacementDebugger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private IncidentPayloadAnchor anchor;
    [SerializeField] private IncidentFireSpawnProfile fireSpawnProfile;
    [SerializeField] private Transform seedTransform;

    [Header("Playback")]
    [SerializeField] private bool autoStartOnPlay = true;
    [SerializeField] [Min(0.05f)] private float attemptIntervalSeconds = 1f;
    [SerializeField] [Min(1)] private int maxSuccessfulPlacements = 10;
    [SerializeField] private bool chainFromLastSuccessfulPlacement = true;

    [Header("Fallback Seed")]
    [SerializeField] private Vector3 fallbackSeedSurfaceNormal = Vector3.up;

    [Header("Markers")]
    [SerializeField] [Min(0.02f)] private float markerScale = 0.16f;
    [SerializeField] private Color seedMarkerColor = new Color(0.2f, 1f, 0.35f, 1f);
    [SerializeField] private Color successMarkerColor = new Color(1f, 0.5f, 0.1f, 1f);
    [SerializeField] private Color promotedMarkerColor = new Color(0.1f, 0.8f, 1f, 1f);

    [Header("Trajectory Gizmos")]
    [SerializeField] private bool drawTrajectories = true;
    [SerializeField] private Color missTrajectoryColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color acceptedTrajectoryColor = new Color(1f, 0.5f, 0.1f, 0.9f);
    [SerializeField] private Color promotedTrajectoryColor = new Color(0.1f, 0.8f, 1f, 0.9f);
    [SerializeField] [Min(0.01f)] private float gizmoHitSphereRadius = 0.05f;

    private readonly List<Vector3> existingPositions = new List<Vector3>();
    private readonly List<GameObject> spawnedMarkers = new List<GameObject>();
    private readonly List<AttemptRecord> attempts = new List<AttemptRecord>();

    private Coroutine playbackRoutine;
    private Vector3 currentSeedPosition;
    private Vector3 currentSeedSurfaceNormal = Vector3.up;
    private int successfulPlacementCount;
    private int attemptSequence;

    private void Start()
    {
        if (autoStartOnPlay)
        {
            BeginDebugPlayback();
        }
    }

    [ContextMenu("Begin Debug Playback")]
    public void BeginDebugPlayback()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResetDebugVisualization();
        if (!TryInitializeSeed())
        {
            Debug.LogWarning($"{nameof(IncidentFirePlacementDebugger)} requires an anchor and fire spawn profile.", this);
            return;
        }

        playbackRoutine = StartCoroutine(RunPlayback());
    }

    [ContextMenu("Run Single Attempt")]
    public void RunSingleAttempt()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }

        if (!TryInitializeSeed())
        {
            Debug.LogWarning($"{nameof(IncidentFirePlacementDebugger)} requires an anchor and fire spawn profile.", this);
            return;
        }

        ExecuteAttempt();
    }

    [ContextMenu("Reset Debug Visualization")]
    public void ResetDebugVisualization()
    {
        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }

        successfulPlacementCount = 0;
        attemptSequence = 0;
        existingPositions.Clear();
        attempts.Clear();

        for (int i = 0; i < spawnedMarkers.Count; i++)
        {
            if (spawnedMarkers[i] != null)
            {
                Destroy(spawnedMarkers[i]);
            }
        }

        spawnedMarkers.Clear();
    }

    private IEnumerator RunPlayback()
    {
        WaitForSeconds delay = new WaitForSeconds(attemptIntervalSeconds);
        while (successfulPlacementCount < maxSuccessfulPlacements)
        {
            ExecuteAttempt();
            yield return delay;
        }

        playbackRoutine = null;
    }

    private void ExecuteAttempt()
    {
        if (anchor == null || fireSpawnProfile == null)
        {
            return;
        }

        attemptSequence++;
        int randomSeed = unchecked((System.Environment.TickCount * 397) ^ attemptSequence);
        if (!anchor.TryCreateDebugPlacementSample(
                fireSpawnProfile,
                currentSeedPosition,
                currentSeedSurfaceNormal,
                fireSpawnProfile.SecondaryFireRange,
                fireSpawnProfile.MinimumSecondaryFireSpacing,
                existingPositions,
                randomSeed,
                out IncidentPayloadAnchor.DebugPlacementSample sample))
        {
            attempts.Add(new AttemptRecord(sample, wasPromoted: false));
            return;
        }

        bool wasPromoted = WasPromoted(sample);
        attempts.Add(new AttemptRecord(sample, wasPromoted));
        existingPositions.Add(sample.PlacementPosition);
        SpawnMarker(
            sample.PlacementPosition,
            wasPromoted ? promotedMarkerColor : successMarkerColor,
            wasPromoted ? "PromotedPlacementMarker" : "PlacementMarker");
        successfulPlacementCount++;

        if (chainFromLastSuccessfulPlacement)
        {
            currentSeedPosition = sample.PlacementPosition;
            currentSeedSurfaceNormal = sample.PlacementSurfaceNormal;
        }
    }

    private bool TryInitializeSeed()
    {
        ResolveReferences();
        if (anchor == null || fireSpawnProfile == null)
        {
            return false;
        }

        if (existingPositions.Count > 0)
        {
            return true;
        }

        Vector3 seedPosition = seedTransform != null ? seedTransform.position : transform.position;
        Vector3 seedNormal = seedTransform != null ? seedTransform.up : fallbackSeedSurfaceNormal;
        currentSeedPosition = seedPosition;
        currentSeedSurfaceNormal = seedNormal.sqrMagnitude > 0.0001f ? seedNormal.normalized : Vector3.up;
        existingPositions.Add(currentSeedPosition);
        SpawnMarker(currentSeedPosition, seedMarkerColor, "SeedMarker");
        return true;
    }

    private void ResolveReferences()
    {
        if (anchor == null)
        {
            anchor = GetComponent<IncidentPayloadAnchor>();
        }
    }

    private void SpawnMarker(Vector3 worldPosition, Color color, string objectName)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = objectName;
        marker.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
        marker.transform.localScale = Vector3.one * markerScale;

        Collider markerCollider = marker.GetComponent<Collider>();
        if (markerCollider != null)
        {
            Destroy(markerCollider);
        }

        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }

        spawnedMarkers.Add(marker);
    }

    private bool WasPromoted(IncidentPayloadAnchor.DebugPlacementSample sample)
    {
        if (!sample.Success || sample.Traces == null)
        {
            return false;
        }

        for (int i = 0; i < sample.Traces.Length; i++)
        {
            IncidentPayloadAnchor.DebugPlacementTrace trace = sample.Traces[i];
            if (!trace.Accepted)
            {
                continue;
            }

            if ((trace.ResolvedPoint - sample.PlacementPosition).sqrMagnitude <= 0.0001f)
            {
                return trace.PromotedToTopSurface;
            }
        }

        return false;
    }

    private void OnDrawGizmos()
    {
        if (!drawTrajectories || attempts.Count == 0)
        {
            return;
        }

        for (int attemptIndex = 0; attemptIndex < attempts.Count; attemptIndex++)
        {
            AttemptRecord attempt = attempts[attemptIndex];
            IncidentPayloadAnchor.DebugPlacementTrace[] traces = attempt.Sample.Traces;
            if (traces == null)
            {
                continue;
            }

            for (int traceIndex = 0; traceIndex < traces.Length; traceIndex++)
            {
                DrawTrace(traces[traceIndex]);
            }
        }
    }

    private void DrawTrace(IncidentPayloadAnchor.DebugPlacementTrace trace)
    {
        Vector3[] pathPoints = trace.PathPoints;
        if (pathPoints == null || pathPoints.Length <= 0)
        {
            return;
        }

        Gizmos.color = trace.PromotedToTopSurface
            ? promotedTrajectoryColor
            : (trace.Accepted ? acceptedTrajectoryColor : missTrajectoryColor);
        for (int i = 1; i < pathPoints.Length; i++)
        {
            Gizmos.DrawLine(pathPoints[i - 1], pathPoints[i]);
        }

        if (trace.HadHit)
        {
            Gizmos.DrawWireSphere(trace.HitPoint, gizmoHitSphereRadius);
        }

        if (trace.PromotedToTopSurface)
        {
            Gizmos.DrawSphere(trace.ResolvedPoint, gizmoHitSphereRadius * 0.8f);
        }
    }

    private readonly struct AttemptRecord
    {
        public AttemptRecord(IncidentPayloadAnchor.DebugPlacementSample sample, bool wasPromoted)
        {
            Sample = sample;
            WasPromoted = wasPromoted;
        }

        public IncidentPayloadAnchor.DebugPlacementSample Sample { get; }
        public bool WasPromoted { get; }
    }
}
