using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneCursorStateRuntime
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplyForScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode _)
    {
        ApplyForScene(scene);
    }

    private static void ApplyForScene(Scene scene)
    {
        StarterAssetsInputs gameplayInputs = FindActiveSceneGameplayInputs(scene);
        if (gameplayInputs != null)
        {
            gameplayInputs.cursorLocked = true;
            gameplayInputs.cursorInputForLook = true;
            gameplayInputs.LookInput(Vector2.zero);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static StarterAssetsInputs FindActiveSceneGameplayInputs(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        FirstPersonController[] controllers = Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Exclude);
        for (int i = 0; i < controllers.Length; i++)
        {
            FirstPersonController controller = controllers[i];
            if (controller == null || controller.gameObject.scene != scene)
            {
                continue;
            }

            StarterAssetsInputs inputs = controller.GetComponent<StarterAssetsInputs>();
            if (inputs != null && inputs.isActiveAndEnabled)
            {
                return inputs;
            }
        }

        return null;
    }
}
