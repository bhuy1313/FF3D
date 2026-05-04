using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(PowerLineSplineBuilder))]
public class PowerLineSplineBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PowerLineSplineBuilder builder = (PowerLineSplineBuilder)target;

        EditorGUILayout.Space(8f);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Build Spline", GUILayout.Height(28f)))
            {
                Object container = builder.SplineContainer;
                if (container != null)
                {
                    Undo.RecordObjects(new[] { builder, container }, "Build Power Line Spline");
                }
                else
                {
                    Undo.RecordObject(builder, "Build Power Line Spline");
                }

                builder.BuildSpline();
                EditorUtility.SetDirty(builder);
                if (container != null)
                {
                    EditorUtility.SetDirty(container);
                }

                if (!Application.isPlaying)
                {
                    EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
                }
            }

            if (GUILayout.Button("Add Line", GUILayout.Height(28f)))
            {
                Undo.RecordObject(builder, "Add Power Line");
                builder.AddLine();
                EditorUtility.SetDirty(builder);
            }
        }

        if (GUILayout.Button("Clear Lines"))
        {
            Undo.RecordObject(builder, "Clear Power Lines");
            builder.ClearLines();
            EditorUtility.SetDirty(builder);
        }
    }
}
