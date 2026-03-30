using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Serialization;

[DefaultExecutionOrder(-100)]
public class LoadingSceneController : MonoBehaviour
{
    private const string LoadingSceneName = "LoadingScene";
    private const string LoadingStatusKey = "loading.status.loading";
    private const string LoadingCompleteKey = "loading.status.complete";

    [Header("References")]
    [SerializeField] private Image progressBar;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private TMP_Text progressPercentLabel;
    [SerializeField] private RectTransform progressIndicator;
    [SerializeField] private RectTransform progressIndicatorEndReference;

    [Header("Flow")]
    [FormerlySerializedAs("fakeLoadingDuration")]
    [SerializeField] private float minimumDisplayTime = 1f;
    [SerializeField] private string fallbackTargetSceneName = "LevelSelectScene";

    private IEnumerator Start()
    {
        if (SceneManager.GetActiveScene().name != LoadingSceneName)
        {
            yield break;
        }

        ConfigureProgressBar();
        UpdateProgress(0f);

        yield return null;

        string targetSceneName = fallbackTargetSceneName;
        if (LoadingFlowState.TryGetPendingTargetScene(out string pendingSceneName))
        {
            targetSceneName = pendingSceneName;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogError($"LoadingSceneController: Scene '{targetSceneName}' is not available in build settings.");
            SetStatusText("Load failed");
            yield break;
        }

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetSceneName);
        if (loadOperation == null)
        {
            Debug.LogError($"LoadingSceneController: Failed to create async load operation for scene '{targetSceneName}'.");
            SetStatusText("Load failed");
            yield break;
        }

        loadOperation.allowSceneActivation = false;

        float requiredDisplayTime = Mathf.Max(0f, minimumDisplayTime);
        float elapsed = 0f;

        while (true)
        {
            elapsed += Time.unscaledDeltaTime;

            float sceneProgress = Mathf.Clamp01(loadOperation.progress / 0.9f);
            float timeProgress = requiredDisplayTime <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / requiredDisplayTime);

            float normalizedProgress = Mathf.Min(sceneProgress, timeProgress);
            UpdateProgress(normalizedProgress);

            bool finishedLoading = loadOperation.progress >= 0.9f;
            bool metMinimumDisplayTime = elapsed >= requiredDisplayTime;
            if (finishedLoading && metMinimumDisplayTime)
            {
                break;
            }

            yield return null;
        }

        UpdateProgress(1f);
        LoadingFlowState.ClearPendingTargetScene();
        loadOperation.allowSceneActivation = true;
    }

    private void ConfigureProgressBar()
    {
        if (progressBar == null)
        {
            Debug.LogWarning("LoadingSceneController: Progress Bar reference is missing.");
            return;
        }

        progressBar.type = Image.Type.Filled;
        progressBar.fillMethod = Image.FillMethod.Horizontal;
        progressBar.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressBar.fillAmount = 0f;

        ResolveProgressIndicatorReferences();
    }

    private void UpdateProgress(float normalizedProgress)
    {
        if (progressBar != null)
        {
            progressBar.fillAmount = normalizedProgress;
        }

        UpdateProgressIndicator(normalizedProgress);

        if (progressPercentLabel != null)
        {
            progressPercentLabel.text = $"{Mathf.RoundToInt(normalizedProgress * 100f)}%";
        }

        string localizedStatus = normalizedProgress >= 1f
            ? LanguageManager.Tr(LoadingCompleteKey, "Loading Complete")
            : LanguageManager.Tr(LoadingStatusKey, "Loading ...");

        SetStatusText(localizedStatus);
    }

    private void SetStatusText(string value)
    {
        if (progressLabel != null)
        {
            progressLabel.text = value;
        }
    }

    private void ResolveProgressIndicatorReferences()
    {
        if (progressBar == null)
        {
            return;
        }

        RectTransform barRect = progressBar.rectTransform;
        if (barRect == null)
        {
            return;
        }

        if (progressIndicator == null)
        {
            progressIndicator = FindBarChildByAnchor(barRect, useRightAnchor: false);
        }

        if (progressIndicatorEndReference == null)
        {
            progressIndicatorEndReference = FindBarChildByAnchor(barRect, useRightAnchor: true);
        }
    }

    private void UpdateProgressIndicator(float normalizedProgress)
    {
        if (progressBar == null)
        {
            return;
        }

        ResolveProgressIndicatorReferences();
        if (progressIndicator == null)
        {
            return;
        }

        RectTransform barRect = progressBar.rectTransform;
        float startX = progressIndicator.anchoredPosition.x;
        float endX = progressIndicatorEndReference != null
            ? barRect.rect.width + progressIndicatorEndReference.anchoredPosition.x
            : barRect.rect.width - startX;

        Vector2 anchoredPosition = progressIndicator.anchoredPosition;
        anchoredPosition.x = Mathf.Lerp(startX, endX, Mathf.Clamp01(normalizedProgress));
        progressIndicator.anchoredPosition = anchoredPosition;
    }

    private static RectTransform FindBarChildByAnchor(RectTransform barRect, bool useRightAnchor)
    {
        if (barRect == null)
        {
            return null;
        }

        float targetAnchorX = useRightAnchor ? 1f : 0f;
        for (int i = 0; i < barRect.childCount; i++)
        {
            RectTransform child = barRect.GetChild(i) as RectTransform;
            if (child == null)
            {
                continue;
            }

            if (Mathf.Approximately(child.anchorMin.x, targetAnchorX) &&
                Mathf.Approximately(child.anchorMax.x, targetAnchorX))
            {
                return child;
            }
        }

        return null;
    }
}
