using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FF3D.Editor
{
    public sealed class IncidentSceneSetupWindow : EditorWindow
    {
        private const string DefaultRootName = "IncidentSetup";
        private const string SimulationRootPrefix = "FireSimulation_";
        private const string DefaultWindowLockTaskName = "WindowLockRandomizerStartupTask";
        private const string DefaultPayloadTaskName = "IncidentPayloadStartupTask";
        private const string DefaultVictimPlacementTaskName = "VictimPlacementStartupTask";
        private const string DefaultStepsRootName = "SetupSteps";
        private const string DefaultDebugSpawnerName = "DebugIncidentSpawner";
        private const string OriginAreaTaskName = "IncidentOriginAreaMapSetupTask";
        private const string AnchorTaskName = "IncidentAnchorHazardMapSetupTask";
        private const string VentilationTaskName = "IncidentVentilationPresetMapSetupTask";

        [SerializeField] private bool ensureSceneStartupFlow = true;
        [SerializeField] private bool ensureWindowLockStartupTask = true;
        [SerializeField] private bool ensurePayloadStartupTask = true;
        [SerializeField] private bool ensureVictimPlacementStartupTask = true;
        [SerializeField] private bool ensureMapSetupRoot = true;
        [SerializeField] private bool ensureSimulationManager = true;
        [SerializeField] private bool ensureDefaultSteps = true;
        [SerializeField] private bool ensureDebugPayloadSpawner;
        [SerializeField] private bool ensureEffectManagerAndClusterTemplate = true;
        [SerializeField] private bool assignSceneMatchedProfiles = true;
        [SerializeField] private bool createProfilesIfMissing = true;
        [SerializeField] private bool setupSelectedAreasWithIncidentComponents = true;
        [SerializeField] private bool ensureSmokeHazardOnSelectedAreas = true;
        [SerializeField] private bool assignKeysFromObjectNames = true;

        [MenuItem("Tools/TrueJourney/Scenes/Incident Setup")]
        public static void OpenWindow()
        {
            IncidentSceneSetupWindow window = GetWindow<IncidentSceneSetupWindow>("Incident Scene Setup");
            window.minSize = new Vector2(460f, 340f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Incident Scene Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Sets up the incident payload/simulation pipeline for the active scene without touching the core shell setup tool. " +
                "Existing scene objects are reused when possible.",
                MessageType.Info);

            DrawToggleList();

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Setup Current Scene", GUILayout.Height(32f)))
                {
                    SetupCurrentScene();
                }

                if (GUILayout.Button("Setup Selected Areas", GUILayout.Height(32f)))
                {
                    SetupSelectedAreas();
                }
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Validate Current Scene", GUILayout.Height(28f)))
            {
                ValidateCurrentScene(showDialog: true);
            }
        }

        private void DrawToggleList()
        {
            EditorGUILayout.LabelField("Scene Setup", EditorStyles.boldLabel);
            ensureSceneStartupFlow = EditorGUILayout.ToggleLeft("Ensure SceneStartupFlow", ensureSceneStartupFlow);
            ensureWindowLockStartupTask = EditorGUILayout.ToggleLeft("Ensure WindowLockRandomizerStartupTask", ensureWindowLockStartupTask);
            ensurePayloadStartupTask = EditorGUILayout.ToggleLeft("Ensure IncidentPayloadStartupTask", ensurePayloadStartupTask);
            ensureVictimPlacementStartupTask = EditorGUILayout.ToggleLeft("Ensure VictimPlacementStartupTask", ensureVictimPlacementStartupTask);
            ensureMapSetupRoot = EditorGUILayout.ToggleLeft("Ensure IncidentMapSetupRoot", ensureMapSetupRoot);
            ensureSimulationManager = EditorGUILayout.ToggleLeft("Ensure FireSimulationManager + FireSurfaceGraph", ensureSimulationManager);
            ensureDefaultSteps = EditorGUILayout.ToggleLeft("Ensure default IncidentMapSetupTasks", ensureDefaultSteps);
            ensureDebugPayloadSpawner = EditorGUILayout.ToggleLeft("Ensure DebugIncidentPayloadSpawner", ensureDebugPayloadSpawner);
            ensureEffectManagerAndClusterTemplate = EditorGUILayout.ToggleLeft("Ensure FireEffectManager + FireNodeEffectView template", ensureEffectManagerAndClusterTemplate);
            assignSceneMatchedProfiles = EditorGUILayout.ToggleLeft("Assign scene-matched profiles", assignSceneMatchedProfiles);
            createProfilesIfMissing = EditorGUILayout.ToggleLeft("Create profiles if missing", createProfilesIfMissing);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Area Setup", EditorStyles.boldLabel);
            setupSelectedAreasWithIncidentComponents = EditorGUILayout.ToggleLeft(
                "Setup selected collider objects as Incident areas",
                setupSelectedAreasWithIncidentComponents);
            ensureSmokeHazardOnSelectedAreas = EditorGUILayout.ToggleLeft(
                "Ensure SmokeHazard + Rigidbody on selected areas",
                ensureSmokeHazardOnSelectedAreas);
            assignKeysFromObjectNames = EditorGUILayout.ToggleLeft(
                "Fill empty area/location keys from object names",
                assignKeysFromObjectNames);
        }

        private void SetupCurrentScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Incident Scene Setup", "No active scene is loaded.", "OK");
                return;
            }

            SetupResult result = new SetupResult();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Incident Scene");

            try
            {
                GameObject incidentRoot = ResolveIncidentRoot(scene, result);
                SceneStartupFlow startupFlow = ensureSceneStartupFlow
                    ? EnsureComponent<SceneStartupFlow>(incidentRoot, "SceneStartupFlow", result)
                    : incidentRoot.GetComponent<SceneStartupFlow>();
                IncidentMapSetupRoot mapSetupRoot = ensureMapSetupRoot
                    ? EnsureComponent<IncidentMapSetupRoot>(incidentRoot, "IncidentMapSetupRoot", result)
                    : incidentRoot.GetComponent<IncidentMapSetupRoot>();
                ConfigureStartupFlow(startupFlow, result);
                ConfigureMapSetupRoot(mapSetupRoot, result);

                FireSimulationManager simulationManager = null;
                FireSurfaceGraph surfaceGraph = null;
                if (ensureSimulationManager)
                {
                    GameObject simulationRoot = ResolveSimulationRoot(scene, result);
                    surfaceGraph = EnsureComponent<FireSurfaceGraph>(simulationRoot, "FireSurfaceGraph", result);
                    simulationManager = EnsureComponent<FireSimulationManager>(simulationRoot, "FireSimulationManager", result);
                    AssignManagerDependencies(scene, surfaceGraph, simulationManager, result);
                    if (ensureEffectManagerAndClusterTemplate)
                    {
                        EnsureSimulationEffects(simulationRoot, simulationManager, result);
                    }
                }
                else
                {
                    simulationManager = FindFirstInScene<FireSimulationManager>(scene);
                    surfaceGraph = simulationManager != null ? simulationManager.GetComponent<FireSurfaceGraph>() : FindFirstInScene<FireSurfaceGraph>(scene);
                }

                IncidentFireSpawnProfile fireSpawnProfile = ResolveOrCreateSceneProfile<IncidentFireSpawnProfile>(scene, "FireSpawnProfile", result);
                if (mapSetupRoot != null)
                {
                    AssignMapSetupBindings(mapSetupRoot, fireSpawnProfile, simulationManager, result);
                }

                if (ensureDefaultSteps && mapSetupRoot != null)
                {
                    GameObject stepsRoot = EnsureChildObject(incidentRoot, DefaultStepsRootName, result);
                    IncidentOriginAreaMapSetupTask originAreaTask = EnsureSetupTask<IncidentOriginAreaMapSetupTask>(stepsRoot, OriginAreaTaskName, result);
                    IncidentAnchorHazardMapSetupTask anchorTask = EnsureSetupTask<IncidentAnchorHazardMapSetupTask>(stepsRoot, AnchorTaskName, result);
                    IncidentVentilationPresetMapSetupTask ventilationTask = EnsureSetupTask<IncidentVentilationPresetMapSetupTask>(stepsRoot, VentilationTaskName, result);
                    AddMapSetupTask(mapSetupRoot, originAreaTask, result);
                    AddMapSetupTask(mapSetupRoot, anchorTask, result);
                    AddMapSetupTask(mapSetupRoot, ventilationTask, result);
                }

                if (ensurePayloadStartupTask)
                {
                    GameObject payloadTaskRoot = EnsureChildObject(incidentRoot, DefaultPayloadTaskName, result);
                    IncidentPayloadStartupTask payloadTask = EnsureComponent<IncidentPayloadStartupTask>(
                        payloadTaskRoot,
                        "IncidentPayloadStartupTask",
                        result);
                    ConfigureStartupTask(payloadTask, result);
                    AssignPayloadTaskBindings(payloadTask, mapSetupRoot, result);
                    AddStartupTaskToFlow(startupFlow, payloadTask, result);
                }

                if (ensureWindowLockStartupTask)
                {
                    GameObject windowLockTaskRoot = EnsureChildObject(incidentRoot, DefaultWindowLockTaskName, result);
                    WindowLockRandomizerStartupTask windowLockTask = EnsureComponent<WindowLockRandomizerStartupTask>(
                        windowLockTaskRoot,
                        "WindowLockRandomizerStartupTask",
                        result);
                    ConfigureStartupTask(windowLockTask, result);
                    ConfigureWindowLockStartupTask(windowLockTask, result);
                    AddStartupTaskToFlow(startupFlow, windowLockTask, result);
                }

                if (ensureVictimPlacementStartupTask)
                {
                    GameObject victimPlacementTaskRoot = EnsureChildObject(incidentRoot, DefaultVictimPlacementTaskName, result);
                    VictimPlacementStartupTask victimPlacementTask = EnsureComponent<VictimPlacementStartupTask>(
                        victimPlacementTaskRoot,
                        "VictimPlacementStartupTask",
                        result);
                    ConfigureStartupTask(victimPlacementTask, result);
                    ConfigureVictimPlacementStartupTask(scene, victimPlacementTask, result);
                    AddStartupTaskToFlow(startupFlow, victimPlacementTask, result);
                }

                if (ensureDebugPayloadSpawner)
                {
                    GameObject debugSpawnerRoot = ResolveDebugSpawnerRoot(scene, result);
                    EnsureComponent<DebugIncidentPayloadSpawner>(
                        debugSpawnerRoot,
                        "DebugIncidentPayloadSpawner",
                        result);
                }

                if (setupSelectedAreasWithIncidentComponents)
                {
                    SetupSelectedAreasInternal(result, warnIfNoSelection: false);
                }

                if (result.HasChanges)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }

                ValidateCurrentScene(showDialog: false, result);
                ShowSetupResult(scene, result);
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private void SetupSelectedAreas()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Incident Scene Setup", "No active scene is loaded.", "OK");
                return;
            }

            SetupResult result = new SetupResult();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Incident Areas");

            try
            {
                SetupSelectedAreasInternal(result, warnIfNoSelection: true);
                if (result.HasChanges)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }

                ShowSetupResult(scene, result);
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private void SetupSelectedAreasInternal(SetupResult result, bool warnIfNoSelection)
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                if (warnIfNoSelection)
                {
                    result.Warnings.Add("No objects selected for area setup.");
                }
                return;
            }

            for (int i = 0; i < selectedObjects.Length; i++)
            {
                GameObject selectedObject = selectedObjects[i];
                if (selectedObject == null)
                {
                    continue;
                }

                Collider areaCollider = selectedObject.GetComponent<Collider>();
                if (areaCollider == null)
                {
                    result.Warnings.Add($"Skipped '{selectedObject.name}' because it has no Collider.");
                    continue;
                }

                IncidentPayloadAnchor payloadAnchor = EnsureComponent<IncidentPayloadAnchor>(selectedObject, "IncidentPayloadAnchor", result);
                IncidentOriginArea originArea = EnsureComponent<IncidentOriginArea>(selectedObject, "IncidentOriginArea", result);
                AssignAreaCollider(originArea, areaCollider, result);
                ConfigureAreaCollider(areaCollider, result);
                AssignAnchorRuntimeZoneSize(payloadAnchor, areaCollider, result);

                if (assignKeysFromObjectNames)
                {
                    AssignDefaultAreaKeys(originArea, payloadAnchor, selectedObject.name, result);
                }

                if (ensureSmokeHazardOnSelectedAreas)
                {
                    SmokeHazard smokeHazard = EnsureComponent<SmokeHazard>(selectedObject, "SmokeHazard", result);
                    Rigidbody triggerBody = EnsureComponent<Rigidbody>(selectedObject, "Rigidbody", result);
                    ConfigureSmokeHazard(payloadAnchor, smokeHazard, triggerBody, areaCollider, result);
                }

                result.RepairedObjects.Add($"Configured incident area on '{selectedObject.name}'");
            }
        }

        private void ValidateCurrentScene(bool showDialog)
        {
            ValidateCurrentScene(showDialog, new SetupResult());
        }

        private void ValidateCurrentScene(bool showDialog, SetupResult setupResult)
        {
            Scene scene = SceneManager.GetActiveScene();
            List<string> warnings = setupResult.Warnings;

            SceneStartupFlow startupFlow = FindFirstInScene<SceneStartupFlow>(scene);
            IncidentMapSetupRoot mapSetupRoot = FindFirstInScene<IncidentMapSetupRoot>(scene);
            IncidentPayloadStartupTask payloadTask = FindFirstInScene<IncidentPayloadStartupTask>(scene);
            WindowLockRandomizerStartupTask windowLockTask = FindFirstInScene<WindowLockRandomizerStartupTask>(scene);
            VictimPlacementStartupTask victimPlacementTask = FindFirstInScene<VictimPlacementStartupTask>(scene);
            FireSimulationManager simulationManager = FindFirstInScene<FireSimulationManager>(scene);
            FireSurfaceGraph surfaceGraph = FindFirstInScene<FireSurfaceGraph>(scene);
            DebugIncidentPayloadSpawner debugSpawner = FindFirstInScene<DebugIncidentPayloadSpawner>(scene);
            FireEffectManager effectManager = FindFirstInScene<FireEffectManager>(scene);
            IncidentOriginArea[] areas = FindAllInScene<IncidentOriginArea>(scene);

            if (startupFlow == null)
            {
                warnings.Add("Missing SceneStartupFlow.");
            }

            if (payloadTask == null)
            {
                warnings.Add("Missing IncidentPayloadStartupTask.");
            }

            if (windowLockTask == null)
            {
                warnings.Add("Missing WindowLockRandomizerStartupTask.");
            }

            if (ensureVictimPlacementStartupTask && victimPlacementTask == null)
            {
                warnings.Add("Missing VictimPlacementStartupTask.");
            }

            if (mapSetupRoot == null)
            {
                warnings.Add("Missing IncidentMapSetupRoot.");
            }

            if (simulationManager == null)
            {
                warnings.Add("Missing FireSimulationManager.");
            }

            if (surfaceGraph == null)
            {
                warnings.Add("Missing FireSurfaceGraph.");
            }

            if (ensureEffectManagerAndClusterTemplate && effectManager == null)
            {
                warnings.Add("Missing FireEffectManager.");
            }

            if (ensureDebugPayloadSpawner && debugSpawner == null)
            {
                warnings.Add("Missing DebugIncidentPayloadSpawner.");
            }

            if (mapSetupRoot != null)
            {
                SerializedObject setupRootSerialized = new SerializedObject(mapSetupRoot);
                if (setupRootSerialized.FindProperty("fireSpawnProfile")?.objectReferenceValue == null)
                {
                    warnings.Add("IncidentMapSetupRoot has no IncidentFireSpawnProfile assigned.");
                }

                if (setupRootSerialized.FindProperty("fireSimulationManager")?.objectReferenceValue == null)
                {
                    warnings.Add("IncidentMapSetupRoot has no FireSimulationManager assigned.");
                }
            }

            if (simulationManager != null)
            {
                SerializedObject managerSerialized = new SerializedObject(simulationManager);
                if (managerSerialized.FindProperty("simulationProfile")?.objectReferenceValue == null)
                {
                    warnings.Add("FireSimulationManager has no FireSimulationProfile assigned.");
                }

                if (managerSerialized.FindProperty("surfaceGraph")?.objectReferenceValue == null)
                {
                    warnings.Add("FireSimulationManager has no FireSurfaceGraph assigned.");
                }

                if (ensureEffectManagerAndClusterTemplate)
                {
                    if (managerSerialized.FindProperty("effectManager")?.objectReferenceValue == null)
                    {
                        warnings.Add("FireSimulationManager has no FireEffectManager assigned.");
                    }
                }
            }

            if (areas == null || areas.Length == 0)
            {
                warnings.Add("No IncidentOriginArea found in scene.");
            }
            else
            {
                for (int i = 0; i < areas.Length; i++)
                {
                    IncidentOriginArea area = areas[i];
                    if (area == null)
                    {
                        continue;
                    }

                    if (area.AreaCollider == null)
                    {
                        warnings.Add($"IncidentOriginArea '{area.name}' has no area collider.");
                    }
                }
            }

            if (!showDialog)
            {
                return;
            }

            string message = warnings.Count == 0
                ? "Incident scene setup looks valid."
                : "- " + string.Join("\n- ", warnings);
            EditorUtility.DisplayDialog("Incident Scene Validation", message, "OK");
        }

        private static void AssignDefaultAreaKeys(
            IncidentOriginArea originArea,
            IncidentPayloadAnchor payloadAnchor,
            string objectName,
            SetupResult result)
        {
            string key = SanitizeKey(objectName);
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (originArea != null)
            {
                SerializedObject originAreaSerialized = new SerializedObject(originArea);
                SerializedProperty areaKeyProperty = originAreaSerialized.FindProperty("areaKey");
                if (areaKeyProperty != null && string.IsNullOrWhiteSpace(areaKeyProperty.stringValue))
                {
                    Undo.RecordObject(originArea, "Assign IncidentOriginArea Key");
                    areaKeyProperty.stringValue = key;
                    originAreaSerialized.ApplyModifiedProperties();
                    result.RepairedObjects.Add($"{originArea.name}.areaKey");
                }
            }

            if (payloadAnchor != null)
            {
                SerializedObject payloadAnchorSerialized = new SerializedObject(payloadAnchor);
                SerializedProperty logicalKeyProperty = payloadAnchorSerialized.FindProperty("logicalLocationKey");
                if (logicalKeyProperty != null && string.IsNullOrWhiteSpace(logicalKeyProperty.stringValue))
                {
                    Undo.RecordObject(payloadAnchor, "Assign IncidentPayloadAnchor Logical Key");
                    logicalKeyProperty.stringValue = key;
                    payloadAnchorSerialized.ApplyModifiedProperties();
                    result.RepairedObjects.Add($"{payloadAnchor.name}.logicalLocationKey");
                }
            }
        }

        private static void AssignAreaCollider(IncidentOriginArea originArea, Collider areaCollider, SetupResult result)
        {
            if (originArea == null || areaCollider == null || originArea.AreaCollider == areaCollider)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(originArea);
            serializedObject.Update();
            SerializedProperty areaVolumeProperty = serializedObject.FindProperty("areaVolume");
            if (areaVolumeProperty == null || areaVolumeProperty.objectReferenceValue == areaCollider)
            {
                return;
            }

            Undo.RecordObject(originArea, "Assign IncidentOriginArea Area Collider");
            areaVolumeProperty.objectReferenceValue = areaCollider;
            serializedObject.ApplyModifiedProperties();
            result.RepairedObjects.Add($"{originArea.name}.areaVolume");
        }

        private static void AssignPayloadTaskBindings(
            IncidentPayloadStartupTask payloadTask,
            IncidentMapSetupRoot mapSetupRoot,
            SetupResult result)
        {
            if (payloadTask == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(payloadTask);
            serializedObject.Update();

            SerializedProperty setupRootProperty = serializedObject.FindProperty("explicitMapSetupRoot");
            if (setupRootProperty != null && setupRootProperty.objectReferenceValue != mapSetupRoot)
            {
                Undo.RecordObject(payloadTask, "Assign IncidentPayloadStartupTask Map Setup Root");
                setupRootProperty.objectReferenceValue = mapSetupRoot;
                serializedObject.ApplyModifiedProperties();
                result.RepairedObjects.Add("IncidentPayloadStartupTask.explicitMapSetupRoot");
            }
        }

        private static void ConfigureAreaCollider(Collider areaCollider, SetupResult result)
        {
            if (areaCollider == null || areaCollider.isTrigger)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(areaCollider);
            serializedObject.Update();
            SerializedProperty isTriggerProperty = serializedObject.FindProperty("m_IsTrigger");
            if (isTriggerProperty == null || isTriggerProperty.boolValue)
            {
                return;
            }

            Undo.RecordObject(areaCollider, "Configure Area Collider Trigger");
            isTriggerProperty.boolValue = true;
            serializedObject.ApplyModifiedProperties();
            result.RepairedObjects.Add($"{areaCollider.name}.isTrigger");
        }

        private static void AssignAnchorRuntimeZoneSize(
            IncidentPayloadAnchor payloadAnchor,
            Collider areaCollider,
            SetupResult result)
        {
            if (payloadAnchor == null || areaCollider == null)
            {
                return;
            }

            Vector3 lossyScale = areaCollider.transform.lossyScale;
            Vector3 safeScale = new Vector3(
                Mathf.Approximately(lossyScale.x, 0f) ? 1f : Mathf.Abs(lossyScale.x),
                Mathf.Approximately(lossyScale.y, 0f) ? 1f : Mathf.Abs(lossyScale.y),
                Mathf.Approximately(lossyScale.z, 0f) ? 1f : Mathf.Abs(lossyScale.z));

            Vector3 runtimeZoneSize = areaCollider.bounds.size;
            if (areaCollider is BoxCollider boxCollider)
            {
                runtimeZoneSize = Vector3.Scale(boxCollider.size, safeScale);
            }

            runtimeZoneSize = Vector3.Max(runtimeZoneSize, Vector3.one * 0.05f);

            SerializedObject serializedObject = new SerializedObject(payloadAnchor);
            serializedObject.Update();
            SerializedProperty zoneSizeProperty = serializedObject.FindProperty("runtimeZoneSize");
            if (zoneSizeProperty == null || zoneSizeProperty.vector3Value == runtimeZoneSize)
            {
                return;
            }

            Undo.RecordObject(payloadAnchor, "Assign IncidentPayloadAnchor Runtime Zone Size");
            zoneSizeProperty.vector3Value = runtimeZoneSize;
            serializedObject.ApplyModifiedProperties();
            result.RepairedObjects.Add($"{payloadAnchor.name}.runtimeZoneSize");
        }

        private static void ConfigureSmokeHazard(
            IncidentPayloadAnchor payloadAnchor,
            SmokeHazard smokeHazard,
            Rigidbody triggerBody,
            Collider areaCollider,
            SetupResult result)
        {
            if (smokeHazard == null || triggerBody == null || areaCollider == null)
            {
                return;
            }

            Undo.RecordObject(triggerBody, "Configure SmokeHazard Rigidbody");
            triggerBody.isKinematic = true;
            triggerBody.useGravity = false;

            SerializedObject smokeSerialized = new SerializedObject(smokeHazard);
            smokeSerialized.Update();
            bool smokeChanged = false;

            smokeChanged |= SetObjectReferenceProperty(
                smokeSerialized,
                smokeHazard,
                "triggerZone",
                areaCollider,
                $"{smokeHazard.name}.triggerZone",
                result);
            smokeChanged |= SetObjectReferenceProperty(
                smokeSerialized,
                smokeHazard,
                "triggerBody",
                triggerBody,
                $"{smokeHazard.name}.triggerBody",
                result);
            smokeChanged |= SetEnumProperty(
                smokeSerialized,
                smokeHazard,
                "autoCollectMode",
                1,
                $"{smokeHazard.name}.autoCollectMode",
                result);

            if (smokeChanged)
            {
                smokeSerialized.ApplyModifiedProperties();
            }

            if (payloadAnchor == null)
            {
                return;
            }

            SerializedObject anchorSerialized = new SerializedObject(payloadAnchor);
            anchorSerialized.Update();
            bool anchorChanged = false;
            anchorChanged |= SetObjectReferenceProperty(
                anchorSerialized,
                payloadAnchor,
                "smokeHazard",
                smokeHazard,
                $"{payloadAnchor.name}.smokeHazard",
                result);
            anchorChanged |= SetBoolProperty(
                anchorSerialized,
                payloadAnchor,
                "createRuntimeSmokeHazard",
                false,
                $"{payloadAnchor.name}.createRuntimeSmokeHazard",
                result);

            if (anchorChanged)
            {
                anchorSerialized.ApplyModifiedProperties();
            }
        }

        private static void ConfigureStartupTask(SceneStartupTask startupTask, SetupResult result)
        {
        }

        private static void AddStartupTaskToFlow(SceneStartupFlow startupFlow, SceneStartupTask startupTask, SetupResult result)
        {
            if (startupFlow == null || startupTask == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(startupFlow);
            serializedObject.Update();
            SerializedProperty tasksProperty = serializedObject.FindProperty("explicitTasks");
            if (tasksProperty == null)
            {
                return;
            }

            for (int i = 0; i < tasksProperty.arraySize; i++)
            {
                SerializedProperty element = tasksProperty.GetArrayElementAtIndex(i);
                if (element != null && element.objectReferenceValue == startupTask)
                {
                    return;
                }
            }

            Undo.RecordObject(startupFlow, $"Add {startupTask.GetType().Name} To SceneStartupFlow");
            tasksProperty.InsertArrayElementAtIndex(tasksProperty.arraySize);
            tasksProperty.GetArrayElementAtIndex(tasksProperty.arraySize - 1).objectReferenceValue = startupTask;
            serializedObject.ApplyModifiedProperties();
            result.RepairedObjects.Add($"SceneStartupFlow.explicitTasks += {startupTask.GetType().Name}");
        }

        private static void ConfigureWindowLockStartupTask(WindowLockRandomizerStartupTask startupTask, SetupResult result)
        {
            if (startupTask == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(startupTask);
            serializedObject.Update();
            bool changed = false;

            changed |= SetBoolProperty(serializedObject, startupTask, "useDeterministicSeed", false, "WindowLockRandomizerStartupTask.useDeterministicSeed", result);

            if (changed)
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        private static void ConfigureVictimPlacementStartupTask(
            Scene scene,
            VictimPlacementStartupTask startupTask,
            SetupResult result)
        {
            if (startupTask == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(startupTask);
            serializedObject.Update();
            bool changed = false;

            changed |= SetBoolProperty(serializedObject, startupTask, "deterministicPlacement", true, "VictimPlacementStartupTask.deterministicPlacement", result);
            changed |= SetBoolProperty(serializedObject, startupTask, "refreshMissionObjectivesAfterSpawn", true, "VictimPlacementStartupTask.refreshMissionObjectivesAfterSpawn", result);

            GameObject victimPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Features/Incident/Prefabs/Bot/Joe.prefab");
            changed |= SetObjectReferenceProperty(
                serializedObject,
                startupTask,
                "victimPrefab",
                victimPrefab,
                "VictimPlacementStartupTask.victimPrefab",
                result);

            IncidentMissionSystem missionSystem = FindFirstInScene<IncidentMissionSystem>(scene);
            changed |= SetObjectReferenceProperty(
                serializedObject,
                startupTask,
                "incidentMissionSystem",
                missionSystem,
                "VictimPlacementStartupTask.incidentMissionSystem",
                result);

            if (changed)
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        private static void ConfigureStartupFlow(SceneStartupFlow startupFlow, SetupResult result)
        {
            if (startupFlow == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(startupFlow);
            serializedObject.Update();
            bool changed = false;

            if (changed)
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        private static void ConfigureMapSetupRoot(IncidentMapSetupRoot mapSetupRoot, SetupResult result)
        {
        }

        private static void EnsureSimulationEffects(
            GameObject simulationRoot,
            FireSimulationManager simulationManager,
            SetupResult result)
        {
            if (simulationRoot == null || simulationManager == null)
            {
                return;
            }

            FireEffectManager effectManager = EnsureComponent<FireEffectManager>(
                simulationRoot,
                "FireEffectManager",
                result);

            SerializedObject managerSerialized = new SerializedObject(simulationManager);
            managerSerialized.Update();
            bool managerChanged = SetObjectReferenceProperty(
                managerSerialized,
                simulationManager,
                "effectManager",
                effectManager,
                "FireSimulationManager.effectManager",
                result);

            if (managerChanged)
            {
                managerSerialized.ApplyModifiedProperties();
            }
        }

        private static void AssignMapSetupBindings(
            IncidentMapSetupRoot mapSetupRoot,
            IncidentFireSpawnProfile fireSpawnProfile,
            FireSimulationManager simulationManager,
            SetupResult result)
        {
            if (mapSetupRoot == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(mapSetupRoot);
            serializedObject.Update();

            bool changed = false;
            SerializedProperty fireSpawnProfileProperty = serializedObject.FindProperty("fireSpawnProfile");
            if (fireSpawnProfileProperty != null && fireSpawnProfileProperty.objectReferenceValue != fireSpawnProfile)
            {
                Undo.RecordObject(mapSetupRoot, "Assign IncidentMapSetupRoot Fire Spawn Profile");
                fireSpawnProfileProperty.objectReferenceValue = fireSpawnProfile;
                result.RepairedObjects.Add("IncidentMapSetupRoot.fireSpawnProfile");
                changed = true;
            }

            SerializedProperty fireSimulationManagerProperty = serializedObject.FindProperty("fireSimulationManager");
            if (fireSimulationManagerProperty != null && fireSimulationManagerProperty.objectReferenceValue != simulationManager)
            {
                Undo.RecordObject(mapSetupRoot, "Assign IncidentMapSetupRoot Fire Simulation Manager");
                fireSimulationManagerProperty.objectReferenceValue = simulationManager;
                result.RepairedObjects.Add("IncidentMapSetupRoot.fireSimulationManager");
                changed = true;
            }

            if (changed)
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void AssignManagerDependencies(
            Scene scene,
            FireSurfaceGraph surfaceGraph,
            FireSimulationManager simulationManager,
            SetupResult result)
        {
            if (simulationManager == null)
            {
                return;
            }

            FireSimulationProfile simulationProfile = ResolveOrCreateSceneProfile<FireSimulationProfile>(scene, "FireSimulationProfile", result);
            SerializedObject serializedObject = new SerializedObject(simulationManager);
            serializedObject.Update();

            bool changed = false;
            SerializedProperty graphProperty = serializedObject.FindProperty("surfaceGraph");
            if (graphProperty != null && graphProperty.objectReferenceValue != surfaceGraph)
            {
                Undo.RecordObject(simulationManager, "Assign FireSimulationManager Surface Graph");
                graphProperty.objectReferenceValue = surfaceGraph;
                result.RepairedObjects.Add("FireSimulationManager.surfaceGraph");
                changed = true;
            }

            SerializedProperty profileProperty = serializedObject.FindProperty("simulationProfile");
            if (profileProperty != null && profileProperty.objectReferenceValue != simulationProfile)
            {
                Undo.RecordObject(simulationManager, "Assign FireSimulationManager Simulation Profile");
                profileProperty.objectReferenceValue = simulationProfile;
                result.RepairedObjects.Add("FireSimulationManager.simulationProfile");
                changed = true;
            }

            if (changed)
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        private T ResolveOrCreateSceneProfile<T>(Scene scene, string suffix, SetupResult result) where T : ScriptableObject
        {
            if (!assignSceneMatchedProfiles)
            {
                return null;
            }

            string sceneName = string.IsNullOrWhiteSpace(scene.name) ? "Scene" : scene.name;
            string[] assetGuids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            for (int i = 0; i < assetGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
                if (assetPath.IndexOf(sceneName, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                T existingAsset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (existingAsset != null)
                {
                    result.ReusedObjects.Add($"{typeof(T).Name}: {assetPath}");
                    return existingAsset;
                }
            }

            if (!createProfilesIfMissing)
            {
                return null;
            }

            string folderPath = $"Assets/Game/Features/Incident/Missions/{sceneName}";
            EnsureFolder(folderPath);
            string assetPathToCreate = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{sceneName}_{suffix}.asset");
            T createdAsset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(createdAsset, assetPathToCreate);
            AssetDatabase.SaveAssets();
            result.CreatedAssetPaths.Add(assetPathToCreate);
            return createdAsset;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string currentPath = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string nextPath = $"{currentPath}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[i]);
                }

                currentPath = nextPath;
            }
        }

        private static T EnsureSetupTask<T>(GameObject parent, string objectName, SetupResult result) where T : IncidentMapSetupTask
        {
            GameObject taskRoot = EnsureChildObject(parent, objectName, result);
            return EnsureComponent<T>(taskRoot, typeof(T).Name, result);
        }

        private static void AddMapSetupTask(IncidentMapSetupRoot mapSetupRoot, IncidentMapSetupTask setupTask, SetupResult result)
        {
            if (mapSetupRoot == null || setupTask == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(mapSetupRoot);
            serializedObject.Update();
            SerializedProperty tasksProperty = serializedObject.FindProperty("explicitTasks");
            if (tasksProperty == null)
            {
                return;
            }

            for (int i = 0; i < tasksProperty.arraySize; i++)
            {
                SerializedProperty element = tasksProperty.GetArrayElementAtIndex(i);
                if (element != null && element.objectReferenceValue == setupTask)
                {
                    return;
                }
            }

            Undo.RecordObject(mapSetupRoot, $"Add {setupTask.GetType().Name} To IncidentMapSetupRoot");
            tasksProperty.InsertArrayElementAtIndex(tasksProperty.arraySize);
            tasksProperty.GetArrayElementAtIndex(tasksProperty.arraySize - 1).objectReferenceValue = setupTask;
            serializedObject.ApplyModifiedProperties();
            result.RepairedObjects.Add($"IncidentMapSetupRoot.explicitTasks += {setupTask.GetType().Name}");
        }

        private static GameObject EnsureRootObject(Scene scene, string objectName, SetupResult result)
        {
            GameObject existing = FindRootObject(scene, objectName);
            if (existing != null)
            {
                result.ReusedObjects.Add(objectName);
                return existing;
            }

            GameObject root = new GameObject(objectName);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, $"Add {objectName}");
            result.CreatedObjects.Add(root);
            result.AddedObjects.Add(objectName);
            return root;
        }

        private static GameObject ResolveIncidentRoot(Scene scene, SetupResult result)
        {
            SceneStartupFlow startupFlow = FindFirstInScene<SceneStartupFlow>(scene);
            if (startupFlow != null)
            {
                result.ReusedObjects.Add($"Incident root: {startupFlow.gameObject.name}");
                return startupFlow.gameObject;
            }

            IncidentMapSetupRoot mapSetupRoot = FindFirstInScene<IncidentMapSetupRoot>(scene);
            if (mapSetupRoot != null)
            {
                result.ReusedObjects.Add($"Incident root: {mapSetupRoot.gameObject.name}");
                return mapSetupRoot.gameObject;
            }

            return EnsureRootObject(scene, DefaultRootName, result);
        }

        private static GameObject ResolveSimulationRoot(Scene scene, SetupResult result)
        {
            FireSimulationManager simulationManager = FindFirstInScene<FireSimulationManager>(scene);
            if (simulationManager != null)
            {
                result.ReusedObjects.Add($"Simulation root: {simulationManager.gameObject.name}");
                return simulationManager.gameObject;
            }

            FireSurfaceGraph surfaceGraph = FindFirstInScene<FireSurfaceGraph>(scene);
            if (surfaceGraph != null)
            {
                result.ReusedObjects.Add($"Simulation root: {surfaceGraph.gameObject.name}");
                return surfaceGraph.gameObject;
            }

            string objectName = GetSimulationRootName(scene);
            return EnsureRootObject(scene, objectName, result);
        }

        private static GameObject ResolveDebugSpawnerRoot(Scene scene, SetupResult result)
        {
            DebugIncidentPayloadSpawner existingSpawner = FindFirstInScene<DebugIncidentPayloadSpawner>(scene);
            if (existingSpawner != null)
            {
                result.ReusedObjects.Add($"Debug spawner: {existingSpawner.gameObject.name}");
                return existingSpawner.gameObject;
            }

            return EnsureRootObject(scene, DefaultDebugSpawnerName, result);
        }

        private static string GetSimulationRootName(Scene scene)
        {
            string sceneName = string.IsNullOrWhiteSpace(scene.name) ? "Scene" : scene.name.Trim();
            return $"{SimulationRootPrefix}{sceneName}";
        }

        private static GameObject EnsureChildObject(GameObject parent, string objectName, SetupResult result)
        {
            if (parent == null)
            {
                return null;
            }

            Transform child = parent.transform.Find(objectName);
            if (child != null)
            {
                result.ReusedObjects.Add($"{parent.name}/{objectName}");
                return child.gameObject;
            }

            GameObject childObject = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(childObject, $"Add {objectName}");
            childObject.transform.SetParent(parent.transform, false);
            result.CreatedObjects.Add(childObject);
            result.AddedObjects.Add($"{parent.name}/{objectName}");
            return childObject;
        }

        private static T EnsureComponent<T>(GameObject gameObject, string label, SetupResult result) where T : Component
        {
            if (gameObject == null)
            {
                return null;
            }

            T component = gameObject.GetComponent<T>();
            if (component != null)
            {
                result.ReusedObjects.Add(label);
                return component;
            }

            component = Undo.AddComponent<T>(gameObject);
            result.RepairedObjects.Add(label);
            return component;
        }

        private static bool SetBoolProperty(
            SerializedObject serializedObject,
            UnityEngine.Object targetObject,
            string propertyName,
            bool value,
            string resultLabel,
            SetupResult result)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.boolValue == value)
            {
                return false;
            }

            Undo.RecordObject(targetObject, $"Set {resultLabel}");
            property.boolValue = value;
            result.RepairedObjects.Add(resultLabel);
            return true;
        }

        private static bool SetIntProperty(
            SerializedObject serializedObject,
            UnityEngine.Object targetObject,
            string propertyName,
            int value,
            string resultLabel,
            SetupResult result)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.intValue == value)
            {
                return false;
            }

            Undo.RecordObject(targetObject, $"Set {resultLabel}");
            property.intValue = value;
            result.RepairedObjects.Add(resultLabel);
            return true;
        }

        private static bool SetEnumProperty(
            SerializedObject serializedObject,
            UnityEngine.Object targetObject,
            string propertyName,
            int enumValueIndex,
            string resultLabel,
            SetupResult result)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.enumValueIndex == enumValueIndex)
            {
                return false;
            }

            Undo.RecordObject(targetObject, $"Set {resultLabel}");
            property.enumValueIndex = enumValueIndex;
            result.RepairedObjects.Add(resultLabel);
            return true;
        }

        private static bool SetObjectReferenceProperty(
            SerializedObject serializedObject,
            UnityEngine.Object targetObject,
            string propertyName,
            UnityEngine.Object value,
            string resultLabel,
            SetupResult result)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == value)
            {
                return false;
            }

            Undo.RecordObject(targetObject, $"Set {resultLabel}");
            property.objectReferenceValue = value;
            result.RepairedObjects.Add(resultLabel);
            return true;
        }

        private static GameObject FindRootObject(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == objectName)
                {
                    return roots[i];
                }
            }

            return null;
        }

        private static T FindFirstInScene<T>(Scene scene) where T : Component
        {
            T[] all = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < all.Length; i++)
            {
                T candidate = all[i];
                if (candidate != null &&
                    !EditorUtility.IsPersistent(candidate) &&
                    candidate.gameObject.scene == scene)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static T[] FindAllInScene<T>(Scene scene) where T : Component
        {
            T[] all = Resources.FindObjectsOfTypeAll<T>();
            List<T> results = new List<T>();
            for (int i = 0; i < all.Length; i++)
            {
                T candidate = all[i];
                if (candidate != null &&
                    !EditorUtility.IsPersistent(candidate) &&
                    candidate.gameObject.scene == scene)
                {
                    results.Add(candidate);
                }
            }

            return results.ToArray();
        }

        private static string SanitizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            return trimmed.Replace(" ", string.Empty);
        }

        private static void ShowSetupResult(Scene scene, SetupResult result)
        {
            string message =
                $"Scene: {scene.name}\n\n" +
                $"Added:\n{FormatList(result.AddedObjects)}\n\n" +
                $"Reused:\n{FormatList(result.ReusedObjects)}\n\n" +
                $"Repaired:\n{FormatList(result.RepairedObjects)}\n\n" +
                $"Created Assets:\n{FormatList(result.CreatedAssetPaths)}\n\n" +
                $"Warnings:\n{FormatList(result.Warnings)}";

            EditorUtility.DisplayDialog("Incident Scene Setup", message, "OK");
        }

        private static string FormatList(List<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return "- None";
            }

            return "- " + string.Join("\n- ", values);
        }

        private sealed class SetupResult
        {
            public readonly List<GameObject> CreatedObjects = new List<GameObject>();
            public readonly List<string> AddedObjects = new List<string>();
            public readonly List<string> ReusedObjects = new List<string>();
            public readonly List<string> RepairedObjects = new List<string>();
            public readonly List<string> CreatedAssetPaths = new List<string>();
            public readonly List<string> Warnings = new List<string>();

            public bool HasChanges =>
                CreatedObjects.Count > 0 ||
                RepairedObjects.Count > 0 ||
                CreatedAssetPaths.Count > 0;
        }
    }

}
