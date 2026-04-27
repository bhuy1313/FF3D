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
    [SerializeField] [Min(1)] private int requestedActiveFireCount = 4;

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

    private readonly List<GameObject> spawnedMarkers = new List<GameObject>();
    private readonly List<AttemptRecord> revealedAttempts = new List<AttemptRecord>();

    private Coroutine playbackRoutine;
    private IncidentPayloadAnchor.DebugRuntimePlacementSession runtimeSession;
    private Vector3 primarySeedPosition;
    private Quaternion primarySeedRotation = Quaternion.identity;
    private int nextAttemptIndexToReveal;

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
        if (!TryPrepareSession())
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

        if (!TryPrepareSession())
        {
            Debug.LogWarning($"{nameof(IncidentFirePlacementDebugger)} requires an anchor and fire spawn profile.", this);
            return;
        }

        RevealNextAttempt();
    }

    [ContextMenu("Reset Debug Visualization")]
    public void ResetDebugVisualization()
    {
        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }

        runtimeSession = default;
        nextAttemptIndexToReveal = 0;
        revealedAttempts.Clear();

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
        while (RevealNextAttempt())
        {
            yield return delay;
        }

        playbackRoutine = null;
    }

    private bool TryPrepareSession()
    {
        ResolveReferences();
        if (anchor == null || fireSpawnProfile == null)
        {
            return false;
        }

        Vector3 seedPosition = seedTransform != null ? seedTransform.position : transform.position;
        Vector3 seedNormal = seedTransform != null ? seedTransform.up : fallbackSeedSurfaceNormal;
        primarySeedPosition = seedPosition;
        Vector3 resolvedSeedNormal = seedNormal.sqrMagnitude > 0.0001f ? seedNormal.normalized : Vector3.up;
        primarySeedRotation = ResolveSeedRotation(resolvedSeedNormal);

        SpawnMarker(primarySeedPosition, seedMarkerColor, "SeedMarker");

        int activeSeed = unchecked(System.Environment.TickCount * 397);
        int latentSeed = unchecked((System.Environment.TickCount * 733) ^ 0x5F3759DF);
        if (!anchor.TryCreateDebugRuntimePlacementSession(
                fireSpawnProfile,
                primarySeedPosition,
                primarySeedRotation,
                requestedActiveFireCount,
                activeSeed,
                latentSeed,
                out runtimeSession))
        {
            return false;
        }

        nextAttemptIndexToReveal = 0;
        return true;
    }

    private Quaternion ResolveSeedRotation(Vector3 seedNormal)
    {
        Vector3 up = seedNormal.sqrMagnitude > 0.0001f ? seedNormal.normalized : Vector3.up;
        Vector3 projectedForward = Vector3.ProjectOnPlane(
            transform.forward.sqrMagnitude > 0.0001f ? transform.forward : Vector3.forward,
            up);
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
            projectedForward = Vector3.forward;
        }

        return Quaternion.LookRotation(projectedForward.normalized, up);
    }

    private bool RevealNextAttempt()
    {
        if (runtimeSession.Attempts == null || nextAttemptIndexToReveal >= runtimeSession.Attempts.Length)
        {
            return false;
        }

        IncidentPayloadAnchor.DebugRuntimePlacementAttempt attempt = runtimeSession.Attempts[nextAttemptIndexToReveal++];
        bool wasPromoted = WasPromoted(attempt);
        revealedAttempts.Add(new AttemptRecord(attempt));
        if (attempt.Success)
        {
            SpawnMarker(
                attempt.PlacementPosition,
                wasPromoted ? promotedMarkerColor : successMarkerColor,
                wasPromoted ? "PromotedPlacementMarker" : "PlacementMarker");
        }

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

    private static bool WasPromoted(IncidentPayloadAnchor.DebugRuntimePlacementAttempt attempt)
    {
        IncidentPayloadAnchor.DebugPlacementTrace[] traces = attempt.Traces;
        if (!attempt.Success || traces == null)
        {
            return false;
        }

        for (int i = 0; i < traces.Length; i++)
        {
            IncidentPayloadAnchor.DebugPlacementTrace trace = traces[i];
            if (!trace.Accepted)
            {
                continue;
            }

            if ((trace.ResolvedPoint - attempt.PlacementPosition).sqrMagnitude <= 0.0001f)
            {
                return trace.PromotedToTopSurface;
            }
        }

        return false;
    }

    private void OnDrawGizmos()
    {
        if (!drawTrajectories || revealedAttempts.Count == 0)
        {
            return;
        }

        for (int attemptIndex = 0; attemptIndex < revealedAttempts.Count; attemptIndex++)
        {
            IncidentPayloadAnchor.DebugPlacementTrace[] traces = revealedAttempts[attemptIndex].Attempt.Traces;
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
        public AttemptRecord(IncidentPayloadAnchor.DebugRuntimePlacementAttempt attempt)
        {
            Attempt = attempt;
        }

        public IncidentPayloadAnchor.DebugRuntimePlacementAttempt Attempt { get; }
    }
}
