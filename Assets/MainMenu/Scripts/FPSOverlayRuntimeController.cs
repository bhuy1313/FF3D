using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-990)]
public sealed class FPSOverlayRuntimeController : MonoBehaviour
{
    private const string RuntimeObjectName = "__FPSOverlayRuntimeController";
    private const string SceneOverlayRootName = "FPSOverlayController";
    private const string SceneOverlayTextName = "FPSText";
    private const string OverlayPrefabResourcePath = "FPSOverlayController";
    private const string GeneratedOverlayRootName = "__GeneratedFPSOverlay";
    private const int OverlaySortingOrder = 1000;

    [SerializeField] private float refreshInterval = 0.25f;

    private static FPSOverlayRuntimeController instance;

    private GameObject overlayRoot;
    private CanvasGroup overlayCanvasGroup;
    private TMP_Text overlayText;
    private float sampleElapsed;
    private int sampleFrames;

    public static void EnsureCreated()
    {
        if (instance != null)
        {
            return;
        }

        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(runtimeObject);
        instance = runtimeObject.AddComponent<FPSOverlayRuntimeController>();
    }

    public static void SetOverlayVisible(bool visible)
    {
        EnsureCreated();
        instance.ResolveOverlayPresentation();
        instance.ApplyOverlayVisibility(visible);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        ResolveOverlayPresentation();
        DisableSceneOverlayDuplicates();
        ApplyVisibilityFromSavedSettings();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }

    private void Update()
    {
        if (overlayRoot == null || !overlayRoot.activeInHierarchy || overlayText == null)
        {
            return;
        }

        sampleElapsed += Time.unscaledDeltaTime;
        sampleFrames++;

        if (sampleElapsed < refreshInterval)
        {
            return;
        }

        float fps = sampleFrames / Mathf.Max(sampleElapsed, 0.0001f);
        float frameMs = 1000f / Mathf.Max(fps, 0.0001f);
        overlayText.SetText("{0:0} | {1:0.0} ms", fps, frameMs);

        sampleElapsed = 0f;
        sampleFrames = 0;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveOverlayPresentation();
        DisableSceneOverlayDuplicates();
        ApplyVisibilityFromSavedSettings();
    }

    private void ResolveOverlayPresentation()
    {
        if (overlayRoot == null)
        {
            if (!TryCreateOverlayFromPrefab())
            {
                CreateGeneratedOverlay();
            }
        }
        else
        {
            if (overlayCanvasGroup == null)
            {
                overlayCanvasGroup = overlayRoot.GetComponent<CanvasGroup>();
            }

            if (overlayText != null)
            {
                return;
            }

            overlayCanvasGroup = overlayRoot.GetComponent<CanvasGroup>();
            overlayText = FindSceneOverlayText(overlayRoot);
        }
    }

    private void ApplyVisibilityFromSavedSettings()
    {
        if (DisplaySettingsService.TryGetSavedShowFpsOverlay(out bool showFps))
        {
            ApplyOverlayVisibility(showFps);
            return;
        }

        if (overlayRoot != null)
        {
            ApplyOverlayVisibility(IsOverlayCurrentlyVisible());
        }
    }

    private void ApplyOverlayVisibility(bool visible)
    {
        if (overlayRoot == null)
        {
            return;
        }

        if (overlayCanvasGroup != null)
        {
            overlayRoot.SetActive(true);
            overlayCanvasGroup.alpha = visible ? 1f : 0f;
            overlayCanvasGroup.interactable = false;
            overlayCanvasGroup.blocksRaycasts = false;
        }
        else
        {
            overlayRoot.SetActive(visible);
        }

        if (!visible)
        {
            sampleElapsed = 0f;
            sampleFrames = 0;
        }
    }

    private bool IsOverlayCurrentlyVisible()
    {
        if (overlayRoot == null)
        {
            return false;
        }

        if (overlayCanvasGroup != null)
        {
            return overlayCanvasGroup.alpha > 0.001f;
        }

        return overlayRoot.activeSelf;
    }

    private bool TryCreateOverlayFromPrefab()
    {
        GameObject overlayPrefab = Resources.Load<GameObject>(OverlayPrefabResourcePath);
        if (overlayPrefab == null)
        {
            return false;
        }

        overlayRoot = Instantiate(overlayPrefab);
        overlayRoot.name = SceneOverlayRootName;
        DontDestroyOnLoad(overlayRoot);

        ConfigureOverlayCanvas(overlayRoot);
        overlayCanvasGroup = overlayRoot.GetComponent<CanvasGroup>();
        overlayText = FindSceneOverlayText(overlayRoot);
        return true;
    }

    private TMP_Text FindSceneOverlayText(GameObject root)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform child = transforms[i];
            if (child != null && child.name == SceneOverlayTextName)
            {
                TMP_Text namedText = child.GetComponent<TMP_Text>();
                if (namedText != null)
                {
                    return namedText;
                }
            }
        }

        return root.GetComponentInChildren<TMP_Text>(true);
    }

    private void DisableSceneOverlayDuplicates()
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject root = roots[rootIndex];
                if (root == null)
                {
                    continue;
                }

                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                {
                    Transform child = transforms[transformIndex];
                    if (child == null || child.gameObject == overlayRoot || child.name != SceneOverlayRootName)
                    {
                        continue;
                    }

                    child.gameObject.SetActive(false);
                }
            }
        }
    }

    private void ConfigureOverlayCanvas(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Canvas canvas = root.GetComponent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = OverlaySortingOrder;
    }

    private void CreateGeneratedOverlay()
    {
        overlayRoot = new GameObject(GeneratedOverlayRootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        DontDestroyOnLoad(overlayRoot);

        Canvas canvas = overlayRoot.GetComponent<Canvas>();
        canvas.pixelPerfect = false;
        ConfigureOverlayCanvas(overlayRoot);

        overlayCanvasGroup = overlayRoot.GetComponent<CanvasGroup>();
        overlayCanvasGroup.alpha = 1f;
        overlayCanvasGroup.interactable = false;
        overlayCanvasGroup.blocksRaycasts = false;

        CanvasScaler scaler = overlayRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform rootRect = overlayRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        GameObject textObject = new GameObject("FPS Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(overlayRoot.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(1f, 0f);
        textRect.anchorMax = new Vector2(1f, 0f);
        textRect.pivot = new Vector2(1f, 0f);
        textRect.anchoredPosition = new Vector2(-16f, 16f);
        textRect.sizeDelta = new Vector2(180f, 56f);

        overlayText = textObject.GetComponent<TextMeshProUGUI>();
        overlayText.alignment = TextAlignmentOptions.BottomRight;
        overlayText.fontSize = 20f;
        overlayText.textWrappingMode = TextWrappingModes.NoWrap;
        overlayText.text = "-- | --.- ms";
        overlayText.color = Color.white;
        overlayText.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
        {
            overlayText.font = TMP_Settings.defaultFontAsset;
        }

    }
}
