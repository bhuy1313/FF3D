using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PlayerActionLockTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type FirstPersonControllerType = FindType("FirstPersonController");
    private static readonly Type FPSInteractionSystemType = FindType("FPSInteractionSystem");
    private static readonly Type FPSInventorySystemType = FindType("FPSInventorySystem");
    private static readonly Type PlayerActionLockType = FindType("PlayerActionLock");
    private static readonly Type RescuableType = FindType("Rescuable");
    private static readonly Type VictimConditionType = FindType("VictimCondition");

    [Test]
    public void AcquireFullLock_DisablesPlayerControllerInteractionAndInventory()
    {
        GameObject player = new GameObject("Player");

        try
        {
            player.AddComponent<CharacterController>();
            Behaviour controller = (Behaviour)player.AddComponent(FirstPersonControllerType);
            Behaviour interactionSystem = (Behaviour)player.AddComponent(FPSInteractionSystemType);
            Behaviour inventorySystem = (Behaviour)player.AddComponent(FPSInventorySystemType);
            Component actionLock = player.AddComponent(PlayerActionLockType);

            controller.enabled = true;
            interactionSystem.enabled = true;
            inventorySystem.enabled = true;

            InvokeInstanceMethod(actionLock, "AcquireFullLock");

            Assert.That(controller.enabled, Is.False);
            Assert.That(interactionSystem.enabled, Is.False);
            Assert.That(inventorySystem.enabled, Is.False);

            InvokeInstanceMethod(actionLock, "ReleaseFullLock");

            Assert.That(controller.enabled, Is.True);
            Assert.That(interactionSystem.enabled, Is.True);
            Assert.That(inventorySystem.enabled, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void AcquireCarryRestriction_PreservesMovementButBlocksInventoryActions()
    {
        GameObject player = new GameObject("Player");

        try
        {
            player.AddComponent<CharacterController>();
            Behaviour controller = (Behaviour)player.AddComponent(FirstPersonControllerType);
            Behaviour interactionSystem = (Behaviour)player.AddComponent(FPSInteractionSystemType);
            Behaviour inventorySystem = (Behaviour)player.AddComponent(FPSInventorySystemType);
            Component actionLock = player.AddComponent(PlayerActionLockType);

            controller.enabled = true;
            interactionSystem.enabled = true;
            inventorySystem.enabled = true;

            InvokeInstanceMethod(actionLock, "AcquireCarryRestriction");

            Assert.That(controller.enabled, Is.True);
            Assert.That(interactionSystem.enabled, Is.True);
            Assert.That(inventorySystem.enabled, Is.True);
            Assert.That(GetPropertyValue<bool>(actionLock, "HasCarryRestriction"), Is.True);
            Assert.That(GetPropertyValue<bool>(actionLock, "AllowsInventoryActions"), Is.False);
            Assert.That(GetPropertyValue<bool>(actionLock, "AllowsSafeZoneInteractionOnly"), Is.True);

            InvokeInstanceMethod(actionLock, "ReleaseCarryRestriction");

            Assert.That(GetPropertyValue<bool>(actionLock, "HasCarryRestriction"), Is.False);
            Assert.That(GetPropertyValue<bool>(actionLock, "AllowsInventoryActions"), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void TryBeginCarry_AcquiresFullLockForPlayerPickupPhase()
    {
        GameObject player = new GameObject("Player");
        GameObject victim = new GameObject("Victim");

        try
        {
            player.AddComponent<CharacterController>();
            player.AddComponent(FirstPersonControllerType);
            player.AddComponent(FPSInteractionSystemType);
            player.AddComponent(FPSInventorySystemType);
            Component actionLock = player.AddComponent(PlayerActionLockType);
            Component rescuable = victim.AddComponent(RescuableType);

            bool started = (bool)InvokeInstanceMethod(rescuable, "TryBeginCarry", player, player.transform);

            Assert.That(started, Is.True);
            Assert.That(GetPropertyValue<bool>(actionLock, "IsFullyLocked"), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(victim);
        }
    }

    [Test]
    public void TryStabilize_AcquiresFullLockWhenVictimRequiresStabilization()
    {
        GameObject player = new GameObject("Player");
        GameObject victim = new GameObject("Victim");

        try
        {
            player.AddComponent<CharacterController>();
            player.AddComponent(FirstPersonControllerType);
            player.AddComponent(FPSInteractionSystemType);
            player.AddComponent(FPSInventorySystemType);
            Component actionLock = player.AddComponent(PlayerActionLockType);
            Component rescuable = victim.AddComponent(RescuableType);
            Component victimCondition = victim.AddComponent(VictimConditionType);

            SetPrivateField(victimCondition, "requireStabilizationBeforeCarryWhenCritical", true);
            SetPrivateField(victimCondition, "currentCondition", 10f);
            InvokeInstanceMethod(victimCondition, "OnValidate");

            bool started = (bool)InvokeInstanceMethod(rescuable, "TryStabilize", player);

            Assert.That(started, Is.True);
            Assert.That(GetPropertyValue<bool>(actionLock, "IsFullyLocked"), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(victim);
        }
    }

    private static object InvokeInstanceMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Could not find method '{methodName}'.");
        return method.Invoke(target, args);
    }

    private static T GetPropertyValue<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"Could not find property '{propertyName}'.");
        return (T)property.GetValue(target);
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
}
