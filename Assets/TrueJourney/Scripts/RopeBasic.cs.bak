using System.Collections.Generic;
using UnityEngine;

public class RopeBasic : MonoBehaviour
{
    [Header("Endpoints")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;

    [Header("Rope Body")]
    [SerializeField] private bool tubeMode = true;
    [SerializeField] private bool autoSegmentCount = true;
    [SerializeField] private bool usePhysics = true;
    [Min(0.1f)]
    [SerializeField] private float spacingMultiplier = 1f; // 1 = touching, >1 = gap, <1 = overlap
    [SerializeField] private bool padEndsByRadius = true;
    [Min(1)]
    [SerializeField] private int minSegments = 2;
    [Min(1)]
    [SerializeField] private int maxSegments = 200;
    [Min(1)]
    [SerializeField] private int segmentCount = 8;
    [SerializeField] private GameObject spherePrefab;
    [SerializeField] private GameObject bodyPrefab;
    [SerializeField] private float sphereScale = 0.15f;
    [SerializeField] private bool removeColliders = true;

    [Header("Physics")]
    [Min(1)]
    [SerializeField] private int solverIterations = 8;
    [SerializeField] private Vector3 gravity = new Vector3(0f, -9.81f, 0f);
    [Range(0.0f, 1.0f)]
    [SerializeField] private float damping = 0.98f;
    [SerializeField] private bool resetOnEnable = true;
    [SerializeField] private bool preserveSimulationOnResize = true;
    [Min(1)]
    [SerializeField] private int physicsSubsteps = 2;
    [Min(0f)]
    [SerializeField] private float maxStepDistance = 0f; // 0 = no clamp

    [Header("Collision")]
    [SerializeField] private bool enableCollision = true;
    [SerializeField] private bool useSegmentCollision = true;
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private bool includeTriggers = false;
    [Min(0.1f)]
    [SerializeField] private float collisionRadiusMultiplier = 1f;
    [Min(0f)]
    [SerializeField] private float surfaceOffset = 0.001f;
    [Min(1)]
    [SerializeField] private int collisionIterations = 1;
    [Min(1)]
    [SerializeField] private int collisionMaxOverlaps = 8;
    [SerializeField] private bool collisionDampenVelocity = true;

    [Header("Runtime (Debug)")]
    [SerializeField] private Transform startCap;
    [SerializeField] private Transform endCap;
    [SerializeField] private List<Transform> segments = new List<Transform>();

    private bool lastTubeMode;
    private Vector3[] nodePositions;
    private Vector3[] nodePrevPositions;
    private float restSegmentLength;
    private Collider[] overlapBuffer;
    private CapsuleCollider queryCapsule;

    private void Awake()
    {
        lastTubeMode = tubeMode;
        EnsureOverlapBuffer();
        Build(preserveSimulation: false);
        UpdateSegments();
    }

    private void OnEnable()
    {
        if (usePhysics)
        {
            if (resetOnEnable)
                ResetSimulation();
            else
                EnsureNodes(preserveSimulationOnResize);
        }

        UpdateSegments();
    }

    private void Update()
    {
        if (!usePhysics)
            UpdateSegments();
    }

    private void FixedUpdate()
    {
        if (usePhysics)
            Simulate(Time.fixedDeltaTime);
    }

    private void LateUpdate()
    {
        if (usePhysics)
            UpdateSegments();
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        Build(preserveSimulation: false);
        UpdateSegments();
    }

    [ContextMenu("Reset Simulation")]
    public void ResetSimulation()
    {
        EnsureNodes(preserveSimulation: false);
        UpdateSegments();
    }

    private void Build(bool preserveSimulation, bool forceRecreateSegments = false)
    {
        int minForMode = GetMinSegmentsForMode();
        minSegments = Mathf.Max(1, minSegments);
        maxSegments = Mathf.Max(minSegments, maxSegments);
        segmentCount = Mathf.Clamp(segmentCount, minForMode, maxSegments);

        if (segments == null)
            segments = new List<Transform>();
        segments.RemoveAll(s => s == null);

        if (forceRecreateSegments && segments.Count > 0)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i] != null)
                    DestroySegment(segments[i].gameObject);
            }
            segments.Clear();
        }

        if (tubeMode)
        {
            EnsureCaps();
        }
        else
        {
            DestroyCap(ref startCap);
            DestroyCap(ref endCap);
        }

        for (int i = segments.Count - 1; i >= segmentCount; i--)
        {
            if (segments[i] != null)
                DestroySegment(segments[i].gameObject);
            segments.RemoveAt(i);
        }

        for (int i = segments.Count; i < segmentCount; i++)
        {
            GameObject seg = tubeMode ? CreateBodySegment(i) : CreateSphereSegment(i);

            segments.Add(seg.transform);
        }

        EnsureNodes(preserveSimulation);
        EnsureOverlapBuffer();
    }

    private void UpdateSegments()
    {
        if (startPoint == null || endPoint == null)
            return;

        if (lastTubeMode != tubeMode)
        {
            lastTubeMode = tubeMode;
            Build(preserveSimulation: false, forceRecreateSegments: true);
            if (usePhysics)
                ResetSimulation();
        }

        Vector3 rawA = startPoint.position;
        Vector3 rawB = endPoint.position;
        Vector3 dir = rawB - rawA;
        float rawDist = dir.magnitude;
        Vector3 n = (rawDist > 0.0001f) ? (dir / rawDist) : transform.up;

        Vector3 a = rawA;
        Vector3 b = rawB;
        if (!usePhysics && padEndsByRadius && rawDist > 0.0001f)
        {
            float radius = GetSphereDiameter() * 0.5f;
            float pad = Mathf.Min(radius, rawDist * 0.5f);
            a += n * pad;
            b -= n * pad;
        }

        float distance = (b - a).magnitude;

        if (autoSegmentCount)
        {
            int desired = CalculateSegmentCount(distance);
            if (desired != segmentCount || segments == null || segments.Count != desired)
            {
                segmentCount = desired;
                bool preserve = usePhysics && preserveSimulationOnResize;
                Build(preserveSimulation: preserve);
            }
        }

        if (segments == null || segments.Count == 0)
            return;

        if (usePhysics)
            UpdateFromNodes();
        else if (tubeMode)
            UpdateTube(rawA, rawB, a, b, n, distance);
        else
            UpdateSpheres(a, b);
    }

    private void Simulate(float dt)
    {
        if (startPoint == null || endPoint == null)
            return;

        EnsureNodes(preserveSimulationOnResize);
        UpdateRestSegmentLength();
        int count = nodePositions.Length;
        if (count <= 1)
            return;

        int substeps = Mathf.Max(1, physicsSubsteps);
        float stepDt = dt / substeps;

        for (int s = 0; s < substeps; s++)
        {
            // Pin endpoints
            nodePositions[0] = startPoint.position;
            nodePositions[count - 1] = endPoint.position;
            nodePrevPositions[0] = nodePositions[0];
            nodePrevPositions[count - 1] = nodePositions[count - 1];

            // Damping adjusted for substeps
            float stepDamping = Mathf.Pow(damping, 1f / substeps);

            // Verlet integration
            for (int i = 1; i < count - 1; i++)
            {
                Vector3 current = nodePositions[i];
                Vector3 velocity = (current - nodePrevPositions[i]) * stepDamping;

                if (maxStepDistance > 0f)
                {
                    float vLen = velocity.magnitude;
                    if (vLen > maxStepDistance)
                        velocity = velocity / vLen * maxStepDistance;
                }

                nodePrevPositions[i] = current;
                nodePositions[i] = current + velocity + gravity * (stepDt * stepDt);
            }

            // Constraints
            for (int iter = 0; iter < solverIterations; iter++)
            {
                nodePositions[0] = startPoint.position;
                nodePositions[count - 1] = endPoint.position;

                for (int i = 0; i < count - 1; i++)
                {
                    int j = i + 1;
                    Vector3 p1 = nodePositions[i];
                    Vector3 p2 = nodePositions[j];
                    Vector3 delta = p2 - p1;
                    float dist = delta.magnitude;
                    if (dist <= 0.0001f)
                        continue;

                    float diff = (dist - restSegmentLength) / dist;
                    bool pin1 = (i == 0);
                    bool pin2 = (j == count - 1);

                    if (pin1 && pin2)
                        continue;
                    if (pin1)
                    {
                        p2 -= delta * diff;
                    }
                    else if (pin2)
                    {
                        p1 += delta * diff;
                    }
                    else
                    {
                        Vector3 correction = delta * (0.5f * diff);
                        p1 += correction;
                        p2 -= correction;
                    }

                    nodePositions[i] = p1;
                    nodePositions[j] = p2;
                }
            }

            if (enableCollision && collisionIterations > 0)
            {
                for (int c = 0; c < collisionIterations; c++)
                    ResolveCollisions();
            }
        }
    }

    private void UpdateFromNodes()
    {
        if (nodePositions == null || nodePositions.Length == 0)
            return;

        if (tubeMode)
        {
            EnsureCaps();

            if (startCap != null)
            {
                startCap.position = nodePositions[0];
                startCap.localScale = Vector3.one * sphereScale;
            }

            if (endCap != null)
            {
                endCap.position = nodePositions[nodePositions.Length - 1];
                endCap.localScale = Vector3.one * sphereScale;
            }

            int count = segments.Count;
            for (int i = 0; i < count; i++)
            {
                Transform seg = segments[i];
                if (seg == null) continue;

                Vector3 p1 = nodePositions[i];
                Vector3 p2 = nodePositions[i + 1];
                Vector3 dir = p2 - p1;
                float len = dir.magnitude;
                if (len <= 0.0001f)
                    continue;

                Vector3 mid = (p1 + p2) * 0.5f;
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir / len);
                float radius = GetSphereDiameter() * 0.5f;
                Vector3 scale = new Vector3(radius * 2f, len * 0.5f, radius * 2f);

                seg.position = mid;
                seg.rotation = rot;
                seg.localScale = scale;
            }
        }
        else
        {
            int count = segments.Count;
            for (int i = 0; i < count; i++)
            {
                Transform seg = segments[i];
                if (seg == null) continue;

                seg.position = nodePositions[i];
                seg.localScale = Vector3.one * sphereScale;
            }
        }
    }

    private void UpdateTube(Vector3 rawA, Vector3 rawB, Vector3 a, Vector3 b, Vector3 n, float distance)
    {
        EnsureCaps();

        if (startCap != null)
        {
            startCap.position = rawA;
            startCap.localScale = Vector3.one * sphereScale;
        }

        if (endCap != null)
        {
            endCap.position = rawB;
            endCap.localScale = Vector3.one * sphereScale;
        }

        int count = segments.Count;
        if (count <= 0) return;

        float segLength = (count > 0) ? (distance / count) : 0f;
        Quaternion rot = (rawB != rawA) ? Quaternion.FromToRotation(Vector3.up, n) : Quaternion.identity;

        float radius = GetSphereDiameter() * 0.5f;
        float scaleY = Mathf.Max(0.0001f, segLength * 0.5f);
        Vector3 segScale = new Vector3(radius * 2f, scaleY, radius * 2f);

        for (int i = 0; i < count; i++)
        {
            Transform seg = segments[i];
            if (seg == null) continue;

            float t = (i + 0.5f) / count;
            seg.position = Vector3.Lerp(a, b, t);
            seg.rotation = rot;
            seg.localScale = segScale;
        }
    }

    private void UpdateSpheres(Vector3 a, Vector3 b)
    {
        int count = segments.Count;
        for (int i = 0; i < count; i++)
        {
            float t = (count == 1) ? 0f : (float)i / (count - 1);
            Transform seg = segments[i];
            if (seg == null) continue;

            seg.position = Vector3.Lerp(a, b, t);
            seg.localScale = Vector3.one * sphereScale;
        }
    }

    private void EnsureCaps()
    {
        if (startCap == null)
            startCap = CreateSphere("RopeCap_Start").transform;
        if (endCap == null)
            endCap = CreateSphere("RopeCap_End").transform;
    }

    private void DestroyCap(ref Transform cap)
    {
        if (cap == null) return;
        DestroySegment(cap.gameObject);
        cap = null;
    }

    private GameObject CreateSphereSegment(int index)
    {
        GameObject seg = CreateSphere($"RopeSphere_{index}");
        seg.transform.localScale = Vector3.one * sphereScale;
        return seg;
    }

    private GameObject CreateBodySegment(int index)
    {
        GameObject seg;
        if (bodyPrefab != null)
        {
            seg = Instantiate(bodyPrefab, transform);
            seg.name = $"RopeBody_{index}";
        }
        else
        {
            seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            seg.name = $"RopeBody_{index}";
            seg.transform.SetParent(transform, false);
        }

        RemoveCollidersIfNeeded(seg);
        return seg;
    }

    private GameObject CreateSphere(string name)
    {
        GameObject seg;
        if (spherePrefab != null)
        {
            seg = Instantiate(spherePrefab, transform);
            seg.name = name;
        }
        else
        {
            seg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            seg.name = name;
            seg.transform.SetParent(transform, false);
        }

        RemoveCollidersIfNeeded(seg);
        return seg;
    }

    private void RemoveCollidersIfNeeded(GameObject seg)
    {
        if (!removeColliders || seg == null) return;

        Collider[] cols = seg.GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++)
            DestroySegment(cols[i]);
    }

    private int CalculateSegmentCount(float distance)
    {
        float diameter = GetSphereDiameter();
        if (diameter <= 0.0001f)
            return GetMinSegmentsForMode();

        float spacing = diameter * Mathf.Max(0.1f, spacingMultiplier);
        int desired = tubeMode ? Mathf.CeilToInt(distance / spacing) : (Mathf.CeilToInt(distance / spacing) + 1);
        return Mathf.Clamp(desired, GetMinSegmentsForMode(), maxSegments);
    }

    private float GetSphereDiameter()
    {
        return Mathf.Max(0.0001f, sphereScale);
    }

    private int GetMinSegmentsForMode()
    {
        int min = Mathf.Max(1, minSegments);
        return tubeMode ? min : Mathf.Max(2, min);
    }

    private int GetNodeCount()
    {
        return tubeMode ? (segmentCount + 1) : segmentCount;
    }

    private void EnsureNodes(bool preserveSimulation)
    {
        int count = GetNodeCount();
        if (count <= 0)
            return;

        if (nodePositions == null || nodePrevPositions == null ||
            nodePositions.Length != count || nodePrevPositions.Length != count)
        {
            ResizeNodes(count, preserveSimulation);
        }

        UpdateRestSegmentLength();
    }

    private void ResizeNodes(int newCount, bool preserveSimulation)
    {
        if (newCount <= 0)
            return;

        Vector3[] newPos = new Vector3[newCount];
        Vector3[] newPrev = new Vector3[newCount];

        if (!preserveSimulation || nodePositions == null || nodePrevPositions == null || nodePositions.Length <= 0)
        {
            InitializeNodes(newPos, newPrev);
            nodePositions = newPos;
            nodePrevPositions = newPrev;
            return;
        }

        int oldCount = nodePositions.Length;

        int oldInterior = Mathf.Max(0, oldCount - 2);
        int newInterior = Mathf.Max(0, newCount - 2);
        int keep = Mathf.Min(oldInterior, newInterior);

        // Keep endpoints
        newPos[0] = nodePositions[0];
        newPrev[0] = nodePrevPositions[0];
        newPos[newCount - 1] = nodePositions[oldCount - 1];
        newPrev[newCount - 1] = nodePrevPositions[oldCount - 1];

        // Keep as many interior nodes as possible
        for (int i = 0; i < keep; i++)
        {
            newPos[i + 1] = nodePositions[i + 1];
            newPrev[i + 1] = nodePrevPositions[i + 1];
        }

        // Add new nodes near the end (if lengthened)
        if (newInterior > oldInterior)
        {
            int extra = newInterior - oldInterior;
            Vector3 from = (keep > 0) ? newPos[keep] : newPos[0];
            Vector3 to = newPos[newCount - 1];
            for (int e = 0; e < extra; e++)
            {
                float t = (float)(e + 1) / (extra + 1);
                Vector3 p = Vector3.Lerp(from, to, t);
                newPos[keep + 1 + e] = p;
                newPrev[keep + 1 + e] = p;
            }
        }

        nodePositions = newPos;
        nodePrevPositions = newPrev;
    }

    private void InitializeNodes(Vector3[] pos, Vector3[] prev)
    {
        if (startPoint == null || endPoint == null)
            return;

        int count = Mathf.Min(pos.Length, prev.Length);
        if (count <= 0)
            return;

        Vector3 a = startPoint.position;
        Vector3 b = endPoint.position;
        for (int i = 0; i < count; i++)
        {
            float t = (count == 1) ? 0f : (float)i / (count - 1);
            Vector3 p = Vector3.Lerp(a, b, t);
            pos[i] = p;
            prev[i] = p;
        }
    }

    private void UpdateRestSegmentLength()
    {
        restSegmentLength = Mathf.Max(0.0001f, GetSphereDiameter() * Mathf.Max(0.1f, spacingMultiplier));
    }

    private void ResolveCollisions()
    {
        if (!enableCollision)
            return;

        if (nodePositions == null || nodePositions.Length <= 2)
            return;

        if (useSegmentCollision)
            ResolveSegmentCollisions();
        else
            ResolveNodeCollisions();
    }

    private void ResolveSegmentCollisions()
    {
        EnsureOverlapBuffer();
        EnsureQueryCapsule();

        float radius = GetSphereDiameter() * 0.5f * Mathf.Max(0.1f, collisionRadiusMultiplier);
        if (radius <= 0.0001f)
            return;

        QueryTriggerInteraction q = includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        int last = nodePositions.Length - 1;

        for (int i = 0; i < last; i++)
        {
            Vector3 p1 = nodePositions[i];
            Vector3 p2 = nodePositions[i + 1];
            Vector3 prev1 = nodePrevPositions[i];
            Vector3 prev2 = nodePrevPositions[i + 1];

            // Sweep capsule from previous segment to current segment to reduce tunneling
            Vector3 prevMid = (prev1 + prev2) * 0.5f;
            Vector3 currMid = (p1 + p2) * 0.5f;
            Vector3 sweep = currMid - prevMid;
            float sweepDist = sweep.magnitude;

            if (sweepDist > 0.0001f)
            {
                Vector3 sweepDir = sweep / sweepDist;
                if (Physics.CapsuleCast(prev1, prev2, radius, sweepDir, out RaycastHit hit, sweepDist, collisionMask, q))
                {
                    Vector3 targetMid = prevMid + sweepDir * hit.distance;
                    Vector3 normal = hit.normal;
                    Vector3 shift = (targetMid - currMid) + normal * surfaceOffset;
                    ApplySegmentCorrection(i, shift, normal);
                    p1 = nodePositions[i];
                    p2 = nodePositions[i + 1];
                }
            }

            int hitCount = Physics.OverlapCapsuleNonAlloc(p1, p2, radius, overlapBuffer, collisionMask, q);
            for (int h = 0; h < hitCount; h++)
            {
                Collider col = overlapBuffer[h];
                if (col == null) continue;

                ConfigureQueryCapsule(p1, p2, radius);
                if (Physics.ComputePenetration(
                        queryCapsule, queryCapsule.transform.position, queryCapsule.transform.rotation,
                        col, col.transform.position, col.transform.rotation,
                        out Vector3 pushDir, out float pushDist))
                {
                    Vector3 normal = pushDir.normalized;
                    ApplySegmentCorrection(i, pushDir * (pushDist + surfaceOffset), normal);
                    p1 = nodePositions[i];
                    p2 = nodePositions[i + 1];
                }
            }
        }
    }

    private void ResolveNodeCollisions()
    {
        EnsureOverlapBuffer();
        float radius = GetSphereDiameter() * 0.5f * Mathf.Max(0.1f, collisionRadiusMultiplier);
        if (radius <= 0.0001f)
            return;

        QueryTriggerInteraction q = includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        int last = nodePositions.Length - 1;

        for (int i = 1; i < last; i++)
        {
            Vector3 prev = nodePrevPositions[i];
            Vector3 pos = nodePositions[i];
            Vector3 dir = pos - prev;
            float dist = dir.magnitude;
            bool moved = false;

            if (dist > 0.0001f)
            {
                if (Physics.SphereCast(prev, radius, dir / dist, out RaycastHit hit, dist, collisionMask, q))
                {
                    pos = hit.point + hit.normal * (radius + surfaceOffset);
                    moved = true;
                }
            }

            int hitCount = Physics.OverlapSphereNonAlloc(pos, radius, overlapBuffer, collisionMask, q);
            for (int h = 0; h < hitCount; h++)
            {
                Collider col = overlapBuffer[h];
                if (col == null) continue;
                Vector3 closest = col.ClosestPoint(pos);
                Vector3 delta = pos - closest;
                float d = delta.magnitude;

                if (d > 0.0001f && d < radius)
                {
                    pos += (delta / d) * (radius - d + surfaceOffset);
                    moved = true;
                }
                else if (d <= 0.0001f)
                {
                    Vector3 fallback = (pos - col.bounds.center);
                    if (fallback.sqrMagnitude < 0.0001f)
                        fallback = Vector3.up;
                    pos += fallback.normalized * (radius + surfaceOffset);
                    moved = true;
                }
            }

            nodePositions[i] = pos;
            if (moved && collisionDampenVelocity)
            {
                // Slide along the surface instead of full stop
                Vector3 vel = nodePositions[i] - nodePrevPositions[i];
                Vector3 normal = (pos - prev).normalized; 
                if (normal.sqrMagnitude > 0.0001f)
                {
                    float proj = Vector3.Dot(vel, normal);
                    if (proj < 0)
                    {
                        vel -= normal * proj;
                        nodePrevPositions[i] = nodePositions[i] - vel;
                    }
                }
            }
        }
    }

    private void ApplySegmentCorrection(int index, Vector3 correction, Vector3 normal)
    {
        int last = nodePositions.Length - 1;
        bool pin1 = (index == 0);
        bool pin2 = (index + 1 == last);

        if (pin1 && pin2)
            return;

        if (pin1)
        {
            nodePositions[index + 1] += correction;
            if (collisionDampenVelocity)
            {
                Vector3 vel = nodePositions[index + 1] - nodePrevPositions[index + 1];
                float proj = Vector3.Dot(vel, normal);
                if (proj < 0)
                {
                    vel -= normal * proj;
                    nodePrevPositions[index + 1] = nodePositions[index + 1] - vel;
                }
            }
        }
        else if (pin2)
        {
            nodePositions[index] += correction;
            if (collisionDampenVelocity)
            {
                Vector3 vel = nodePositions[index] - nodePrevPositions[index];
                float proj = Vector3.Dot(vel, normal);
                if (proj < 0)
                {
                    vel -= normal * proj;
                    nodePrevPositions[index] = nodePositions[index] - vel;
                }
            }
        }
        else
        {
            Vector3 half = correction * 0.5f;
            nodePositions[index] += half;
            nodePositions[index + 1] += half;
            if (collisionDampenVelocity)
            {
                Vector3 vel1 = nodePositions[index] - nodePrevPositions[index];
                float proj1 = Vector3.Dot(vel1, normal);
                if (proj1 < 0)
                {
                    vel1 -= normal * proj1;
                    nodePrevPositions[index] = nodePositions[index] - vel1;
                }

                Vector3 vel2 = nodePositions[index + 1] - nodePrevPositions[index + 1];
                float proj2 = Vector3.Dot(vel2, normal);
                if (proj2 < 0)
                {
                    vel2 -= normal * proj2;
                    nodePrevPositions[index + 1] = nodePositions[index + 1] - vel2;
                }
            }
        }
    }

    private void EnsureOverlapBuffer()
    {
        if (collisionMaxOverlaps < 1)
            collisionMaxOverlaps = 1;

        if (overlapBuffer == null || overlapBuffer.Length != collisionMaxOverlaps)
            overlapBuffer = new Collider[collisionMaxOverlaps];
    }

    private void EnsureQueryCapsule()
    {
        if (queryCapsule != null) return;

        GameObject go = new GameObject("Rope_QueryCapsule");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.SetParent(transform, false);

        queryCapsule = go.AddComponent<CapsuleCollider>();
        queryCapsule.isTrigger = true;
        queryCapsule.direction = 1;
    }

    private void ConfigureQueryCapsule(Vector3 p1, Vector3 p2, float radius)
    {
        if (queryCapsule == null) return;

        Vector3 dir = p2 - p1;
        float len = dir.magnitude;
        Vector3 up = (len > 0.0001f) ? (dir / len) : Vector3.up;

        Transform t = queryCapsule.transform;
        t.position = (p1 + p2) * 0.5f;
        t.rotation = Quaternion.FromToRotation(Vector3.up, up);
        t.localScale = Vector3.one;

        queryCapsule.radius = radius;
        queryCapsule.height = Mathf.Max(radius * 2f, len + radius * 2f);
    }

    private void DestroySegment(Object obj)
    {
        if (obj == null) return;

        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }
}
