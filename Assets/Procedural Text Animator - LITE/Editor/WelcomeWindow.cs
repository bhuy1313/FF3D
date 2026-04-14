// Copyright (c) SpankyBoy 2025
// This script is part of the "Procedural Text Animator Pack - Lite".

using UnityEditor;
using UnityEngine;


namespace ProceduralUIEffects.Editor
{
    [InitializeOnLoad]
    public class WelcomeWindow : EditorWindow
    {
        private Texture2D logoTexture;
        private static bool showAtStartup = true;
        private const string ShowAtStartupKey = "ProceduralTextAnimator_Lite_ShowAtStartup";
        private const string AssetStoreURL_Free = "https://assetstore.unity.com/packages/tools/gui/procedural-ui-effects-lite-free";
        private const string AssetStoreURL_Paid = "https://assetstore.unity.com/packages/tools/gui/procedural-ui-effects-animations-327131";
        private const string version = "v1.4.0 - Lite";

        private GUIStyle centeredBoldLabel;
        private GUIStyle centeredWrappedLabel;
        private GUIStyle centeredGreyLabel;
        private GUIStyle largeBoldLabel;
        private GUIStyle upgradeButtonStyle;
        private bool stylesInitialized = false;

        [MenuItem("Tools/Procedural Text Animator/Welcome Screen")]
        public static void ShowWindow()
        {
            // Get existing open window or if none, make a new one
            WelcomeWindow window = (WelcomeWindow)EditorWindow.GetWindow(typeof(WelcomeWindow), true, "Welcome to Procedural Text Animator - Lite");
            window.minSize = new Vector2(350, 550);
            window.maxSize = new Vector2(350, 550);
            window.Show();
        }


        void OnEnable()
        {
            logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Procedural Text Animator - LITE/Documentation/TextAnimator.png");
            showAtStartup = EditorPrefs.GetBool(ShowAtStartupKey, true);
        }

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            centeredBoldLabel = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13
            };

            largeBoldLabel = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16
            };

            centeredWrappedLabel = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };

            centeredGreyLabel = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.gray },
                fontSize = 10
            };

            upgradeButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };

            stylesInitialized = true;
        }


        void OnGUI()
        {
            InitializeStyles();


            if (logoTexture != null)
            {
                GUILayout.BeginHorizontal();
               // GUILayout.FlexibleSpace();
                GUILayout.Label(logoTexture, GUILayout.Width(330), GUILayout.Height(120));
              //  GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("Version " + version, centeredGreyLabel);
            GUILayout.Space(5);

            // Welcome Text
            EditorGUILayout.LabelField("Thank you for installing", centeredWrappedLabel);
            EditorGUILayout.LabelField("Procedural Text Animator - Lite!", centeredBoldLabel);
            GUILayout.Space(5);
            EditorGUILayout.LabelField("Enjoy beautiful Text effects with minimal setup!", centeredWrappedLabel);

            GUILayout.Space(15);

            // Upgrade Section
            DrawUpgradeSection();

            GUILayout.Space(15);

            // Documentation Button
            if (GUILayout.Button("📖 Read the Documentation", GUILayout.Height(35)))
            {
                string docPath = FindFile("Lite_Documentation.pdf");
                if (!string.IsNullOrEmpty(docPath))
                {
                    AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(docPath));
                }
                else
                {
                    UnityEngine.Debug.LogWarning("Procedural Text Animator: Could not find documentation file.");
                }
            }

            GUILayout.Space(5);

            // More Assets Button
            if (GUILayout.Button("🎨 More Assets by SpankyBoy", GUILayout.Height(35)))
            {
                UnityEngine.Application.OpenURL("https://assetstore.unity.com/publishers/109386");
            }

            GUILayout.Space(5);

            // Review Button
            if (GUILayout.Button("⭐ Please Leave a Review ⭐", GUILayout.Height(35)))
            {
                UnityEngine.Application.OpenURL(AssetStoreURL_Free);
            }

            GUILayout.FlexibleSpace();

            // Show at Startup Toggle
            GUILayout.Space(10);
            showAtStartup = EditorGUILayout.Toggle("Show this window on startup", showAtStartup);
            EditorPrefs.SetBool(ShowAtStartupKey, showAtStartup);
        }

        private void DrawUpgradeSection()
        {
            // Draw a box for the upgrade section
            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Space(5);
            EditorGUILayout.LabelField("🚀 Want More Effects?", largeBoldLabel);
            GUILayout.Space(5);

            EditorGUILayout.LabelField("Upgrade to the Full Version for:", centeredWrappedLabel);
            GUILayout.Space(3);

            // Benefits list
            GUILayout.Label("• 3x More Effect Presets", centeredWrappedLabel);
            GUILayout.Label("• Advanced Customization Options", centeredWrappedLabel);
            GUILayout.Label("• Priority Support", centeredWrappedLabel);
            GUILayout.Label("• Future Updates & Effects", centeredWrappedLabel);

            GUILayout.Space(8);

            // Upgrade button with custom color
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f); // Green color

            if (GUILayout.Button("⬆️ UPGRADE TO FULL VERSION", upgradeButtonStyle, GUILayout.Height(40)))
            {
                UnityEngine.Application.OpenURL(AssetStoreURL_Paid);
            }

            GUI.backgroundColor = originalColor;
            GUILayout.Space(5);

            GUILayout.EndVertical();
        }

        private static string FindFile(string fileName)
        {
            string[] guids = AssetDatabase.FindAssets(System.IO.Path.GetFileNameWithoutExtension(fileName));
            if (guids.Length > 0)
            {
                return AssetDatabase.GUIDToAssetPath(guids[0]);
            }
            return null;
        }
    }
}