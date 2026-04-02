using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class MeshShatterTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    private static readonly Type MeshShatterType = FindType("MeshShatter");
    private static readonly Type MeshShatterPresetType = FindNestedType(MeshShatterType, "ShatterPreset");
    private static readonly Type MeshShatterUtilityType = FindType("MeshShatterUtility");

    [Test]
    public void CreateTriangleShards_SubdivisionDepthOneSplitsQuadIntoEightShards()
    {
        Mesh quadMesh = CreateQuadMesh();

        try
        {
            object shards = InvokeStaticMethod(MeshShatterUtilityType, "CreateTriangleShards", quadMesh, 1);
            int count = (int)shards.GetType().GetProperty("Count", InstanceFlags).GetValue(shards);

            Assert.That(count, Is.EqualTo(8));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(quadMesh);
        }
    }

    [Test]
    public void Shatter_DisablesOriginalRendererAndCreatesRigidBodyShards()
    {
        Mesh quadMesh = CreateQuadMesh();
        GameObject glass = new GameObject("Glass");

        try
        {
            MeshFilter meshFilter = glass.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = quadMesh;
            MeshRenderer meshRenderer = glass.AddComponent<MeshRenderer>();
            glass.AddComponent<BoxCollider>();

            Component shatter = glass.AddComponent(MeshShatterType);
            SetPrivateField(shatter, "subdivisionDepth", 1);
            SetPrivateField(shatter, "destroyAfterSeconds", 0f);

            InvokeInstanceMethod(shatter, "Shatter", Vector3.zero, Vector3.forward, 1f);

            bool isShattered = (bool)MeshShatterType.GetProperty("IsShattered", InstanceFlags).GetValue(shatter);
            Rigidbody[] rigidbodies = UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);

            Assert.That(isShattered, Is.True);
            Assert.That(meshRenderer.enabled, Is.False);
            Assert.That(rigidbodies.Length, Is.EqualTo(8));
        }
        finally
        {
            GameObject shardRoot = GameObject.Find("Glass_Shards");
            if (shardRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(shardRoot);
            }

            UnityEngine.Object.DestroyImmediate(glass);
            UnityEngine.Object.DestroyImmediate(quadMesh);
        }
    }

    [Test]
    public void CreateTriangleShards_WithJitterAndFixedSeedProducesIrregularShardVertices()
    {
        Mesh quadMesh = CreateQuadMesh();

        try
        {
            object shards = InvokeStaticMethod(MeshShatterUtilityType, "CreateTriangleShards", quadMesh, 1, 0.2f, 12345);
            object firstShard = shards.GetType().GetProperty("Item", InstanceFlags).GetValue(shards, new object[] { 0 });

            Vector3 b = (Vector3)firstShard.GetType().GetProperty("B", InstanceFlags).GetValue(firstShard);
            Vector3 c = (Vector3)firstShard.GetType().GetProperty("C", InstanceFlags).GetValue(firstShard);

            Assert.That(Vector3.Distance(b, new Vector3(-0.5f, 0f, 0f)), Is.GreaterThan(0.0001f));
            Assert.That(Vector3.Distance(c, new Vector3(0f, -0.5f, 0f)), Is.GreaterThan(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(quadMesh);
        }
    }

    [Test]
    public void CreateMeshWithoutSubmesh_RemovesOnlySelectedSubmesh()
    {
        Mesh mesh = CreateTwoSubmeshQuadMesh();

        try
        {
            Mesh preserved = (Mesh)InvokeStaticMethod(MeshShatterUtilityType, "CreateMeshWithoutSubmesh", mesh, 1);

            Assert.That(preserved, Is.Not.Null);
            Assert.That(preserved.subMeshCount, Is.EqualTo(1));
            Assert.That(preserved.GetTriangles(0).Length, Is.EqualTo(3));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    [Test]
    public void ApplyPresetValues_DoesNotOverwriteForceSettings()
    {
        GameObject target = new GameObject("ShatterTarget");

        try
        {
            Component shatter = target.AddComponent(MeshShatterType);
            SetPrivateField(shatter, "impulseStrength", 4.5f);
            SetPrivateField(shatter, "impactDirectionWeight", 1.9f);
            SetPrivateField(shatter, "surfaceNormalWeight", 0.12f);
            SetPrivateField(shatter, "impactSpread", 0.07f);
            SetPrivateField(shatter, "randomImpulse", 0.33f);
            SetPrivateField(shatter, "randomTorque", 0.44f);
            SetPrivateField(shatter, "destroyAfterSeconds", 12.5f);
            SetPrivateField(shatter, "preset", Enum.Parse(MeshShatterPresetType, "Chaotic"));

            InvokeInstanceMethod(shatter, "ApplyPresetValues");

            Assert.That(GetPrivateField<float>(shatter, "impulseStrength"), Is.EqualTo(4.5f).Within(0.0001f));
            Assert.That(GetPrivateField<float>(shatter, "impactDirectionWeight"), Is.EqualTo(1.9f).Within(0.0001f));
            Assert.That(GetPrivateField<float>(shatter, "surfaceNormalWeight"), Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(GetPrivateField<float>(shatter, "impactSpread"), Is.EqualTo(0.07f).Within(0.0001f));
            Assert.That(GetPrivateField<float>(shatter, "randomImpulse"), Is.EqualTo(0.33f).Within(0.0001f));
            Assert.That(GetPrivateField<float>(shatter, "randomTorque"), Is.EqualTo(0.44f).Within(0.0001f));
            Assert.That(GetPrivateField<float>(shatter, "destroyAfterSeconds"), Is.EqualTo(12.5f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void ResolveImpulseDirection_PrefersImpactDirection()
    {
        GameObject target = new GameObject("ShatterTarget");

        try
        {
            Component shatter = target.AddComponent(MeshShatterType);
            SetPrivateField(shatter, "impactDirectionWeight", 2f);
            SetPrivateField(shatter, "surfaceNormalWeight", 0.2f);
            SetPrivateField(shatter, "impactSpread", 0.15f);
            SetPrivateField(shatter, "randomImpulse", 0f);

            Vector3 resolvedDirection = (Vector3)InvokeInstanceMethod(
                shatter,
                "ResolveImpulseDirection",
                new Vector3(0.35f, 0.1f, 1f),
                Vector3.zero,
                Vector3.forward,
                Vector3.back);

            Assert.That(Vector3.Dot(resolvedDirection.normalized, Vector3.forward), Is.GreaterThan(0.85f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh
        {
            name = "Quad"
        };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateTwoSubmeshQuadMesh()
    {
        Mesh mesh = new Mesh
        {
            name = "TwoSubmeshQuad"
        };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
        mesh.subMeshCount = 2;
        mesh.SetTriangles(new[] { 0, 2, 1 }, 0);
        mesh.SetTriangles(new[] { 2, 3, 1 }, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static object InvokeInstanceMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = FindMethod(target.GetType(), methodName, args?.Length ?? 0, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Could not find method '{methodName}'.");
        return method.Invoke(target, args);
    }

    private static object InvokeStaticMethod(Type targetType, string methodName, params object[] args)
    {
        MethodInfo method = FindMethod(targetType, methodName, args?.Length ?? 0, StaticFlags);
        Assert.That(method, Is.Not.Null, $"Could not find static method '{methodName}'.");
        return method.Invoke(null, args);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = FindField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = FindField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        return (T)field.GetValue(target);
    }

    private static MethodInfo FindMethod(Type type, string methodName, int parameterCount, BindingFlags bindingFlags)
    {
        while (type != null)
        {
            MethodInfo[] methods = type.GetMethods(bindingFlags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo candidate = methods[i];
                if (candidate.Name == methodName && candidate.GetParameters().Length == parameterCount)
                {
                    return candidate;
                }
            }

            type = type.BaseType;
        }

        return null;
    }

    private static FieldInfo FindField(Type type, string fieldName)
    {
        while (type != null)
        {
            FieldInfo field = type.GetField(fieldName, InstanceFlags);
            if (field != null)
            {
                return field;
            }

            type = type.BaseType;
        }

        return null;
    }

    private static Type FindType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type found = assembly.GetType(typeName);
            if (found != null)
            {
                return found;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types;
            }

            if (types == null)
            {
                continue;
            }

            for (int i = 0; i < types.Length; i++)
            {
                Type candidate = types[i];
                if (candidate != null && candidate.Name == typeName)
                {
                    return candidate;
                }
            }
        }

        Assert.Fail($"Could not find type '{typeName}'.");
        return null;
    }

    private static Type FindNestedType(Type parentType, string typeName)
    {
        Type nestedType = parentType.GetNestedType(typeName, BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(nestedType, Is.Not.Null, $"Could not find nested type '{typeName}'.");
        return nestedType;
    }
}
