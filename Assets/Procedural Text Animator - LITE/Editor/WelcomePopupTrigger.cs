using UnityEditor;

namespace ProceduralUIEffects.Editor
{
    [InitializeOnLoad]
    public class WelcomePopupTrigger
    {
        private const string FirstTimeKey = "MotionUI_Lite_FirstTimeImport";

        static WelcomePopupTrigger()
        {
            if (!EditorPrefs.GetBool(FirstTimeKey, false))
            {
                EditorPrefs.SetBool(FirstTimeKey, true);
                WelcomeWindow.ShowWindow();
            }
        }
    }
}