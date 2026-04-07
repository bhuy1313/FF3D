using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class StandardLadderTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type StandardLadderType = FindType("StandardLadder");
    private static readonly Type LadderType = FindType("Ladder");

    [Test]
    public void ApplyPlacement_UsesAuthoredColliderAndBuildsVisuals()
    {
        GameObject ladderObject = new GameObject("StandardLadder");
        ladderObject.AddComponent<Rigidbody>();
        BoxCollider authoredCollider = ladderObject.AddComponent<BoxCollider>();
        authoredCollider.size = new Vector3(0.65f, 4f, 0.3f);
        GameObject art = GameObject.CreatePrimitive(PrimitiveType.Cube);
        art.name = "Art";
        art.transform.SetParent(ladderObject.transform, false);

        try
        {
            Component ladder = ladderObject.AddComponent(StandardLadderType);
            InvokeInstanceMethod(ladder, "ApplyPlacement", Vector3.zero, new Vector3(0f, 4f, 0f), Vector3.forward);

            BoxCollider collider = ladderObject.GetComponent<BoxCollider>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.size.x, Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(collider.size.y, Is.EqualTo(4f).Within(0.0001f));

            Transform visuals = ladderObject.transform.Find("Visuals");
            Assert.That(visuals, Is.Not.Null);
            Assert.That(visuals.Find("Art"), Is.Not.Null);

            Component ladderComponent = ladderObject.GetComponent(LadderType);
            Assert.That(ladderComponent, Is.Not.Null);
            float climbMaxY = (float)InvokeInstanceMethod(ladderComponent, "GetClimbMaxY");
            Assert.That(climbMaxY, Is.GreaterThan(4f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(art);
            UnityEngine.Object.DestroyImmediate(ladderObject);
        }
    }

    [Test]
    public void ApplyPlacement_PreservesExistingRootBoxColliderSize()
    {
        GameObject ladderObject = new GameObject("StandardLadder");
        ladderObject.AddComponent<Rigidbody>();
        BoxCollider authoredCollider = ladderObject.AddComponent<BoxCollider>();
        authoredCollider.center = new Vector3(0.1f, 0.2f, 0.3f);
        authoredCollider.size = new Vector3(1.1f, 2.2f, 3.3f);

        try
        {
            Component ladder = ladderObject.AddComponent(StandardLadderType);
            InvokeInstanceMethod(ladder, "ApplyPlacement", Vector3.zero, new Vector3(0f, 4f, 0f), Vector3.forward);

            Assert.That(authoredCollider.center, Is.EqualTo(new Vector3(0.1f, 0.2f, 0.3f)));
            Assert.That(authoredCollider.size, Is.EqualTo(new Vector3(1.1f, 2.2f, 3.3f)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ladderObject);
        }
    }

    [Test]
    public void OnGrabStarted_DisablesClimbAndOnGrabCancelled_RestoresIt()
    {
        GameObject ladderObject = new GameObject("StandardLadder");
        ladderObject.AddComponent<Rigidbody>();

        try
        {
            Component ladder = ladderObject.AddComponent(StandardLadderType);
            InvokeInstanceMethod(ladder, "ApplyPlacement", Vector3.zero, new Vector3(0f, 3f, 0f), Vector3.forward);

            BoxCollider collider = ladderObject.GetComponent<BoxCollider>();
            Behaviour ladderBehaviour = ladderObject.GetComponent(LadderType) as Behaviour;

            InvokeInstanceMethod(ladder, "OnGrabStarted");
            Assert.That(collider.enabled, Is.False);
            Assert.That(ladderBehaviour.enabled, Is.False);

            InvokeInstanceMethod(ladder, "OnGrabCancelled");
            Assert.That(collider.enabled, Is.True);
            Assert.That(ladderBehaviour.enabled, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ladderObject);
        }
    }

    [Test]
    public void ApplyPlacement_KeepsVisualHeightScaleAtOne()
    {
        GameObject ladderObject = new GameObject("StandardLadder");
        ladderObject.AddComponent<Rigidbody>();
        GameObject art = GameObject.CreatePrimitive(PrimitiveType.Cube);
        art.name = "Art";
        art.transform.SetParent(ladderObject.transform, false);
        art.transform.localScale = new Vector3(1f, 1f, 0.2f);

        try
        {
            Component ladder = ladderObject.AddComponent(StandardLadderType);
            InvokeInstanceMethod(ladder, "ApplyPlacement", Vector3.zero, new Vector3(0f, 3f, 0f), Vector3.forward);

            Transform visuals = ladderObject.transform.Find("Visuals");
            Assert.That(visuals, Is.Not.Null);
            Assert.That(visuals.localScale.y, Is.EqualTo(1f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ladderObject);
        }
    }

    [Test]
    public void ApplyPlacement_UsesLadderHeightInsteadOfRequestedTopPoint()
    {
        GameObject ladderObject = new GameObject("StandardLadder");
        ladderObject.AddComponent<Rigidbody>();
        BoxCollider authoredCollider = ladderObject.AddComponent<BoxCollider>();
        authoredCollider.size = new Vector3(0.8f, 3f, 0.2f);

        try
        {
            Component ladder = ladderObject.AddComponent(StandardLadderType);
            InvokeInstanceMethod(ladder, "ApplyPlacement", new Vector3(0f, 1f, 0f), new Vector3(0f, 4.2f, 0f), Vector3.forward);

            float ladderHeight = (float)GetPropertyOrFieldValue(ladder, "LadderHeight");
            Assert.That(ladderHeight, Is.EqualTo(3f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ladderObject);
        }
    }

    [Test]
    public void PlacementHeightWithinLimits_AllowsHeightBelowLegacyMinimum()
    {
        GameObject ladderObject = new GameObject("StandardLadder");
        ladderObject.AddComponent<Rigidbody>();
        BoxCollider authoredCollider = ladderObject.AddComponent<BoxCollider>();
        authoredCollider.size = new Vector3(0.8f, 3f, 0.2f);

        try
        {
            Component ladder = ladderObject.AddComponent(StandardLadderType);

            bool allowed = (bool)InvokeInstanceMethod(ladder, "IsPlacementHeightWithinLimits", 1.2f);
            Assert.That(allowed, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ladderObject);
        }
    }

    [Test]
    public void ApplyPlacement_UsesExistingBoxColliderHeightAsPlacementHeight()
    {
        GameObject ladderObject = new GameObject("StandardLadder");
        ladderObject.AddComponent<Rigidbody>();
        BoxCollider authoredCollider = ladderObject.AddComponent<BoxCollider>();
        authoredCollider.size = new Vector3(0.8f, 4.25f, 0.2f);

        try
        {
            Component ladder = ladderObject.AddComponent(StandardLadderType);
            InvokeInstanceMethod(ladder, "ApplyPlacement", Vector3.zero, new Vector3(0f, 10f, 0f), Vector3.forward);

            float ladderHeight = (float)GetPropertyOrFieldValue(ladder, "LadderHeight");
            Assert.That(ladderHeight, Is.EqualTo(4.25f).Within(0.0001f));
            Assert.That(authoredCollider.size.y, Is.EqualTo(4.25f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ladderObject);
        }
    }

    [Test]
    public void PlacementHeightWithinLimits_RejectsHeightAboveExistingBoxColliderHeight()
    {
        GameObject ladderObject = new GameObject("StandardLadder");
        ladderObject.AddComponent<Rigidbody>();
        BoxCollider authoredCollider = ladderObject.AddComponent<BoxCollider>();
        authoredCollider.size = new Vector3(0.8f, 3.4f, 0.2f);

        try
        {
            Component ladder = ladderObject.AddComponent(StandardLadderType);

            bool allowed = (bool)InvokeInstanceMethod(ladder, "IsPlacementHeightWithinLimits", 3.6f);
            Assert.That(allowed, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ladderObject);
        }
    }

    [Test]
    public void ResolvePlacementReferencePoint_UsesNearestBoundsEdgeAlongOutward()
    {
        GameObject ladderObject = new GameObject("StandardLadder");

        try
        {
            ladderObject.AddComponent<Rigidbody>();
            Component ladder = ladderObject.AddComponent(StandardLadderType);
            Bounds bounds = new Bounds(new Vector3(0f, 2f, 5f), new Vector3(4f, 4f, 6f));

            Vector3 referencePoint = (Vector3)InvokeInstanceMethod(
                ladder,
                "ResolvePlacementReferencePoint",
                bounds,
                Vector3.back,
                new Vector3(0.25f, 1.5f, 4.5f));

            Assert.That(referencePoint.x, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(referencePoint.y, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(referencePoint.z, Is.EqualTo(2f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ladderObject);
        }
    }

    [Test]
    public void TryFindTopAnchor_UsesPreferredTopSurfaceHeightBeforeRaySearch()
    {
        GameObject ladderObject = new GameObject("StandardLadder");
        ladderObject.AddComponent<Rigidbody>();
        BoxCollider authoredCollider = ladderObject.AddComponent<BoxCollider>();
        authoredCollider.size = new Vector3(0.8f, 3f, 0.2f);

        try
        {
            Component ladder = ladderObject.AddComponent(StandardLadderType);

            object[] args =
            {
                Vector3.zero,
                Vector3.forward,
                1f,
                3.5f,
                Vector3.zero
            };

            bool resolved = (bool)InvokeInstanceMethod(ladder, "TryFindTopAnchor", args);
            Assert.That(resolved, Is.True);

            Vector3 topAnchor = (Vector3)args[4];
            Assert.That(topAnchor.y, Is.EqualTo(4f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ladderObject);
        }
    }

    [Test]
    public void TryFindPlatformPlacementReference_UsesColliderTopWhenLookingUpAtUnderside()
    {
        GameObject ladderObject = new GameObject("StandardLadder");
        GameObject aimObject = new GameObject("Aim");
        GameObject platformObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

        try
        {
            ladderObject.AddComponent<Rigidbody>();
            Component ladder = ladderObject.AddComponent(StandardLadderType);

            aimObject.transform.position = new Vector3(0f, 2f, -2f);
            Vector3 targetPoint = new Vector3(0f, 2.75f, 3f);
            aimObject.transform.rotation = Quaternion.LookRotation((targetPoint - aimObject.transform.position).normalized, Vector3.up);

            platformObject.transform.position = new Vector3(0f, 3f, 2f);
            platformObject.transform.localScale = new Vector3(4f, 0.5f, 4f);

            object[] args =
            {
                aimObject.transform,
                Vector3.zero,
                0f,
                0f,
                Vector3.zero
            };

            bool resolved = (bool)InvokeInstanceMethod(ladder, "TryFindPlatformPlacementReference", args);
            Assert.That(resolved, Is.True);

            Vector3 wallReference = (Vector3)args[1];
            float probeBaseY = (float)args[2];
            float preferredTopSurfaceY = (float)args[3];
            Vector3 outward = (Vector3)args[4];

            Assert.That(preferredTopSurfaceY, Is.EqualTo(platformObject.GetComponent<Collider>().bounds.max.y).Within(0.0001f));
            Assert.That(probeBaseY, Is.EqualTo(preferredTopSurfaceY).Within(0.0001f));
            Assert.That(outward, Is.EqualTo(Vector3.back));
            Assert.That(wallReference.z, Is.EqualTo(platformObject.GetComponent<Collider>().bounds.min.z).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ladderObject);
            UnityEngine.Object.DestroyImmediate(aimObject);
            UnityEngine.Object.DestroyImmediate(platformObject);
        }
    }

    [Test]
    public void TryResolvePlatformTopSurfaceY_ReturnsColliderTopSurface()
    {
        GameObject ladderObject = new GameObject("StandardLadder");
        GameObject platformObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

        try
        {
            ladderObject.AddComponent<Rigidbody>();
            Component ladder = ladderObject.AddComponent(StandardLadderType);

            platformObject.transform.position = new Vector3(0f, 3f, 2f);
            platformObject.transform.localScale = new Vector3(4f, 0.5f, 4f);
            Collider collider = platformObject.GetComponent<Collider>();

            object[] args =
            {
                collider,
                new Vector3(0f, collider.bounds.max.y, collider.bounds.min.z),
                Vector3.back,
                0f
            };

            bool resolved = (bool)InvokeInstanceMethod(ladder, "TryResolvePlatformTopSurfaceY", args);
            Assert.That(resolved, Is.True);
            Assert.That((float)args[3], Is.EqualTo(collider.bounds.max.y).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ladderObject);
            UnityEngine.Object.DestroyImmediate(platformObject);
        }
    }

    [Test]
    public void GetPlatformSearchDistance_GrowsWithLadderHeight()
    {
        GameObject ladderObject = new GameObject("StandardLadder");
        ladderObject.AddComponent<Rigidbody>();
        BoxCollider authoredCollider = ladderObject.AddComponent<BoxCollider>();
        authoredCollider.size = new Vector3(0.8f, 5f, 0.2f);

        try
        {
            Component ladder = ladderObject.AddComponent(StandardLadderType);
            float distance = (float)InvokeInstanceMethod(ladder, "GetPlatformSearchDistance");
            Assert.That(distance, Is.EqualTo(6f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ladderObject);
        }
    }


    private static object InvokeInstanceMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Could not find method '{methodName}' on {target.GetType().Name}.");
        return method.Invoke(target, args);
    }

    private static object GetPropertyOrFieldValue(object target, string memberName)
    {
        PropertyInfo property = target.GetType().GetProperty(memberName, InstanceFlags);
        if (property != null)
        {
            return property.GetValue(target);
        }

        FieldInfo field = target.GetType().GetField(memberName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Could not find property or field '{memberName}' on {target.GetType().Name}.");
        return field.GetValue(target);
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
}
