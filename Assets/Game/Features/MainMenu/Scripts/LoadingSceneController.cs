using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Serialization;

[DefaultExecutionOrder(-100)]
public class LoadingSceneController : MonoBehaviour
{
    private const string BackgroundFadeOverlayName = "Image Fade Overlay";
    private const string LoadingSceneName = "LoadingScene";
    private const string LoadingStreamingKey = "loading.status.loading";
    private const string LoadingPreparingKey = "loading.status.preparing";
    private const string LoadingFinalizingKey = "loading.status.finalizing";
    private const string LoadingCompleteKey = "loading.status.complete";

    [Header("References")]
    [SerializeField] private RawImage backgroundImage;
    [SerializeField] private Texture[] backgroundSlides;
    [SerializeField] private Image[] backgroundSlideIndicators;
    [SerializeField] private RectTransform backgroundSlideIndicatorContainer;
    [SerializeField] private Image backgroundSlideIndicatorTemplate;
    [SerializeField] private Image progressBar;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private TMP_Text progressPercentLabel;
    [SerializeField] private RectTransform progressIndicator;
    [SerializeField] private RectTransform progressIndicatorEndReference;

    [Header("Flow")]
    [FormerlySerializedAs("fakeLoadingDuration")]
    [SerializeField] private float minimumDisplayTime = 5f;
    [SerializeField] private float backgroundSlideInterval = 1.5f;
    [SerializeField] private float backgroundFadeDuration = 0.6f;
    [SerializeField] private string fallbackTargetSceneName = "LevelSelectScene";
    [SerializeField] [Range(0.9f, 0.99f)] private float readyHoldProgress = 0.95f;
    [SerializeField] [Min(1f)] private float progressCurveExponent = 1.35f;
    [SerializeField] private float initialProgressFillSpeed = 0.22f;
    [SerializeField] private float lateProgressFillSpeed = 0.55f;

    [Header("Style")]
    [SerializeField] private Color activeSlideIndicatorColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private Color inactiveSlideIndicatorColor = new Color(1f, 1f, 1f, 0.3f);
    [SerializeField] [Range(0.1f, 1f)] private float backgroundFadeMidBrightness = 0.7f;

    private float displayedProgress;
    private RawImage backgroundFadeOverlay;
    private Color backgroundBaseColor = Color.white;
    private int currentBackgroundIndex = -1;
    private int targetBackgroundIndex = -1;
    private float backgroundFadeElapsed;
    private bool isBackgroundFading;

    private IEnumerator Start()
    {
        if (SceneManager.GetActiveScene().name != LoadingSceneName)
        {
            yield break;
        }

        ResolveBackgroundReferences();
        InitializeBackgroundSlideshow();
        ConfigureProgressBar();
        displayedProgress = 0f;
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
            float curvedSceneProgress = Mathf.Pow(sceneProgress, Mathf.Max(1f, progressCurveExponent));
            bool finishedLoading = loadOperation.progress >= 0.9f;
            float targetVisualProgress = finishedLoading
                ? Mathf.Clamp01(readyHoldProgress)
                : Mathf.Clamp(curvedSceneProgress * readyHoldProgress, 0f, readyHoldProgress);

            float currentFillSpeed = GetCurrentFillSpeed(targetVisualProgress);

            displayedProgress = Mathf.MoveTowards(
                displayedProgress,
                targetVisualProgress,
                currentFillSpeed * Time.unscaledDeltaTime);

            UpdateProgress(displayedProgress, finishedLoading, false);
            UpdateBackgroundSlideshow(elapsed);

            bool metMinimumDisplayTime = elapsed >= requiredDisplayTime;
            if (finishedLoading && metMinimumDisplayTime)
            {
                break;
            }

            yield return null;
        }

        UpdateProgress(1f, sceneReady: true, canActivateScene: true);
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
        displayedProgress = 0f;

        ResolveProgressIndicatorReferences();
    }

    private void UpdateProgress(float normalizedProgress, bool sceneReady = false, bool canActivateScene = false)
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

        string localizedStatus = ResolveProgressStatus(normalizedProgress, sceneReady, canActivateScene);

        SetStatusText(localizedStatus);
    }

    private string ResolveProgressStatus(float normalizedProgress, bool sceneReady, bool canActivateScene)
    {
        if (canActivateScene || normalizedProgress >= 1f)
        {
            return LanguageManager.Tr(LoadingCompleteKey, "Ready");
        }

        if (sceneReady)
        {
            return LanguageManager.Tr(LoadingFinalizingKey, "Finalizing...");
        }

        if (normalizedProgress >= 0.45f)
        {
            return LanguageManager.Tr(LoadingPreparingKey, "Preparing scene...");
        }

        return LanguageManager.Tr(LoadingStreamingKey, "Loading assets...");
    }

    private float GetCurrentFillSpeed(float targetVisualProgress)
    {
        float clampedReadyHold = Mathf.Max(0.01f, readyHoldProgress);
        float phaseT = Mathf.Clamp01(targetVisualProgress / clampedReadyHold);
        float fillSpeed = Mathf.Lerp(initialProgressFillSpeed, lateProgressFillSpeed, phaseT);
        return Mathf.Max(0.01f, fillSpeed);
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
        float startX = 0f;
        if (progressIndicatorEndReference != null)
        {
            startX = progressIndicatorEndReference.anchoredPosition.x;
        }

        float endX = progressIndicatorEndReference != null
            ? barRect.rect.width + progressIndicatorEndReference.anchoredPosition.x
            : barRect.rect.width;

        Vector2 anchoredPosition = progressIndicator.anchoredPosition;
        anchoredPosition.x = Mathf.Lerp(startX, endX, Mathf.Clamp01(normalizedProgress));
        progressIndicator.anchoredPosition = anchoredPosition;
    }

    private void ResolveBackgroundReferences()
    {
        if (backgroundImage == null)
        {
            backgroundImage = FindComponentByName<RawImage>(transform, "Image");
        }

        if (backgroundSlideIndicators == null || backgroundSlideIndicators.Length == 0)
        {
            backgroundSlideIndicators = new[]
            {
                FindComponentByName<Image>(transform, "Barh1"),
                FindComponentByName<Image>(transform, "Barh2"),
                FindComponentByName<Image>(transform, "Barh3")
            };
        }

        ResolveBackgroundSlideIndicatorSupportReferences();
        SyncBackgroundSlideIndicators();
    }

    private void InitializeBackgroundSlideshow()
    {
        backgroundBaseColor = backgroundImage != null ? backgroundImage.color : Color.white;
        EnsureBackgroundFadeOverlay();
        SetBackgroundSlideImmediate(0);
    }

    private void UpdateBackgroundSlideshow(float elapsed)
    {
        if (backgroundSlides == null || backgroundSlides.Length == 0)
        {
            return;
        }

        float interval = Mathf.Max(0.1f, backgroundSlideInterval);
        int slideIndex = Mathf.FloorToInt(elapsed / interval) % backgroundSlides.Length;
        if (slideIndex != currentBackgroundIndex && slideIndex != targetBackgroundIndex)
        {
            StartBackgroundFade(slideIndex);
        }

        UpdateBackgroundFade();
    }

    private void SetBackgroundSlideImmediate(int slideIndex)
    {
        if (backgroundSlides == null || backgroundSlides.Length == 0)
        {
            UpdateSlideIndicators(-1);
            return;
        }

        int resolvedIndex = Mathf.Clamp(slideIndex, 0, backgroundSlides.Length - 1);
        if (backgroundImage != null && backgroundSlides[resolvedIndex] != null)
        {
            backgroundImage.texture = backgroundSlides[resolvedIndex];
            backgroundImage.color = backgroundBaseColor;
        }

        if (backgroundFadeOverlay != null)
        {
            backgroundFadeOverlay.texture = backgroundSlides[resolvedIndex];
            backgroundFadeOverlay.color = WithAlpha(backgroundBaseColor, 0f);
        }

        currentBackgroundIndex = resolvedIndex;
        targetBackgroundIndex = resolvedIndex;
        isBackgroundFading = false;
        backgroundFadeElapsed = 0f;
        UpdateSlideIndicators(resolvedIndex);
    }

    private void StartBackgroundFade(int slideIndex)
    {
        if (backgroundSlides == null || backgroundSlides.Length == 0)
        {
            return;
        }

        int resolvedIndex = Mathf.Clamp(slideIndex, 0, backgroundSlides.Length - 1);
        if (resolvedIndex == currentBackgroundIndex && !isBackgroundFading)
        {
            return;
        }

        if (backgroundImage == null || backgroundFadeOverlay == null || backgroundFadeDuration <= 0f)
        {
            SetBackgroundSlideImmediate(resolvedIndex);
            return;
        }

        targetBackgroundIndex = resolvedIndex;
        backgroundFadeElapsed = 0f;
        isBackgroundFading = true;
        backgroundFadeOverlay.texture = backgroundSlides[resolvedIndex];
        backgroundFadeOverlay.color = WithAlpha(backgroundBaseColor, 0f);
        UpdateSlideIndicators(resolvedIndex);
    }

    private void UpdateBackgroundFade()
    {
        if (!isBackgroundFading || backgroundImage == null || backgroundFadeOverlay == null)
        {
            return;
        }

        backgroundFadeElapsed += Time.unscaledDeltaTime;

        float duration = Mathf.Max(0.01f, backgroundFadeDuration);
        float normalizedFade = Mathf.Clamp01(backgroundFadeElapsed / duration);
        float overlayAlpha = Mathf.SmoothStep(0f, 1f, normalizedFade);
        float brightnessT = 1f - Mathf.Abs((normalizedFade * 2f) - 1f);
        float brightnessMultiplier = Mathf.Lerp(1f, backgroundFadeMidBrightness, brightnessT);

        backgroundImage.color = MultiplyRgb(backgroundBaseColor, brightnessMultiplier);
        backgroundFadeOverlay.color = WithAlpha(backgroundBaseColor, overlayAlpha);

        if (normalizedFade < 1f)
        {
            return;
        }

        if (backgroundSlides != null &&
            targetBackgroundIndex >= 0 &&
            targetBackgroundIndex < backgroundSlides.Length)
        {
            backgroundImage.texture = backgroundSlides[targetBackgroundIndex];
        }

        backgroundImage.color = backgroundBaseColor;
        backgroundFadeOverlay.color = WithAlpha(backgroundBaseColor, 0f);
        currentBackgroundIndex = targetBackgroundIndex;
        isBackgroundFading = false;
    }

    private void EnsureBackgroundFadeOverlay()
    {
        if (backgroundImage == null)
        {
            return;
        }

        if (backgroundFadeOverlay == null)
        {
            backgroundFadeOverlay = backgroundImage.transform.parent != null
                ? FindComponentByName<RawImage>(backgroundImage.transform.parent, BackgroundFadeOverlayName)
                : null;

            if (backgroundFadeOverlay == backgroundImage)
            {
                backgroundFadeOverlay = null;
            }
        }

        if (backgroundFadeOverlay == null)
        {
            GameObject overlayObject = new GameObject(BackgroundFadeOverlayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
            RectTransform backgroundRect = backgroundImage.rectTransform;

            overlayRect.SetParent(backgroundRect.parent, false);
            overlayRect.anchorMin = backgroundRect.anchorMin;
            overlayRect.anchorMax = backgroundRect.anchorMax;
            overlayRect.anchoredPosition = backgroundRect.anchoredPosition;
            overlayRect.sizeDelta = backgroundRect.sizeDelta;
            overlayRect.pivot = backgroundRect.pivot;
            overlayRect.localRotation = backgroundRect.localRotation;
            overlayRect.localScale = backgroundRect.localScale;
            overlayRect.SetSiblingIndex(backgroundRect.GetSiblingIndex() + 1);

            backgroundFadeOverlay = overlayObject.GetComponent<RawImage>();
        }

        if (backgroundFadeOverlay == null)
        {
            return;
        }

        backgroundFadeOverlay.raycastTarget = false;
        backgroundFadeOverlay.maskable = backgroundImage.maskable;
        backgroundFadeOverlay.material = backgroundImage.material;
        backgroundFadeOverlay.uvRect = backgroundImage.uvRect;
        backgroundFadeOverlay.color = WithAlpha(backgroundBaseColor, 0f);
    }

    private void UpdateSlideIndicators(int activeIndex)
    {
        if (backgroundSlideIndicators == null)
        {
            return;
        }

        for (int i = 0; i < backgroundSlideIndicators.Length; i++)
        {
            Image indicator = backgroundSlideIndicators[i];
            if (indicator == null)
            {
                continue;
            }

            indicator.color = i == activeIndex ? activeSlideIndicatorColor : inactiveSlideIndicatorColor;
        }
    }

    private void ResolveBackgroundSlideIndicatorSupportReferences()
    {
        if (backgroundSlideIndicators != null)
        {
            for (int i = 0; i < backgroundSlideIndicators.Length; i++)
            {
                Image indicator = backgroundSlideIndicators[i];
                if (indicator == null)
                {
                    continue;
                }

                if (backgroundSlideIndicatorContainer == null)
                {
                    backgroundSlideIndicatorContainer = indicator.transform.parent as RectTransform;
                }

                if (backgroundSlideIndicatorTemplate == null)
                {
                    backgroundSlideIndicatorTemplate = indicator;
                }

                if (backgroundSlideIndicatorContainer != null && backgroundSlideIndicatorTemplate != null)
                {
                    break;
                }
            }
        }

        if (backgroundSlideIndicatorContainer == null && backgroundSlideIndicatorTemplate != null)
        {
            backgroundSlideIndicatorContainer = backgroundSlideIndicatorTemplate.transform.parent as RectTransform;
        }
    }

    private void SyncBackgroundSlideIndicators()
    {
        int slideCount = backgroundSlides != null ? backgroundSlides.Length : 0;
        if (slideCount <= 0)
        {
            backgroundSlideIndicators = System.Array.Empty<Image>();
            return;
        }

        ResolveBackgroundSlideIndicatorSupportReferences();
        if (backgroundSlideIndicatorContainer == null || backgroundSlideIndicatorTemplate == null)
        {
            return;
        }

        List<Image> indicators = CollectBackgroundSlideIndicators();
        if (indicators.Count == 0)
        {
            indicators.Add(backgroundSlideIndicatorTemplate);
        }

        while (indicators.Count < slideCount)
        {
            Image clonedIndicator = Instantiate(backgroundSlideIndicatorTemplate, backgroundSlideIndicatorContainer);
            clonedIndicator.gameObject.name = $"Barh{indicators.Count + 1}";
            clonedIndicator.gameObject.SetActive(true);
            clonedIndicator.transform.SetAsLastSibling();
            indicators.Add(clonedIndicator);
        }

        for (int i = 0; i < indicators.Count; i++)
        {
            Image indicator = indicators[i];
            if (indicator == null)
            {
                continue;
            }

            bool shouldBeActive = i < slideCount;
            indicator.gameObject.SetActive(shouldBeActive);
            indicator.color = inactiveSlideIndicatorColor;
        }

        if (backgroundSlideIndicatorContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(backgroundSlideIndicatorContainer);
        }

        backgroundSlideIndicators = indicators.GetRange(0, slideCount).ToArray();
    }

    private List<Image> CollectBackgroundSlideIndicators()
    {
        var indicators = new List<Image>();
        if (backgroundSlideIndicatorContainer == null)
        {
            return indicators;
        }

        for (int i = 0; i < backgroundSlideIndicatorContainer.childCount; i++)
        {
            Transform child = backgroundSlideIndicatorContainer.GetChild(i);
            if (child == null)
            {
                continue;
            }

            Image indicator = child.GetComponent<Image>();
            if (indicator == null)
            {
                continue;
            }

            indicators.Add(indicator);
        }

        return indicators;
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

    private static T FindComponentByName<T>(Transform root, string objectName) where T : Component
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in transforms)
        {
            if (child == null || child.name != objectName)
            {
                continue;
            }

            T component = child.GetComponent<T>();
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static Color MultiplyRgb(Color color, float multiplier)
    {
        color.r *= multiplier;
        color.g *= multiplier;
        color.b *= multiplier;
        return color;
    }
}
