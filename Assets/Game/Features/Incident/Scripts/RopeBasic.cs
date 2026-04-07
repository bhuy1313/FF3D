using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class RopeCurve
{
    public Transform startPoint;
    public Transform endPoint;

    [NonSerialized] public MeshFilter meshFilter;
    [NonSerialized] public MeshRenderer meshRenderer;
    [NonSerialized] public Mesh mesh;
    [NonSerialized] public MeshFilter bakedMeshFilter;
    [NonSerialized] public MeshRenderer bakedMeshRenderer;
    [NonSerialized] public Mesh bakedMesh;
    [NonSerialized] public Vector3[] renderPositions;
    [NonSerialized] public Vector3[] nodePositions;
    [NonSerialized] public Vector3[] nodePrevPositions;
    [NonSerialized] public bool[] nodeGrounded;
    [NonSerialized] public int activeSegmentCount;
    [NonSerialized] public float restSegmentLength;
    [NonSerialized] public Vector3 lastEndPointPosition;
    [NonSerialized] public float pendingGrowthDistance;
    [NonSerialized] public bool growthTrackingInitialized;
    [NonSerialized] public Vector3[] meshVertices;
    [NonSerialized] public Vector3[] meshNormals;
    [NonSerialized] public Vector2[] meshUvs;
    [NonSerialized] public int[] meshTriangles;
    [NonSerialized] public Vector3[] bakedMeshVertices;
    [NonSerialized] public Vector3[] bakedMeshNormals;
    [NonSerialized] public Vector2[] bakedMeshUvs;
    [NonSerialized] public int[] bakedMeshTriangles;
    [NonSerialized] public List<Vector3> bakedPositions = new List<Vector3>();
}

public class RopeBasic : MonoBehaviour
{
    private const int MinSegments = 2;
    private const int PhysicsSubsteps = 1;
    private const int CollisionMaxOverlaps = 8;
    private const float CollisionRadiusMultiplier = 1f;
    private const float SurfaceOffset = 0.001f;
    private const float GroundBakeNormalMinY = 0.1f;
    private const float GroundProjectionDistance = 1000f;
    private const bool CollisionDampenVelocity = true;
    private const QueryTriggerInteraction TriggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Ropes")]
    [SerializeField] private List<RopeCurve> ropes = new List<RopeCurve>();

    [Header("Rope Setup")]
    [SerializeField] private bool usePhysics = true;
    [Min(0.1f)]
    [SerializeField] private float spacingMultiplier = 1f;
    [Min(0)]
    [SerializeField] private int maxSegments = 200;
    [FormerlySerializedAs("sphereScale")]
    [SerializeField] private float ropeWidth = 0.15f;
    [FormerlySerializedAs("lineMaterial")]
    [SerializeField] private Material ropeMaterial;
    [Range(3, 12)]
    [SerializeField] private int radialSegments = 6;
    [Min(4)]
    [SerializeField] private int maxDynamicTailNodes = 12;

    [Header("Physics")]
    [Min(1)]
    [SerializeField] private int solverIterations = 8;
    [SerializeField] private Vector3 gravity = new Vector3(0f, -9.81f, 0f);
    [Range(0.0f, 1.0f)]
    [SerializeField] private float damping = 0.98f;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [Min(1)]
    [SerializeField] private int collisionIterations = 1;

    private Collider[] overlapBuffer;
    private Material defaultRenderMaterial;

    private void Awake()
    {
        EnsureOverlapBuffer();
        CleanupLegacyVisuals();
        BuildAll(preserveSimulation: false);
        UpdateSegments();
    }

    private void OnEnable()
    {
        if (usePhysics)
            ForceResetNodes();

        UpdateSegments();
    }

    private void OnDestroy()
    {
        foreach (RopeCurve rope in ropes)
            DestroyRenderMeshes(rope);

        if (defaultRenderMaterial != null)
            DestroyUnityObject(defaultRenderMaterial);
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
        BuildAll(preserveSimulation: false);
        UpdateSegments();
    }

    [ContextMenu("Reset Simulation")]
    public void ResetSimulation()
    {
        ForceResetNodes();
        UpdateSegments();
    }

    private void BuildAll(bool preserveSimulation)
    {
        ClampSharedSettings();

        foreach (RopeCurve rope in ropes)
            BuildRope(rope, GetBuildSegmentCount(rope), preserveSimulation);

        EnsureOverlapBuffer();
    }

    private int GetBuildSegmentCount(RopeCurve rope)
    {
        int desired = GetDesiredSegmentCount(rope);
        if (!usePhysics)
            return desired;

        return ClampDynamicSegmentCount(desired);
    }

    private void BuildRope(RopeCurve rope, int desiredSegmentCount, bool preserveSimulation)
    {
        if (rope == null)
            return;

        if (!HasEndpoints(rope))
        {
            rope.activeSegmentCount = 0;
            ClearNodeState(rope);
            SetRenderMeshesActive(rope, false);
            return;
        }

        if (!preserveSimulation)
            ResetBakedState(rope);

        rope.activeSegmentCount = usePhysics
            ? ClampDynamicSegmentCount(desiredSegmentCount)
            : ClampSegmentCount(desiredSegmentCount);
        rope.restSegmentLength = CalculateRestSegmentLength();

        EnsureDynamicRenderMesh(rope);
        EnsureBakedRenderMesh(rope);
        SetRenderMeshesActive(rope, true);
        EnsureNodes(rope, preserveSimulation);
    }

    private void UpdateSegments()
    {
        foreach (RopeCurve rope in ropes)
        {
            if (rope == null)
                continue;

            if (!TryGetEndpoints(rope, out Vector3 start, out Vector3 end, out float distance))
            {
                rope.activeSegmentCount = 0;
                ClearNodeState(rope);
                SetRenderMeshesActive(rope, false);
                continue;
            }

            bool needsRebuild =
                rope.meshFilter == null ||
                rope.meshRenderer == null ||
                rope.mesh == null;

            if (!usePhysics)
            {
                int desiredSegmentCount = GetDesiredSegmentCount(rope, distance);
                needsRebuild |= rope.activeSegmentCount != desiredSegmentCount;

                if (needsRebuild)
                    BuildRope(rope, desiredSegmentCount, preserveSimulation: false);
                else
                    SetRenderMeshesActive(rope, true);
            }
            else
            {
                if (needsRebuild)
                {
                    int rebuildSegmentCount = rope.activeSegmentCount > 0 ? rope.activeSegmentCount : GetDesiredSegmentCount(rope, distance);
                    BuildRope(rope, rebuildSegmentCount, preserveSimulation: true);
                }
                else
                {
                    SetRenderMeshesActive(rope, true);
                }
            }

            if (rope.activeSegmentCount <= 0)
                continue;

            if (usePhysics)
            {
                EnsureNodes(rope, preserveSimulation: true);
                GrowRopeIfNeeded(rope);
                UpdateSegmentObjects(rope, rope.nodePositions);
            }
            else
            {
                UpdateStaticSegments(rope, start, end);
            }
        }
    }

    private void Simulate(float dt)
    {
        EnsureAllNodes(preserveSimulation: true);

        int substeps = PhysicsSubsteps;
        float stepDt = dt / substeps;
        float stepDamping = Mathf.Pow(damping, 1f / substeps);

        for (int s = 0; s < substeps; s++)
        {
            foreach (RopeCurve rope in ropes)
            {
                if (!HasEndpoints(rope) || rope.nodePositions == null || rope.nodePrevPositions == null)
                    continue;

                int count = rope.nodePositions.Length;
                if (count <= 1)
                    continue;

                rope.restSegmentLength = CalculateRestSegmentLength();
                GrowRopeIfNeeded(rope);
                count = rope.nodePositions.Length;
                int simulationStartIndex = GetSimulationStartIndex(rope);
                Vector3 dynamicStart = GetDynamicStartPosition(rope);
                rope.nodePositions[0] = dynamicStart;
                rope.nodePositions[count - 1] = rope.endPoint.position;
                rope.nodePrevPositions[0] = rope.nodePositions[0];
                rope.nodePrevPositions[count - 1] = rope.nodePositions[count - 1];

                if (rope.nodeGrounded != null)
                {
                    rope.nodeGrounded[0] = false;
                    rope.nodeGrounded[count - 1] = false;
                    for (int i = simulationStartIndex + 1; i < count - 1; i++)
                        rope.nodeGrounded[i] = false;
                }

                for (int i = 1; i <= simulationStartIndex; i++)
                    rope.nodePrevPositions[i] = rope.nodePositions[i];

                for (int i = simulationStartIndex + 1; i < count - 1; i++)
                {
                    Vector3 current = rope.nodePositions[i];
                    Vector3 velocity = (current - rope.nodePrevPositions[i]) * stepDamping;
                    rope.nodePrevPositions[i] = current;
                    rope.nodePositions[i] = current + velocity + gravity * (stepDt * stepDt);
                }

                for (int iter = 0; iter < solverIterations; iter++)
                {
                    rope.nodePositions[0] = dynamicStart;
                    rope.nodePositions[count - 1] = rope.endPoint.position;

                    for (int i = simulationStartIndex; i < count - 1; i++)
                    {
                        int next = i + 1;
                        Vector3 p1 = rope.nodePositions[i];
                        Vector3 p2 = rope.nodePositions[next];
                        Vector3 delta = p2 - p1;
                        float distance = delta.magnitude;
                        if (distance <= 0.0001f)
                            continue;

                        float difference = (distance - rope.restSegmentLength) / distance;
                        bool pin1 = IsPinnedNode(rope, i);
                        bool pin2 = IsPinnedNode(rope, next);

                        if (pin1 && pin2)
                            continue;

                        if (pin1)
                        {
                            p2 -= delta * difference;
                        }
                        else if (pin2)
                        {
                            p1 += delta * difference;
                        }
                        else
                        {
                            Vector3 correction = delta * (0.5f * difference);
                            p1 += correction;
                            p2 -= correction;
                        }

                        rope.nodePositions[i] = p1;
                        rope.nodePositions[next] = p2;
                    }
                }

                for (int c = 0; c < collisionIterations; c++)
                    ResolveNodeCollisions(rope);

                TryBakeFrozenPrefix(rope);
            }
        }
    }

    private void UpdateStaticSegments(RopeCurve rope, Vector3 start, Vector3 end)
    {
        int pointCount = GetPointCount(rope.activeSegmentCount);
        if (pointCount <= 0)
            return;

        EnsureRenderBuffer(rope, pointCount);
        for (int i = 0; i < pointCount; i++)
        {
            float t = pointCount == 1 ? 0f : (float)i / (pointCount - 1);
            rope.renderPositions[i] = Vector3.Lerp(start, end, t);
        }

        UpdateSegmentObjects(rope, rope.renderPositions);
    }

    private void UpdateSegmentObjects(RopeCurve rope, Vector3[] positions)
    {
        if (rope == null || rope.mesh == null || positions == null)
            return;

        UpdateTubeMesh(
            rope.meshFilter.transform,
            rope.mesh,
            positions,
            ref rope.meshVertices,
            ref rope.meshNormals,
            ref rope.meshUvs,
            ref rope.meshTriangles);
    }

    private void UpdateTubeMesh(
        Transform meshTransform,
        Mesh mesh,
        Vector3[] positions,
        ref Vector3[] vertices,
        ref Vector3[] normals,
        ref Vector2[] uvs,
        ref int[] triangles)
    {
        if (mesh == null)
            return;

        int pointCount = positions != null ? positions.Length : 0;
        int ringSegments = Mathf.Max(3, radialSegments);
        if (pointCount < 2)
        {
            mesh.Clear();
            return;
        }

        int vertexCount = pointCount * ringSegments;
        mesh.indexFormat =
            vertexCount > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

        EnsureMeshBuffers(ref vertices, ref normals, ref uvs, ref triangles, pointCount, ringSegments);

        float radius = Mathf.Max(0.0001f, ropeWidth * 0.5f);
        float lengthTiling = 0f;
        Vector3 previousNormal = Vector3.zero;

        for (int i = 0; i < pointCount; i++)
        {
            Vector3 center = meshTransform != null
                ? meshTransform.InverseTransformPoint(positions[i])
                : positions[i];
            Vector3 tangent = GetLocalTangent(meshTransform, positions, i);
            Vector3 normal = GetRingNormal(tangent, previousNormal);
            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
            normal = Vector3.Cross(binormal, tangent).normalized;
            previousNormal = normal;

            if (i > 0)
                lengthTiling += Vector3.Distance(positions[i - 1], positions[i]);

            int ringStart = i * ringSegments;
            for (int r = 0; r < ringSegments; r++)
            {
                float angle = (Mathf.PI * 2f * r) / ringSegments;
                Vector3 radialOffset = (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * radius;
                int vertexIndex = ringStart + r;
                vertices[vertexIndex] = center + radialOffset;
                normals[vertexIndex] = radialOffset.normalized;
                uvs[vertexIndex] = new Vector2((float)r / ringSegments, lengthTiling);
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private Vector3 GetLocalTangent(Transform meshTransform, Vector3[] positions, int index)
    {
        Vector3 tangent = GetTangent(positions, index);
        if (meshTransform != null)
            tangent = meshTransform.InverseTransformDirection(tangent);

        if (tangent.sqrMagnitude <= 0.000001f)
            return Vector3.up;

        return tangent.normalized;
    }

    private void EnsureMeshBuffers(
        ref Vector3[] vertices,
        ref Vector3[] normals,
        ref Vector2[] uvs,
        ref int[] triangles,
        int pointCount,
        int ringSegments)
    {
        int vertexCount = pointCount * ringSegments;
        if (vertices == null || vertices.Length != vertexCount)
        {
            vertices = new Vector3[vertexCount];
            normals = new Vector3[vertexCount];
            uvs = new Vector2[vertexCount];
        }

        int triangleCount = Mathf.Max(0, (pointCount - 1) * ringSegments * 6);
        if (triangles == null || triangles.Length != triangleCount)
            triangles = BuildTubeTriangles(pointCount, ringSegments);
    }

    private int[] BuildTubeTriangles(int pointCount, int ringSegments)
    {
        int[] triangles = new int[Mathf.Max(0, (pointCount - 1) * ringSegments * 6)];
        int triangleIndex = 0;

        for (int i = 0; i < pointCount - 1; i++)
        {
            int ringStart = i * ringSegments;
            int nextRingStart = (i + 1) * ringSegments;
            for (int r = 0; r < ringSegments; r++)
            {
                int next = (r + 1) % ringSegments;
                int a = ringStart + r;
                int b = ringStart + next;
                int c = nextRingStart + r;
                int d = nextRingStart + next;

                triangles[triangleIndex++] = a;
                triangles[triangleIndex++] = c;
                triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = c;
                triangles[triangleIndex++] = d;
            }
        }

        return triangles;
    }

    private Vector3 GetTangent(Vector3[] positions, int index)
    {
        int last = positions.Length - 1;
        Vector3 tangent;
        if (index <= 0)
            tangent = positions[1] - positions[0];
        else if (index >= last)
            tangent = positions[last] - positions[last - 1];
        else
            tangent = positions[index + 1] - positions[index - 1];

        if (tangent.sqrMagnitude <= 0.000001f)
            return Vector3.up;

        return tangent.normalized;
    }

    private Vector3 GetRingNormal(Vector3 tangent, Vector3 previousNormal)
    {
        if (previousNormal.sqrMagnitude > 0.000001f)
        {
            Vector3 projected = previousNormal - tangent * Vector3.Dot(previousNormal, tangent);
            if (projected.sqrMagnitude > 0.000001f)
                return projected.normalized;
        }

        Vector3 reference = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
        Vector3 normal = Vector3.Cross(tangent, reference);
        if (normal.sqrMagnitude <= 0.000001f)
            normal = Vector3.Cross(tangent, Vector3.forward);

        return normal.normalized;
    }

    private void EnsureDynamicRenderMesh(RopeCurve rope)
    {
        if (rope == null || (rope.meshFilter != null && rope.meshRenderer != null && rope.mesh != null))
            return;

        GameObject meshObject = new GameObject($"RopeMesh_{ropes.IndexOf(rope)}");
        meshObject.transform.SetParent(transform, false);

        MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        Material resolvedMaterial = ropeMaterial != null ? ropeMaterial : CreateDefaultRenderMaterial();
        if (resolvedMaterial != null)
            meshRenderer.sharedMaterial = resolvedMaterial;

        Mesh mesh = new Mesh
        {
            name = $"RopeMeshRuntime_{ropes.IndexOf(rope)}"
        };
        mesh.MarkDynamic();
        meshFilter.sharedMesh = mesh;

        rope.meshFilter = meshFilter;
        rope.meshRenderer = meshRenderer;
        rope.mesh = mesh;
    }

    private void EnsureBakedRenderMesh(RopeCurve rope)
    {
        if (rope == null || (rope.bakedMeshFilter != null && rope.bakedMeshRenderer != null && rope.bakedMesh != null))
            return;

        GameObject meshObject = new GameObject($"RopeBakedMesh_{ropes.IndexOf(rope)}");
        meshObject.transform.SetParent(transform, false);

        MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        Material resolvedMaterial = ropeMaterial != null ? ropeMaterial : CreateDefaultRenderMaterial();
        if (resolvedMaterial != null)
            meshRenderer.sharedMaterial = resolvedMaterial;

        Mesh mesh = new Mesh
        {
            name = $"RopeBakedMeshRuntime_{ropes.IndexOf(rope)}"
        };
        mesh.MarkDynamic();
        meshFilter.sharedMesh = mesh;

        rope.bakedMeshFilter = meshFilter;
        rope.bakedMeshRenderer = meshRenderer;
        rope.bakedMesh = mesh;
        SetBakedRenderMeshActive(rope, false);
    }

    private Material CreateDefaultRenderMaterial()
    {
        if (defaultRenderMaterial != null)
            return defaultRenderMaterial;

        Shader shader =
            Shader.Find("Universal Render Pipeline/Lit") ??
            Shader.Find("Standard") ??
            Shader.Find("Sprites/Default");
        if (shader == null)
            return null;

        defaultRenderMaterial = new Material(shader);
        return defaultRenderMaterial;
    }

    private void SetDynamicRenderMeshActive(RopeCurve rope, bool active)
    {
        if (rope?.meshFilter != null && rope.meshFilter.gameObject.activeSelf != active)
            rope.meshFilter.gameObject.SetActive(active);
    }

    private void SetBakedRenderMeshActive(RopeCurve rope, bool active)
    {
        if (rope?.bakedMeshFilter != null && rope.bakedMeshFilter.gameObject.activeSelf != active)
            rope.bakedMeshFilter.gameObject.SetActive(active);
    }

    private void SetRenderMeshesActive(RopeCurve rope, bool active)
    {
        SetDynamicRenderMeshActive(rope, active);
        if (!active)
            SetBakedRenderMeshActive(rope, false);
    }

    private void DestroyRenderMeshes(RopeCurve rope)
    {
        if (rope == null)
            return;

        if (rope.mesh != null)
        {
            DestroyUnityObject(rope.mesh);
            rope.mesh = null;
        }

        if (rope.meshFilter != null)
            DestroyUnityObject(rope.meshFilter.gameObject);

        if (rope.bakedMesh != null)
        {
            DestroyUnityObject(rope.bakedMesh);
            rope.bakedMesh = null;
        }

        if (rope.bakedMeshFilter != null)
            DestroyUnityObject(rope.bakedMeshFilter.gameObject);

        rope.meshFilter = null;
        rope.meshRenderer = null;
        rope.bakedMeshFilter = null;
        rope.bakedMeshRenderer = null;
    }

    private void CleanupLegacyVisuals()
    {
        foreach (Transform child in transform)
        {
            if (child == null)
                continue;

            if (child.name.StartsWith("RopeSphere_") ||
                child.name.StartsWith("RopeLine_") ||
                child.name.StartsWith("RopeMesh_") ||
                child.name.StartsWith("RopeBakedMesh_"))
            {
                DestroyUnityObject(child.gameObject);
            }
        }
    }

    private void UpdateBakedRenderMesh(RopeCurve rope)
    {
        if (rope == null)
            return;

        if (rope.bakedPositions == null || rope.bakedPositions.Count < 2)
        {
            if (rope.bakedMesh != null)
                rope.bakedMesh.Clear();

            SetBakedRenderMeshActive(rope, false);
            return;
        }

        EnsureBakedRenderMesh(rope);
        Vector3[] bakedPoints = rope.bakedPositions.ToArray();
        UpdateTubeMesh(
            rope.bakedMeshFilter.transform,
            rope.bakedMesh,
            bakedPoints,
            ref rope.bakedMeshVertices,
            ref rope.bakedMeshNormals,
            ref rope.bakedMeshUvs,
            ref rope.bakedMeshTriangles);
        SetBakedRenderMeshActive(rope, true);
    }

    private void ResetBakedState(RopeCurve rope)
    {
        if (rope == null)
            return;

        if (rope.bakedPositions == null)
            rope.bakedPositions = new List<Vector3>();
        else
            rope.bakedPositions.Clear();

        rope.bakedMeshVertices = null;
        rope.bakedMeshNormals = null;
        rope.bakedMeshUvs = null;
        rope.bakedMeshTriangles = null;

        if (rope.bakedMesh != null)
            rope.bakedMesh.Clear();

        SetBakedRenderMeshActive(rope, false);
    }

    private Vector3 GetDynamicStartPosition(RopeCurve rope)
    {
        if (rope != null && rope.bakedPositions != null && rope.bakedPositions.Count > 0)
            return rope.bakedPositions[rope.bakedPositions.Count - 1];

        return rope != null && rope.startPoint != null ? rope.startPoint.position : Vector3.zero;
    }

    private int GetDesiredSegmentCount(RopeCurve rope)
    {
        if (!TryGetEndpoints(rope, out _, out _, out float distance))
            return MinSegments;

        return GetDesiredSegmentCount(rope, distance);
    }

    private int GetDesiredSegmentCount(RopeCurve rope, float distance)
    {
        int desired = CalculateSegmentCount(distance);
        if (rope != null && rope.activeSegmentCount > 0)
            desired = Mathf.Max(desired, rope.activeSegmentCount);

        return desired;
    }

    private int CalculateSegmentCount(float distance)
    {
        float diameter = GetRopeDiameter();
        if (diameter <= 0.0001f)
            return MinSegments;

        float spacing = diameter * Mathf.Max(0.1f, spacingMultiplier);
        int desired = Mathf.CeilToInt(distance / spacing);
        return ClampSegmentCount(desired);
    }

    private int ClampSegmentCount(int desiredSegmentCount)
    {
        int clampedMinimum = Mathf.Max(MinSegments, desiredSegmentCount);
        if (maxSegments <= 0)
            return clampedMinimum;

        return Mathf.Min(clampedMinimum, maxSegments);
    }

    private int ClampDynamicSegmentCount(int desiredSegmentCount)
    {
        int clamped = ClampSegmentCount(desiredSegmentCount);
        int maxDynamicSegments = Mathf.Max(MinSegments, maxDynamicTailNodes - 1);
        return Mathf.Min(clamped, maxDynamicSegments);
    }

    private float GetRopeDiameter()
    {
        return Mathf.Max(0.0001f, ropeWidth);
    }

    private float CalculateRestSegmentLength()
    {
        return Mathf.Max(0.0001f, GetRopeDiameter() * Mathf.Max(0.1f, spacingMultiplier));
    }

    private int GetPointCount(int activeSegmentCount)
    {
        return activeSegmentCount + 1;
    }

    private void EnsureAllNodes(bool preserveSimulation)
    {
        foreach (RopeCurve rope in ropes)
            EnsureNodes(rope, preserveSimulation);
    }

    private void ForceResetNodes()
    {
        foreach (RopeCurve rope in ropes)
            ResetBakedState(rope);

        EnsureAllNodes(preserveSimulation: false);
    }

    private bool IsPinnedNode(RopeCurve rope, int index)
    {
        if (rope == null || rope.nodePositions == null)
            return false;

        int last = rope.nodePositions.Length - 1;
        return index <= 0 || index >= last;
    }

    private void EnsureNodes(RopeCurve rope, bool preserveSimulation)
    {
        if (!HasEndpoints(rope) || rope.activeSegmentCount <= 0)
        {
            ClearNodeState(rope);
            return;
        }

        int count = GetPointCount(rope.activeSegmentCount);
        bool needsResize =
            rope.nodePositions == null ||
            rope.nodePrevPositions == null ||
            rope.nodePositions.Length != count ||
            rope.nodePrevPositions.Length != count;

        if (needsResize || !preserveSimulation)
            ResizeNodes(rope, count, preserveSimulation);

        rope.restSegmentLength = CalculateRestSegmentLength();
    }

    private void ClearNodeState(RopeCurve rope)
    {
        ResetBakedState(rope);
        rope.nodePositions = null;
        rope.nodePrevPositions = null;
        rope.nodeGrounded = null;
        rope.renderPositions = null;
        rope.meshVertices = null;
        rope.meshNormals = null;
        rope.meshUvs = null;
        rope.meshTriangles = null;
        if (rope.mesh != null)
            rope.mesh.Clear();
        rope.lastEndPointPosition = Vector3.zero;
        rope.pendingGrowthDistance = 0f;
        rope.growthTrackingInitialized = false;
    }

    private void ResizeNodes(RopeCurve rope, int newCount, bool preserveSimulation)
    {
        Vector3[] newPositions = new Vector3[newCount];
        Vector3[] newPreviousPositions = new Vector3[newCount];
        bool[] newGrounded = new bool[newCount];

        if (!preserveSimulation || rope.nodePositions == null || rope.nodePrevPositions == null || rope.nodePositions.Length <= 0)
        {
            InitializeNodes(rope, newPositions, newPreviousPositions, newGrounded);
            rope.nodePositions = newPositions;
            rope.nodePrevPositions = newPreviousPositions;
            rope.nodeGrounded = newGrounded;
            ResetGrowthTracking(rope);
            return;
        }

        int oldCount = rope.nodePositions.Length;
        int oldInterior = Mathf.Max(0, oldCount - 2);
        int newInterior = Mathf.Max(0, newCount - 2);
        int keep = Mathf.Min(oldInterior, newInterior);

        newPositions[0] = rope.nodePositions[0];
        newPreviousPositions[0] = rope.nodePrevPositions[0];
        newPositions[newCount - 1] = rope.nodePositions[oldCount - 1];
        newPreviousPositions[newCount - 1] = rope.nodePrevPositions[oldCount - 1];

        for (int i = 0; i < keep; i++)
        {
            newPositions[i + 1] = rope.nodePositions[i + 1];
            newPreviousPositions[i + 1] = rope.nodePrevPositions[i + 1];
            if (rope.nodeGrounded != null && i + 1 < rope.nodeGrounded.Length)
                newGrounded[i + 1] = rope.nodeGrounded[i + 1];
        }

        if (newInterior > oldInterior)
        {
            int extra = newInterior - oldInterior;
            Vector3 from = keep > 0 ? newPositions[keep] : newPositions[0];
            Vector3 to = newPositions[newCount - 1];
            for (int e = 0; e < extra; e++)
            {
                float t = (float)(e + 1) / (extra + 1);
                Vector3 position = Vector3.Lerp(from, to, t);
                newPositions[keep + 1 + e] = position;
                newPreviousPositions[keep + 1 + e] = position;
            }
        }

        rope.nodePositions = newPositions;
        rope.nodePrevPositions = newPreviousPositions;
        rope.nodeGrounded = newGrounded;

        if (rope.endPoint != null && !rope.growthTrackingInitialized)
        {
            rope.lastEndPointPosition = rope.endPoint.position;
            rope.growthTrackingInitialized = true;
        }
    }

    private void InitializeNodes(RopeCurve rope, Vector3[] positions, Vector3[] previousPositions, bool[] grounded)
    {
        if (!HasEndpoints(rope))
            return;

        int count = Mathf.Min(positions.Length, previousPositions.Length);
        Vector3 start = GetDynamicStartPosition(rope);
        Vector3 end = rope.endPoint.position;
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0f : (float)i / (count - 1);
            Vector3 position = Vector3.Lerp(start, end, t);
            positions[i] = position;
            previousPositions[i] = position;
            grounded[i] = false;
        }
    }

    private int GetSimulationStartIndex(RopeCurve rope)
    {
        return 0;
    }

    private void GrowRopeIfNeeded(RopeCurve rope)
    {
        if (rope == null || rope.nodePositions == null || rope.nodePrevPositions == null || rope.activeSegmentCount <= 0 || rope.endPoint == null)
            return;

        float restLength = CalculateRestSegmentLength();
        if (restLength <= 0.0001f)
            return;

        if (!rope.growthTrackingInitialized)
        {
            rope.lastEndPointPosition = rope.endPoint.position;
            rope.growthTrackingInitialized = true;
        }

        float movedDistance = Vector3.Distance(rope.lastEndPointPosition, rope.endPoint.position);
        rope.lastEndPointPosition = rope.endPoint.position;
        if (movedDistance <= 0.0001f)
            return;

        rope.pendingGrowthDistance += movedDistance;
        int extraSegmentsNeeded = Mathf.FloorToInt(rope.pendingGrowthDistance / restLength);
        if (extraSegmentsNeeded <= 0)
            return;

        int maxAllowedSegments = maxSegments <= 0 ? int.MaxValue : maxSegments;
        int targetSegmentCount = Mathf.Min(maxAllowedSegments, rope.activeSegmentCount + extraSegmentsNeeded);
        if (targetSegmentCount <= rope.activeSegmentCount)
            return;

        int appendedSegmentCount = targetSegmentCount - rope.activeSegmentCount;
        AppendSegmentsBeforeEnd(rope, appendedSegmentCount);
        rope.pendingGrowthDistance = Mathf.Max(0f, rope.pendingGrowthDistance - (appendedSegmentCount * restLength));
    }

    private void TryBakeFrozenPrefix(RopeCurve rope)
    {
        if (rope == null || rope.nodePositions == null)
            return;

        int maxTailNodes = Mathf.Max(4, maxDynamicTailNodes);
        int overflow = rope.nodePositions.Length - maxTailNodes;
        if (overflow <= 0)
            return;

        int trimCount = GetGroundedTrimCount(rope, overflow);
        if (trimCount <= 0)
        {
            ProjectOverflowPrefixToGround(rope, overflow);
            trimCount = GetGroundedTrimCount(rope, overflow);
        }

        if (trimCount <= 0)
            return;

        AppendBakedPrefix(rope, trimCount);
        TrimDynamicPrefix(rope, trimCount);
        UpdateBakedRenderMesh(rope);
    }

    private void ProjectOverflowPrefixToGround(RopeCurve rope, int maxTrimCount)
    {
        if (rope == null || rope.nodePositions == null || rope.nodePrevPositions == null || rope.nodeGrounded == null)
            return;

        float radius = GetRopeDiameter() * 0.5f * CollisionRadiusMultiplier;
        if (radius <= 0.0001f)
            return;

        int maxAllowedTrim = Mathf.Min(maxTrimCount, rope.nodePositions.Length - 2);
        float lift = Mathf.Max(radius * 2f, rope.restSegmentLength);
        float castDistance = lift + GroundProjectionDistance;

        for (int i = 1; i <= maxAllowedTrim; i++)
        {
            if (rope.nodeGrounded[i])
                continue;

            Vector3 origin = rope.nodePositions[i] + Vector3.up * lift;
            if (!Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, castDistance, collisionMask, TriggerInteraction))
                break;

            if (hit.normal.y < GroundBakeNormalMinY)
                break;

            Vector3 groundedPosition = hit.point + hit.normal * (radius + SurfaceOffset);
            rope.nodePositions[i] = groundedPosition;
            rope.nodePrevPositions[i] = groundedPosition;
            rope.nodeGrounded[i] = true;
        }
    }

    private int GetGroundedTrimCount(RopeCurve rope, int maxTrimCount)
    {
        if (rope == null || rope.nodePositions == null || rope.nodeGrounded == null)
            return 0;

        int maxAllowedTrim = Mathf.Min(maxTrimCount, rope.nodePositions.Length - 2);
        int trimCount = 0;
        for (int i = 1; i <= maxAllowedTrim; i++)
        {
            if (!rope.nodeGrounded[i])
                break;

            trimCount = i;
        }

        return trimCount;
    }

    private void AppendBakedPrefix(RopeCurve rope, int trimCount)
    {
        if (rope == null || rope.nodePositions == null || trimCount <= 0)
            return;

        if (rope.bakedPositions == null)
            rope.bakedPositions = new List<Vector3>();

        int startIndex = rope.bakedPositions.Count > 0 ? 1 : 0;
        for (int i = startIndex; i <= trimCount; i++)
            rope.bakedPositions.Add(rope.nodePositions[i]);
    }

    private void TrimDynamicPrefix(RopeCurve rope, int trimCount)
    {
        if (rope == null || rope.nodePositions == null || trimCount <= 0)
            return;

        int oldCount = rope.nodePositions.Length;
        int newCount = oldCount - trimCount;
        if (newCount < 2)
            return;

        Vector3[] newPositions = new Vector3[newCount];
        Vector3[] newPreviousPositions = new Vector3[newCount];
        bool[] newGrounded = new bool[newCount];

        Array.Copy(rope.nodePositions, trimCount, newPositions, 0, newCount);
        Array.Copy(rope.nodePrevPositions, trimCount, newPreviousPositions, 0, newCount);
        Array.Copy(rope.nodeGrounded, trimCount, newGrounded, 0, newCount);

        rope.nodePositions = newPositions;
        rope.nodePrevPositions = newPreviousPositions;
        rope.nodeGrounded = newGrounded;
        rope.activeSegmentCount = Mathf.Max(MinSegments, rope.activeSegmentCount - trimCount);
    }

    private void AppendSegmentsBeforeEnd(RopeCurve rope, int extraSegmentCount)
    {
        if (rope == null || rope.nodePositions == null || rope.nodePrevPositions == null || extraSegmentCount <= 0)
            return;

        int oldCount = rope.nodePositions.Length;
        int newCount = oldCount + extraSegmentCount;
        Vector3[] newPositions = new Vector3[newCount];
        Vector3[] newPreviousPositions = new Vector3[newCount];
        bool[] newGrounded = new bool[newCount];

        int insertIndex = oldCount - 1;
        Array.Copy(rope.nodePositions, 0, newPositions, 0, insertIndex);
        Array.Copy(rope.nodePrevPositions, 0, newPreviousPositions, 0, insertIndex);

        if (rope.nodeGrounded != null)
            Array.Copy(rope.nodeGrounded, 0, newGrounded, 0, Mathf.Min(rope.nodeGrounded.Length, insertIndex));

        Vector3 from = rope.nodePositions[insertIndex - 1];
        Vector3 to = rope.endPoint.position;
        for (int i = 0; i < extraSegmentCount; i++)
        {
            float t = (float)(i + 1) / (extraSegmentCount + 1);
            Vector3 position = Vector3.Lerp(from, to, t);
            int targetIndex = insertIndex + i;
            newPositions[targetIndex] = position;
            newPreviousPositions[targetIndex] = position;
            newGrounded[targetIndex] = false;
        }

        newPositions[newCount - 1] = rope.endPoint.position;
        newPreviousPositions[newCount - 1] = rope.endPoint.position;
        newGrounded[newCount - 1] = false;

        rope.nodePositions = newPositions;
        rope.nodePrevPositions = newPreviousPositions;
        rope.nodeGrounded = newGrounded;
        rope.activeSegmentCount += extraSegmentCount;
        rope.restSegmentLength = CalculateRestSegmentLength();

        EnsureDynamicRenderMesh(rope);
        SetDynamicRenderMeshActive(rope, true);
    }

    private void ResetGrowthTracking(RopeCurve rope)
    {
        if (rope == null)
            return;

        if (rope.endPoint == null)
            return;

        rope.lastEndPointPosition = rope.endPoint.position;
        rope.pendingGrowthDistance = 0f;
        rope.growthTrackingInitialized = true;
    }

    private void EnsureRenderBuffer(RopeCurve rope, int count)
    {
        if (rope.renderPositions == null || rope.renderPositions.Length != count)
            rope.renderPositions = new Vector3[count];
    }

    private void ResolveNodeCollisions(RopeCurve rope)
    {
        if (rope == null || rope.nodePositions == null || rope.nodePrevPositions == null || rope.nodePositions.Length < 3)
            return;

        EnsureOverlapBuffer();

        float radius = GetRopeDiameter() * 0.5f * CollisionRadiusMultiplier;
        if (radius <= 0.0001f)
            return;

        int last = rope.nodePositions.Length - 1;
        int simulationStartIndex = GetSimulationStartIndex(rope);
        for (int i = simulationStartIndex + 1; i < last; i++)
        {
            Vector3 previous = rope.nodePrevPositions[i];
            Vector3 position = rope.nodePositions[i];
            Vector3 direction = position - previous;
            float distance = direction.magnitude;
            bool moved = false;
            bool grounded = false;
            Vector3 collisionNormal = Vector3.zero;

            if (distance > 0.0001f && Physics.SphereCast(previous, radius, direction / distance, out RaycastHit hit, distance, collisionMask, TriggerInteraction))
            {
                position = hit.point + hit.normal * (radius + SurfaceOffset);
                collisionNormal = hit.normal;
                moved = true;
                grounded |= hit.normal.y >= GroundBakeNormalMinY;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(position, radius, overlapBuffer, collisionMask, TriggerInteraction);
            for (int h = 0; h < hitCount; h++)
            {
                Collider collider = overlapBuffer[h];
                if (collider == null)
                    continue;

                Vector3 closest = GetClosestCollisionPoint(collider, position);
                Vector3 delta = position - closest;
                float overlapDistance = delta.magnitude;

                if (overlapDistance > 0.0001f && overlapDistance < radius)
                {
                    collisionNormal = delta / overlapDistance;
                    position += collisionNormal * (radius - overlapDistance + SurfaceOffset);
                    moved = true;
                    grounded |= collisionNormal.y >= GroundBakeNormalMinY;
                }
                else if (overlapDistance <= 0.0001f)
                {
                    Vector3 fallback = position - collider.bounds.center;
                    if (fallback.sqrMagnitude < 0.0001f)
                        fallback = Vector3.up;

                    collisionNormal = fallback.normalized;
                    position += collisionNormal * (radius + SurfaceOffset);
                    moved = true;
                    grounded |= collisionNormal.y >= GroundBakeNormalMinY;
                }
            }

            rope.nodePositions[i] = position;
            if (rope.nodeGrounded != null && i < rope.nodeGrounded.Length)
                rope.nodeGrounded[i] = grounded;
            if (moved && CollisionDampenVelocity && collisionNormal.sqrMagnitude > 0.0001f)
            {
                Vector3 velocity = rope.nodePositions[i] - rope.nodePrevPositions[i];
                float projection = Vector3.Dot(velocity, collisionNormal);
                if (projection < 0f)
                {
                    velocity -= collisionNormal * projection;
                    rope.nodePrevPositions[i] = rope.nodePositions[i] - velocity;
                }
            }

        }
    }

    private Vector3 GetClosestCollisionPoint(Collider collider, Vector3 position)
    {
        if (collider == null)
            return position;

        if (SupportsAccurateClosestPoint(collider))
            return collider.ClosestPoint(position);

        return collider.ClosestPointOnBounds(position);
    }

    private bool SupportsAccurateClosestPoint(Collider collider)
    {
        if (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider)
            return true;

        if (collider is MeshCollider meshCollider)
            return meshCollider.convex;

        return false;
    }

    private void EnsureOverlapBuffer()
    {
        if (overlapBuffer == null || overlapBuffer.Length != CollisionMaxOverlaps)
            overlapBuffer = new Collider[CollisionMaxOverlaps];
    }

    private bool HasEndpoints(RopeCurve rope)
    {
        return rope != null && rope.startPoint != null && rope.endPoint != null;
    }

    private bool TryGetEndpoints(RopeCurve rope, out Vector3 start, out Vector3 end, out float distance)
    {
        start = Vector3.zero;
        end = Vector3.zero;
        distance = 0f;

        if (!HasEndpoints(rope))
            return false;

        start = rope.startPoint.position;
        end = rope.endPoint.position;
        distance = Vector3.Distance(start, end);
        return true;
    }

    private void ClampSharedSettings()
    {
        maxSegments = Mathf.Max(0, maxSegments);
        radialSegments = Mathf.Max(3, radialSegments);
        maxDynamicTailNodes = Mathf.Max(4, maxDynamicTailNodes);
    }

    private void DestroyUnityObject(UnityEngine.Object obj)
    {
        if (obj == null)
            return;

        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }
}
