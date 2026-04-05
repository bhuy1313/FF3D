using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class MissionEditorAssetCreationUtility
{
    public static void ShowCreateAssetMenu<TBase>(SerializedObject owner, string listPropertyName, string menuTitle) where TBase : ScriptableObject
    {
        if (owner == null || owner.targetObject == null)
        {
            return;
        }

        List<Type> types = GetConcreteDerivedTypes<TBase>();
        if (types.Count == 0)
        {
            Debug.LogWarning($"No asset types found for {typeof(TBase).Name}.", owner.targetObject);
            return;
        }

        GenericMenu menu = new GenericMenu();
        for (int i = 0; i < types.Count; i++)
        {
            Type type = types[i];
            menu.AddItem(new GUIContent(type.Name), false, () => CreateAndAppendAsset(owner, listPropertyName, type));
        }

        menu.ShowAsContext();
    }

    private static void CreateAndAppendAsset(SerializedObject owner, string listPropertyName, Type assetType)
    {
        string ownerPath = AssetDatabase.GetAssetPath(owner.targetObject);
        string directory = Path.GetDirectoryName(ownerPath);
        string combinedPath = Path.Combine(directory ?? "Assets", $"{owner.targetObject.name}_{assetType.Name}.asset").Replace('\\', '/');
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(combinedPath);

        ScriptableObject createdAsset = ScriptableObject.CreateInstance(assetType);
        createdAsset.name = Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(createdAsset, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        owner.Update();
        SerializedProperty property = owner.FindProperty(listPropertyName);
        if (property != null && property.isArray)
        {
            int index = property.arraySize;
            property.InsertArrayElementAtIndex(index);
            property.GetArrayElementAtIndex(index).objectReferenceValue = createdAsset;
            owner.ApplyModifiedProperties();
        }

        EditorGUIUtility.PingObject(createdAsset);
        Selection.activeObject = createdAsset;
    }

    private static List<Type> GetConcreteDerivedTypes<TBase>() where TBase : ScriptableObject
    {
        List<Type> results = new List<Type>();
        Type baseType = typeof(TBase);

        if (!baseType.IsAbstract)
        {
            results.Add(baseType);
        }

        foreach (Type type in TypeCache.GetTypesDerivedFrom(baseType))
        {
            if (type == null || type.IsAbstract || !baseType.IsAssignableFrom(type))
            {
                continue;
            }

            results.Add(type);
        }

        results.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
        return results;
    }
}
