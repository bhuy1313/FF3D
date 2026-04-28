using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class LevelPanelAnimation : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField]
    private Button btnInfo;

    [SerializeField]
    private RectTransform go1;

    [SerializeField]
    private CanvasGroup go2;

    [Header("Animation Settings")]
    [SerializeField]
    private float duration = 0.5f;

    [SerializeField]
    private Ease moveEase = Ease.OutQuint;

    [SerializeField]
    private Ease fadeEase = Ease.InOutSine;

    [SerializeField]
    private bool ignoreTimeScale = true;

    [Header("Target")]
    [Tooltip("Offset từ góc TOP-LEFT")]
    [SerializeField]
    private Vector2 topLeftOffset = Vector2.zero;

    private Tween moveTween;
    private Tween fadeTween;

    private void Awake()
    {
        if (btnInfo != null)
            btnInfo.onClick.AddListener(OnInfoButtonClicked);

        SetupInitialState();
    }

    private void SetupInitialState()
    {
        // Ẩn GO2
        if (go2 != null)
        {
            go2.alpha = 0f;
            go2.interactable = false;
            go2.blocksRaycasts = false;
        }
    }

    private void OnInfoButtonClicked()
    {
        AnimateGO1();
        AnimateGO2();
    }

    // =========================
    // GO1: Move to Top-Left
    // =========================
    private void AnimateGO1()
    {
        if (go1 == null)
            return;

        // Kill tween cũ
        moveTween?.Kill();

        // 👉 Đảm bảo anchor + pivot = TOP LEFT
        SetTopLeft(go1);

        // 👉 Fix layout nếu có (ContentSizeFitter / LayoutGroup)
        LayoutRebuilder.ForceRebuildLayoutImmediate(go1);

        // 👉 Animate
        moveTween = go1.DOAnchorPos(topLeftOffset, duration)
            .SetEase(moveEase)
            .SetUpdate(ignoreTimeScale);
    }

    // =========================
    // GO2: Fade In
    // =========================
    private void AnimateGO2()
    {
        if (go2 == null)
            return;

        fadeTween?.Kill();

        fadeTween = go2.DOFade(1f, duration)
            .SetEase(fadeEase)
            .SetUpdate(ignoreTimeScale)
            .OnStart(() =>
            {
                go2.blocksRaycasts = true;
                go2.interactable = true;
            });
    }

    // =========================
    // Utility
    // =========================
    private void SetTopLeft(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
    }

    private void OnDestroy()
    {
        if (btnInfo != null)
            btnInfo.onClick.RemoveListener(OnInfoButtonClicked);

        moveTween?.Kill();
        fadeTween?.Kill();
    }
}
