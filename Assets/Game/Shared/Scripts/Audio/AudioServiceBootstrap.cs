public static class AudioServiceBootstrap
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeAudio()
    {
        AudioManager.EnsureHostObject();
        AudioService.ApplySavedVolumes();
    }
}
