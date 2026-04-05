using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class MissionAuthoringAssetUtility
{
    public readonly struct ConversionSummary
    {
        public ConversionSummary(int convertedAssetCount, int rewrittenReferenceCount)
        {
            ConvertedAssetCount = convertedAssetCount;
            RewrittenReferenceCount = rewrittenReferenceCount;
        }

        public int ConvertedAssetCount { get; }

        public int RewrittenReferenceCount { get; }
    }

    public static MissionDefinition CreateMissionAssetInteractive()
    {
        string assetPath = EditorUtility.SaveFilePanelInProject(
            "Create Mission Definition",
            "NewMissionDefinition",
            "asset",
            "Choose where to create the mission asset.");

        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return null;
        }

        MissionDefinition mission = ScriptableObject.CreateInstance<MissionDefinition>();
        mission.name = Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(mission, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(mission);
        Selection.activeObject = mission;
        return mission;
    }

    public static ScriptableObject CreateMissionChildAsset(MissionDefinition ownerMission, Type assetType, string nameHint = null)
    {
        if (ownerMission == null || assetType == null || !typeof(ScriptableObject).IsAssignableFrom(assetType))
        {
            return null;
        }

        string ownerPath = AssetDatabase.GetAssetPath(ownerMission);
        if (string.IsNullOrWhiteSpace(ownerPath))
        {
            Debug.LogWarning("Mission must be saved as an asset before child assets can be created.", ownerMission);
            return null;
        }

        ScriptableObject childAsset = ScriptableObject.CreateInstance(assetType);
        childAsset.name = GenerateUniqueChildName(ownerMission, assetType, nameHint);

        Undo.RegisterCreatedObjectUndo(childAsset, $"Create {assetType.Name}");
        AssetDatabase.AddObjectToAsset(childAsset, ownerMission);
        EditorUtility.SetDirty(childAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.SetDirty(ownerMission);
        EditorGUIUtility.PingObject(childAsset);
        Selection.activeObject = childAsset;
        return childAsset;
    }

    public static void AppendReference(SerializedObject owner, string listPropertyName, UnityEngine.Object referencedObject)
    {
        if (owner == null || referencedObject == null)
        {
            return;
        }

        owner.Update();
        SerializedProperty property = owner.FindProperty(listPropertyName);
        if (property == null || !property.isArray)
        {
            return;
        }

        int index = property.arraySize;
        property.InsertArrayElementAtIndex(index);
        property.GetArrayElementAtIndex(index).objectReferenceValue = referencedObject;
        owner.ApplyModifiedProperties();
        EditorUtility.SetDirty(owner.targetObject);
    }

    public static bool RemoveReferenceAt(SerializedObject owner, string listPropertyName, int index)
    {
        if (owner == null)
        {
            return false;
        }

        owner.Update();
        SerializedProperty property = owner.FindProperty(listPropertyName);
        if (property == null || !property.isArray || index < 0 || index >= property.arraySize)
        {
            return false;
        }

        property.DeleteArrayElementAtIndex(index);
        if (index < property.arraySize && property.GetArrayElementAtIndex(index).propertyType == SerializedPropertyType.ObjectReference && property.GetArrayElementAtIndex(index).objectReferenceValue == null)
        {
            property.DeleteArrayElementAtIndex(index);
        }

        owner.ApplyModifiedProperties();
        EditorUtility.SetDirty(owner.targetObject);
        return true;
    }

    public static bool MoveReference(SerializedObject owner, string listPropertyName, int fromIndex, int toIndex)
    {
        if (owner == null)
        {
            return false;
        }

        owner.Update();
        SerializedProperty property = owner.FindProperty(listPropertyName);
        if (property == null || !property.isArray || fromIndex < 0 || fromIndex >= property.arraySize || toIndex < 0 || toIndex >= property.arraySize)
        {
            return false;
        }

        property.MoveArrayElement(fromIndex, toIndex);
        owner.ApplyModifiedProperties();
        EditorUtility.SetDirty(owner.targetObject);
        return true;
    }

    public static bool ReplaceReferenceAt(SerializedObject owner, string listPropertyName, int index, UnityEngine.Object referencedObject)
    {
        if (owner == null || referencedObject == null)
        {
            return false;
        }

        owner.Update();
        SerializedProperty property = owner.FindProperty(listPropertyName);
        if (property == null || !property.isArray || index < 0 || index >= property.arraySize)
        {
            return false;
        }

        property.GetArrayElementAtIndex(index).objectReferenceValue = referencedObject;
        owner.ApplyModifiedProperties();
        EditorUtility.SetDirty(owner.targetObject);
        return true;
    }

    public static ScriptableObject ConvertToOwnedSubAsset(MissionDefinition ownerMission, ScriptableObject sourceAsset)
    {
        if (ownerMission == null || sourceAsset == null || IsOwnedSubAsset(ownerMission, sourceAsset))
        {
            return sourceAsset;
        }

        string ownerPath = AssetDatabase.GetAssetPath(ownerMission);
        if (string.IsNullOrWhiteSpace(ownerPath))
        {
            return null;
        }

        Dictionary<ScriptableObject, ScriptableObject> convertedAssets = new Dictionary<ScriptableObject, ScriptableObject>();
        ScriptableObject convertedAsset = ConvertToOwnedSubAssetInternal(ownerMission, sourceAsset, convertedAssets);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.SetDirty(ownerMission);
        return convertedAsset;
    }

    public static ConversionSummary ConvertAllLinkedNodesToSubAssets(MissionDefinition ownerMission)
    {
        if (ownerMission == null)
        {
            return default;
        }

        string ownerPath = AssetDatabase.GetAssetPath(ownerMission);
        if (string.IsNullOrWhiteSpace(ownerPath))
        {
            return default;
        }

        Dictionary<ScriptableObject, ScriptableObject> convertedAssets = new Dictionary<ScriptableObject, ScriptableObject>();
        int rewrittenReferenceCount = RewriteNestedMissionNodeReferences(ownerMission, ownerMission, convertedAssets);

        UnityEngine.Object[] nestedAssets = AssetDatabase.LoadAllAssetsAtPath(ownerPath);
        for (int i = 0; i < nestedAssets.Length; i++)
        {
            if (nestedAssets[i] is not ScriptableObject nestedAsset || nestedAsset == ownerMission)
            {
                continue;
            }

            if (!IsOwnedSubAsset(ownerMission, nestedAsset))
            {
                continue;
            }

            rewrittenReferenceCount += RewriteNestedMissionNodeReferences(ownerMission, nestedAsset, convertedAssets);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.SetDirty(ownerMission);
        return new ConversionSummary(convertedAssets.Count, rewrittenReferenceCount);
    }

    public static bool DeleteOwnedSubAsset(MissionDefinition ownerMission, ScriptableObject candidate)
    {
        if (!IsOwnedSubAsset(ownerMission, candidate))
        {
            return false;
        }

        Undo.DestroyObjectImmediate(candidate);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.SetDirty(ownerMission);
        return true;
    }

    public static int CountMissionReferences(MissionDefinition ownerMission, UnityEngine.Object candidate)
    {
        if (ownerMission == null || candidate == null)
        {
            return 0;
        }

        string ownerPath = AssetDatabase.GetAssetPath(ownerMission);
        if (string.IsNullOrWhiteSpace(ownerPath))
        {
            return 0;
        }

        int referenceCount = 0;
        UnityEngine.Object[] nestedAssets = AssetDatabase.LoadAllAssetsAtPath(ownerPath);
        for (int i = 0; i < nestedAssets.Length; i++)
        {
            UnityEngine.Object asset = nestedAssets[i];
            if (asset == null)
            {
                continue;
            }

            SerializedObject serializedObject = new SerializedObject(asset);
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.Next(enterChildren))
            {
                enterChildren = true;
                if (iterator.propertyPath == "m_Script" || iterator.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                if (iterator.objectReferenceValue == candidate)
                {
                    referenceCount++;
                }
            }
        }

        return referenceCount;
    }

    public static List<TBase> GetOwnedSubAssets<TBase>(MissionDefinition ownerMission) where TBase : ScriptableObject
    {
        List<TBase> results = new List<TBase>();
        if (ownerMission == null)
        {
            return results;
        }

        string ownerPath = AssetDatabase.GetAssetPath(ownerMission);
        if (string.IsNullOrWhiteSpace(ownerPath))
        {
            return results;
        }

        UnityEngine.Object[] nestedAssets = AssetDatabase.LoadAllAssetsAtPath(ownerPath);
        for (int i = 0; i < nestedAssets.Length; i++)
        {
            if (nestedAssets[i] is not TBase asset || asset == ownerMission)
            {
                continue;
            }

            if (!IsOwnedSubAsset(ownerMission, asset))
            {
                continue;
            }

            results.Add(asset);
        }

        return results;
    }

    public static bool IsOwnedSubAsset(MissionDefinition ownerMission, ScriptableObject candidate)
    {
        if (ownerMission == null || candidate == null)
        {
            return false;
        }

        return AssetDatabase.IsSubAsset(candidate) &&
               string.Equals(AssetDatabase.GetAssetPath(candidate), AssetDatabase.GetAssetPath(ownerMission), StringComparison.OrdinalIgnoreCase);
    }

    public static List<Type> GetConcreteDerivedTypes<TBase>() where TBase : ScriptableObject
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

    private static string GenerateUniqueChildName(MissionDefinition ownerMission, Type assetType, string nameHint)
    {
        string prefix = string.IsNullOrWhiteSpace(nameHint)
            ? ownerMission.name
            : nameHint.Trim();
        string baseName = $"{prefix}_{assetType.Name}";
        string candidate = baseName;
        int suffix = 1;

        while (AssetNameExists(ownerMission, candidate))
        {
            suffix++;
            candidate = $"{baseName}_{suffix}";
        }

        return candidate;
    }

    private static bool AssetNameExists(MissionDefinition ownerMission, string candidateName)
    {
        if (ownerMission == null || string.IsNullOrWhiteSpace(candidateName))
        {
            return false;
        }

        UnityEngine.Object[] nestedAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(ownerMission));
        for (int i = 0; i < nestedAssets.Length; i++)
        {
            UnityEngine.Object nestedAsset = nestedAssets[i];
            if (nestedAsset != null && nestedAsset != ownerMission && string.Equals(nestedAsset.name, candidateName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static ScriptableObject ConvertToOwnedSubAssetInternal(
        MissionDefinition ownerMission,
        ScriptableObject sourceAsset,
        Dictionary<ScriptableObject, ScriptableObject> convertedAssets)
    {
        if (ownerMission == null || sourceAsset == null)
        {
            return null;
        }

        if (IsOwnedSubAsset(ownerMission, sourceAsset) || !ShouldConvertMissionNode(sourceAsset))
        {
            return sourceAsset;
        }

        if (convertedAssets != null && convertedAssets.TryGetValue(sourceAsset, out ScriptableObject existingConvertedAsset))
        {
            return existingConvertedAsset;
        }

        ScriptableObject convertedAsset = ScriptableObject.CreateInstance(sourceAsset.GetType());
        Undo.RegisterCreatedObjectUndo(convertedAsset, $"Convert {sourceAsset.GetType().Name} To Sub-Asset");
        AssetDatabase.AddObjectToAsset(convertedAsset, ownerMission);
        EditorUtility.CopySerialized(sourceAsset, convertedAsset);
        convertedAsset.name = GenerateUniqueChildName(ownerMission, sourceAsset.GetType(), sourceAsset.name);
        EditorUtility.SetDirty(convertedAsset);

        convertedAssets?.Add(sourceAsset, convertedAsset);
        RewriteNestedMissionNodeReferences(ownerMission, convertedAsset, convertedAssets);
        return convertedAsset;
    }

    private static int RewriteNestedMissionNodeReferences(
        MissionDefinition ownerMission,
        ScriptableObject convertedAsset,
        Dictionary<ScriptableObject, ScriptableObject> convertedAssets)
    {
        if (ownerMission == null || convertedAsset == null)
        {
            return 0;
        }

        SerializedObject serializedObject = new SerializedObject(convertedAsset);
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        bool hasChanges = false;
        int rewrittenReferenceCount = 0;
        while (iterator.Next(enterChildren))
        {
            enterChildren = true;
            if (iterator.propertyPath == "m_Script" || iterator.propertyType != SerializedPropertyType.ObjectReference)
            {
                continue;
            }

            if (iterator.objectReferenceValue is not ScriptableObject referencedAsset || referencedAsset == ownerMission)
            {
                continue;
            }

            if (!ShouldConvertMissionNode(referencedAsset))
            {
                continue;
            }

            ScriptableObject convertedReference = ConvertToOwnedSubAssetInternal(ownerMission, referencedAsset, convertedAssets);
            if (convertedReference == null || ReferenceEquals(convertedReference, referencedAsset))
            {
                continue;
            }

            iterator.objectReferenceValue = convertedReference;
            hasChanges = true;
            rewrittenReferenceCount++;
        }

        if (hasChanges)
        {
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(convertedAsset);
        }

        return rewrittenReferenceCount;
    }

    private static bool ShouldConvertMissionNode(ScriptableObject candidate)
    {
        return candidate is MissionStageDefinition ||
               candidate is MissionObjectiveDefinition ||
               candidate is MissionActionDefinition ||
               candidate is MissionFailConditionDefinition;
    }
}
