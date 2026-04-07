using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MissionAuthoringSceneScanUtility
{
    public readonly struct SignalEmitterEntry
    {
        public SignalEmitterEntry(string key, string emitterType, MonoBehaviour component)
        {
            Key = key;
            EmitterType = emitterType;
            Component = component;
        }

        public string Key { get; }
        public string EmitterType { get; }
        public MonoBehaviour Component { get; }
    }

    public readonly struct RegistryEntry
    {
        public RegistryEntry(string key, GameObject targetObject)
        {
            Key = key;
            TargetObject = targetObject;
        }

        public string Key { get; }
        public GameObject TargetObject { get; }
    }

    public sealed class ScanResult
    {
        public Scene Scene;
        public MissionSceneObjectRegistry Registry;
        public readonly List<SignalEmitterEntry> SignalEmitters = new List<SignalEmitterEntry>();
        public readonly List<RegistryEntry> RegistryEntries = new List<RegistryEntry>();
    }

    public static ScanResult ScanActiveScene(MissionSceneObjectRegistry preferredRegistry = null)
    {
        return ScanScene(SceneManager.GetActiveScene(), preferredRegistry);
    }

    public static ScanResult ScanScene(Scene scene, MissionSceneObjectRegistry preferredRegistry = null)
    {
        ScanResult result = new ScanResult
        {
            Scene = scene,
            Registry = ResolveRegistry(scene, preferredRegistry)
        };

        if (!scene.IsValid())
        {
            return result;
        }

        AppendSignalEntries(UnityEngine.Object.FindObjectsByType<MissionSignalSource>(FindObjectsInactive.Include), scene, "Signal Source", result.SignalEmitters);
        AppendSignalEntries(UnityEngine.Object.FindObjectsByType<MissionInteractionSignalRelay>(FindObjectsInactive.Include), scene, "Interaction Relay", result.SignalEmitters);
        AppendSignalEntries(UnityEngine.Object.FindObjectsByType<MissionBreakableSignalRelay>(FindObjectsInactive.Include), scene, "Breakable Relay", result.SignalEmitters);
        AppendSignalEntries(UnityEngine.Object.FindObjectsByType<MissionRescueDeliverySignalRelay>(FindObjectsInactive.Include), scene, "Rescue Delivery Relay", result.SignalEmitters);
        AppendRegistryEntries(result.Registry, result.RegistryEntries);
        return result;
    }

    private static MissionSceneObjectRegistry ResolveRegistry(Scene scene, MissionSceneObjectRegistry preferredRegistry)
    {
        if (preferredRegistry != null && preferredRegistry.gameObject.scene == scene)
        {
            return preferredRegistry;
        }

        MissionSceneObjectRegistry[] registries = UnityEngine.Object.FindObjectsByType<MissionSceneObjectRegistry>(FindObjectsInactive.Include);
        for (int i = 0; i < registries.Length; i++)
        {
            MissionSceneObjectRegistry registry = registries[i];
            if (registry != null && registry.gameObject.scene == scene)
            {
                return registry;
            }
        }

        return null;
    }

    private static void AppendSignalEntries<T>(T[] behaviours, Scene scene, string emitterType, List<SignalEmitterEntry> results) where T : MonoBehaviour
    {
        if (behaviours == null || results == null)
        {
            return;
        }

        for (int i = 0; i < behaviours.Length; i++)
        {
            T behaviour = behaviours[i];
            if (behaviour == null || behaviour.gameObject.scene != scene)
            {
                continue;
            }

            SerializedObject serializedBehaviour = new SerializedObject(behaviour);
            string signalKey = serializedBehaviour.FindProperty("signalKey")?.stringValue;
            results.Add(new SignalEmitterEntry(signalKey?.Trim() ?? string.Empty, emitterType, behaviour));
        }
    }

    private static void AppendRegistryEntries(MissionSceneObjectRegistry registry, List<RegistryEntry> results)
    {
        if (registry == null || results == null)
        {
            return;
        }

        SerializedObject serializedRegistry = new SerializedObject(registry);
        SerializedProperty entriesProperty = serializedRegistry.FindProperty("entries");
        if (entriesProperty == null || !entriesProperty.isArray)
        {
            return;
        }

        for (int i = 0; i < entriesProperty.arraySize; i++)
        {
            SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(i);
            string key = entryProperty.FindPropertyRelative("key")?.stringValue?.Trim() ?? string.Empty;
            GameObject targetObject = entryProperty.FindPropertyRelative("targetObject")?.objectReferenceValue as GameObject;
            results.Add(new RegistryEntry(key, targetObject));
        }
    }
}
