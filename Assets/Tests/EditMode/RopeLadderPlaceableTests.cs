using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RopeLadderPlaceableTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type RopeLadderDeployedType = FindType("RopeLadderDeployed");
    private static readonly Type LadderType = FindType("Ladder");
    private static readonly Type FPSInventorySystemType = FindType("FPSInventorySystem");
    private static readonly Type CubeType = FindType("Cube");

    [Test]
    public void Configure_BuildsColliderAndVisualsForDeployedLadder()
    {
        GameObject ladderObject = new GameObject("Ladder");

        try
        {
            Component deployed = ladderObject.AddComponent(RopeLadderDeployedType);

            InvokeInstanceMethod(deployed, "ApplyDimensions", 0.5f, 0.4f, 0.05f, 0.04f, 0.25f, 0.2f);
            InvokeInstanceMethod(deployed, "Configure", new Vector3(1f, 4f, 2f), new Vector3(0f, 0f, 0f), Vector3.forward);

            BoxCollider collider = ladderObject.GetComponent<BoxCollider>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.size.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(collider.size.y, Is.EqualTo(4f).Within(0.0001f));

            Transform visuals = ladderObject.transform.Find("Visuals");
            Assert.That(visuals, Is.Not.Null);
            Assert.That(visuals.childCount, Is.GreaterThanOrEqualTo(4));

            Component ladder = ladderObject.GetComponent(LadderType);
            Assert.That(ladder, Is.Not.Null);
            float climbMaxY = (float)InvokeInstanceMethod(ladder, "GetClimbMaxY");
            Assert.That(climbMaxY, Is.GreaterThan(4f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ladderObject);
        }
    }

    [Test]
    public void RemoveHeld_ConsumesActiveItemAndEquipsNextSlot()
    {
        GameObject cameraObject = new GameObject("MainCamera");
        GameObject player = new GameObject("Player");
        GameObject firstItem = new GameObject("First");
        GameObject secondItem = new GameObject("Second");

        try
        {
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();

            Component inventory = player.AddComponent(FPSInventorySystemType);
            InvokeAwake(inventory);

            firstItem.AddComponent<Rigidbody>();
            Component firstCube = firstItem.AddComponent(CubeType);
            secondItem.AddComponent<Rigidbody>();
            Component secondCube = secondItem.AddComponent(CubeType);
            InvokeAwake(firstCube);
            InvokeAwake(secondCube);

            Assert.That((bool)InvokeInstanceMethod(inventory, "TryPickup", firstItem, player), Is.True);
            Assert.That((bool)InvokeInstanceMethod(inventory, "TryPickup", secondItem, player), Is.True);
            Assert.That(GetPropertyValue<GameObject>(inventory, "HeldObject"), Is.EqualTo(firstItem));

            Assert.That((bool)InvokeInstanceMethod(inventory, "RemoveHeld", player, true), Is.True);
            Assert.That(GetPropertyValue<int>(inventory, "ItemCount"), Is.EqualTo(1));
            Assert.That(GetPropertyValue<GameObject>(inventory, "HeldObject"), Is.EqualTo(secondItem));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(player);
            if (firstItem != null)
            {
                UnityEngine.Object.DestroyImmediate(firstItem);
            }

            if (secondItem != null)
            {
                UnityEngine.Object.DestroyImmediate(secondItem);
            }
        }
    }

    private static void InvokeAwake(object target)
    {
        MethodInfo awake = target.GetType().GetMethod("Awake", InstanceFlags);
        if (awake != null)
        {
            awake.Invoke(target, null);
        }
    }

    private static object InvokeInstanceMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Could not find method '{methodName}' on {target.GetType().Name}.");
        return method.Invoke(target, args);
    }

    private static T GetPropertyValue<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"Could not find property '{propertyName}'.");
        return (T)property.GetValue(target);
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
