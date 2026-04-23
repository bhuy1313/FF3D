using DG.Tweening;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DispatchNotesUIController : MonoBehaviour
{
    [Header("UI Container")]
    [SerializeField]
    private GameObject uiContainer;

    [Header("UI Text Reference")]
    [SerializeField]
    private TMP_Text notesContentText;

    [Header("Audio Settings")]
    [SerializeField]
    private AudioClip openSound;

    [SerializeField]
    private AudioClip closeSound;

    [Header("Animation")]
    [SerializeField]
    private bool useOpenCloseAnimation = true;

    [SerializeField]
    private RectTransform animatedRoot;

    [SerializeField]
    private CanvasGroup animatedCanvasGroup;

    [SerializeField]
    private bool useCanvasGroupVisibility = true;

    [SerializeField]
    private float openDuration = 0.22f;

    [SerializeField]
    private float closeDuration = 0.16f;

    [SerializeField]
    private float openStartScale = 0.94f;

    [SerializeField]
    private float closeEndScale = 0.97f;

    [SerializeField]
    private float openStartRotationZ = -2.5f;

    [SerializeField]
    private float restRotationZ = 0.8f;

    [SerializeField]
    private Ease openEase = Ease.OutCubic;

    [SerializeField]
    private Ease closeEase = Ease.InCubic;

    [Header("Readability")]
    [SerializeField]
    private Color highContrastTextColor = new Color(0.11f, 0.12f, 0.14f, 1f);

    [SerializeField]
    private float lineSpacing = 6f;

    [SerializeField]
    private float paragraphSpacing = 5f;

    [SerializeField]
    private Vector4 textMargins = new Vector4(14f, 8f, 14f, 8f);

    [SerializeField]
    private bool forceTopLeftAlignment = true;

    [Header("Low Resolution")]
    [SerializeField]
    private bool adaptForLowResolution = true;

    [SerializeField]
    private int lowResolutionShortSideThreshold = 900;

    [SerializeField]
    private bool hideScrollbarsOnLowResolution = true;

    [SerializeField]
    private Scrollbar horizontalScrollbar;

    [SerializeField]
    private Scrollbar verticalScrollbar;

    [SerializeField]
    private GameObject[] decorativeEffectsToDisable;

    [SerializeField]
    private Image[] decorativeImagesToFade;

    [SerializeField]
    [Range(0f, 1f)]
    private float lowResolutionDecorativeAlpha = 0f;

    [Header("Stamp Integration")]
    [SerializeField]
    private GameObject stampPrefab;

    [SerializeField]
    private RectTransform stampParent;

    [SerializeField]
    private RectTransform stampRoot;

    [SerializeField]
    private Image stampIconImage;

    [SerializeField]
    private TMP_Text stampLevelText;

    [SerializeField]
    private string defaultStampText = "SEVERITY\nUNKNOWN";

    [SerializeField]
    private Color stampLowColor = new Color(0.14f, 0.45f, 0.26f, 0.82f);

    [SerializeField]
    private Color stampMediumColor = new Color(0.63f, 0.31f, 0.06f, 0.82f);

    [SerializeField]
    private Color stampHighColor = new Color(0.56f, 0.12f, 0.12f, 0.84f);

    [SerializeField]
    private Color stampCriticalColor = new Color(0.4f, 0.08f, 0.08f, 0.9f);

    [SerializeField]
    private Color stampUnknownColor = new Color(0.23f, 0.27f, 0.31f, 0.65f);

    [SerializeField]
    private Color stampTextColor = new Color(0.35f, 0.03f, 0.03f, 1f);

    [SerializeField]
    private bool randomizeStampRotation = true;

    [SerializeField]
    private float stampBaseRotationZ = -5f;

    [SerializeField]
    private Vector2 stampRotationRange = new Vector2(-4f, 4f);

    [Header("Cursor While Open")]
    [SerializeField]
    private bool unlockCursorWhileOpen = true;

    [SerializeField]
    private bool disableLookInputWhileOpen = true;

    [SerializeField]
    private bool deferLookInputLockToGameplayManager = true;

    private StarterAssetsInputs inputState;
    private GameMasterUiMovementInputLock cachedGameplayInputLock;
    private bool isInitialized;
    [SerializeField]
    private bool isNotesOpenRuntime;
    private bool lowResolutionProfileApplied;
    private Sequence activeSequence;
    private bool hasCursorSnapshot;
    private bool restoreCursorLocked;
    private bool restoreCursorInputForLook;
    private bool restoreLookInputByDispatchController;
    private CursorLockMode restoreCursorLockMode;
    private bool restoreCursorVisible;

    public bool IsNotesOpenRuntime => isNotesOpenRuntime;

    private void Awake()
    {
        inputState = Object.FindAnyObjectByType<StarterAssetsInputs>();

        if (uiContainer != null)
        {
            ResolveAnimationReferences();
            ApplyVisibilityStateImmediate(false);
        }

        isNotesOpenRuntime = false;

        ResolveLowResolutionReferences();
        ResolveStampReferences();
        ApplyReadabilityProfile();
        ApplyLowResolutionProfileIfNeeded();
    }

    private void OnDisable()
    {
        KillActiveSequence();
        RestoreCursorStateIfNeeded();
    }

    private void OnDestroy()
    {
        KillActiveSequence();
        RestoreCursorStateIfNeeded();
    }

    private void Update()
    {
        bool togglePressed = false;

        if (inputState != null)
        {
            if (inputState.dispatchNotes)
            {
                togglePressed = true;
                inputState.dispatchNotes = false;
            }
        }
        else
        {
            inputState = Object.FindAnyObjectByType<StarterAssetsInputs>();
            if (Input.GetKeyDown(KeyCode.L))
            {
                togglePressed = true;
            }
        }

        if (togglePressed)
        {
            Debug.Log(
                "[DispatchNotesUIController] Input detected (Player or Direct Key), toggling notes."
            );
            ToggleNotes();
        }
    }

    public void ToggleNotes()
    {
        if (uiContainer == null)
        {
            return;
        }

        bool targetOpen = !isNotesOpenRuntime;
        if (targetOpen && !isInitialized)
        {
            InitializeData();
        }

        if (useOpenCloseAnimation)
        {
            PlayToggleAnimation(targetOpen);
        }
        else
        {
            ApplyVisibilityStateImmediate(targetOpen);
        }

        isNotesOpenRuntime = targetOpen;
        if (targetOpen && animatedCanvasGroup != null)
        {
            // Keep ScrollRect raycasts available even while open tween is playing.
            animatedCanvasGroup.interactable = true;
            animatedCanvasGroup.blocksRaycasts = true;
        }
        UpdateCursorStateForNotes(targetOpen);

        AudioClip clipToPlay = targetOpen ? openSound : closeSound;
        if (clipToPlay != null)
        {
            AudioService.PlayClip2D(clipToPlay, AudioBus.Ui);
        }
    }

    private void InitializeData()
    {
        ApplyReadabilityProfile();

        if (
            !LoadingFlowState.TryGetPendingIncidentPayload(out IncidentWorldSetupPayload payload)
            || payload == null
        )
        {
            Debug.LogWarning("[DispatchNotesUIController] No pending incident payload found.");
            return;
        }

        if (notesContentText == null)
        {
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("<size=140%><b>DISPATCH NOTES</b></size>");

        string displayId = string.IsNullOrEmpty(payload.caseId)
            ? $"FD-{Random.Range(1000, 9999)}-X"
            : payload.caseId.ToUpper();
        string timeStr = System.DateTime.Now.ToString("HH:mm");
        string dateStr = System.DateTime.Now.ToString("dd/MM/yyyy");
        sb.AppendLine(
            $"<size=80%><color=#2f3b45>ID: {displayId}  |  {timeStr}  |  {dateStr}</color></size>"
        );
        sb.AppendLine("<color=#2b3137>---------------------------------</color>");
        sb.AppendLine();

        if (payload.reportSnapshot != null)
        {
            AppendField(sb, "ADDRESS", payload.reportSnapshot.address, "#1f4f7a");
            AppendField(sb, "FIRE LOC", payload.reportSnapshot.fireLocation, "#1f4f7a");
            sb.AppendLine();

            string riskColor = GetRiskColor(payload.reportSnapshot.occupantRisk);
            AppendField(sb, "OCCUPANT RISK", payload.reportSnapshot.occupantRisk, riskColor);

            string hazardColor = GetHazardColor(payload.reportSnapshot.hazard);
            AppendField(sb, "HAZARD", payload.reportSnapshot.hazard, hazardColor);

            AppendField(sb, "SPREAD", payload.reportSnapshot.spreadStatus);

            string severityColor = GetSeverityColor(payload.reportSnapshot.severity);
            AppendField(sb, "SEVERITY", payload.reportSnapshot.severity, severityColor);
            ApplyStampSeverity(payload.reportSnapshot.severity);
        }
        else
        {
            ApplyStampSeverity(null);
        }

        sb.AppendLine();
        sb.AppendLine("<b><color=#2b3137>APPLIED SIGNALS:</color></b>");
        if (payload.appliedSignals != null && payload.appliedSignals.Count > 0)
        {
            foreach (string signal in payload.appliedSignals)
            {
                sb.AppendLine($"<color=#2f3b45>  - {signal.ToUpper()}</color>");
            }
        }
        else
        {
            sb.AppendLine("<color=#5f6971>  - NONE</color>");
        }

        notesContentText.text = sb.ToString();
        RefreshNotesScrollLayout();
        isInitialized = true;
    }

    private void ResolveAnimationReferences()
    {
        if (uiContainer == null)
        {
            return;
        }

        if (animatedRoot == null)
        {
            animatedRoot = uiContainer.transform as RectTransform;
        }

        if (animatedCanvasGroup == null)
        {
            animatedCanvasGroup = uiContainer.GetComponent<CanvasGroup>();
            if (animatedCanvasGroup == null)
            {
                animatedCanvasGroup = uiContainer.AddComponent<CanvasGroup>();
            }
        }
    }

    private void ResolveLowResolutionReferences()
    {
        if (uiContainer == null)
        {
            return;
        }

        if (horizontalScrollbar == null || verticalScrollbar == null)
        {
            Scrollbar[] scrollbars = uiContainer.GetComponentsInChildren<Scrollbar>(true);
            for (int i = 0; i < scrollbars.Length; i++)
            {
                if (scrollbars[i] == null)
                {
                    continue;
                }

                if (
                    scrollbars[i].direction == Scrollbar.Direction.LeftToRight
                    && horizontalScrollbar == null
                )
                {
                    horizontalScrollbar = scrollbars[i];
                    continue;
                }

                if (
                    (
                        scrollbars[i].direction == Scrollbar.Direction.BottomToTop
                        || scrollbars[i].direction == Scrollbar.Direction.TopToBottom
                    )
                    && verticalScrollbar == null
                )
                {
                    verticalScrollbar = scrollbars[i];
                }
            }
        }
    }

    private void ApplyReadabilityProfile()
    {
        if (notesContentText == null)
        {
            return;
        }

        notesContentText.color = highContrastTextColor;
        notesContentText.lineSpacing = lineSpacing;
        notesContentText.paragraphSpacing = paragraphSpacing;
        notesContentText.margin = textMargins;
        notesContentText.textWrappingMode = TextWrappingModes.Normal;

        if (forceTopLeftAlignment)
        {
            notesContentText.alignment = TextAlignmentOptions.TopLeft;
        }
    }

    private void ApplyLowResolutionProfileIfNeeded()
    {
        if (!adaptForLowResolution || lowResolutionProfileApplied)
        {
            return;
        }

        int shortSide = Mathf.Min(Screen.width, Screen.height);
        if (shortSide > lowResolutionShortSideThreshold)
        {
            return;
        }

        lowResolutionProfileApplied = true;

        if (hideScrollbarsOnLowResolution)
        {
            if (horizontalScrollbar != null)
            {
                SetScrollbarVisualVisible(horizontalScrollbar, false);
            }

            if (verticalScrollbar != null)
            {
                SetScrollbarVisualVisible(verticalScrollbar, false);
            }
        }

        if (decorativeEffectsToDisable != null)
        {
            for (int i = 0; i < decorativeEffectsToDisable.Length; i++)
            {
                if (decorativeEffectsToDisable[i] != null)
                {
                    decorativeEffectsToDisable[i].SetActive(false);
                }
            }
        }

        if (decorativeImagesToFade != null)
        {
            for (int i = 0; i < decorativeImagesToFade.Length; i++)
            {
                if (decorativeImagesToFade[i] == null)
                {
                    continue;
                }

                Color color = decorativeImagesToFade[i].color;
                color.a = Mathf.Min(color.a, lowResolutionDecorativeAlpha);
                decorativeImagesToFade[i].color = color;
            }
        }
    }

    private void ResolveStampReferences()
    {
        if (uiContainer == null)
        {
            return;
        }

        if (stampParent == null)
        {
            stampParent = uiContainer.transform as RectTransform;
        }

        if (stampRoot == null)
        {
            Transform existingStamp = FindDeepChildByName(
                stampParent != null ? stampParent : uiContainer.transform,
                "Stamp"
            );
            if (existingStamp != null)
            {
                stampRoot = existingStamp as RectTransform;
            }
        }

        if (stampRoot == null && stampPrefab != null && stampParent != null)
        {
            GameObject stampInstance = Object.Instantiate(stampPrefab, stampParent, false);
            stampInstance.name = "Stamp";
            stampRoot = stampInstance.transform as RectTransform;
        }

        if (stampRoot == null)
        {
            return;
        }

        if (stampIconImage == null)
        {
            Transform iconImage = FindDeepChildByName(stampRoot, "Image");
            if (iconImage != null)
            {
                stampIconImage = iconImage.GetComponent<Image>();
            }

            if (stampIconImage == null)
            {
                stampIconImage = stampRoot.GetComponentInChildren<Image>(true);
            }
        }

        if (stampLevelText == null)
        {
            stampLevelText = stampRoot.GetComponentInChildren<TMP_Text>(true);
        }

        Graphic[] graphics = stampRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].raycastTarget = false;
            }
        }
    }

    private void ApplyStampSeverity(string severityRaw)
    {
        ResolveStampReferences();

        if (stampRoot == null)
        {
            return;
        }

        string severityKey = ResolveSeverityKey(severityRaw);
        string label = defaultStampText;
        Color stampColor = stampUnknownColor;

        switch (severityKey)
        {
            case "critical":
                label = "SEVERITY\nCRITICAL";
                stampColor = stampCriticalColor;
                break;
            case "high":
                label = "SEVERITY\nHIGH";
                stampColor = stampHighColor;
                break;
            case "medium":
                label = "SEVERITY\nMEDIUM";
                stampColor = stampMediumColor;
                break;
            case "low":
                label = "SEVERITY\nLOW";
                stampColor = stampLowColor;
                break;
            default:
                label = defaultStampText;
                stampColor = stampUnknownColor;
                break;
        }

        if (stampIconImage != null)
        {
            stampIconImage.color = stampColor;
        }

        if (stampLevelText != null)
        {
            stampLevelText.text = label;
            stampLevelText.color = stampTextColor;
        }

        ApplyStampRotation();
    }

    private void ApplyStampRotation()
    {
        if (stampRoot == null)
        {
            return;
        }

        float rotationZ = stampBaseRotationZ;
        if (randomizeStampRotation)
        {
            float min = Mathf.Min(stampRotationRange.x, stampRotationRange.y);
            float max = Mathf.Max(stampRotationRange.x, stampRotationRange.y);
            rotationZ += Random.Range(min, max);
        }

        Vector3 euler = stampRoot.localEulerAngles;
        euler.z = rotationZ;
        stampRoot.localEulerAngles = euler;
    }

    private static string ResolveSeverityKey(string severityRaw)
    {
        if (string.IsNullOrEmpty(severityRaw))
        {
            return "unknown";
        }

        string s = severityRaw.ToLower();
        if (s.Contains("critical"))
            return "critical";
        if (s.Contains("high") || s.Contains("heavy"))
            return "high";
        if (s.Contains("medium") || s.Contains("moderate"))
            return "medium";
        if (s.Contains("low") || s.Contains("minor"))
            return "low";
        return "unknown";
    }

    private static Transform FindDeepChildByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == targetName)
            {
                return child;
            }

            Transform nested = FindDeepChildByName(child, targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void UpdateCursorStateForNotes(bool notesOpen)
    {
        if (!unlockCursorWhileOpen)
        {
            return;
        }

        if (notesOpen)
        {
            CaptureCursorSnapshot();

            if (inputState == null)
            {
                inputState = Object.FindAnyObjectByType<StarterAssetsInputs>();
            }

            if (inputState != null)
            {
                inputState.cursorLocked = false;
                if (restoreLookInputByDispatchController)
                {
                    inputState.cursorInputForLook = false;
                    inputState.look = Vector2.zero;
                }
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        RestoreCursorStateIfNeeded();
    }

    private void CaptureCursorSnapshot()
    {
        if (hasCursorSnapshot)
        {
            return;
        }

        if (inputState == null)
        {
            inputState = Object.FindAnyObjectByType<StarterAssetsInputs>();
        }

        restoreLookInputByDispatchController = ShouldDispatchManageLookInputLock();

        if (inputState != null)
        {
            restoreCursorLocked = inputState.cursorLocked;
            if (restoreLookInputByDispatchController)
            {
                restoreCursorInputForLook = inputState.cursorInputForLook;
            }
        }

        restoreCursorLockMode = Cursor.lockState;
        restoreCursorVisible = Cursor.visible;
        hasCursorSnapshot = true;
    }

    private void RestoreCursorStateIfNeeded()
    {
        if (!hasCursorSnapshot)
        {
            return;
        }

        if (inputState == null)
        {
            inputState = Object.FindAnyObjectByType<StarterAssetsInputs>();
        }

        if (inputState != null)
        {
            inputState.cursorLocked = restoreCursorLocked;
            if (restoreLookInputByDispatchController)
            {
                inputState.cursorInputForLook = restoreCursorInputForLook;
            }
            inputState.look = Vector2.zero;
        }

        Cursor.lockState = restoreCursorLockMode;
        Cursor.visible = restoreCursorVisible;
        restoreLookInputByDispatchController = false;
        hasCursorSnapshot = false;
    }

    private bool ShouldDispatchManageLookInputLock()
    {
        if (!disableLookInputWhileOpen)
        {
            return false;
        }

        if (!deferLookInputLockToGameplayManager)
        {
            return true;
        }

        if (cachedGameplayInputLock == null)
        {
            cachedGameplayInputLock =
                Object.FindAnyObjectByType<GameMasterUiMovementInputLock>(FindObjectsInactive.Include);
        }

        return cachedGameplayInputLock == null || !cachedGameplayInputLock.enabled;
    }

    private void PlayToggleAnimation(bool targetOpen)
    {
        ResolveAnimationReferences();

        if (animatedRoot == null || animatedCanvasGroup == null)
        {
            ApplyVisibilityStateImmediate(targetOpen);
            return;
        }

        KillActiveSequence();
        EnsureUiContainerActiveForTween();

        if (targetOpen)
        {
            animatedCanvasGroup.alpha = 0f;
            animatedCanvasGroup.interactable = false;
            animatedCanvasGroup.blocksRaycasts = false;

            animatedRoot.localScale = Vector3.one * openStartScale;
            animatedRoot.localEulerAngles = new Vector3(0f, 0f, openStartRotationZ);

            activeSequence = DOTween
                .Sequence()
                .Join(animatedCanvasGroup.DOFade(1f, openDuration).SetEase(Ease.OutSine))
                .Join(animatedRoot.DOScale(1f, openDuration).SetEase(openEase))
                .Join(
                    animatedRoot
                        .DOLocalRotate(new Vector3(0f, 0f, restRotationZ), openDuration)
                        .SetEase(openEase)
                )
                .OnComplete(() =>
                {
                    animatedCanvasGroup.interactable = true;
                    animatedCanvasGroup.blocksRaycasts = true;
                    activeSequence = null;
                })
                .OnKill(() =>
                {
                    if (activeSequence != null)
                    {
                        activeSequence = null;
                    }
                });
        }
        else
        {
            animatedCanvasGroup.interactable = false;
            animatedCanvasGroup.blocksRaycasts = false;

            activeSequence = DOTween
                .Sequence()
                .Join(animatedCanvasGroup.DOFade(0f, closeDuration).SetEase(closeEase))
                .Join(animatedRoot.DOScale(closeEndScale, closeDuration).SetEase(closeEase))
                .Join(
                    animatedRoot
                        .DOLocalRotate(
                            new Vector3(0f, 0f, openStartRotationZ * 0.45f),
                            closeDuration
                        )
                        .SetEase(closeEase)
                )
                .OnComplete(() =>
                {
                    animatedCanvasGroup.alpha = 0f;
                    if (!useCanvasGroupVisibility && uiContainer != null)
                    {
                        uiContainer.SetActive(false);
                    }
                    activeSequence = null;
                })
                .OnKill(() =>
                {
                    if (activeSequence != null)
                    {
                        activeSequence = null;
                    }
                });
        }
    }

    private void ApplyVisibilityStateImmediate(bool visible)
    {
        ResolveAnimationReferences();

        if (uiContainer == null)
        {
            return;
        }

        if (useCanvasGroupVisibility && animatedCanvasGroup != null)
        {
            uiContainer.SetActive(true);
            animatedCanvasGroup.alpha = visible ? 1f : 0f;
            animatedCanvasGroup.interactable = visible;
            animatedCanvasGroup.blocksRaycasts = visible;

            if (animatedRoot != null && visible)
            {
                animatedRoot.localScale = Vector3.one;
                animatedRoot.localEulerAngles = new Vector3(0f, 0f, restRotationZ);
            }

            return;
        }

        uiContainer.SetActive(visible);
    }

    private void RefreshNotesScrollLayout()
    {
        if (notesContentText == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        RectTransform textRect = notesContentText.rectTransform;
        if (textRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);

            RectTransform parentRect = textRect.parent as RectTransform;
            if (parentRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }
        }

        if (uiContainer == null)
        {
            return;
        }

        ScrollRect scrollRect = uiContainer.GetComponentInChildren<ScrollRect>(true);
        if (scrollRect != null)
        {
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private static void SetScrollbarVisualVisible(Scrollbar scrollbar, bool visible)
    {
        if (scrollbar == null)
        {
            return;
        }

        scrollbar.gameObject.SetActive(true);
        scrollbar.interactable = visible;

        Graphic targetGraphic = scrollbar.targetGraphic;
        if (targetGraphic != null)
        {
            Color c = targetGraphic.color;
            c.a = visible ? 1f : 0f;
            targetGraphic.color = c;
            targetGraphic.raycastTarget = visible;
        }

        Image[] images = scrollbar.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null)
            {
                continue;
            }

            Color c = images[i].color;
            c.a = visible ? 1f : 0f;
            images[i].color = c;
            images[i].raycastTarget = visible;
        }
    }

    private void EnsureUiContainerActiveForTween()
    {
        if (uiContainer != null && !uiContainer.activeSelf)
        {
            uiContainer.SetActive(true);
        }
    }

    private void KillActiveSequence()
    {
        if (activeSequence == null)
        {
            return;
        }

        activeSequence.Kill();
        activeSequence = null;
    }

    private void AppendField(
        System.Text.StringBuilder sb,
        string label,
        string value,
        string colorHex = null
    )
    {
        string displayValue = string.IsNullOrEmpty(value) ? "---" : value.ToUpper();

        if (!string.IsNullOrEmpty(colorHex))
        {
            sb.AppendLine($"<b>{label}:</b> <color={colorHex}>{displayValue}</color>");
        }
        else
        {
            sb.AppendLine($"<b>{label}:</b> {displayValue}");
        }
    }

    private string GetRiskColor(string risk)
    {
        if (
            string.IsNullOrEmpty(risk)
            || risk.ToLower().Contains("none")
            || risk.ToLower().Contains("clear")
        )
        {
            return "#1f6a3a";
        }

        return "#9a4f00";
    }

    private string GetHazardColor(string hazard)
    {
        if (string.IsNullOrEmpty(hazard) || hazard.ToLower().Contains("none"))
        {
            return null;
        }

        return "#8f1f1f";
    }

    private string GetSeverityColor(string severity)
    {
        string s = severity?.ToLower() ?? string.Empty;
        if (s.Contains("high") || s.Contains("critical") || s.Contains("heavy"))
            return "#8f1f1f";
        if (s.Contains("medium") || s.Contains("moderate"))
            return "#9a4f00";
        if (s.Contains("low") || s.Contains("minor"))
            return "#1f6a3a";
        return null;
    }
}
