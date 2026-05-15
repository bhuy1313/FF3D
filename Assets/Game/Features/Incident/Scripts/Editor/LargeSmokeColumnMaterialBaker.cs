using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class LargeSmokeColumnMaterialBaker
{
    private const string MenuPath = "Tools/FF3D/Bake Large Smoke Column Materials";
    private const string PrefabPath = "Assets/Game/Features/Incident/Prefabs/Environment/LargeSmokeColumn.prefab";
    private const string MaterialDirectory = "Assets/Game/Features/Incident/Materials";
    private const string TextureDirectory = "Assets/Game/Features/Incident/Textures";
    private const string SmokeMaterialPath = MaterialDirectory + "/LargeSmokeColumnSmoke.mat";
    private const string CoreMaterialPath = MaterialDirectory + "/LargeSmokeColumnCoreOcclusion.mat";
    private const string TexturePath = TextureDirectory + "/LargeSmokeColumnSmokeTexture.png";

    [MenuItem(MenuPath)]
    private static void Bake()
    {
        Directory.CreateDirectory(Path.GetFullPath(MaterialDirectory));
        Directory.CreateDirectory(Path.GetFullPath(TextureDirectory));

        Texture2D bakedTexture = BuildSmokeTextureReadable();
        try
        {
            File.WriteAllBytes(Path.GetFullPath(TexturePath), bakedTexture.EncodeToPNG());
        }
        finally
        {
            Object.DestroyImmediate(bakedTexture);
        }

        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
        ConfigureImportedTexture(TexturePath);

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            LargeSmokeColumnVfx smokeColumn = prefabRoot.GetComponent<LargeSmokeColumnVfx>();
            if (smokeColumn == null)
            {
                throw new System.InvalidOperationException("LargeSmokeColumnVfx was not found on the prefab root.");
            }

            SerializedObject serializedSmokeColumn = new SerializedObject(smokeColumn);
            serializedSmokeColumn.FindProperty("smokeMaterial").objectReferenceValue = null;
            serializedSmokeColumn.FindProperty("coreOcclusionMaterial").objectReferenceValue = null;
            serializedSmokeColumn.ApplyModifiedPropertiesWithoutUndo();

            smokeColumn.BuildOrRefresh();

            Material runtimeSmokeMaterial = GetPrivateField<Material>(smokeColumn, "runtimeSmokeMaterial");
            Material runtimeCoreMaterial = GetPrivateField<Material>(smokeColumn, "runtimeCoreOcclusionMaterial");
            Texture2D textureAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);

            if (runtimeSmokeMaterial == null || runtimeCoreMaterial == null || textureAsset == null)
            {
                throw new System.InvalidOperationException("Failed to resolve runtime smoke assets for baking.");
            }

            Material smokeAsset = new Material(runtimeSmokeMaterial) { name = "LargeSmokeColumnSmoke" };
            Material coreAsset = new Material(runtimeCoreMaterial) { name = "LargeSmokeColumnCoreOcclusion" };
            ApplyTexture(smokeAsset, textureAsset);
            ApplyTexture(coreAsset, textureAsset);

            CreateOrReplaceAsset(smokeAsset, SmokeMaterialPath);
            CreateOrReplaceAsset(coreAsset, CoreMaterialPath);

            Material savedSmokeAsset = AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterialPath);
            Material savedCoreAsset = AssetDatabase.LoadAssetAtPath<Material>(CoreMaterialPath);
            serializedSmokeColumn.FindProperty("smokeMaterial").objectReferenceValue = savedSmokeAsset;
            serializedSmokeColumn.FindProperty("coreOcclusionMaterial").objectReferenceValue = savedCoreAsset;
            serializedSmokeColumn.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Baked LargeSmokeColumn materials to {MaterialDirectory}", savedSmokeAsset);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool BakeValidate()
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
    }

    private static Texture2D BuildSmokeTextureReadable()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = "LargeSmokeColumnSmokeTexture",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float v = (y + 0.5f) / size;
                float dx = (u - 0.5f) * 2f;
                float dy = (v - 0.5f) * 2f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float softEdge = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.62f, 1f, distance));
                float core = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.18f, 0.72f, distance));
                float noise = Mathf.PerlinNoise(u * 6.5f + 17.3f, v * 6.5f + 41.7f);
                float alpha = Mathf.Clamp01(Mathf.Lerp(softEdge * 0.62f, core, 0.72f) * Mathf.Lerp(0.82f, 1f, noise));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(true, false);
        return texture;
    }

    private static void ConfigureImportedTexture(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.SaveAndReimport();
    }

    private static void CreateOrReplaceAsset(Material material, string assetPath)
    {
        Material existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (existingMaterial != null)
        {
            EditorUtility.CopySerialized(material, existingMaterial);
            existingMaterial.name = material.name;
            EditorUtility.SetDirty(existingMaterial);
            Object.DestroyImmediate(material);
            return;
        }

        AssetDatabase.CreateAsset(material, assetPath);
    }

    private static void ApplyTexture(Material material, Texture texture)
    {
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }
    }

    private static T GetPrivateField<T>(object target, string fieldName) where T : class
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null ? field.GetValue(target) as T : null;
    }
}
