using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(600)]
public class FireHoseTailVisual : MonoBehaviour
{
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private Material hoseMaterial;
    [SerializeField] private float sampleSpacing = 0.15f;
    [SerializeField] private float radius = 0.12f;
    [SerializeField] private int radialSegments = 12;
    [SerializeField] private int particleCount = 9;
    [SerializeField] private int constraintIterations = 16;
    [SerializeField] private float slackLength = 0.45f;
    [SerializeField] private float gravity = 14f;
    [SerializeField, Range(0f, 1f)] private float damping = 0.08f;
    [SerializeField] private float maxTimestep = 0.033f;

    private GameObject tailObject;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;
    private Vector3 lastUp = Vector3.up;
    private readonly List<Vector3> particles = new List<Vector3>();
    private readonly List<Vector3> previousParticles = new List<Vector3>();
    private readonly List<Knot> controlPoints = new List<Knot>();
    private bool hasSimulation;

    void LateUpdate()
    {
        EnsureTailObject();

        if (startPoint == null || endPoint == null)
        {
            ResetSimulation();
            ClearMesh();
            return;
        }

        Vector3 startWorld = startPoint.position;
        Vector3 endWorld = endPoint.position;
        Vector3 deltaWorld = endWorld - startWorld;
        float distance = deltaWorld.magnitude;

        if (distance <= 0.05f)
        {
            ResetSimulation();
            ClearMesh();
            return;
        }

        Simulate(startWorld, endWorld, distance);

        controlPoints.Clear();

        for (int i = 0; i < particles.Count; i++)
        {
            Vector3 localPoint = tailObject.transform.InverseTransformPoint(particles[i]);
            controlPoints.Add(new Knot(localPoint, Vector3.up));
        }

        List<Vector3> resampled = FireHosePathSampler.Resample(controlPoints, Mathf.Max(0.02f, sampleSpacing));
        List<Vector3> spline = FireHosePathSampler.CatmullRom(resampled);

        mesh = FireHoseMeshBuilder.Build(
            spline,
            mesh,
            lastUp,
            out Vector3 newLastUp,
            radius,
            Mathf.Max(3, radialSegments));

        lastUp = newLastUp;
        meshFilter.sharedMesh = mesh;
    }

    void OnDestroy()
    {
        if (mesh != null)
        {
            Destroy(mesh);
        }

        if (tailObject != null)
        {
            Destroy(tailObject);
        }
    }

    void EnsureTailObject()
    {
        if (tailObject != null)
        {
            return;
        }

        tailObject = new GameObject("FireHoseTailVisual");
        tailObject.transform.SetParent(transform, false);

        meshFilter = tailObject.AddComponent<MeshFilter>();
        meshRenderer = tailObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = hoseMaterial;
    }

    void Simulate(Vector3 startWorld, Vector3 endWorld, float distance)
    {
        int count = Mathf.Max(3, particleCount);

        if (!hasSimulation || particles.Count != count)
        {
            InitializeSimulation(startWorld, endWorld, count);
        }

        float dt = Mathf.Clamp(Time.deltaTime, 0.0001f, Mathf.Max(0.0001f, maxTimestep));
        Vector3 acceleration = Vector3.down * Mathf.Max(0f, gravity);
        float velocityRetention = 1f - Mathf.Clamp01(damping);

        particles[0] = startWorld;
        particles[count - 1] = endWorld;

        for (int i = 1; i < count - 1; i++)
        {
            Vector3 current = particles[i];
            Vector3 velocity = (current - previousParticles[i]) * velocityRetention;

            previousParticles[i] = current;
            particles[i] = current + velocity + acceleration * (dt * dt);
        }

        float totalLength = distance + Mathf.Max(0f, slackLength);
        float restLength = totalLength / (count - 1);
        int iterations = Mathf.Max(1, constraintIterations);

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            particles[0] = startWorld;
            particles[count - 1] = endWorld;

            for (int i = 0; i < count - 1; i++)
            {
                SatisfyDistance(i, i + 1, restLength);
            }
        }

        particles[0] = startWorld;
        particles[count - 1] = endWorld;
        previousParticles[0] = startWorld;
        previousParticles[count - 1] = endWorld;
    }

    void InitializeSimulation(Vector3 startWorld, Vector3 endWorld, int count)
    {
        particles.Clear();
        previousParticles.Clear();

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            Vector3 point = Vector3.Lerp(startWorld, endWorld, t);
            point += Vector3.down * Mathf.Sin(t * Mathf.PI) * Mathf.Max(0f, slackLength);

            particles.Add(point);
            previousParticles.Add(point);
        }

        hasSimulation = true;
    }

    void SatisfyDistance(int firstIndex, int secondIndex, float restLength)
    {
        Vector3 first = particles[firstIndex];
        Vector3 second = particles[secondIndex];
        Vector3 delta = second - first;
        float length = delta.magnitude;

        if (length <= 0.0001f)
        {
            return;
        }

        Vector3 correction = delta * ((length - restLength) / length);
        bool firstPinned = firstIndex == 0;
        bool secondPinned = secondIndex == particles.Count - 1;

        if (firstPinned)
        {
            particles[secondIndex] = second - correction;
        }
        else if (secondPinned)
        {
            particles[firstIndex] = first + correction;
        }
        else
        {
            particles[firstIndex] = first + correction * 0.5f;
            particles[secondIndex] = second - correction * 0.5f;
        }
    }

    void ResetSimulation()
    {
        hasSimulation = false;
        particles.Clear();
        previousParticles.Clear();
        controlPoints.Clear();
    }

    void ClearMesh()
    {
        if (mesh != null)
        {
            mesh.Clear();
        }
    }
}
