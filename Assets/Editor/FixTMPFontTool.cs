using UnityEngine;
using UnityEditor;
using TMPro;
using System.Linq;

public class FixTMPFontTool : Editor
{
    [MenuItem("Tools/Fix Missing TMP Characters")]
    public static void FixFont()
    {
        string fontAssetPath = "Assets/Game/UI/Fonts/Be_Vietnam_Pro/BeVietnamPro-Regular SDF FIX.asset";
        string sourceFontPath = "Assets/Game/UI/Fonts/Be_Vietnam_Pro/BeVietnamPro-Regular.ttf";
        
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourceFontPath);
        
        if (fontAsset != null && sourceFont != null)
        {
            SerializedObject so = new SerializedObject(fontAsset);
            so.Update();
            
            // Assign source font so it can generate dynamically
            so.FindProperty("m_SourceFontFile").objectReferenceValue = sourceFont;
            so.FindProperty("m_AtlasPopulationMode").intValue = 1; // 1 = Dynamic
            
            so.ApplyModifiedProperties();
            
            // Force character generation for all basic and Vietnamese characters
            string basicChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+-=[]{}|;':\",./<>? \\";
            string vietnameseChars = "aAàÀảẢãÃáÁạẠăĂằẰẳẲẵẴắẮặẶâÂầẦẩẨẫẪấẤậẬbBcCdDđĐeEèÈẻẺẽẼéÉẹẸêÊềỀểỂễỄếẾệỆfFgGhHiIìÌỉỈĩĨíÍịỊjJkKlLmMnNoOòÒỏỎõÕóÓọỌôÔồỒổỔỗỖốỐộỘơƠờỜởỞỡỠớỚợỢpPqQrRsStTuUùÙủỦũŨúÚụỤưƯừỪửỬữỮứỨựỰvVwWxXyYỳỲỷỶỹỸýÝỵỴzZ";
            string allChars = new string((basicChars + vietnameseChars).Distinct().ToArray());
            
            uint[] characterSet = allChars.Select(c => (uint)c).ToArray();
            
            // Add characters. In Dynamic mode, this will rasterize them into the atlas.
            bool success = fontAsset.TryAddCharacters(characterSet, out uint[] missing);
            
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            if (missing != null && missing.Length > 0)
            {
                Debug.LogWarning($"Fixed font, but {missing.Length} characters couldn't fit in the atlas.");
            }
            else
            {
                Debug.Log("Successfully updated BeVietnamPro-Regular SDF FIX to Dynamic mode and added all basic + Vietnamese characters!");
            }
            
            // Clean up the script itself since we only need it once
            AssetDatabase.MoveAssetToTrash("Assets/Editor/FixTMPFontTool.cs");
        }
        else
        {
            Debug.LogError("Could not find the font asset or source font! Please check the paths.");
        }
    }
}
