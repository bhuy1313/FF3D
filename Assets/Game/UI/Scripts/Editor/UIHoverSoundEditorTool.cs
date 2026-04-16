using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using FF3D.UI;

namespace FF3D.Editor
{
    public class UIHoverSoundEditorTool : EditorWindow
    {
        private AudioClip defaultHoverSound;

        [MenuItem("Tools/UI Hover Sound Configurator")]
        public static void ShowWindow()
        {
            GetWindow<UIHoverSoundEditorTool>("UI Hover Sound Tool");
        }

        private void OnGUI()
        {
            GUILayout.Label("UI Hover Sound Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            defaultHoverSound = (AudioClip)EditorGUILayout.ObjectField("Default Hover Sound", defaultHoverSound, typeof(AudioClip), false);
            EditorGUILayout.Space();

            if (GUILayout.Button("Scan and Add to All Buttons in Scene"))
            {
                AddHoverSoundToButtons();
            }
        }

        private void AddHoverSoundToButtons()
        {
            // Find all buttons in the active scene
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int addedCount = 0;

            foreach (Button btn in buttons)
            {
                // Check if the button already has the UIHoverSound component
                UIHoverSound hoverSoundComponent = btn.GetComponent<UIHoverSound>();
                
                if (hoverSoundComponent == null)
                {
                    // Add the component
                    hoverSoundComponent = btn.gameObject.AddComponent<UIHoverSound>();
                    
                    // Mark the GameObject as dirty so the change is saved
                    EditorUtility.SetDirty(btn.gameObject);
                    addedCount++;
                }

                // If a default sound was selected, try assigning it if none is set
                if (defaultHoverSound != null)
                {
                    SerializedObject serializedObj = new SerializedObject(hoverSoundComponent);
                    SerializedProperty soundProp = serializedObj.FindProperty("hoverSound");
                    
                    if (soundProp != null && soundProp.objectReferenceValue == null)
                    {
                        soundProp.objectReferenceValue = defaultHoverSound;
                        serializedObj.ApplyModifiedProperties();
                        EditorUtility.SetDirty(hoverSoundComponent);
                    }
                }
            }

            // Show a notification in the editor window
            ShowNotification(new GUIContent($"Added UIHoverSound to {addedCount} buttons."));
            Debug.Log($"[UI Hover Sound Tool] Scanned {buttons.Length} buttons. Added component to {addedCount} buttons.");
        }
    }
}