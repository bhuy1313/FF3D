#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.EditorTools;

namespace FF3D.Editor
{
    [InitializeOnLoad]
    internal static class PlayModeToolStateGuard
    {
        private const string LightProbeGroupToolTypeName = "UnityEditor.LightProbeGroupTool";
        private const string MoveToolTypeName = "UnityEditor.MoveTool";
        private static bool isSwitchingTool;

        static PlayModeToolStateGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.delayCall += EnsureSafeToolState;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            EnsureSafeToolState();
        }

        private static void OnEditorUpdate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EnsureSafeToolState();
        }

        private static void OnSelectionChanged()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EnsureSafeToolState();
        }

        private static void EnsureSafeToolState()
        {
            if (isSwitchingTool || !HasStaleLightProbeToolState())
            {
                return;
            }

            isSwitchingTool = true;

            try
            {
                Selection.objects = Array.Empty<UnityEngine.Object>();
                TrySetActiveTool(MoveToolTypeName);
            }
            finally
            {
                isSwitchingTool = false;
            }
        }

        private static bool HasStaleLightProbeToolState()
        {
            UnityEngine.Object activeObject = Selection.activeObject;
            if (activeObject != null &&
                string.Equals(activeObject.GetType().Name, "LightProbeGroup", StringComparison.Ordinal))
            {
                return true;
            }

            PropertyInfo activeToolTypeProperty = typeof(ToolManager).GetProperty(
                "activeToolType",
                BindingFlags.Public | BindingFlags.Static);

            Type activeToolType = activeToolTypeProperty?.GetValue(null) as Type;
            return activeToolType != null &&
                   string.Equals(activeToolType.FullName, LightProbeGroupToolTypeName, StringComparison.Ordinal);
        }

        private static void TrySetActiveTool(string fullTypeName)
        {
            Type toolType = Type.GetType($"{fullTypeName}, UnityEditor");
            if (toolType == null)
            {
                return;
            }

            MethodInfo setActiveTool = typeof(ToolManager).GetMethod(
                "SetActiveTool",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Type) },
                null);

            if (setActiveTool != null)
            {
                setActiveTool.Invoke(null, new object[] { toolType });
                return;
            }

            MethodInfo restorePreviousPersistentTool = typeof(ToolManager).GetMethod(
                "RestorePreviousPersistentTool",
                BindingFlags.Public | BindingFlags.Static);

            restorePreviousPersistentTool?.Invoke(null, null);
        }
    }
}
#endif
