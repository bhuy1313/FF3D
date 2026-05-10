using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TMPFontSizeBatchTool))]
public sealed class TMPFontSizeBatchToolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8f);

        TMPFontSizeBatchTool tool = (TMPFontSizeBatchTool)target;
        if (GUILayout.Button("Scan"))
        {
            bool hasAnyAction =
                tool.ApplyFontSize ||
                (tool.ApplyFontAsset && tool.TargetFontAsset != null);
            if (!hasAnyAction)
            {
                Debug.LogWarning("TMPFontSizeBatchTool has nothing to apply. Enable Apply Font Size or assign a Target Font Asset.", tool);
                return;
            }

            Undo.RecordObject(tool, "Apply TMP Font Size Batch");

            int changedCount = 0;
            TMPro.TextMeshProUGUI[] texts = tool.GetComponentsInChildren<TMPro.TextMeshProUGUI>(tool.IncludeInactive);
            for (int i = 0; i < texts.Length; i++)
            {
                TMPro.TextMeshProUGUI text = texts[i];
                if (text == null)
                {
                    continue;
                }

                Undo.RecordObject(text, "Apply TMP Font Size Batch");
            }

            changedCount = tool.ApplyToChildren();
            EditorUtility.SetDirty(tool);

            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null)
                {
                    EditorUtility.SetDirty(texts[i]);
                }
            }

            Debug.Log($"TMPFontSizeBatchTool updated {changedCount} TextMeshProUGUI component(s) on '{tool.name}'.", tool);
        }
    }
}
