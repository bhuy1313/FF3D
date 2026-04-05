using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PlayerMovementWeightTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type CharacterControllerType = FindType("CharacterController");
    private static readonly Type InteractionSystemType = FindType("StarterAssets.FPSInteractionSystem");
    private static readonly Type FirstPersonControllerType = FindType("StarterAssets.FirstPersonController");
    private static readonly Type CubeType = FindType("Cube");
    private static readonly Type RescuableType = FindType("Rescuable");

    [Test]
    public void InteractionSystem_UsesMovementWeightSource_ForGrabAndCarryBurden()
    {
        GameObject playerObject = new GameObject("Player");
        GameObject grabbedObject = new GameObject("GrabbedCube");
        GameObject victimObject = new GameObject("Victim");

        try
        {
            playerObject.AddComponent(CharacterControllerType);
            Component interactionSystem = playerObject.AddComponent(InteractionSystemType);

            Rigidbody grabbedBody = grabbedObject.AddComponent<Rigidbody>();
            grabbedObject.AddComponent(CubeType);
            SetFieldValue(interactionSystem, "grabbedBody", grabbedBody);

            victimObject.AddComponent<Rigidbody>();
            Component rescuable = victimObject.AddComponent(RescuableType);
            InvokeInstanceMethod(rescuable, "OnEnable");
            SetFieldValue(rescuable, "isCarried", true);
            SetFieldValue(rescuable, "activeRescuer", playerObject);

            Assert.That(GetPropertyValue<float>(interactionSystem, "CurrentGrabWeightKg"), Is.EqualTo(5f).Within(0.001f));
            Assert.That(GetPropertyValue<float>(interactionSystem, "CurrentCarryWeightKg"), Is.EqualTo(75f).Within(0.001f));
            Assert.That(GetPropertyValue<float>(interactionSystem, "CurrentMovementBurdenKg"), Is.EqualTo(80f).Within(0.001f));

            InvokeInstanceMethod(rescuable, "OnDisable");
        }
        finally
        {
            if (playerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(playerObject);
            }

            if (grabbedObject != null)
            {
                UnityEngine.Object.DestroyImmediate(grabbedObject);
            }

            if (victimObject != null)
            {
                UnityEngine.Object.DestroyImmediate(victimObject);
            }
        }
    }

    [Test]
    public void InteractionSystem_FallsBackToRigidbodyMass_WhenWeightSourceIsMissing()
    {
        GameObject playerObject = new GameObject("Player");
        GameObject grabbedObject = new GameObject("GrabbedBody");

        try
        {
            playerObject.AddComponent(CharacterControllerType);
            Component interactionSystem = playerObject.AddComponent(InteractionSystemType);

            Rigidbody grabbedBody = grabbedObject.AddComponent<Rigidbody>();
            grabbedBody.mass = 13f;
            SetFieldValue(interactionSystem, "grabbedBody", grabbedBody);

            Assert.That(GetPropertyValue<float>(interactionSystem, "CurrentGrabWeightKg"), Is.EqualTo(13f).Within(0.001f));
        }
        finally
        {
            if (playerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(playerObject);
            }

            if (grabbedObject != null)
            {
                UnityEngine.Object.DestroyImmediate(grabbedObject);
            }
        }
    }

    [Test]
    public void FirstPersonController_EvaluatesMovementWeightMultiplier_FromConfiguredThresholds()
    {
        GameObject playerObject = new GameObject("Player");

        try
        {
            playerObject.AddComponent(CharacterControllerType);
            Component controller = playerObject.AddComponent(FirstPersonControllerType);
            SetFieldOrPropertyValue(controller, "EnableMovementWeightPenalty", true);
            SetFieldOrPropertyValue(controller, "WeightForMinimumSpeed", 80f);
            SetFieldOrPropertyValue(controller, "MinimumWeightSpeedMultiplier", 0.35f);

            float noBurdenMultiplier = (float)InvokeInstanceMethod(controller, "EvaluateMovementWeightMultiplier", 0f, 1f);
            float fullBurdenMultiplier = (float)InvokeInstanceMethod(controller, "EvaluateMovementWeightMultiplier", 80f, 1f);
            float crouchBurdenMultiplier = (float)InvokeInstanceMethod(controller, "EvaluateMovementWeightMultiplier", 80f, 0.5f);

            Assert.That(noBurdenMultiplier, Is.EqualTo(1f).Within(0.001f));
            Assert.That(fullBurdenMultiplier, Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(crouchBurdenMultiplier, Is.EqualTo(0.675f).Within(0.001f));
        }
        finally
        {
            if (playerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(playerObject);
            }
        }
    }

    [Test]
    public void FirstPersonController_NormalizesCameraMotionBurden_FromCurrentWeight()
    {
        GameObject playerObject = new GameObject("Player");

        try
        {
            playerObject.AddComponent(CharacterControllerType);
            Component interactionSystem = playerObject.AddComponent(InteractionSystemType);
            Component controller = playerObject.AddComponent(FirstPersonControllerType);

            SetFieldOrPropertyValue(controller, "EnableWeightCameraMotionImpact", true);
            SetFieldOrPropertyValue(controller, "WeightForMinimumSpeed", 80f);
            SetFieldValue(interactionSystem, "grabbedBody", CreateWeightedBody(playerObject, "WeightedBody", 40f));

            MethodInfo startMethod = controller.GetType().GetMethod("Start", InstanceFlags);
            if (startMethod != null)
            {
                startMethod.Invoke(controller, null);
            }

            float burdenT = (float)InvokeInstanceMethod(controller, "GetMovementBurdenNormalized");
            Assert.That(burdenT, Is.EqualTo(0.5f).Within(0.001f));
        }
        finally
        {
            if (playerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(playerObject);
            }
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

    private static T GetPropertyValue<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"Could not find property '{propertyName}' on {target.GetType().Name}.");
        return (T)property.GetValue(target);
    }

    private static void SetFieldOrPropertyValue(object target, string memberName, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(memberName, InstanceFlags);
        if (property != null)
        {
            property.SetValue(target, value);
            return;
        }

        FieldInfo field = target.GetType().GetField(memberName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Could not find property or field '{memberName}' on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private static Rigidbody CreateWeightedBody(GameObject parent, string name, float mass)
    {
        GameObject bodyObject = new GameObject(name);
        Rigidbody body = bodyObject.AddComponent<Rigidbody>();
        body.mass = mass;
        bodyObject.transform.SetParent(parent.transform, false);
        return body;
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
                if (candidate != null && (candidate.FullName == typeName || candidate.Name == typeName))
                {
                    return candidate;
                }
            }
        }

        Assert.Fail($"Could not find type '{typeName}'.");
        return null;
    }
}
