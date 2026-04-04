using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

[DisallowMultipleComponent]
public class BootIntroController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Flow")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private float minimumIntroTime = 0.5f;
    [SerializeField] private bool allowSkip = true;
    [SerializeField] private bool waitForVideoCompletion = true;
    [SerializeField] private bool stopVideoWhenSkipRequested = true;

    private AsyncOperation preloadOperation;
    private bool videoFinished;
    private bool isTransitioning;
    private bool skipRequested;

    private IEnumerator Start()
    {
        ResolveReferences();

        preloadOperation = BeginMainMenuPreload();

        if (!HasPlayableVideo())
        {
            yield return WaitForSceneReady();
            ActivateMainMenu();
            yield break;
        }

        videoPlayer.loopPointReached -= HandleVideoFinished;
        videoPlayer.loopPointReached += HandleVideoFinished;
        videoPlayer.Stop();
        videoPlayer.Prepare();

        while (!videoPlayer.isPrepared)
        {
            TryHandleSkip();
            if (skipRequested && IsSceneReady())
            {
                ActivateMainMenu();
                yield break;
            }

            yield return null;
        }

        videoPlayer.Play();

        float elapsed = 0f;
        while (!CanTransition(elapsed))
        {
            elapsed += Time.unscaledDeltaTime;
            TryHandleSkip();
            yield return null;
        }

        ActivateMainMenu();
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= HandleVideoFinished;
        }
    }

    private void ResolveReferences()
    {
        if (videoPlayer == null)
        {
            videoPlayer = FindFirstObjectByType<VideoPlayer>();
        }
    }

    private AsyncOperation BeginMainMenuPreload()
    {
        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogError("BootIntroController: Main Menu scene name is empty.", this);
            return null;
        }

        if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError($"BootIntroController: Scene '{mainMenuSceneName}' is not available in build settings.", this);
            return null;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(mainMenuSceneName);
        if (operation != null)
        {
            operation.allowSceneActivation = false;
        }

        return operation;
    }

    private bool HasPlayableVideo()
    {
        if (videoPlayer == null)
        {
            return false;
        }

        return videoPlayer.clip != null || !string.IsNullOrWhiteSpace(videoPlayer.url);
    }

    private IEnumerator WaitForSceneReady()
    {
        while (!IsSceneReady())
        {
            yield return null;
        }
    }

    private bool CanTransition(float elapsed)
    {
        bool introTimeSatisfied = skipRequested || elapsed >= Mathf.Max(0f, minimumIntroTime);
        bool videoSatisfied = !waitForVideoCompletion || skipRequested || videoFinished;
        return introTimeSatisfied && videoSatisfied && IsSceneReady();
    }

    private bool IsSceneReady()
    {
        return preloadOperation == null || preloadOperation.progress >= 0.9f;
    }

    private void TryHandleSkip()
    {
        if (!allowSkip || skipRequested)
        {
            return;
        }

        if (!Input.anyKeyDown && !Input.GetMouseButtonDown(0))
        {
            return;
        }

        skipRequested = true;
        if (stopVideoWhenSkipRequested && videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }
    }

    private void HandleVideoFinished(VideoPlayer _)
    {
        videoFinished = true;
    }

    private void ActivateMainMenu()
    {
        if (isTransitioning)
        {
            return;
        }

        isTransitioning = true;

        if (preloadOperation != null)
        {
            preloadOperation.allowSceneActivation = true;
            return;
        }

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
