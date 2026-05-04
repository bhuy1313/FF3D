using System.Collections.Generic;
using UnityEngine;

public class FireHoseDeployed : MonoBehaviour
{
    public FireHoseDeployable source;

    public float sampleSpacing = 0.5f;
    public float segmentMaxLength = 10f;

    public Material hoseMaterial;

    private readonly List<HoseSegment> segments = new List<HoseSegment>();
    private HoseSegment activeSegment;

    private Vector3 lastSegmentUp = Vector3.up;

    void Start()
    {
        CreateNewSegment();

        if (source != null)
        {
            source.OnKnotAdded += OnKnotAdded;
        }
    }

    void OnDestroy()
    {
        if (source != null)
        {
            source.OnKnotAdded -= OnKnotAdded;
        }

        for (int i = 0; i < segments.Count; i++)
        {
            Mesh mesh = segments[i].Mesh;
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }
    }

    void OnKnotAdded(Knot k)
    {
        activeSegment.AddKnot(k);

        if (activeSegment.Length > segmentMaxLength)
        {
            BuildSegment(activeSegment);
            lastSegmentUp = activeSegment.LastUp;

            CreateNewSegment();
            activeSegment.AddKnot(k);
        }

        BuildSegment(activeSegment);
    }

    void CreateNewSegment()
    {
        GameObject go = new GameObject("HoseSegment");
        go.transform.SetParent(transform, false);

        MeshFilter meshFilter = go.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();

        if (hoseMaterial != null)
        {
            meshRenderer.sharedMaterial = hoseMaterial;
        }
        else
        {
            Debug.LogError("Hose material missing!");
        }

        activeSegment = new HoseSegment
        {
            GameObject = go,
            MeshFilter = meshFilter,
            MeshRenderer = meshRenderer,
            LastUp = lastSegmentUp
        };

        segments.Add(activeSegment);
    }

    void BuildSegment(HoseSegment segment)
    {
        if (segment.Knots.Count < 2)
        {
            return;
        }

        List<Vector3> resampled = FireHosePathSampler.Resample(segment.Knots, sampleSpacing);
        List<Vector3> spline = FireHosePathSampler.CatmullRom(resampled);

        segment.Mesh = FireHoseMeshBuilder.Build(
            spline,
            segment.Mesh,
            segment.LastUp,
            out Vector3 lastUp);
        segment.MeshFilter.sharedMesh = segment.Mesh;
        segment.LastUp = lastUp;
    }
}
