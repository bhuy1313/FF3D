using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrueJourney.BotBehavior;
using Unity.Behavior;
using UnityEditor;
using UnityEngine;

namespace TrueJourney.BotBehavior.Editor
{
    internal static class BotBehaviorGraphBootstrapper
    {
        private const string GraphAssetPath = "Assets/TrueJourney/Behavior/Bot Basic Behavior.asset";
        private const string AuthoringGraphTypeName = "Unity.Behavior.BehaviorAuthoringGraph";
        private const string StartRuntimeTypeName = "Unity.Behavior.Start";
        private const string SelectorCompositeRuntimeTypeName = "Unity.Behavior.SelectorComposite";
        private const string GraphAssetTypeName = "Unity.Behavior.GraphFramework.GraphAsset";
        private const string BlackboardAssetTypeName = "Unity.Behavior.GraphFramework.BlackboardAsset";
        private const string NodeModelTypeName = "Unity.Behavior.GraphFramework.NodeModel";
        private const string VariableModelTypeName = "Unity.Behavior.GraphFramework.VariableModel";
        private const string SerializableGuidTypeName = "Unity.Behavior.GraphFramework.SerializableGUID";
        private const string SerializableTypeTypeName = "Unity.Behavior.GraphFramework.SerializableType";
        private const string PortModelTypeName = "Unity.Behavior.GraphFramework.PortModel";
        private const string NodeRegistryTypeName = "Unity.Behavior.NodeRegistry";
        private const string NodeInfoTypeName = "Unity.Behavior.NodeInfo";
        private const string GraphAssetProcessorTypeName = "Unity.Behavior.GraphAssetProcessor";

        [MenuItem("Tools/Bot Behavior/Create Basic Graph Asset")]
        private static void CreateBasicGraphAsset()
        {
            UnityEngine.Object authoringGraph = GetOrCreateAuthoringGraphAsset();
            BuildBasicGraph(authoringGraph);
            Selection.activeObject = authoringGraph;
        }

        [MenuItem("Tools/Bot Behavior/Setup Selected Bot")]
        private static void SetupSelectedBot()
        {
            if (Selection.gameObjects.Length == 0)
            {
                Debug.LogWarning("[BotBehaviorGraphBootstrapper] Select at least one bot GameObject first.");
                return;
            }

            UnityEngine.Object authoringGraph = GetOrCreateAuthoringGraphAsset();
            BuildBasicGraph(authoringGraph);
            BehaviorGraph runtimeGraph = GetRuntimeGraph(authoringGraph);
            if (runtimeGraph == null)
            {
                Debug.LogError("[BotBehaviorGraphBootstrapper] Failed to resolve the runtime BehaviorGraph asset.");
                return;
            }

            int configuredCount = 0;
            foreach (GameObject gameObject in Selection.gameObjects)
            {
                if (gameObject == null)
                {
                    continue;
                }

                BotCommandAgent commandAgent = gameObject.GetComponent<BotCommandAgent>();
                if (commandAgent == null)
                {
                    Debug.LogWarning($"[BotBehaviorGraphBootstrapper] Skipped '{gameObject.name}' because it has no BotCommandAgent.", gameObject);
                    continue;
                }

                BotBehaviorContext context = gameObject.GetComponent<BotBehaviorContext>();
                if (context == null)
                {
                    context = Undo.AddComponent<BotBehaviorContext>(gameObject);
                }

                BehaviorGraphAgent behaviorAgent = gameObject.GetComponent<BehaviorGraphAgent>();
                if (behaviorAgent == null)
                {
                    behaviorAgent = Undo.AddComponent<BehaviorGraphAgent>(gameObject);
                }

                Undo.RecordObject(context, "Configure Bot Behavior Context");
                context.SetUseMoveOrdersAsBehaviorInput(true);
                EditorUtility.SetDirty(context);

                Undo.RecordObject(behaviorAgent, "Assign Bot Behavior Graph");
                behaviorAgent.Graph = runtimeGraph;
                EditorUtility.SetDirty(behaviorAgent);
                configuredCount++;
            }

            if (configuredCount > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[BotBehaviorGraphBootstrapper] Configured {configuredCount} bot(s) with the basic behavior graph.");
            }
        }

        private static UnityEngine.Object GetOrCreateAuthoringGraphAsset()
        {
            EnsureFolder("Assets/TrueJourney");
            EnsureFolder("Assets/TrueJourney/Behavior");

            Type authoringGraphType = GetBehaviorType(AuthoringGraphTypeName);
            UnityEngine.Object authoringGraph = AssetDatabase.LoadAssetAtPath(GraphAssetPath, authoringGraphType);
            if (authoringGraph != null)
            {
                return authoringGraph;
            }

            ScriptableObject createdAsset = ScriptableObject.CreateInstance(authoringGraphType);
            createdAsset.name = "Bot Basic Behavior";
            AssetDatabase.CreateAsset(createdAsset, GraphAssetPath);
            AssetDatabase.SaveAssets();
            return createdAsset;
        }

        private static void BuildBasicGraph(UnityEngine.Object authoringGraph)
        {
            if (authoringGraph == null)
            {
                throw new ArgumentNullException(nameof(authoringGraph));
            }

            object blackboard = GetFieldValue(authoringGraph, "Blackboard");
            InvokeStatic(GetBehaviorType(GraphAssetProcessorTypeName), "EnsureBlackboardGraphOwnerVariable", blackboard);

            ClearBlackboardVariablesExceptSelf(blackboard);
            ClearGraphNodes(authoringGraph);

            object startNode = CreateNode(authoringGraph, GetBehaviorType(StartRuntimeTypeName), new Vector2(0f, 0f), null);
            object startOutput = GetDefaultPort(startNode, useOutputPort: true);
            object selectorNode = CreateNode(authoringGraph, GetBehaviorType(SelectorCompositeRuntimeTypeName), new Vector2(0f, 220f), startOutput);
            object selectorOutput = GetDefaultPort(selectorNode, useOutputPort: true);

            object moveNode = CreateNode(authoringGraph, typeof(BotExecuteMoveOrderAction), new Vector2(-320f, 460f), selectorOutput);
            object patrolNode = CreateNode(authoringGraph, typeof(BotPatrolRouteAction), new Vector2(0f, 460f), selectorOutput);
            object idleNode = CreateNode(authoringGraph, typeof(BotIdleLookAction), new Vector2(320f, 460f), selectorOutput);

            object selfVariable = GetSelfVariable(blackboard);
            LinkAgentField(moveNode, selfVariable);
            LinkAgentField(patrolNode, selfVariable);
            LinkAgentField(idleNode, selfVariable);

            Invoke(authoringGraph, "SetAssetDirty", true);
            Invoke(authoringGraph, "ValidateAsset");
            RebuildRuntimeGraph(authoringGraph);
            Invoke(authoringGraph, "SaveAsset");

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(GraphAssetPath, ImportAssetOptions.ForceUpdate);
        }

        private static void RebuildRuntimeGraph(UnityEngine.Object authoringGraph)
        {
            BehaviorGraph runtimeGraph = GetRuntimeGraph(authoringGraph);
            object processor = InvokeStatic(
                GetBehaviorType(GraphAssetProcessorTypeName),
                "CreateInstanceForRebuild",
                authoringGraph,
                runtimeGraph);

            Invoke(processor, "ProcessGraph");
        }

        private static BehaviorGraph GetRuntimeGraph(UnityEngine.Object authoringGraph)
        {
            return InvokeStatic(GetBehaviorType(AuthoringGraphTypeName), "GetOrCreateGraph", authoringGraph) as BehaviorGraph;
        }

        private static object CreateNode(UnityEngine.Object authoringGraph, Type runtimeNodeType, Vector2 position, object connectedPort)
        {
            object nodeInfo = InvokeStatic(GetBehaviorType(NodeRegistryTypeName), "GetInfo", runtimeNodeType);
            if (nodeInfo == null)
            {
                throw new InvalidOperationException($"Unable to resolve NodeInfo for runtime type '{runtimeNodeType.FullName}'.");
            }

            object modelSerializableType = GetFieldValue(nodeInfo, "ModelType");
            Type modelType = GetPropertyValue<Type>(modelSerializableType, "Type");
            if (modelType == null)
            {
                throw new InvalidOperationException($"Unable to resolve node model type for '{runtimeNodeType.FullName}'.");
            }

            object[] arguments = { nodeInfo };
            return Invoke(authoringGraph, "CreateNode", modelType, position, connectedPort, arguments);
        }

        private static void LinkAgentField(object nodeModel, object selfVariable)
        {
            Type variableModelType = GetBehaviorType(VariableModelTypeName);
            MethodInfo setFieldMethod = nodeModel.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .First(method =>
                {
                    if (method.Name != "SetField")
                    {
                        return false;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 3 && parameters[1].ParameterType == variableModelType;
                });

            setFieldMethod.Invoke(nodeModel, new[] { "Agent", selfVariable, typeof(GameObject) });
        }

        private static object GetDefaultPort(object nodeModel, bool useOutputPort)
        {
            MethodInfo method = nodeModel.GetType().GetMethod(
                useOutputPort ? "TryDefaultOutputPortModel" : "TryDefaultInputPortModel",
                BindingFlags.Instance | BindingFlags.Public);

            object[] parameters = { null };
            bool found = (bool)method.Invoke(nodeModel, parameters);
            return found ? parameters[0] : null;
        }

        private static void ClearGraphNodes(UnityEngine.Object authoringGraph)
        {
            Type nodeModelType = GetBehaviorType(NodeModelTypeName);
            Type listType = typeof(List<>).MakeGenericType(nodeModelType);
            object emptyList = Activator.CreateInstance(listType);
            SetPropertyValue(authoringGraph, "Nodes", emptyList);
        }

        private static void ClearBlackboardVariablesExceptSelf(object blackboard)
        {
            IList variables = GetPropertyValue<IList>(blackboard, "Variables");
            object selfId = CreateSelfGuid();

            for (int i = variables.Count - 1; i >= 0; i--)
            {
                object variable = variables[i];
                object variableId = GetFieldValue(variable, "ID");
                if (Equals(variableId, selfId))
                {
                    continue;
                }

                variables.RemoveAt(i);
            }
        }

        private static object GetSelfVariable(object blackboard)
        {
            IList variables = GetPropertyValue<IList>(blackboard, "Variables");
            object selfId = CreateSelfGuid();

            foreach (object variable in variables)
            {
                if (Equals(GetFieldValue(variable, "ID"), selfId))
                {
                    return variable;
                }
            }

            throw new InvalidOperationException("Graph self variable was not found on the blackboard.");
        }

        private static object CreateSelfGuid()
        {
            return Activator.CreateInstance(GetBehaviorType(SerializableGuidTypeName), 1UL, 0UL);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parentPath = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(parentPath))
            {
                EnsureFolder(parentPath);
            }

            string folderName = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private static Type GetBehaviorType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, throwOnError: false);
                if (type != null)
                {
                    return type;
                }
            }

            throw new InvalidOperationException($"Type '{fullName}' was not found in the loaded AppDomain assemblies.");
        }

        private static object GetFieldValue(object instance, string fieldName)
        {
            Type currentType = instance.GetType();
            while (currentType != null)
            {
                FieldInfo field = currentType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field.GetValue(instance);
                }

                currentType = currentType.BaseType;
            }

            throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        private static T GetPropertyValue<T>(object instance, string propertyName)
        {
            Type currentType = instance.GetType();
            while (currentType != null)
            {
                PropertyInfo property = currentType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                {
                    return (T)property.GetValue(instance);
                }

                currentType = currentType.BaseType;
            }

            throw new MissingMemberException(instance.GetType().FullName, propertyName);
        }

        private static void SetPropertyValue(object instance, string propertyName, object value)
        {
            Type currentType = instance.GetType();
            while (currentType != null)
            {
                PropertyInfo property = currentType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                {
                    property.SetValue(instance, value);
                    return;
                }

                currentType = currentType.BaseType;
            }

            throw new MissingMemberException(instance.GetType().FullName, propertyName);
        }

        private static object Invoke(object instance, string methodName, params object[] arguments)
        {
            MethodInfo method = FindMethod(instance.GetType(), methodName, arguments);
            if (method == null)
            {
                throw new MissingMethodException(instance.GetType().FullName, methodName);
            }

            return method.Invoke(instance, arguments);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            MethodInfo method = FindMethod(type, methodName, arguments);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, methodName);
            }

            return method.Invoke(null, arguments);
        }

        private static MethodInfo FindMethod(Type type, string methodName, object[] arguments)
        {
            Type currentType = type;
            while (currentType != null)
            {
                MethodInfo method = currentType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(candidate => candidate.Name == methodName && candidate.GetParameters().Length == arguments.Length);

                if (method != null)
                {
                    return method;
                }

                currentType = currentType.BaseType;
            }

            return null;
        }
    }
}
