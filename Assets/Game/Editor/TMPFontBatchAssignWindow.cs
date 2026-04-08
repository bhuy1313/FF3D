using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TMPFontBatchAssignWindow : EditorWindow
{
    private const string DefaultPrefabSearchFolders = "Assets/Game";
    private const int PreviewLimit = 200;

    private sealed class ScanEntry
    {
        public string Scope;
        public string Location;
        public UnityEngine.Object Context;
    }

    private TMP_FontAsset targetFont;
    private string prefabSearchFolders = DefaultPrefabSearchFolders;
    private bool includeLoadedScenes = true;
    private bool includePrefabAssets = true;
    private bool skipObjectsAlreadyUsingTargetFont = true;
    private bool assignDefaultFontMaterial = true;
    private Vector2 previewScrollPosition;
    private readonly List<ScanEntry> previewEntries = new List<ScanEntry>();
    private int sceneMatchCount;
    private int prefabMatchCount;
    private int totalMatchCount;

    [MenuItem("Tools/TrueJourney/UI/TMP Font Batch Assign")]
    public static void OpenWindow()
    {
        TMPFontBatchAssignWindow window = GetWindow<TMPFontBatchAssignWindow>("TMP Font Batch");
        window.minSize = new Vector2(720f, 420f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Batch Assign TextMeshProUGUI Font Asset", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Target Font Asset", targetFont, typeof(TMP_FontAsset), false);
        includeLoadedScenes = EditorGUILayout.ToggleLeft("Include loaded scenes", includeLoadedScenes);
        includePrefabAssets = EditorGUILayout.ToggleLeft("Include prefab assets", includePrefabAssets);
        skipObjectsAlreadyUsingTargetFont = EditorGUILayout.ToggleLeft("Skip objects already using target font", skipObjectsAlreadyUsingTargetFont);
        assignDefaultFontMaterial = EditorGUILayout.ToggleLeft("Also assign target font default material", assignDefaultFontMaterial);

        EditorGUILayout.Space();
        DrawPrefabFolderField();
        EditorGUILayout.Space();
        DrawActions();
        EditorGUILayout.Space();
        DrawSummary();
        DrawPreview();
    }

    private void DrawPrefabFolderField()
    {
        EditorGUILayout.LabelField("Prefab Search Folders", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "One folder per line. Used only when scanning or applying prefab assets. Loaded scenes are always scanned from the currently open scenes.",
            MessageType.None);

        prefabSearchFolders = EditorGUILayout.TextArea(prefabSearchFolders ?? string.Empty, GUILayout.MinHeight(54f));

        List<string> invalidFolders = GetInvalidPrefabSearchFolders();
        if (invalidFolders.Count == 0)
        {
            return;
        }

        EditorGUILayout.HelpBox(
            "Ignored invalid prefab folders:\n- " + string.Join("\n- ", invalidFolders),
            MessageType.Warning);
    }

    private void DrawActions()
    {
        if (!includeLoadedScenes && !includePrefabAssets)
        {
            EditorGUILayout.HelpBox("Enable at least one scope before scanning or applying.", MessageType.Warning);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!includeLoadedScenes && !includePrefabAssets))
            {
                if (GUILayout.Button("Scan"))
                {
                    RunScan();
                }
            }

            using (new EditorGUI.DisabledScope(targetFont == null || (!includeLoadedScenes && !includePrefabAssets)))
            {
                if (GUILayout.Button("Apply Target Font"))
                {
                    ApplyTargetFont();
                }
            }
        }
    }

    private void DrawSummary()
    {
        EditorGUILayout.LabelField(
            $"Matches: {totalMatchCount} | Scenes: {sceneMatchCount} | Prefabs: {prefabMatchCount}",
            EditorStyles.miniBoldLabel);
    }

    private void DrawPreview()
    {
        if (previewEntries.Count == 0)
        {
            EditorGUILayout.HelpBox("Run Scan to preview matching TextMeshProUGUI components.", MessageType.Info);
            return;
        }

        if (totalMatchCount > previewEntries.Count)
        {
            EditorGUILayout.HelpBox(
                $"Preview is limited to the first {PreviewLimit} matches. Apply will still process all detected items.",
                MessageType.None);
        }

        previewScrollPosition = EditorGUILayout.BeginScrollView(previewScrollPosition);

        for (int i = 0; i < previewEntries.Count; i++)
        {
            ScanEntry entry = previewEntries[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(entry.Scope, EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(entry.Location, EditorStyles.wordWrappedMiniLabel);

                if (entry.Context != null)
                {
                    EditorGUILayout.ObjectField("Object", entry.Context, typeof(UnityEngine.Object), true);
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void RunScan()
    {
        previewEntries.Clear();
        sceneMatchCount = 0;
        prefabMatchCount = 0;
        totalMatchCount = 0;

        try
        {
            if (includeLoadedScenes)
            {
                ScanLoadedScenes();
            }

            if (includePrefabAssets)
            {
                ScanPrefabAssets();
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Repaint();
    }

    private void ApplyTargetFont()
    {
        if (targetFont == null)
        {
            EditorUtility.DisplayDialog("TMP Font Batch Assign", "Assign a target TMP_FontAsset before applying.", "OK");
            return;
        }

        int appliedSceneCount = 0;
        int appliedPrefabCount = 0;

        try
        {
            if (includeLoadedScenes)
            {
                appliedSceneCount = ApplyToLoadedScenes();
            }

            if (includePrefabAssets)
            {
                appliedPrefabCount = ApplyToPrefabAssets();
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        RunScan();

        EditorUtility.DisplayDialog(
            "TMP Font Batch Assign",
            $"Applied '{targetFont.name}' to {appliedSceneCount + appliedPrefabCount} TextMeshProUGUI component(s).\n\n" +
            $"- Scene objects: {appliedSceneCount}\n" +
            $"- Prefab objects: {appliedPrefabCount}",
            "OK");
    }

    private void ScanLoadedScenes()
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                TextMeshProUGUI[] components = roots[rootIndex].GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    TextMeshProUGUI component = components[componentIndex];
                    if (!ShouldProcess(component))
                    {
                        continue;
                    }

                    sceneMatchCount++;
                    totalMatchCount++;
                    TryAddPreviewEntry("Scene", $"{scene.path} :: {BuildHierarchyPath(component.transform)}", component);
                }
            }
        }
    }

    private void ScanPrefabAssets()
    {
        string[] prefabFolders = GetValidPrefabSearchFolders();
        if (prefabFolders.Length == 0)
        {
            return;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", prefabFolders);
        for (int prefabIndex = 0; prefabIndex < prefabGuids.Length; prefabIndex++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[prefabIndex]);
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null || PrefabUtility.GetPrefabAssetType(prefabAsset) == PrefabAssetType.Model)
            {
                continue;
            }

            if (EditorUtility.DisplayCancelableProgressBar(
                    "Scanning TMP Font Targets",
                    prefabPath,
                    prefabGuids.Length == 0 ? 1f : (float)prefabIndex / prefabGuids.Length))
            {
                break;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                TextMeshProUGUI[] components = prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    TextMeshProUGUI component = components[componentIndex];
                    if (!ShouldProcess(component))
                    {
                        continue;
                    }

                    prefabMatchCount++;
                    totalMatchCount++;
                    TryAddPreviewEntry(
                        "Prefab",
                        $"{prefabPath} :: {BuildHierarchyPath(component.transform)}",
                        prefabAsset);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }

    private int ApplyToLoadedScenes()
    {
        int appliedCount = 0;
        HashSet<Scene> dirtyScenes = new HashSet<Scene>();

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                TextMeshProUGUI[] components = roots[rootIndex].GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    TextMeshProUGUI component = components[componentIndex];
                    if (!ShouldApply(component))
                    {
                        continue;
                    }

                    Undo.RecordObject(component, "Batch Assign TMP Font");
                    component.font = targetFont;
                    if (assignDefaultFontMaterial && targetFont.material != null)
                    {
                        component.fontSharedMaterial = targetFont.material;
                    }

                    component.SetAllDirty();
                    EditorUtility.SetDirty(component);
                    appliedCount++;
                    dirtyScenes.Add(component.gameObject.scene);
                }
            }
        }

        foreach (Scene scene in dirtyScenes)
        {
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        return appliedCount;
    }

    private int ApplyToPrefabAssets()
    {
        string[] prefabFolders = GetValidPrefabSearchFolders();
        if (prefabFolders.Length == 0)
        {
            return 0;
        }

        int appliedCount = 0;
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", prefabFolders);
        for (int prefabIndex = 0; prefabIndex < prefabGuids.Length; prefabIndex++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[prefabIndex]);
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null || PrefabUtility.GetPrefabAssetType(prefabAsset) == PrefabAssetType.Model)
            {
                continue;
            }

            if (EditorUtility.DisplayCancelableProgressBar(
                    "Applying TMP Font",
                    prefabPath,
                    prefabGuids.Length == 0 ? 1f : (float)prefabIndex / prefabGuids.Length))
            {
                break;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            bool hasChanges = false;

            try
            {
                TextMeshProUGUI[] components = prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    TextMeshProUGUI component = components[componentIndex];
                    if (!ShouldApply(component))
                    {
                        continue;
                    }

                    component.font = targetFont;
                    if (assignDefaultFontMaterial && targetFont.material != null)
                    {
                        component.fontSharedMaterial = targetFont.material;
                    }

                    component.SetAllDirty();
                    EditorUtility.SetDirty(component);
                    appliedCount++;
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        return appliedCount;
    }

    private bool ShouldProcess(TextMeshProUGUI component)
    {
        if (component == null)
        {
            return false;
        }

        if (!skipObjectsAlreadyUsingTargetFont || targetFont == null)
        {
            return true;
        }

        return component.font != targetFont;
    }

    private bool ShouldApply(TextMeshProUGUI component)
    {
        return component != null && targetFont != null && (!skipObjectsAlreadyUsingTargetFont || component.font != targetFont);
    }

    private void TryAddPreviewEntry(string scope, string location, UnityEngine.Object context)
    {
        if (previewEntries.Count >= PreviewLimit)
        {
            return;
        }

        previewEntries.Add(new ScanEntry
        {
            Scope = scope,
            Location = location,
            Context = context
        });
    }

    private string[] GetValidPrefabSearchFolders()
    {
        List<string> folders = new List<string>();
        List<string> invalidFolders = GetInvalidPrefabSearchFolders();

        string[] rawFolders = (prefabSearchFolders ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < rawFolders.Length; i++)
        {
            string folder = rawFolders[i].Trim();
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            if (invalidFolders.Contains(folder))
            {
                continue;
            }

            if (!folders.Contains(folder))
            {
                folders.Add(folder);
            }
        }

        return folders.ToArray();
    }

    private List<string> GetInvalidPrefabSearchFolders()
    {
        List<string> invalidFolders = new List<string>();
        string[] rawFolders = (prefabSearchFolders ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < rawFolders.Length; i++)
        {
            string folder = rawFolders[i].Trim();
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            if (!AssetDatabase.IsValidFolder(folder) && !invalidFolders.Contains(folder))
            {
                invalidFolders.Add(folder);
            }
        }

        return invalidFolders;
    }

    private static string BuildHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return "<missing>";
        }

        List<string> parts = new List<string>();
        Transform current = target;
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }
}
