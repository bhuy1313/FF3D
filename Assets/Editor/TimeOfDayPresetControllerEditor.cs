using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(TimeOfDayPresetController))]
public class TimeOfDayPresetControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TimeOfDayPresetController controller = (TimeOfDayPresetController)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPresetButton(controller, "Morning", TimeOfDayPresetController.TimeOfDayPreset.Morning);
            DrawPresetButton(controller, "Noon", TimeOfDayPresetController.TimeOfDayPreset.Noon);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPresetButton(controller, "Evening", TimeOfDayPresetController.TimeOfDayPreset.Evening);
            DrawPresetButton(controller, "Night", TimeOfDayPresetController.TimeOfDayPreset.Night);
        }
    }

    private void DrawPresetButton(TimeOfDayPresetController controller, string label, TimeOfDayPresetController.TimeOfDayPreset preset)
    {
        if (!GUILayout.Button(label))
        {
            return;
        }

        Undo.RecordObject(controller, $"Apply {label} Time Of Day");

        Light light = controller.GetComponent<Light>();
        if (light != null)
        {
            Undo.RecordObject(light, $"Apply {label} Time Of Day");
        }

        Undo.RecordObject(controller.transform, $"Apply {label} Time Of Day");

        controller.SetPreset(preset);
        EditorUtility.SetDirty(controller);

        if (light != null)
        {
            EditorUtility.SetDirty(light);
        }

        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
    }
}
