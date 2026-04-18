using System;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public class MissionResultPopupSequence : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private RectTransform missionCompleteRoot;
    [SerializeField] private CanvasGroup missionCompleteCanvasGroup;
    [SerializeField] private GameObject gradientBackground;
    [SerializeField] private GameObject gameSummaryPanel;
    [SerializeField] private RectTransform iconLogoBackgroundRoot;
    [SerializeField] private RectTransform missionTextRoot;
    [SerializeField] private RectTransform completeTextRoot;

    [Header("Object Names")]
    [SerializeField] private string missionCompleteObjectName = "MissionComplete";
    [SerializeField] private string gradientBackgroundObjectName = "GradentBg";
    [SerializeField] private string gameSummaryPanelObjectName = "GameSummaryPanel";
    [SerializeField] private string iconLogoBackgroundObjectName = "IconLogoBg";
    [SerializeField] private string missionTextObjectName = "MissionText";
    [SerializeField] private string completeTextObjectName = "CompleteText";

    [Header("Animation")]
    [SerializeField] private float logoInDuration = 0.55f;
    [SerializeField] private float textInDuration = 0.4f;
    [SerializeField] private float introStepDelay = 0.2f;
    [SerializeField] private float introHoldDuration = 2.9f;
    [SerializeField] private float outroDuration = 0.35f;
    [SerializeField] private Ease logoInEase = Ease.OutBack;
    [SerializeField] private Ease textInEase = Ease.OutCubic;
    [SerializeField] private Ease outroEase = Ease.InCubic;
    [SerializeField] private Vector2 missionTextInOffset = new Vector2(-40f, 0f);
    [SerializeField] private Vector2 completeTextInOffset = new Vector2(40f, 0f);
    [SerializeField] private Vector2 outroOffset = new Vector2(0f, -28f);
    [SerializeField] private float logoStartScale = 0.78f;
    [SerializeField] private float textStartScale = 0.96f;
    [SerializeField] private float outroScale = 0.92f;

    private Sequence activeSequence;
    private CanvasGroup iconLogoBackgroundCanvasGroup;
    private CanvasGroup missionTextCanvasGroup;
    private CanvasGroup completeTextCanvasGroup;
    private Vector2 cachedMissionCompleteAnchoredPosition;
    private Vector3 cachedMissionCompleteScale;
    private Vector2 cachedIconLogoBackgroundAnchoredPosition;
    private Vector3 cachedIconLogoBackgroundScale;
    private Vector2 cachedMissionTextAnchoredPosition;
    private Vector3 cachedMissionTextScale;
    private Vector2 cachedCompleteTextAnchoredPosition;
    private Vector3 cachedCompleteTextScale;
    private bool hasCachedLayout;

    public bool CanPlayCompletionIntro
    {
        get
        {
            ResolveReferences();
            return missionCompleteRoot != null &&
                   iconLogoBackgroundRoot != null &&
                   missionTextRoot != null &&
                   completeTextRoot != null;
        }
    }

    private void Awake()
    {
        ResolveReferences();
        CacheLayoutState();
        HideImmediate();
    }

    private void OnDestroy()
    {
        KillSequence();
    }

    public void HideImmediate()
    {
        ResolveReferences();
        CacheLayoutState();
        KillSequence();
        PrepareSummaryLayout();

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 0f;
            rootCanvasGroup.interactable = false;
            rootCanvasGroup.blocksRaycasts = false;
        }
    }

    public void PrepareSummaryLayout()
    {
        ResolveReferences();
        CacheLayoutState();
        KillSequence();

        if (gradientBackground != null)
        {
            gradientBackground.SetActive(true);
            CanvasGroup bgCanvasGroup = gradientBackground.GetComponent<CanvasGroup>();
            if (bgCanvasGroup == null) bgCanvasGroup = gradientBackground.AddComponent<CanvasGroup>();
            
            // Nếu root (PopupSequence) đang hiển thị thì chạy fade in, còn nếu bị ẩn ngay từ đầu thì bỏ qua
            if (rootCanvasGroup != null && rootCanvasGroup.alpha > 0)
            {
                bgCanvasGroup.alpha = 0f;
                bgCanvasGroup.DOFade(1f, 0.6f).SetUpdate(true);
            }
            else
            {
                bgCanvasGroup.alpha = 1f;
            }
        }

        SetActive(gameSummaryPanel, true);

        if (missionCompleteRoot != null)
        {
            missionCompleteRoot.anchoredPosition = cachedMissionCompleteAnchoredPosition;
            missionCompleteRoot.localScale = cachedMissionCompleteScale;
            missionCompleteRoot.gameObject.SetActive(false);
        }

        if (missionCompleteCanvasGroup != null)
        {
            missionCompleteCanvasGroup.alpha = 1f;
            missionCompleteCanvasGroup.interactable = false;
            missionCompleteCanvasGroup.blocksRaycasts = false;
        }

        ResetElementState(iconLogoBackgroundRoot, iconLogoBackgroundCanvasGroup, cachedIconLogoBackgroundAnchoredPosition, cachedIconLogoBackgroundScale);
        ResetElementState(missionTextRoot, missionTextCanvasGroup, cachedMissionTextAnchoredPosition, cachedMissionTextScale);
        ResetElementState(completeTextRoot, completeTextCanvasGroup, cachedCompleteTextAnchoredPosition, cachedCompleteTextScale);
    }

    public void PlayCompletionIntro(Action onCompleted = null)
    {
        ResolveReferences();
        CacheLayoutState();
        KillSequence();

        if (missionCompleteRoot == null)
        {
            return;
        }

        SetActive(gradientBackground, false);
        SetActive(gameSummaryPanel, false);

        missionCompleteRoot.gameObject.SetActive(true);
        missionCompleteRoot.anchoredPosition = cachedMissionCompleteAnchoredPosition;
        missionCompleteRoot.localScale = cachedMissionCompleteScale;

        if (missionCompleteCanvasGroup != null)
        {
            missionCompleteCanvasGroup.alpha = 1f;
            missionCompleteCanvasGroup.interactable = false;
            missionCompleteCanvasGroup.blocksRaycasts = false;
        }

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 1f;
            rootCanvasGroup.interactable = false;
            rootCanvasGroup.blocksRaycasts = true;
        }

        PrepareElementForIntro(
            iconLogoBackgroundRoot,
            iconLogoBackgroundCanvasGroup,
            cachedIconLogoBackgroundAnchoredPosition,
            cachedIconLogoBackgroundScale,
            Vector2.zero,
            logoStartScale);
        PrepareElementForIntro(
            missionTextRoot,
            missionTextCanvasGroup,
            cachedMissionTextAnchoredPosition,
            cachedMissionTextScale,
            missionTextInOffset,
            textStartScale);
        PrepareElementForIntro(
            completeTextRoot,
            completeTextCanvasGroup,
            cachedCompleteTextAnchoredPosition,
            cachedCompleteTextScale,
            completeTextInOffset,
            textStartScale);

        activeSequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(this);

        activeSequence.Append(BuildElementInTween(
            iconLogoBackgroundRoot,
            iconLogoBackgroundCanvasGroup,
            cachedIconLogoBackgroundAnchoredPosition,
            cachedIconLogoBackgroundScale,
            logoInDuration,
            logoInEase));

        if (introStepDelay > 0f)
        {
            activeSequence.AppendInterval(introStepDelay);
        }

        activeSequence.Append(BuildElementInTween(
            missionTextRoot,
            missionTextCanvasGroup,
            cachedMissionTextAnchoredPosition,
            cachedMissionTextScale,
            textInDuration,
            textInEase));

        if (introStepDelay > 0f)
        {
            activeSequence.AppendInterval(introStepDelay);
        }

        activeSequence.Append(BuildElementInTween(
            completeTextRoot,
            completeTextCanvasGroup,
            cachedCompleteTextAnchoredPosition,
            cachedCompleteTextScale,
            textInDuration,
            textInEase));

        if (introHoldDuration > 0f)
        {
            activeSequence.AppendInterval(introHoldDuration);
        }

        Sequence outroSequence = DOTween.Sequence();
        outroSequence.Join(BuildElementOutTween(
            iconLogoBackgroundRoot,
            iconLogoBackgroundCanvasGroup,
            cachedIconLogoBackgroundAnchoredPosition,
            cachedIconLogoBackgroundScale));
        outroSequence.Join(BuildElementOutTween(
            missionTextRoot,
            missionTextCanvasGroup,
            cachedMissionTextAnchoredPosition,
            cachedMissionTextScale));
        outroSequence.Join(BuildElementOutTween(
            completeTextRoot,
            completeTextCanvasGroup,
            cachedCompleteTextAnchoredPosition,
            cachedCompleteTextScale));
        activeSequence.Append(outroSequence);

        activeSequence.OnComplete(() =>
        {
            PrepareSummaryLayout();

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 1f;
                rootCanvasGroup.interactable = false;
                rootCanvasGroup.blocksRaycasts = true;
            }

            onCompleted?.Invoke();
        });
    }

    private void ResolveReferences()
    {
        if (rootCanvasGroup == null)
        {
            rootCanvasGroup = GetComponent<CanvasGroup>();
        }

        if (missionCompleteRoot == null)
        {
            missionCompleteRoot = FindChildByName(transform, missionCompleteObjectName) as RectTransform;
        }

        if (missionCompleteCanvasGroup == null && missionCompleteRoot != null)
        {
            missionCompleteCanvasGroup = missionCompleteRoot.GetComponent<CanvasGroup>();
            if (missionCompleteCanvasGroup == null)
            {
                missionCompleteCanvasGroup = missionCompleteRoot.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (gradientBackground == null)
        {
            gradientBackground = FindChildByName(transform, gradientBackgroundObjectName)?.gameObject;
        }

        if (gameSummaryPanel == null)
        {
            gameSummaryPanel = FindChildByName(transform, gameSummaryPanelObjectName)?.gameObject;
        }

        if (iconLogoBackgroundRoot == null)
        {
            iconLogoBackgroundRoot = FindChildByName(transform, iconLogoBackgroundObjectName) as RectTransform;
        }

        if (missionTextRoot == null)
        {
            missionTextRoot = FindChildByName(transform, missionTextObjectName) as RectTransform;
        }

        if (completeTextRoot == null)
        {
            completeTextRoot = FindChildByName(transform, completeTextObjectName) as RectTransform;
        }

        iconLogoBackgroundCanvasGroup = GetOrAddCanvasGroup(iconLogoBackgroundRoot, iconLogoBackgroundCanvasGroup);
        missionTextCanvasGroup = GetOrAddCanvasGroup(missionTextRoot, missionTextCanvasGroup);
        completeTextCanvasGroup = GetOrAddCanvasGroup(completeTextRoot, completeTextCanvasGroup);
    }

    private void CacheLayoutState()
    {
        if (hasCachedLayout || missionCompleteRoot == null)
        {
            return;
        }

        cachedMissionCompleteAnchoredPosition = missionCompleteRoot.anchoredPosition;
        cachedMissionCompleteScale = missionCompleteRoot.localScale;
        CacheElementState(iconLogoBackgroundRoot, out cachedIconLogoBackgroundAnchoredPosition, out cachedIconLogoBackgroundScale);
        CacheElementState(missionTextRoot, out cachedMissionTextAnchoredPosition, out cachedMissionTextScale);
        CacheElementState(completeTextRoot, out cachedCompleteTextAnchoredPosition, out cachedCompleteTextScale);
        hasCachedLayout = true;
    }

    private void KillSequence()
    {
        if (activeSequence == null)
        {
            return;
        }

        activeSequence.Kill();
        activeSequence = null;
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null)
        {
            target.SetActive(value);
        }
    }

    private void PrepareElementForIntro(
        RectTransform target,
        CanvasGroup canvasGroup,
        Vector2 anchoredPosition,
        Vector3 scale,
        Vector2 introOffset,
        float startScale)
    {
        if (target == null)
        {
            return;
        }

        target.gameObject.SetActive(true);
        target.anchoredPosition = anchoredPosition + introOffset;
        target.localScale = scale * startScale;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private Tween BuildElementInTween(
        RectTransform target,
        CanvasGroup canvasGroup,
        Vector2 anchoredPosition,
        Vector3 scale,
        float duration,
        Ease ease)
    {
        Sequence sequence = DOTween.Sequence();

        if (target != null)
        {
            sequence.Join(target.DOAnchorPos(anchoredPosition, duration).SetEase(ease));
            sequence.Join(target.DOScale(scale, duration).SetEase(ease));
        }

        if (canvasGroup != null)
        {
            sequence.Join(canvasGroup.DOFade(1f, duration).SetEase(Ease.OutSine));
        }

        return sequence;
    }

    private Tween BuildElementOutTween(
        RectTransform target,
        CanvasGroup canvasGroup,
        Vector2 anchoredPosition,
        Vector3 scale)
    {
        Sequence sequence = DOTween.Sequence();

        if (target != null)
        {
            sequence.Join(target.DOAnchorPos(anchoredPosition + outroOffset, outroDuration).SetEase(outroEase));
            sequence.Join(target.DOScale(scale * outroScale, outroDuration).SetEase(outroEase));
        }

        if (canvasGroup != null)
        {
            sequence.Join(canvasGroup.DOFade(0f, outroDuration).SetEase(outroEase));
        }

        return sequence;
    }

    private static void ResetElementState(
        RectTransform target,
        CanvasGroup canvasGroup,
        Vector2 anchoredPosition,
        Vector3 scale)
    {
        if (target != null)
        {
            target.anchoredPosition = anchoredPosition;
            target.localScale = scale;
            target.gameObject.SetActive(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private static void CacheElementState(
        RectTransform target,
        out Vector2 anchoredPosition,
        out Vector3 scale)
    {
        anchoredPosition = Vector2.zero;
        scale = Vector3.one;

        if (target == null)
        {
            return;
        }

        anchoredPosition = target.anchoredPosition;
        scale = target.localScale;
    }

    private static CanvasGroup GetOrAddCanvasGroup(RectTransform target, CanvasGroup current)
    {
        if (current != null || target == null)
        {
            return current;
        }

        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = target.gameObject.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    private static Transform FindChildByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
