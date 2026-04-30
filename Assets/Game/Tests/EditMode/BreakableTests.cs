using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class BreakableTests
{
    private sealed class DummyInteractable : MonoBehaviour, IInteractable
    {
        public int InteractionCount { get; private set; }
        public GameObject LastInteractor { get; private set; }

        public void Interact(GameObject interactor)
        {
            InteractionCount++;
            LastInteractor = interactor;
        }
    }

    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type BreakableType = FindType("Breakable");

    [Test]
    public void TryGetBreakStandPose_SelectsNearestConfiguredStandPoint()
    {
        GameObject breakableObject = new GameObject("Breakable");
        GameObject leftStandPoint = new GameObject("LeftStandPoint");
        GameObject rightStandPoint = new GameObject("RightStandPoint");

        try
        {
            Component breakable = breakableObject.AddComponent(BreakableType);
            leftStandPoint.transform.SetParent(breakableObject.transform, true);
            rightStandPoint.transform.SetParent(breakableObject.transform, true);

            leftStandPoint.transform.position = new Vector3(-1.5f, 0f, 0f);
            leftStandPoint.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            rightStandPoint.transform.position = new Vector3(1.5f, 0f, 0f);
            rightStandPoint.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);

            SetPrivateField(breakable, "breakStandPoints", new[] { leftStandPoint.transform, rightStandPoint.transform });

            object[] args = { new Vector3(3f, 0f, 0f), null, null };
            bool foundPose = InvokeTryGetBreakStandPose(breakable, args);

            Assert.That(foundPose, Is.True);

            Vector3 standPosition = (Vector3)args[1];
            Quaternion standRotation = (Quaternion)args[2];
            Assert.That(Vector3.Distance(standPosition, rightStandPoint.transform.position), Is.LessThan(0.001f));

            Vector3 snappedForward = standRotation * Vector3.forward;
            Assert.That(Vector3.Dot(snappedForward.normalized, rightStandPoint.transform.forward), Is.GreaterThan(0.999f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(leftStandPoint);
            UnityEngine.Object.DestroyImmediate(rightStandPoint);
            UnityEngine.Object.DestroyImmediate(breakableObject);
        }
    }

    [Test]
    public void TryGetBreakStandPose_ReturnsFalse_WhenNoStandPointsConfigured()
    {
        GameObject breakableObject = new GameObject("Breakable");

        try
        {
            Component breakable = breakableObject.AddComponent(BreakableType);
            object[] args = { Vector3.zero, null, null };

            bool foundPose = InvokeTryGetBreakStandPose(breakable, args);

            Assert.That(foundPose, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(breakableObject);
        }
    }

    [Test]
    public void TryGetBreakStandPose_PreservesBreakerYWhileUsingStandPointXZ()
    {
        GameObject breakableObject = new GameObject("Breakable");
        GameObject standPoint = new GameObject("StandPoint");

        try
        {
            Component breakable = breakableObject.AddComponent(BreakableType);
            standPoint.transform.SetParent(breakableObject.transform, true);
            standPoint.transform.position = new Vector3(4f, 10f, -2f);

            SetPrivateField(breakable, "breakStandPoints", new[] { standPoint.transform });

            object[] args = { new Vector3(100f, 1.75f, 100f), null, null };
            bool foundPose = InvokeTryGetBreakStandPose(breakable, args);

            Assert.That(foundPose, Is.True);

            Vector3 standPosition = (Vector3)args[1];
            Assert.That(standPosition.x, Is.EqualTo(standPoint.transform.position.x).Within(0.001f));
            Assert.That(standPosition.z, Is.EqualTo(standPoint.transform.position.z).Within(0.001f));
            Assert.That(standPosition.y, Is.EqualTo(1.75f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(standPoint);
            UnityEngine.Object.DestroyImmediate(breakableObject);
        }
    }

    [Test]
    public void SupportsBreakTool_AllowsFireAxeForGlassBreakables()
    {
        GameObject breakableObject = new GameObject("GlassBreakable");

        try
        {
            Component breakable = breakableObject.AddComponent(BreakableType);
            Type breakableTypeEnum = FindNestedType(BreakableType, "BreakableType");
            Type breakToolKindType = FindType("BreakToolKind");

            SetPrivateField(breakable, "breakableType", Enum.Parse(breakableTypeEnum, "Glass"));
            bool supports = (bool)BreakableType.GetMethod("SupportsBreakTool", InstanceFlags)
                .Invoke(breakable, new[] { Enum.Parse(breakToolKindType, "FireAxe") });

            Assert.That(supports, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(breakableObject);
        }
    }

    [Test]
    public void Interact_ForwardsToParentInteractable_WhenBreakerHasNoValidTool()
    {
        GameObject parentObject = new GameObject("WindowParent");
        GameObject breakableObject = new GameObject("GlassPane");
        GameObject interactor = new GameObject("Interactor");

        try
        {
            DummyInteractable parentInteractable = parentObject.AddComponent<DummyInteractable>();
            breakableObject.transform.SetParent(parentObject.transform, false);

            Component breakable = breakableObject.AddComponent(BreakableType);
            Type breakableTypeEnum = FindNestedType(BreakableType, "BreakableType");
            SetPrivateField(breakable, "breakableType", Enum.Parse(breakableTypeEnum, "Glass"));

            BreakableType.GetMethod("Interact", InstanceFlags)?.Invoke(breakable, new object[] { interactor });

            Assert.That(parentInteractable.InteractionCount, Is.EqualTo(1));
            Assert.That(parentInteractable.LastInteractor, Is.SameAs(interactor));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(interactor);
            UnityEngine.Object.DestroyImmediate(parentObject);
        }
    }

    private static bool InvokeTryGetBreakStandPose(Component breakable, object[] args)
    {
        MethodInfo method = BreakableType.GetMethod("TryGetBreakStandPose", InstanceFlags);
        Assert.That(method, Is.Not.Null, "Could not find method 'TryGetBreakStandPose'.");
        return (bool)method.Invoke(breakable, args);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        field.SetValue(target, value);
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
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
