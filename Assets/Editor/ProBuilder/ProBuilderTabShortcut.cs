using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ProBuilder;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.ProBuilder;

public static class ProBuilderTabShortcut
{
    private const string PositionToolContextTypeName = "UnityEditor.ProBuilder.PositionToolContext, Unity.ProBuilder.Editor";
    private const string GameObjectToolContextTypeName = "UnityEditor.GameObjectToolContext, UnityEditor";

    [Shortcut("TrueJourney/ProBuilder/Toggle Edit Mode", typeof(SceneView), KeyCode.Tab)]
    private static void ToggleEditMode()
    {
        if (!TryGetSelectedProBuilderMesh(out _))
        {
            return;
        }

        Type positionToolContextType = Type.GetType(PositionToolContextTypeName);
        Type gameObjectToolContextType = Type.GetType(GameObjectToolContextTypeName);
        bool isInProBuilderContext = positionToolContextType != null && ToolManager.activeContextType == positionToolContextType;

        bool success = isInProBuilderContext
            ? TrySetActiveContext(gameObjectToolContextType)
            : TrySetActiveContext(positionToolContextType);

        if (!success)
        {
            return;
        }

        if (!isInProBuilderContext && ProBuilderEditor.selectMode == SelectMode.None)
        {
            ProBuilderEditor.selectMode = SelectMode.Face;
        }

        SceneView.RepaintAll();
    }

    private static bool TryGetSelectedProBuilderMesh(out ProBuilderMesh mesh)
    {
        mesh = null;
        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject == null)
        {
            return false;
        }

        mesh = selectedObject.GetComponent<ProBuilderMesh>() ?? selectedObject.GetComponentInParent<ProBuilderMesh>();
        return mesh != null;
    }

    private static bool TrySetActiveContext(Type contextType)
    {
        if (contextType == null)
        {
            return false;
        }

        MethodInfo setActiveContextMethod = typeof(ToolManager)
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(method => method.Name == "SetActiveContext" &&
                                      method.IsGenericMethodDefinition &&
                                      method.GetParameters().Length == 0);
        if (setActiveContextMethod == null)
        {
            return false;
        }

        try
        {
            setActiveContextMethod.MakeGenericMethod(contextType).Invoke(null, null);
            return ToolManager.activeContextType == contextType;
        }
        catch
        {
            return false;
        }
    }
}
