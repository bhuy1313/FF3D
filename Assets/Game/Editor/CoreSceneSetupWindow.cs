using System;
using System.Collections.Generic;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace FF3D.Editor
{
    public sealed class CoreSceneSetupWindow : EditorWindow
    {
        private const string CameraPrefabPath = "Assets/Game/Features/Incident/Prefabs/Complete Character Setup/Camera.prefab";
        private const string CanvasPrefabPath = "Assets/Game/Features/Incident/Prefabs/Complete Character Setup/Canvas.prefab";
        private const string DirectionalLightPrefabPath = "Assets/Game/Features/Incident/Prefabs/Complete Character Setup/Directional Light.prefab";
        private const string EventSystemPrefabPath = "Assets/Game/Features/Incident/Prefabs/Complete Character Setup/EventSystem.prefab";
        private const string GameMasterPrefabPath = "Assets/Game/Features/Incident/Prefabs/Complete Character Setup/GameMaster.prefab";
        private const string PlayerCapsulePrefabPath = "Assets/Game/Features/Incident/Prefabs/Complete Character Setup/PlayerCapsule.prefab";

        [SerializeField] private bool includeCamera = true;
        [SerializeField] private bool includeCanvas = true;
        [SerializeField] private bool includeDirectionalLight = true;
        [SerializeField] private bool includeEventSystem = true;
        [SerializeField] private bool includeGameMaster = true;
        [SerializeField] private bool includePlayerCapsule = true;

        [MenuItem("Tools/TrueJourney/Scenes/Core Setup")]
        public static void OpenWindow()
        {
            CoreSceneSetupWindow window = GetWindow<CoreSceneSetupWindow>("Core Scene Setup");
            window.minSize = new Vector2(420f, 280f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Core Scene Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Adds the core scene objects used by the incident gameplay shell without touching mission-specific setup. " +
                "Existing equivalents are reused instead of duplicated.",
                MessageType.Info);

            DrawToggleList();

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Missing To Current Scene", GUILayout.Height(32f)))
                {
                    SetupCurrentScene();
                }

                if (GUILayout.Button("New Empty Scene + Setup", GUILayout.Height(32f)))
                {
                    CreateNewSceneAndSetup();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "If a Directional Light or EventSystem prefab is missing, the tool falls back to a basic built-in object. " +
                "GameMaster.canvas is auto-assigned when both objects exist.",
                MessageType.None);
        }

        private void DrawToggleList()
        {
            EditorGUILayout.LabelField("Objects", EditorStyles.boldLabel);
            includeCamera = EditorGUILayout.ToggleLeft("Camera", includeCamera);
            includeCanvas = EditorGUILayout.ToggleLeft("Canvas", includeCanvas);
            includeDirectionalLight = EditorGUILayout.ToggleLeft("Directional Light", includeDirectionalLight);
            includeEventSystem = EditorGUILayout.ToggleLeft("EventSystem", includeEventSystem);
            includeGameMaster = EditorGUILayout.ToggleLeft("GameMaster", includeGameMaster);
            includePlayerCapsule = EditorGUILayout.ToggleLeft("PlayerCapsule", includePlayerCapsule);
        }

        private void SetupCurrentScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Core Scene Setup", "No active scene is loaded.", "OK");
                return;
            }

            RunSetup(scene);
        }

        private void CreateNewSceneAndSetup()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RunSetup(scene);
        }

        private void RunSetup(Scene scene)
        {
            SetupResult result = new SetupResult();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Core Scene Objects");

            try
            {
                GameObject camera = includeCamera
                    ? EnsurePrefabObject(scene, CameraPrefabPath, "Camera", result, FindCameraObject)
                    : FindCameraObject(scene);

                GameObject canvas = includeCanvas
                    ? EnsurePrefabObject(scene, CanvasPrefabPath, "Canvas", result, FindCanvasObject)
                    : FindCanvasObject(scene);

                GameObject directionalLight = includeDirectionalLight
                    ? EnsureObject(
                        scene,
                        DirectionalLightPrefabPath,
                        "Directional Light",
                        result,
                        FindDirectionalLightObject,
                        CreateFallbackDirectionalLight)
                    : FindDirectionalLightObject(scene);

                GameObject eventSystem = includeEventSystem
                    ? EnsureObject(
                        scene,
                        EventSystemPrefabPath,
                        "EventSystem",
                        result,
                        FindEventSystemObject,
                        CreateFallbackEventSystem)
                    : FindEventSystemObject(scene);

                GameObject gameMaster = includeGameMaster
                    ? EnsurePrefabObject(scene, GameMasterPrefabPath, "GameMaster", result, FindGameMasterObject)
                    : FindGameMasterObject(scene);

                GameObject playerCapsule = includePlayerCapsule
                    ? EnsurePrefabObject(scene, PlayerCapsulePrefabPath, "PlayerCapsule", result, FindPlayerCapsuleObject)
                    : FindPlayerCapsuleObject(scene);

                bool repairedGameMasterCanvas = TryAssignGameMasterCanvas(gameMaster, canvas);
                if (repairedGameMasterCanvas)
                {
                    result.RepairedObjects.Add("GameMaster.canvas");
                }

                bool repairedMissionDefaults = TryResetGameMasterMissionDefaults(
                    gameMaster,
                    result,
                    result.CreatedObjects.Contains(gameMaster));

                if (result.HasChanges || repairedGameMasterCanvas || repairedMissionDefaults)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }

                ApplySelection(camera, canvas, directionalLight, eventSystem, gameMaster, playerCapsule, result);
                ShowResultDialog(scene, result);
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private static GameObject EnsurePrefabObject(
            Scene scene,
            string assetPath,
            string label,
            SetupResult result,
            Func<Scene, GameObject> findExistingObject)
        {
            return EnsureObject(scene, assetPath, label, result, findExistingObject, null);
        }

        private static GameObject EnsureObject(
            Scene scene,
            string assetPath,
            string label,
            SetupResult result,
            Func<Scene, GameObject> findExistingObject,
            Func<Scene, GameObject> createFallbackObject)
        {
            GameObject existingObject = findExistingObject(scene);
            if (existingObject != null)
            {
                result.ReusedObjects.Add(label);
                return existingObject;
            }

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefabAsset != null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset, scene) as GameObject;
                if (instance != null)
                {
                    Undo.RegisterCreatedObjectUndo(instance, $"Add {label}");
                    result.CreatedObjects.Add(instance);
                    result.AddedObjects.Add(label);
                    return instance;
                }
            }

            if (createFallbackObject != null)
            {
                GameObject fallbackObject = createFallbackObject(scene);
                if (fallbackObject != null)
                {
                    result.CreatedObjects.Add(fallbackObject);
                    result.AddedObjects.Add($"{label} (fallback)");
                    return fallbackObject;
                }
            }

            result.MissingAssets.Add($"{label}: {assetPath}");
            return null;
        }

        private static GameObject FindCameraObject(Scene scene)
        {
            Camera[] cameras = FindSceneObjects<Camera>(scene);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera != null && camera.gameObject.scene == scene)
                {
                    return camera.gameObject;
                }
            }

            return null;
        }

        private static GameObject FindCanvasObject(Scene scene)
        {
            Canvas[] canvases = FindSceneObjects<Canvas>(scene);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas != null && canvas.gameObject.scene == scene)
                {
                    return canvas.gameObject;
                }
            }

            return null;
        }

        private static GameObject FindDirectionalLightObject(Scene scene)
        {
            Light[] lights = FindSceneObjects<Light>(scene);
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light != null && light.type == LightType.Directional && light.gameObject.scene == scene)
                {
                    return light.gameObject;
                }
            }

            return null;
        }

        private static GameObject FindEventSystemObject(Scene scene)
        {
            EventSystem[] eventSystems = FindSceneObjects<EventSystem>(scene);
            for (int i = 0; i < eventSystems.Length; i++)
            {
                EventSystem eventSystem = eventSystems[i];
                if (eventSystem != null && eventSystem.gameObject.scene == scene)
                {
                    return eventSystem.gameObject;
                }
            }

            return null;
        }

        private static GameObject FindGameMasterObject(Scene scene)
        {
            GameMaster[] gameMasters = FindSceneObjects<GameMaster>(scene);
            for (int i = 0; i < gameMasters.Length; i++)
            {
                GameMaster gameMaster = gameMasters[i];
                if (gameMaster != null && gameMaster.gameObject.scene == scene)
                {
                    return gameMaster.gameObject;
                }
            }

            return null;
        }

        private static GameObject FindPlayerCapsuleObject(Scene scene)
        {
            FirstPersonController[] players = FindSceneObjects<FirstPersonController>(scene);
            for (int i = 0; i < players.Length; i++)
            {
                FirstPersonController player = players[i];
                if (player != null && player.gameObject.scene == scene)
                {
                    return player.gameObject;
                }
            }

            return null;
        }

        private static T[] FindSceneObjects<T>(Scene scene) where T : UnityEngine.Object
        {
            T[] sceneObjects = Resources.FindObjectsOfTypeAll<T>();
            List<T> filteredObjects = new List<T>(sceneObjects.Length);
            for (int i = 0; i < sceneObjects.Length; i++)
            {
                T sceneObject = sceneObjects[i];
                if (sceneObject == null)
                {
                    continue;
                }

                if (EditorUtility.IsPersistent(sceneObject))
                {
                    continue;
                }

                if (sceneObject is Component component && component.gameObject.scene == scene)
                {
                    filteredObjects.Add(sceneObject);
                }
            }

            return filteredObjects.ToArray();
        }

        private static GameObject CreateFallbackDirectionalLight(Scene scene)
        {
            GameObject lightObject = new GameObject("Directional Light");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            Undo.RegisterCreatedObjectUndo(lightObject, "Add Directional Light");

            Light light = Undo.AddComponent<Light>(lightObject);
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;

            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            return lightObject;
        }

        private static GameObject CreateFallbackEventSystem(Scene scene)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
            Undo.RegisterCreatedObjectUndo(eventSystemObject, "Add EventSystem");

            Undo.AddComponent<EventSystem>(eventSystemObject);

            Type inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModuleType != null)
            {
                Undo.AddComponent(eventSystemObject, inputSystemModuleType);
            }
            else
            {
                Undo.AddComponent<StandaloneInputModule>(eventSystemObject);
            }

            return eventSystemObject;
        }

        private static bool TryAssignGameMasterCanvas(GameObject gameMasterObject, GameObject canvasObject)
        {
            if (gameMasterObject == null || canvasObject == null)
            {
                return false;
            }

            GameMaster gameMaster = gameMasterObject.GetComponent<GameMaster>();
            if (gameMaster == null)
            {
                return false;
            }

            SerializedObject serializedObject = new SerializedObject(gameMaster);
            serializedObject.Update();
            SerializedProperty canvasProperty = serializedObject.FindProperty("canvas");
            if (canvasProperty == null || canvasProperty.objectReferenceValue != null)
            {
                return false;
            }

            Undo.RecordObject(gameMaster, "Assign GameMaster Canvas");
            canvasProperty.objectReferenceValue = canvasObject;
            return serializedObject.ApplyModifiedProperties();
        }

        private static bool TryResetGameMasterMissionDefaults(
            GameObject gameMasterObject,
            SetupResult result,
            bool onlyForFreshlyCreatedGameMaster)
        {
            if (gameMasterObject == null || !onlyForFreshlyCreatedGameMaster)
            {
                return false;
            }

            IncidentMissionSystem missionSystem = gameMasterObject.GetComponent<IncidentMissionSystem>();
            if (missionSystem == null)
            {
                return false;
            }

            SerializedObject serializedObject = new SerializedObject(missionSystem);
            serializedObject.Update();
            SerializedProperty missionDefinitionProperty = serializedObject.FindProperty("missionDefinition");
            SerializedProperty autoStartProperty = serializedObject.FindProperty("autoStartOnEnable");
            bool shouldApply = false;

            Undo.RecordObject(missionSystem, "Reset GameMaster Mission Defaults");

            if (missionDefinitionProperty != null && missionDefinitionProperty.objectReferenceValue != null)
            {
                missionDefinitionProperty.objectReferenceValue = null;
                result.RepairedObjects.Add("IncidentMissionSystem.missionDefinition");
                shouldApply = true;
            }

            if (autoStartProperty != null && autoStartProperty.boolValue)
            {
                autoStartProperty.boolValue = false;
                result.RepairedObjects.Add("IncidentMissionSystem.autoStartOnEnable");
                shouldApply = true;
            }

            if (!shouldApply)
            {
                return false;
            }

            return serializedObject.ApplyModifiedProperties();
        }

        private static void ApplySelection(
            GameObject camera,
            GameObject canvas,
            GameObject directionalLight,
            GameObject eventSystem,
            GameObject gameMaster,
            GameObject playerCapsule,
            SetupResult result)
        {
            if (result.CreatedObjects.Count > 0)
            {
                Selection.objects = result.CreatedObjects.ToArray();
                return;
            }

            List<UnityEngine.Object> selection = new List<UnityEngine.Object>(6);
            TryAddSelection(selection, camera);
            TryAddSelection(selection, canvas);
            TryAddSelection(selection, directionalLight);
            TryAddSelection(selection, eventSystem);
            TryAddSelection(selection, gameMaster);
            TryAddSelection(selection, playerCapsule);

            Selection.objects = selection.ToArray();
        }

        private static void TryAddSelection(List<UnityEngine.Object> selection, GameObject target)
        {
            if (target != null)
            {
                selection.Add(target);
            }
        }

        private static void ShowResultDialog(Scene scene, SetupResult result)
        {
            string message =
                $"Scene: {scene.name}\n\n" +
                $"Added:\n{FormatList(result.AddedObjects)}\n\n" +
                $"Reused:\n{FormatList(result.ReusedObjects)}\n\n" +
                $"Repaired:\n{FormatList(result.RepairedObjects)}";

            if (result.MissingAssets.Count > 0)
            {
                message += $"\n\nMissing assets:\n{FormatList(result.MissingAssets)}";
            }

            EditorUtility.DisplayDialog("Core Scene Setup", message, "OK");
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
            public readonly List<string> MissingAssets = new List<string>();

            public bool HasChanges => CreatedObjects.Count > 0 || RepairedObjects.Count > 0;
        }
    }
}
