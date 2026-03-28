using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RopeBasicTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type RopeBasicType = FindType("RopeBasic");
    private static readonly Type RopeCurveType = FindType("RopeCurve");

    [Test]
    public void ResetSimulation_ReinitializesNodes_WhenNodeCountIsUnchanged()
    {
        GameObject ropeObject = new GameObject("RopeRoot");
        GameObject startObject = new GameObject("RopeStart");
        GameObject endObject = new GameObject("RopeEnd");

        try
        {
            startObject.transform.position = Vector3.zero;
            endObject.transform.position = new Vector3(3f, 0f, 0f);

            object rope = CreateRopeCurve(startObject.transform, endObject.transform);
            Component ropeBasic = ropeObject.AddComponent(RopeBasicType);
            ConfigureRopeBasic(ropeBasic, CreateRopeList(rope), usePhysics: true, ropeWidth: 1f);

            InvokeInstanceMethod(ropeBasic, "Rebuild");

            Vector3[] nodePositions = GetFieldValue<Vector3[]>(rope, "nodePositions");
            Vector3[] nodePrevPositions = GetFieldValue<Vector3[]>(rope, "nodePrevPositions");
            nodePositions[1] = new Vector3(99f, 99f, 99f);
            nodePrevPositions[1] = nodePositions[1];

            InvokeInstanceMethod(ropeBasic, "ResetSimulation");

            nodePositions = GetFieldValue<Vector3[]>(rope, "nodePositions");
            nodePrevPositions = GetFieldValue<Vector3[]>(rope, "nodePrevPositions");

            Vector3 expected = Vector3.Lerp(startObject.transform.position, endObject.transform.position, 1f / 3f);
            Assert.That((nodePositions[1] - expected).sqrMagnitude, Is.LessThan(0.000001f));
            Assert.That((nodePrevPositions[1] - expected).sqrMagnitude, Is.LessThan(0.000001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ropeObject);
            UnityEngine.Object.DestroyImmediate(startObject);
            UnityEngine.Object.DestroyImmediate(endObject);
        }
    }

    [Test]
    public void Rebuild_AssignsSegmentCounts_PerRope_WhenLengthsDiffer()
    {
        GameObject ropeObject = new GameObject("RopeRoot");
        GameObject shortStart = new GameObject("ShortStart");
        GameObject shortEnd = new GameObject("ShortEnd");
        GameObject longStart = new GameObject("LongStart");
        GameObject longEnd = new GameObject("LongEnd");

        try
        {
            shortStart.transform.position = Vector3.zero;
            shortEnd.transform.position = new Vector3(2f, 0f, 0f);
            longStart.transform.position = Vector3.zero;
            longEnd.transform.position = new Vector3(5f, 0f, 0f);

            object shortRope = CreateRopeCurve(shortStart.transform, shortEnd.transform);
            object longRope = CreateRopeCurve(longStart.transform, longEnd.transform);
            Component ropeBasic = ropeObject.AddComponent(RopeBasicType);
            ConfigureRopeBasic(
                ropeBasic,
                CreateRopeList(shortRope, longRope),
                usePhysics: false,
                ropeWidth: 1f);

            InvokeInstanceMethod(ropeBasic, "Rebuild");

            int shortSegmentCount = GetFieldValue<int>(shortRope, "activeSegmentCount");
            int longSegmentCount = GetFieldValue<int>(longRope, "activeSegmentCount");
            MeshFilter shortMeshFilter = GetFieldValue<MeshFilter>(shortRope, "meshFilter");
            MeshFilter longMeshFilter = GetFieldValue<MeshFilter>(longRope, "meshFilter");

            Assert.That(shortSegmentCount, Is.EqualTo(2));
            Assert.That(longSegmentCount, Is.EqualTo(5));
            Assert.That(shortMeshFilter, Is.Not.Null);
            Assert.That(longMeshFilter, Is.Not.Null);
            Assert.That(shortMeshFilter.sharedMesh, Is.Not.Null);
            Assert.That(longMeshFilter.sharedMesh, Is.Not.Null);
            Assert.That(shortMeshFilter.sharedMesh.vertexCount, Is.GreaterThan(0));
            Assert.That(longMeshFilter.sharedMesh.vertexCount, Is.GreaterThan(shortMeshFilter.sharedMesh.vertexCount));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ropeObject);
            UnityEngine.Object.DestroyImmediate(shortStart);
            UnityEngine.Object.DestroyImmediate(shortEnd);
            UnityEngine.Object.DestroyImmediate(longStart);
            UnityEngine.Object.DestroyImmediate(longEnd);
        }
    }

    [Test]
    public void PhysicsBuild_CapsDynamicTailNodeCount()
    {
        GameObject ropeObject = new GameObject("RopeRoot");
        GameObject startObject = new GameObject("RopeStart");
        GameObject endObject = new GameObject("RopeEnd");

        try
        {
            startObject.transform.position = Vector3.zero;
            endObject.transform.position = new Vector3(10f, 0f, 0f);

            object rope = CreateRopeCurve(startObject.transform, endObject.transform);
            Component ropeBasic = ropeObject.AddComponent(RopeBasicType);
            ConfigureRopeBasic(ropeBasic, CreateRopeList(rope), usePhysics: true, ropeWidth: 1f);
            SetPrivateField(ropeBasic, "maxDynamicTailNodes", 4);

            InvokeInstanceMethod(ropeBasic, "Rebuild");

            Assert.That(GetFieldValue<int>(rope, "activeSegmentCount"), Is.EqualTo(3));
            Assert.That(GetFieldValue<Vector3[]>(rope, "nodePositions").Length, Is.EqualTo(4));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ropeObject);
            UnityEngine.Object.DestroyImmediate(startObject);
            UnityEngine.Object.DestroyImmediate(endObject);
        }
    }

    [Test]
    public void UpdateSegments_DoesNotShorten_WhenEndpointsMoveCloser()
    {
        GameObject ropeObject = new GameObject("RopeRoot");
        GameObject startObject = new GameObject("RopeStart");
        GameObject endObject = new GameObject("RopeEnd");

        try
        {
            startObject.transform.position = Vector3.zero;
            endObject.transform.position = new Vector3(5f, 0f, 0f);

            object rope = CreateRopeCurve(startObject.transform, endObject.transform);
            Component ropeBasic = ropeObject.AddComponent(RopeBasicType);
            ConfigureRopeBasic(ropeBasic, CreateRopeList(rope), usePhysics: false, ropeWidth: 1f);

            InvokeInstanceMethod(ropeBasic, "Rebuild");
            int initialSegmentCount = GetFieldValue<int>(rope, "activeSegmentCount");
            Assert.That(initialSegmentCount, Is.EqualTo(5));

            endObject.transform.position = new Vector3(2f, 0f, 0f);
            InvokeInstanceMethod(ropeBasic, "UpdateSegments");

            int shortenedSegmentCount = GetFieldValue<int>(rope, "activeSegmentCount");
            Assert.That(shortenedSegmentCount, Is.EqualTo(initialSegmentCount));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ropeObject);
            UnityEngine.Object.DestroyImmediate(startObject);
            UnityEngine.Object.DestroyImmediate(endObject);
        }
    }

    [Test]
    public void GrowRopeIfNeeded_AppendsSegmentsNearEndpoint()
    {
        GameObject ropeObject = new GameObject("RopeRoot");
        GameObject startObject = new GameObject("RopeStart");
        GameObject endObject = new GameObject("RopeEnd");

        try
        {
            startObject.transform.position = Vector3.zero;
            endObject.transform.position = new Vector3(2f, 0f, 0f);

            object rope = CreateRopeCurve(startObject.transform, endObject.transform);
            Component ropeBasic = ropeObject.AddComponent(RopeBasicType);
            ConfigureRopeBasic(ropeBasic, CreateRopeList(rope), usePhysics: true, ropeWidth: 1f);

            InvokeInstanceMethod(ropeBasic, "Rebuild");
            Assert.That(GetFieldValue<int>(rope, "activeSegmentCount"), Is.EqualTo(2));

            endObject.transform.position = new Vector3(5f, 0f, 0f);
            InvokeInstanceMethod(ropeBasic, "GrowRopeIfNeeded", rope);

            Assert.That(GetFieldValue<int>(rope, "activeSegmentCount"), Is.EqualTo(5));
            Assert.That(GetFieldValue<Vector3[]>(rope, "nodePositions").Length, Is.EqualTo(6));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ropeObject);
            UnityEngine.Object.DestroyImmediate(startObject);
            UnityEngine.Object.DestroyImmediate(endObject);
        }
    }

    [Test]
    public void GrowRopeIfNeeded_DoesNotAppend_WhenEndpointIsStationary()
    {
        GameObject ropeObject = new GameObject("RopeRoot");
        GameObject startObject = new GameObject("RopeStart");
        GameObject endObject = new GameObject("RopeEnd");

        try
        {
            startObject.transform.position = Vector3.zero;
            endObject.transform.position = new Vector3(2f, 0f, 0f);

            object rope = CreateRopeCurve(startObject.transform, endObject.transform);
            Component ropeBasic = ropeObject.AddComponent(RopeBasicType);
            ConfigureRopeBasic(ropeBasic, CreateRopeList(rope), usePhysics: true, ropeWidth: 1f);

            InvokeInstanceMethod(ropeBasic, "Rebuild");
            int initialSegmentCount = GetFieldValue<int>(rope, "activeSegmentCount");

            InvokeInstanceMethod(ropeBasic, "GrowRopeIfNeeded", rope);
            InvokeInstanceMethod(ropeBasic, "GrowRopeIfNeeded", rope);

            Assert.That(GetFieldValue<int>(rope, "activeSegmentCount"), Is.EqualTo(initialSegmentCount));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ropeObject);
            UnityEngine.Object.DestroyImmediate(startObject);
            UnityEngine.Object.DestroyImmediate(endObject);
        }
    }

    public void TryBakeFrozenPrefix_TrimsOnlyGroundedPrefix_AndBuildsBakedMesh()
    {
        GameObject ropeObject = new GameObject("RopeRoot");
        GameObject startObject = new GameObject("RopeStart");
        GameObject endObject = new GameObject("RopeEnd");

        try
        {
            startObject.transform.position = Vector3.zero;
            endObject.transform.position = new Vector3(6f, 0f, 0f);

            object rope = CreateRopeCurve(startObject.transform, endObject.transform);
            Component ropeBasic = ropeObject.AddComponent(RopeBasicType);
            ConfigureRopeBasic(ropeBasic, CreateRopeList(rope), usePhysics: true, ropeWidth: 1f);
            SetPrivateField(ropeBasic, "maxDynamicTailNodes", 4);

            InvokeInstanceMethod(ropeBasic, "Rebuild");

            endObject.transform.position = new Vector3(10f, 0f, 0f);
            InvokeInstanceMethod(ropeBasic, "GrowRopeIfNeeded", rope);

            bool[] nodeGrounded = GetFieldValue<bool[]>(rope, "nodeGrounded");
            nodeGrounded[1] = true;
            nodeGrounded[2] = true;
            nodeGrounded[3] = true;
            nodeGrounded[4] = true;

            InvokeInstanceMethod(ropeBasic, "TryBakeFrozenPrefix", rope);

            Assert.That(GetFieldValue<int>(rope, "activeSegmentCount"), Is.EqualTo(3));
            Assert.That(GetFieldValue<Vector3[]>(rope, "nodePositions").Length, Is.EqualTo(4));
            Assert.That(GetFieldValue<IList>(rope, "bakedPositions").Count, Is.GreaterThan(1));

            MeshFilter bakedMeshFilter = GetFieldValue<MeshFilter>(rope, "bakedMeshFilter");
            Assert.That(bakedMeshFilter, Is.Not.Null);
            Assert.That(bakedMeshFilter.sharedMesh, Is.Not.Null);
            Assert.That(bakedMeshFilter.sharedMesh.vertexCount, Is.GreaterThan(0));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ropeObject);
            UnityEngine.Object.DestroyImmediate(startObject);
            UnityEngine.Object.DestroyImmediate(endObject);
        }
    }

    [Test]
    public void TryBakeFrozenPrefix_DoesNotTrim_WhenPrefixIsNotGrounded()
    {
        GameObject ropeObject = new GameObject("RopeRoot");
        GameObject startObject = new GameObject("RopeStart");
        GameObject endObject = new GameObject("RopeEnd");

        try
        {
            startObject.transform.position = Vector3.zero;
            endObject.transform.position = new Vector3(6f, 0f, 0f);

            object rope = CreateRopeCurve(startObject.transform, endObject.transform);
            Component ropeBasic = ropeObject.AddComponent(RopeBasicType);
            ConfigureRopeBasic(ropeBasic, CreateRopeList(rope), usePhysics: true, ropeWidth: 1f);
            SetPrivateField(ropeBasic, "maxDynamicTailNodes", 4);

            InvokeInstanceMethod(ropeBasic, "Rebuild");

            endObject.transform.position = new Vector3(10f, 0f, 0f);
            InvokeInstanceMethod(ropeBasic, "GrowRopeIfNeeded", rope);
            InvokeInstanceMethod(ropeBasic, "TryBakeFrozenPrefix", rope);

            Assert.That(GetFieldValue<int>(rope, "activeSegmentCount"), Is.EqualTo(7));
            Assert.That(GetFieldValue<Vector3[]>(rope, "nodePositions").Length, Is.EqualTo(8));
            Assert.That(GetFieldValue<IList>(rope, "bakedPositions").Count, Is.EqualTo(0));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ropeObject);
            UnityEngine.Object.DestroyImmediate(startObject);
            UnityEngine.Object.DestroyImmediate(endObject);
        }
    }

    [Test]
    public void TryBakeFrozenPrefix_ProjectsOverflowPrefixToGround_AndTrims()
    {
        GameObject ropeObject = new GameObject("RopeRoot");
        GameObject startObject = new GameObject("RopeStart");
        GameObject endObject = new GameObject("RopeEnd");
        GameObject groundObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

        try
        {
            startObject.transform.position = new Vector3(0f, 2f, 0f);
            endObject.transform.position = new Vector3(6f, 2f, 0f);
            groundObject.transform.position = new Vector3(5f, -0.5f, 0f);
            groundObject.transform.localScale = new Vector3(30f, 1f, 30f);

            object rope = CreateRopeCurve(startObject.transform, endObject.transform);
            Component ropeBasic = ropeObject.AddComponent(RopeBasicType);
            ConfigureRopeBasic(ropeBasic, CreateRopeList(rope), usePhysics: true, ropeWidth: 1f);
            SetPrivateField(ropeBasic, "maxDynamicTailNodes", 4);

            InvokeInstanceMethod(ropeBasic, "Rebuild");

            endObject.transform.position = new Vector3(10f, 2f, 0f);
            InvokeInstanceMethod(ropeBasic, "GrowRopeIfNeeded", rope);
            InvokeInstanceMethod(ropeBasic, "TryBakeFrozenPrefix", rope);

            Assert.That(GetFieldValue<int>(rope, "activeSegmentCount"), Is.EqualTo(3));
            Assert.That(GetFieldValue<Vector3[]>(rope, "nodePositions").Length, Is.EqualTo(4));
            Assert.That(GetFieldValue<IList>(rope, "bakedPositions").Count, Is.GreaterThan(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ropeObject);
            UnityEngine.Object.DestroyImmediate(startObject);
            UnityEngine.Object.DestroyImmediate(endObject);
            UnityEngine.Object.DestroyImmediate(groundObject);
        }
    }

    private static void ConfigureRopeBasic(
        Component ropeBasic,
        IList ropes,
        bool usePhysics,
        float ropeWidth)
    {
        SetPrivateField(ropeBasic, "ropes", ropes);
        SetPrivateField(ropeBasic, "usePhysics", usePhysics);
        SetPrivateField(ropeBasic, "maxSegments", 32);
        SetPrivateField(ropeBasic, "ropeWidth", ropeWidth);
        SetPrivateField(ropeBasic, "spacingMultiplier", 1f);
    }

    private static object CreateRopeCurve(Transform startPoint, Transform endPoint)
    {
        object rope = Activator.CreateInstance(RopeCurveType);
        SetFieldValue(rope, "startPoint", startPoint);
        SetFieldValue(rope, "endPoint", endPoint);
        return rope;
    }

    private static IList CreateRopeList(params object[] ropes)
    {
        Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(RopeCurveType);
        IList list = (IList)Activator.CreateInstance(listType);
        foreach (object rope in ropes)
            list.Add(rope);

        return list;
    }

    private static void InvokeInstanceMethod(object target, string methodName)
    {
        InvokeInstanceMethod(target, methodName, null);
    }

    private static void InvokeInstanceMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Could not find method '{methodName}'.");
        method.Invoke(target, args);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static void SetFieldValue(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static T GetFieldValue<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        return (T)field.GetValue(target);
    }

    private static Type FindType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName, false);
            if (type != null)
                return type;
        }

        Assert.Fail($"Could not find type '{typeName}'.");
        return null;
    }
}
