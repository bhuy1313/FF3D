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
    public void ApplyPlacement_BuildsColliderAndVisuals()
    {
        GameObject ladderObject = new GameObject("StandardLadder");
        ladderObject.AddComponent<Rigidbody>();

        try
        {
            Component ladder = ladderObject.AddComponent(StandardLadderType);
            InvokeInstanceMethod(ladder, "ConfigureDimensions", 0.65f, 0.3f, 0.2f);
            InvokeInstanceMethod(ladder, "ConfigureVisualReference", 0.65f, 3f, 0.25f);
            InvokeInstanceMethod(ladder, "ApplyPlacement", Vector3.zero, new Vector3(0f, 4f, 0f), Vector3.forward);

            BoxCollider collider = ladderObject.GetComponent<BoxCollider>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.size.x, Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(collider.size.y, Is.EqualTo(4f).Within(0.0001f));

            Transform visuals = ladderObject.transform.Find("Visuals");
            Assert.That(visuals, Is.Not.Null);
            Assert.That(visuals.childCount, Is.GreaterThanOrEqualTo(4));

            Component ladderComponent = ladderObject.GetComponent(LadderType);
            Assert.That(ladderComponent, Is.Not.Null);
            float climbMaxY = (float)InvokeInstanceMethod(ladderComponent, "GetClimbMaxY");
            Assert.That(climbMaxY, Is.GreaterThan(4f));
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
    public void UseModelHeightMode_KeepsVisualHeightScaleAtOne()
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
            SetFieldValue(ladder, "heightMode", Enum.Parse(FindType("StandardLadderHeightMode"), "UseModelHeight"));
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
    public void UseModelHeightMode_AnchorsTopFromBottomPlusModelHeight()
    {
        GameObject ladderObject = new GameObject("StandardLadder");
        ladderObject.AddComponent<Rigidbody>();

        try
        {
            Component ladder = ladderObject.AddComponent(StandardLadderType);
            SetFieldValue(ladder, "heightMode", Enum.Parse(FindType("StandardLadderHeightMode"), "UseModelHeight"));
            InvokeInstanceMethod(ladder, "ConfigureVisualReference", 0.65f, 3f, 0.25f);
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
    public void UseModelHeightMode_AllowsPlacementHeightBelowMinLadderHeight()
    {
        GameObject ladderObject = new GameObject("StandardLadder");
        ladderObject.AddComponent<Rigidbody>();

        try
        {
            Component ladder = ladderObject.AddComponent(StandardLadderType);
            SetFieldValue(ladder, "heightMode", Enum.Parse(FindType("StandardLadderHeightMode"), "UseModelHeight"));
            SetFieldValue(ladder, "minLadderHeight", 2f);

            bool allowed = (bool)InvokeInstanceMethod(ladder, "IsPlacementHeightWithinLimits", 1.2f);
            Assert.That(allowed, Is.True);
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

    private static void SetFieldValue(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}' on {target.GetType().Name}.");
        field.SetValue(target, value);
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
