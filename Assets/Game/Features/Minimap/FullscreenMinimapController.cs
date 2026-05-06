using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using StarterAssets;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class FullscreenMinimapController : MonoBehaviour
{
    [System.Serializable]
    private sealed class LegendEntry
    {
        public bool enabled = true;
        public Sprite iconSprite;
        public string label;
        public Color iconColor = Color.white;
    }

    public bool IsFullscreenOpen => isFullscreenOpen;

    [Header("References")]
    [SerializeField] private GameObject fullscreenRoot;
    [SerializeField] private RectTransform mapFrame;
    [SerializeField] private GameObject minimapSquare;
    [SerializeField] private GameObject minimapCircle;
    [SerializeField] private Camera minimapCamera;
    [SerializeField] private MinimapCameraFollow minimapCameraFollow;
    [SerializeField] private MinimapCompassController minimapCompassController;
    [SerializeField] private Image dimmerImage;
    [SerializeField] private GameObject headerObject;
    [SerializeField] private GameObject footerHintObject;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private GameObject legendPanelObject;
    [SerializeField] private RectTransform legendContentRoot;
    [SerializeField] private TMP_Text legendTitleText;
    [SerializeField] private GameObject legendRowTemplateObject;
    [SerializeField] private RenderTexture fullscreenRenderTexture;
    [SerializeField] private GameObject playerRoot;
    [SerializeField] private StarterAssetsInputs starterAssetsInputs;
    [SerializeField] private FPSCommandSystem commandSystem;
    [SerializeField] private PlayerActionLock playerActionLock;
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private PlayerInput playerInput;
#endif

    [Header("Auto Resolve")]
    [SerializeField] private string fullscreenRootName = "FullscreenMinimap";
    [SerializeField] private string mapFrameName = "MapFrame";
    [SerializeField] private string minimapSquareObjectName = "MinimapSquare";
    [SerializeField] private string minimapCircleObjectName = "MinimapCircle";
    [SerializeField] private string minimapCameraObjectName = "MinimapCamera";
    [SerializeField] private string dimmerObjectName = "Dimmer";
    [SerializeField] private string headerObjectName = "Header";
    [SerializeField] private string footerHintObjectName = "FooterHint";
    [SerializeField] private string titleTextObjectName = "TitleText";
    [SerializeField] private string hintTextObjectName = "HintText";
    [SerializeField] private string legendPanelObjectName = "LegendPanel";
    [SerializeField] private string legendTitleTextObjectName = "LegendTitleText";
    [SerializeField] private string legendContentObjectName = "LegendContent";
    [SerializeField] private string legendTitleTextFallbackObjectName = "LegendTittleText";
    [SerializeField] private string legendRowTemplateObjectName = "LegendRow_0";

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.M;
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Header("Fullscreen")]
    [SerializeField] private bool northUpInFullscreen = true;
    [SerializeField] private bool enableScrollZoom = true;
    [SerializeField] private float fullscreenOrthographicSize = 50f;
    [SerializeField] private float minOrthographicSize = 20f;
    [SerializeField] private float maxOrthographicSize = 90f;
    [SerializeField] private float scrollZoomSpeed = 16f;
    [SerializeField] [Range(0f, 1f)] private float fullscreenDimmerAlpha = 0.48f;
    [SerializeField] private float fullscreenCompassScale = 1.35f;
    [SerializeField] private int fullscreenTextureSize = 2048;
    [SerializeField] private string fullscreenTitle = "BẢN ĐỒ";
    [SerializeField] private string fullscreenHint = "M - Đóng | Cuộn chuột - Thu phóng";

    [Header("Legend")]
    [SerializeField] private bool showLegend = true;
    [SerializeField] private string legendTitle = "LEGEND";
    [SerializeField] private Vector2 legendPanelSize = new Vector2(280f, 240f);
    [SerializeField] private Vector2 legendPanelAnchoredPosition = new Vector2(28f, -124f);
    [SerializeField] private Vector2 legendIconSize = new Vector2(22f, 22f);
    [SerializeField] private float legendRowSpacing = 8f;
    [SerializeField] private Color legendPanelColor = new Color(0.08f, 0.08f, 0.08f, 0.86f);
    [SerializeField] private Color legendLabelColor = Color.white;
    [SerializeField] private List<LegendEntry> legendEntries = new List<LegendEntry>();

    private HostedMinimapState hostedMinimap;
    private RawImageTextureState[] rawImageStates = System.Array.Empty<RawImageTextureState>();
    private bool isFullscreenOpen;
    private bool savedFollowTargetYaw;
    private bool hasSavedFollowTargetYaw;
    private bool hasSavedCameraSize;
    private bool hasSavedMiniRenderState;
    private bool isGameplayLocked;
    private bool restoreCursorLocked;
    private bool restoreCursorInputForLook;
    private bool restoreCommandSystemEnabled;
    private CursorLockMode restoreCursorLockMode;
    private bool restoreCursorVisible;
    private float savedOrthographicSize;
    private float savedDimmerAlpha = -1f;
    private RenderTexture savedCameraTargetTexture;
    private RenderTexture runtimeFullscreenRenderTexture;
    private bool legendPanelWasAutoCreated;

    private void Awake()
    {
        ResolveReferences();
        RestoreClosedState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RestoreClosedState();
    }

    private void OnDisable()
    {
        if (isFullscreenOpen)
        {
            TeardownTransientFullscreenState();
        }
    }

    private void Update()
    {
        ResolveReferences();
        if (WasToggleFullscreenPressed())
        {
            ToggleFullscreen();
        }

        if (!isFullscreenOpen)
        {
            return;
        }

        if (WasCloseFullscreenPressed())
        {
            CloseFullscreen();
            return;
        }

        GameObject displayedMinimap = ResolveDisplayedMinimap();
        if (displayedMinimap != hostedMinimap.mapObject)
        {
            HostDisplayedMinimap();
        }

        HandleScrollZoom();
    }

    public void ToggleFullscreen()
    {
        if (isFullscreenOpen)
        {
            CloseFullscreen();
            return;
        }

        OpenFullscreen();
    }

    public void OpenFullscreen()
    {
        ResolveReferences();
        if (fullscreenRoot == null || mapFrame == null)
        {
            return;
        }

        fullscreenRoot.SetActive(true);
        (fullscreenRoot.transform as RectTransform)?.SetAsLastSibling();
        PrepareFullscreenVisuals();
        HostDisplayedMinimap();
        ApplyFullscreenRenderTexture();
        ApplyFullscreenCameraState();
        AcquireGameplayLock();
        isFullscreenOpen = true;
    }

    public void CloseFullscreen()
    {
        RestoreHostedMinimap();
        TeardownTransientFullscreenState();

        if (fullscreenRoot != null)
        {
            fullscreenRoot.SetActive(false);
        }

        isFullscreenOpen = false;
    }

    private void ResolveReferences()
    {
        if (fullscreenRoot == null)
        {
            Transform fullscreenTransform = FindDescendant(transform, fullscreenRootName);
            if (fullscreenTransform != null)
            {
                fullscreenRoot = fullscreenTransform.gameObject;
            }
        }

        if (mapFrame == null && fullscreenRoot != null)
        {
            Transform mapFrameTransform = FindDescendant(fullscreenRoot.transform, mapFrameName);
            if (mapFrameTransform != null)
            {
                mapFrame = mapFrameTransform as RectTransform;
            }
        }

        if (minimapSquare == null)
        {
            Transform squareTransform = FindDescendant(transform, minimapSquareObjectName);
            if (squareTransform != null)
            {
                minimapSquare = squareTransform.gameObject;
            }
        }

        if (minimapCircle == null)
        {
            Transform circleTransform = FindDescendant(transform, minimapCircleObjectName);
            if (circleTransform != null)
            {
                minimapCircle = circleTransform.gameObject;
            }
        }

        if (minimapCamera == null)
        {
            GameObject minimapCameraObject = GameObject.Find(minimapCameraObjectName);
            if (minimapCameraObject != null)
            {
                minimapCamera = minimapCameraObject.GetComponent<Camera>();
                if (minimapCameraFollow == null)
                {
                    minimapCameraFollow = minimapCameraObject.GetComponent<MinimapCameraFollow>();
                }
            }
        }

        if (minimapCompassController == null)
        {
            minimapCompassController = GetComponent<MinimapCompassController>();
        }

        if (playerRoot == null)
        {
            FirstPersonController firstPersonController = FindAnyObjectByType<FirstPersonController>();
            if (firstPersonController != null)
            {
                playerRoot = firstPersonController.gameObject;
            }
        }

        if (starterAssetsInputs == null && playerRoot != null)
        {
            starterAssetsInputs = playerRoot.GetComponent<StarterAssetsInputs>();
        }

        if (starterAssetsInputs == null)
        {
            starterAssetsInputs = FindAnyObjectByType<StarterAssetsInputs>();
        }

#if ENABLE_INPUT_SYSTEM
        if (playerInput == null && playerRoot != null)
        {
            playerInput = playerRoot.GetComponent<PlayerInput>();
        }

        if (playerInput == null && starterAssetsInputs != null)
        {
            playerInput = starterAssetsInputs.GetComponent<PlayerInput>();
        }

        if (playerInput == null)
        {
            playerInput = FindAnyObjectByType<PlayerInput>();
        }
#endif

        if (commandSystem == null && playerRoot != null)
        {
            commandSystem = playerRoot.GetComponent<FPSCommandSystem>();
        }

        if (commandSystem == null)
        {
            commandSystem = FindAnyObjectByType<FPSCommandSystem>();
        }

        if (playerActionLock == null && playerRoot != null)
        {
            playerActionLock = PlayerActionLock.GetOrCreate(playerRoot);
        }

        if (fullscreenRoot != null)
        {
            if (dimmerImage == null)
            {
                Transform dimmerTransform = FindDescendant(fullscreenRoot.transform, dimmerObjectName);
                if (dimmerTransform != null)
                {
                    dimmerImage = dimmerTransform.GetComponent<Image>();
                }
            }

            if (headerObject == null)
            {
                Transform headerTransform = FindDescendant(fullscreenRoot.transform, headerObjectName);
                if (headerTransform != null)
                {
                    headerObject = headerTransform.gameObject;
                }
            }

            if (footerHintObject == null)
            {
                Transform footerTransform = FindDescendant(fullscreenRoot.transform, footerHintObjectName);
                if (footerTransform != null)
                {
                    footerHintObject = footerTransform.gameObject;
                }
            }

            if (titleText == null)
            {
                Transform titleTransform = FindDescendant(fullscreenRoot.transform, titleTextObjectName);
                if (titleTransform != null)
                {
                    titleText = titleTransform.GetComponent<TMP_Text>();
                }
            }

            if (hintText == null)
            {
                Transform hintTransform = FindDescendant(fullscreenRoot.transform, hintTextObjectName);
                if (hintTransform != null)
                {
                    hintText = hintTransform.GetComponent<TMP_Text>();
                }
            }

            EnsureLegendUi();
        }
    }

    private void RestoreClosedState()
    {
        RestoreHostedMinimap();
        TeardownTransientFullscreenState();

        if (fullscreenRoot != null)
        {
            fullscreenRoot.SetActive(false);
        }
    }

    private void TeardownTransientFullscreenState()
    {
        RestoreMiniCameraState();
        RestoreMiniRenderTexture();
        RestoreMiniVisuals();
        ReleaseGameplayLock();
        isFullscreenOpen = false;
    }

    private void HostDisplayedMinimap()
    {
        RestoreHostedMinimap();

        if (mapFrame == null)
        {
            return;
        }

        GameObject displayedMinimap = ResolveDisplayedMinimap();
        if (displayedMinimap == null)
        {
            return;
        }

        RectTransform minimapRect = displayedMinimap.transform as RectTransform;
        if (minimapRect == null)
        {
            return;
        }

        hostedMinimap = HostedMinimapState.Capture(displayedMinimap, minimapRect);
        minimapRect.SetParent(mapFrame, false);
        minimapRect.anchorMin = Vector2.zero;
        minimapRect.anchorMax = Vector2.one;
        minimapRect.anchoredPosition = Vector2.zero;
        minimapRect.sizeDelta = Vector2.zero;
        minimapRect.offsetMin = Vector2.zero;
        minimapRect.offsetMax = Vector2.zero;
        minimapRect.pivot = new Vector2(0.5f, 0.5f);
        minimapRect.localScale = Vector3.one;
        minimapRect.localRotation = Quaternion.identity;

        if (minimapCompassController != null)
        {
            minimapCompassController.RefreshNow();
        }
    }

    private void RestoreHostedMinimap()
    {
        if (!hostedMinimap.IsValid)
        {
            return;
        }

        hostedMinimap.Restore();
        hostedMinimap = default;
    }

    private GameObject ResolveDisplayedMinimap()
    {
        if (minimapSquare != null && minimapSquare.activeInHierarchy)
        {
            return minimapSquare;
        }

        if (minimapCircle != null && minimapCircle.activeInHierarchy)
        {
            return minimapCircle;
        }

        if (minimapSquare != null && minimapSquare.activeSelf)
        {
            return minimapSquare;
        }

        if (minimapCircle != null && minimapCircle.activeSelf)
        {
            return minimapCircle;
        }

        return minimapSquare != null ? minimapSquare : minimapCircle;
    }

    private void ApplyFullscreenCameraState()
    {
        if (minimapCamera != null && minimapCamera.orthographic)
        {
            savedOrthographicSize = minimapCamera.orthographicSize;
            hasSavedCameraSize = true;
            minimapCamera.orthographicSize = Mathf.Clamp(
                fullscreenOrthographicSize,
                minOrthographicSize,
                maxOrthographicSize);
        }

        if (minimapCameraFollow != null && northUpInFullscreen)
        {
            savedFollowTargetYaw = minimapCameraFollow.FollowTargetYaw;
            hasSavedFollowTargetYaw = true;
            minimapCameraFollow.SetFollowTargetYaw(false);
        }
    }

    private void RestoreMiniCameraState()
    {
        if (minimapCamera != null && minimapCamera.orthographic && hasSavedCameraSize)
        {
            minimapCamera.orthographicSize = savedOrthographicSize;
        }

        if (minimapCameraFollow != null && northUpInFullscreen && hasSavedFollowTargetYaw)
        {
            minimapCameraFollow.SetFollowTargetYaw(savedFollowTargetYaw);
            hasSavedFollowTargetYaw = false;
        }
    }

    private void PrepareFullscreenVisuals()
    {
        if (headerObject != null)
        {
            headerObject.SetActive(true);
        }

        if (footerHintObject != null)
        {
            footerHintObject.SetActive(true);
        }

        if (titleText != null && !string.IsNullOrWhiteSpace(fullscreenTitle))
        {
            titleText.text = fullscreenTitle;
        }

        if (hintText != null && !string.IsNullOrWhiteSpace(fullscreenHint))
        {
            hintText.text = fullscreenHint;
        }

        UpdateLegendVisuals();

        if (dimmerImage != null)
        {
            if (savedDimmerAlpha < 0f)
            {
                savedDimmerAlpha = dimmerImage.color.a;
            }

            Color dimmerColor = dimmerImage.color;
            dimmerColor.a = fullscreenDimmerAlpha;
            dimmerImage.color = dimmerColor;
        }

        if (minimapCompassController != null)
        {
            minimapCompassController.SetVisualScale(fullscreenCompassScale);
        }
    }

    private void RestoreMiniVisuals()
    {
        if (minimapCompassController != null)
        {
            minimapCompassController.SetVisualScale(1f);
            if (isActiveAndEnabled && gameObject.activeInHierarchy)
            {
                minimapCompassController.RefreshNow();
            }
        }

        if (dimmerImage != null && savedDimmerAlpha >= 0f)
        {
            Color dimmerColor = dimmerImage.color;
            dimmerColor.a = savedDimmerAlpha;
            dimmerImage.color = dimmerColor;
        }
    }

    private void AcquireGameplayLock()
    {
        if (isGameplayLocked)
        {
            return;
        }

        if (playerActionLock != null)
        {
            playerActionLock.AcquireFullLock();
        }

        if (commandSystem != null)
        {
            restoreCommandSystemEnabled = commandSystem.enabled;
            commandSystem.enabled = false;
        }

        if (starterAssetsInputs != null)
        {
            restoreCursorLocked = starterAssetsInputs.cursorLocked;
            restoreCursorInputForLook = starterAssetsInputs.cursorInputForLook;
            starterAssetsInputs.move = Vector2.zero;
            starterAssetsInputs.look = Vector2.zero;
            starterAssetsInputs.cursorLocked = false;
            starterAssetsInputs.cursorInputForLook = false;
            starterAssetsInputs.ClearGameplayActionInputs();
        }

        restoreCursorLockMode = Cursor.lockState;
        restoreCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        isGameplayLocked = true;
    }

    private void ReleaseGameplayLock()
    {
        if (!isGameplayLocked)
        {
            return;
        }

        if (playerActionLock != null)
        {
            playerActionLock.ReleaseFullLock();
        }

        if (commandSystem != null)
        {
            commandSystem.enabled = restoreCommandSystemEnabled;
        }

        if (starterAssetsInputs != null)
        {
            starterAssetsInputs.move = Vector2.zero;
            starterAssetsInputs.look = Vector2.zero;
            starterAssetsInputs.ClearGameplayActionInputs();
            starterAssetsInputs.cursorLocked = restoreCursorLocked;
            starterAssetsInputs.cursorInputForLook = restoreCursorInputForLook;
        }

        Cursor.lockState = restoreCursorLockMode;
        Cursor.visible = restoreCursorVisible;
        isGameplayLocked = false;
    }

    private void ApplyFullscreenRenderTexture()
    {
        if (minimapCamera == null)
        {
            return;
        }

        CaptureMiniRenderState();

        RenderTexture targetFullscreenTexture = GetOrCreateFullscreenRenderTexture();
        if (targetFullscreenTexture == null)
        {
            return;
        }

        minimapCamera.targetTexture = targetFullscreenTexture;
        for (int index = 0; index < rawImageStates.Length; index++)
        {
            if (rawImageStates[index].rawImage != null)
            {
                rawImageStates[index].rawImage.texture = targetFullscreenTexture;
            }
        }
    }

    private void RestoreMiniRenderTexture()
    {
        if (!hasSavedMiniRenderState || minimapCamera == null)
        {
            return;
        }

        minimapCamera.targetTexture = savedCameraTargetTexture;
        for (int index = 0; index < rawImageStates.Length; index++)
        {
            if (rawImageStates[index].rawImage != null)
            {
                rawImageStates[index].rawImage.texture = rawImageStates[index].originalTexture;
            }
        }
    }

    private void CaptureMiniRenderState()
    {
        if (hasSavedMiniRenderState)
        {
            return;
        }

        savedCameraTargetTexture = minimapCamera != null ? minimapCamera.targetTexture : null;
        RawImage[] rawImages = GetComponentsInChildren<RawImage>(true);
        rawImageStates = new RawImageTextureState[rawImages.Length];
        for (int index = 0; index < rawImages.Length; index++)
        {
            rawImageStates[index] = new RawImageTextureState
            {
                rawImage = rawImages[index],
                originalTexture = rawImages[index] != null ? rawImages[index].texture : null
            };
        }

        hasSavedMiniRenderState = true;
    }

    private RenderTexture GetOrCreateFullscreenRenderTexture()
    {
        if (fullscreenRenderTexture != null)
        {
            return fullscreenRenderTexture;
        }

        int textureSize = Mathf.Max(1024, fullscreenTextureSize);
        if (runtimeFullscreenRenderTexture != null
            && runtimeFullscreenRenderTexture.width == textureSize
            && runtimeFullscreenRenderTexture.height == textureSize)
        {
            return runtimeFullscreenRenderTexture;
        }

        if (runtimeFullscreenRenderTexture != null)
        {
            runtimeFullscreenRenderTexture.Release();
            Destroy(runtimeFullscreenRenderTexture);
        }

        runtimeFullscreenRenderTexture = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32)
        {
            name = "RT_Minimap_Fullscreen_Runtime",
            antiAliasing = 1,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false
        };
        runtimeFullscreenRenderTexture.Create();
        return runtimeFullscreenRenderTexture;
    }

    private void HandleScrollZoom()
    {
        if (!enableScrollZoom || minimapCamera == null || !minimapCamera.orthographic)
        {
            return;
        }

        float scrollDelta = ReadScrollZoomDelta();
        if (Mathf.Abs(scrollDelta) <= Mathf.Epsilon)
        {
            return;
        }

        float nextSize = minimapCamera.orthographicSize - (scrollDelta * scrollZoomSpeed);
        minimapCamera.orthographicSize = Mathf.Clamp(nextSize, minOrthographicSize, maxOrthographicSize);
    }

    private bool WasToggleFullscreenPressed()
    {
        if (starterAssetsInputs != null && starterAssetsInputs.toggleFullscreenMinimap)
        {
            starterAssetsInputs.toggleFullscreenMinimap = false;
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        if (TryGetPlayerAction("ToggleFullscreenMinimap", out InputAction action) && action.WasPressedThisFrame())
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(toggleKey);
#else
        return false;
#endif
    }

    private bool WasCloseFullscreenPressed()
    {
        if (starterAssetsInputs != null && starterAssetsInputs.closeFullscreenMinimap)
        {
            starterAssetsInputs.closeFullscreenMinimap = false;
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        if (TryGetPlayerAction("CloseFullscreenMinimap", out InputAction action) && action.WasPressedThisFrame())
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(closeKey);
#else
        return false;
#endif
    }

    private float ReadScrollZoomDelta()
    {
#if ENABLE_INPUT_SYSTEM
        if (TryGetPlayerAction("MinimapScrollZoom", out InputAction action))
        {
            float value = action.ReadValue<float>();
            if (Mathf.Abs(value) > Mathf.Epsilon)
            {
                return value;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mouseScrollDelta.y;
#else
        return 0f;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private bool TryGetPlayerAction(string actionName, out InputAction action)
    {
        action = null;
        if (playerInput == null || playerInput.actions == null)
        {
            return false;
        }

        action = playerInput.actions.FindAction(actionName, throwIfNotFound: false);
        return action != null;
    }
#endif

    private void EnsureLegendUi()
    {
        if (fullscreenRoot == null)
        {
            return;
        }

        if (legendPanelObject == null)
        {
            Transform panelTransform = FindDescendant(fullscreenRoot.transform, legendPanelObjectName);
            if (panelTransform != null)
            {
                legendPanelObject = panelTransform.gameObject;
            }
        }

        if (legendPanelObject == null)
        {
            legendPanelObject = CreateUiGameObject(fullscreenRoot.transform, legendPanelObjectName);
            RectTransform panelRect = legendPanelObject.transform as RectTransform;
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0f, 1f);
                panelRect.anchorMax = new Vector2(0f, 1f);
                panelRect.pivot = new Vector2(0f, 1f);
                panelRect.anchoredPosition = legendPanelAnchoredPosition;
                panelRect.sizeDelta = legendPanelSize;
            }

            Image panelImage = legendPanelObject.AddComponent<Image>();
            panelImage.color = legendPanelColor;
            panelImage.raycastTarget = false;

            VerticalLayoutGroup panelLayout = legendPanelObject.AddComponent<VerticalLayoutGroup>();
            panelLayout.childAlignment = TextAnchor.UpperLeft;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = false;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;
            panelLayout.spacing = 10f;
            panelLayout.padding = new RectOffset(14, 14, 14, 14);

            legendPanelWasAutoCreated = true;
        }

        RectTransform panelRoot = legendPanelObject.transform as RectTransform;
        if (panelRoot == null)
        {
            return;
        }

        if (legendTitleText == null)
        {
            Transform titleTransform = FindDescendant(panelRoot, legendTitleTextObjectName);
            if (titleTransform == null && !string.IsNullOrWhiteSpace(legendTitleTextFallbackObjectName))
            {
                titleTransform = FindDescendant(panelRoot, legendTitleTextFallbackObjectName);
            }

            if (titleTransform != null)
            {
                legendTitleText = titleTransform.GetComponent<TMP_Text>();
            }
        }

        if (legendTitleText == null)
        {
            GameObject titleObject = CreateUiGameObject(panelRoot, legendTitleTextObjectName);
            LayoutElement titleLayout = titleObject.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 32f;
            legendTitleText = titleObject.AddComponent<TextMeshProUGUI>();
        }

        if (legendContentRoot == null)
        {
            Transform contentTransform = FindDescendant(panelRoot, legendContentObjectName);
            if (contentTransform != null)
            {
                legendContentRoot = contentTransform as RectTransform;
            }
        }

        if (legendContentRoot == null)
        {
            GameObject contentObject = CreateUiGameObject(panelRoot, legendContentObjectName);
            legendContentRoot = contentObject.transform as RectTransform;

            LayoutElement contentLayout = contentObject.AddComponent<LayoutElement>();
            contentLayout.flexibleHeight = 1f;

            VerticalLayoutGroup contentGroup = contentObject.AddComponent<VerticalLayoutGroup>();
            contentGroup.childAlignment = TextAnchor.UpperLeft;
            contentGroup.childControlWidth = true;
            contentGroup.childControlHeight = false;
            contentGroup.childForceExpandWidth = true;
            contentGroup.childForceExpandHeight = false;
            contentGroup.spacing = legendRowSpacing;

            ContentSizeFitter contentFitter = contentObject.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        if (legendRowTemplateObject == null && legendContentRoot != null)
        {
            Transform templateTransform = FindDescendant(legendContentRoot, legendRowTemplateObjectName);
            if (templateTransform != null)
            {
                legendRowTemplateObject = templateTransform.gameObject;
            }
        }

        if (legendRowTemplateObject != null)
        {
            legendRowTemplateObject.SetActive(false);
        }

        ApplyLegendLayoutStyling();
    }

    private void ApplyLegendLayoutStyling()
    {
        if (legendPanelObject == null)
        {
            return;
        }

        if (legendPanelWasAutoCreated)
        {
            RectTransform panelRect = legendPanelObject.transform as RectTransform;
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0f, 1f);
                panelRect.anchorMax = new Vector2(0f, 1f);
                panelRect.pivot = new Vector2(0f, 1f);
                panelRect.anchoredPosition = legendPanelAnchoredPosition;
                panelRect.sizeDelta = legendPanelSize;
            }
        }

        Image panelImage = legendPanelObject.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.raycastTarget = false;
        }

        if (legendTitleText != null)
        {
            legendTitleText.text = legendTitle;
            legendTitleText.color = legendLabelColor;
            legendTitleText.textWrappingMode = TextWrappingModes.NoWrap;
            legendTitleText.raycastTarget = false;

            if (legendTitleText.font == null && hintText != null && hintText.font != null)
            {
                legendTitleText.font = hintText.font;
            }
        }
    }

    private void UpdateLegendVisuals()
    {
        EnsureLegendUi();

        bool shouldShowLegend = showLegend && HasVisibleLegendEntries();
        if (legendPanelObject != null)
        {
            legendPanelObject.SetActive(shouldShowLegend);
        }

        if (shouldShowLegend)
        {
            RebuildLegendRows();
        }
    }

    private bool HasVisibleLegendEntries()
    {
        for (int index = 0; index < legendEntries.Count; index++)
        {
            LegendEntry entry = legendEntries[index];
            if (entry != null && entry.enabled && (!string.IsNullOrWhiteSpace(entry.label) || entry.iconSprite != null))
            {
                return true;
            }
        }

        return false;
    }

    private void RebuildLegendRows()
    {
        if (legendContentRoot == null)
        {
            return;
        }

        ClearLegendRows();

        for (int index = 0; index < legendEntries.Count; index++)
        {
            LegendEntry entry = legendEntries[index];
            if (entry == null || !entry.enabled || (entry.iconSprite == null && string.IsNullOrWhiteSpace(entry.label)))
            {
                continue;
            }

            CreateLegendRow(entry, index);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(legendContentRoot);
        if (legendPanelObject != null)
        {
            RectTransform panelRect = legendPanelObject.transform as RectTransform;
            if (panelRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            }
        }
    }

    private void ClearLegendRows()
    {
        if (legendContentRoot == null)
        {
            return;
        }

        for (int index = legendContentRoot.childCount - 1; index >= 0; index--)
        {
            Transform child = legendContentRoot.GetChild(index);
            if (legendRowTemplateObject != null && child.gameObject == legendRowTemplateObject)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void CreateLegendRow(LegendEntry entry, int index)
    {
        if (TryCreateLegendRowFromTemplate(entry, index))
        {
            return;
        }

        GameObject rowObject = CreateUiGameObject(legendContentRoot, $"LegendRow_{index}");
        LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = Mathf.Max(legendIconSize.y, 24f);

        HorizontalLayoutGroup rowGroup = rowObject.AddComponent<HorizontalLayoutGroup>();
        rowGroup.childAlignment = TextAnchor.MiddleLeft;
        rowGroup.childControlWidth = false;
        rowGroup.childControlHeight = false;
        rowGroup.childForceExpandWidth = false;
        rowGroup.childForceExpandHeight = false;
        rowGroup.spacing = 10f;

        GameObject iconObject = CreateUiGameObject(rowObject.transform, "Icon");
        LayoutElement iconLayout = iconObject.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = legendIconSize.x;
        iconLayout.preferredHeight = legendIconSize.y;

        Image iconImage = iconObject.AddComponent<Image>();
        iconImage.sprite = entry.iconSprite;
        iconImage.color = entry.iconColor;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        GameObject labelObject = CreateUiGameObject(rowObject.transform, "Label");
        LayoutElement labelLayout = labelObject.AddComponent<LayoutElement>();
        labelLayout.flexibleWidth = 1f;
        labelLayout.preferredHeight = Mathf.Max(legendIconSize.y, 24f);

        TMP_Text labelText = labelObject.AddComponent<TextMeshProUGUI>();
        if (hintText != null && hintText.font != null)
        {
            labelText.font = hintText.font;
        }

        labelText.text = entry.label;
        labelText.color = legendLabelColor;
        labelText.fontSize = 18f;
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        labelText.textWrappingMode = TextWrappingModes.NoWrap;
        labelText.raycastTarget = false;
    }

    private bool TryCreateLegendRowFromTemplate(LegendEntry entry, int index)
    {
        if (legendRowTemplateObject == null || legendContentRoot == null)
        {
            return false;
        }

        GameObject rowObject = Instantiate(legendRowTemplateObject, legendContentRoot);
        rowObject.name = $"LegendRow_{index}";
        rowObject.SetActive(true);

        Transform iconTransform = FindDescendant(rowObject.transform, "Icon");
        if (iconTransform != null)
        {
            RawImage iconRawImage = iconTransform.GetComponentInChildren<RawImage>(true);
            if (iconRawImage != null)
            {
                iconRawImage.texture = entry.iconSprite != null ? entry.iconSprite.texture : null;
                iconRawImage.uvRect = GetSpriteUvRect(entry.iconSprite);
                iconRawImage.color = entry.iconColor;
                iconRawImage.raycastTarget = false;
            }

            Image iconImage = iconTransform.GetComponentInChildren<Image>(true);
            if (iconImage != null)
            {
                iconImage.sprite = entry.iconSprite;
                iconImage.color = entry.iconColor;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }
        }

        Transform labelTransform = FindDescendant(rowObject.transform, "Label");
        TMP_Text labelText = null;
        if (labelTransform != null)
        {
            labelText = labelTransform.GetComponentInChildren<TMP_Text>(true);
        }

        if (labelText == null)
        {
            labelText = rowObject.GetComponentInChildren<TMP_Text>(true);
        }

        if (labelText != null)
        {
            labelText.text = entry.label;
            labelText.color = legendLabelColor;
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            labelText.raycastTarget = false;
        }

        return true;
    }

    private static Rect GetSpriteUvRect(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
        {
            return new Rect(0f, 0f, 1f, 1f);
        }

        Rect textureRect = sprite.textureRect;
        Texture texture = sprite.texture;
        return new Rect(
            textureRect.x / texture.width,
            textureRect.y / texture.height,
            textureRect.width / texture.width,
            textureRect.height / texture.height);
    }

    private static GameObject CreateUiGameObject(Transform parent, string objectName)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        gameObject.layer = parent.gameObject.layer;
        RectTransform rectTransform = gameObject.transform as RectTransform;
        rectTransform.SetParent(parent, false);
        rectTransform.localScale = Vector3.one;
        return gameObject;
    }

    private static Transform FindDescendant(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        for (int childIndex = 0; childIndex < root.childCount; childIndex++)
        {
            Transform child = root.GetChild(childIndex);
            if (child.name == targetName)
            {
                return child;
            }

            Transform nestedMatch = FindDescendant(child, targetName);
            if (nestedMatch != null)
            {
                return nestedMatch;
            }
        }

        return null;
    }

    private struct HostedMinimapState
    {
        public GameObject mapObject;
        public RectTransform rectTransform;
        public Transform parent;
        public int siblingIndex;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector2 offsetMin;
        public Vector2 offsetMax;
        public Vector2 pivot;
        public Vector3 localScale;
        public Quaternion localRotation;

        public bool IsValid => mapObject != null && rectTransform != null && parent != null;

        public static HostedMinimapState Capture(GameObject mapObject, RectTransform rectTransform)
        {
            return new HostedMinimapState
            {
                mapObject = mapObject,
                rectTransform = rectTransform,
                parent = rectTransform.parent,
                siblingIndex = rectTransform.GetSiblingIndex(),
                anchorMin = rectTransform.anchorMin,
                anchorMax = rectTransform.anchorMax,
                anchoredPosition = rectTransform.anchoredPosition,
                sizeDelta = rectTransform.sizeDelta,
                offsetMin = rectTransform.offsetMin,
                offsetMax = rectTransform.offsetMax,
                pivot = rectTransform.pivot,
                localScale = rectTransform.localScale,
                localRotation = rectTransform.localRotation
            };
        }

        public void Restore()
        {
            if (!IsValid)
            {
                return;
            }

            rectTransform.SetParent(parent, false);
            rectTransform.SetSiblingIndex(siblingIndex);
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
            rectTransform.pivot = pivot;
            rectTransform.localScale = localScale;
            rectTransform.localRotation = localRotation;
        }
    }

    private struct RawImageTextureState
    {
        public RawImage rawImage;
        public Texture originalTexture;
    }
}
