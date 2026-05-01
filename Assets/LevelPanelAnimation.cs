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
    private Ease fadeEase = Ease.InOutSine;

    [SerializeField]
    private bool ignoreTimeScale = true;

    private Tween go1Tween;
    private Tween fadeTween;
    private bool isSuperDetailOpen = false;
    private CanvasGroup go1CanvasGroup;

    public bool IsSuperDetailOpen => isSuperDetailOpen;

    private void Awake()
    {
        if (btnInfo != null)
            btnInfo.onClick.AddListener(OnInfoButtonClicked);

        if (go1 != null)
        {
            go1CanvasGroup = go1.GetComponent<CanvasGroup>();
            if (go1CanvasGroup == null)
            {
                go1CanvasGroup = go1.gameObject.AddComponent<CanvasGroup>();
            }
        }

        SetupInitialState();
    }

    private void SetupInitialState()
    {
        // Ẩn GO2 (SuperDetail)
        if (go2 != null)
        {
            go2.alpha = 0f;
            go2.interactable = false;
            go2.blocksRaycasts = false;
        }

        // Hiện GO1 (LevelInfoPanel)
        if (go1CanvasGroup != null)
        {
            go1CanvasGroup.alpha = 1f;
            go1CanvasGroup.interactable = true;
            go1CanvasGroup.blocksRaycasts = true;
        }
        
        isSuperDetailOpen = false;
    }

    public void ToggleSuperDetail()
    {
        OnInfoButtonClicked();
    }

    public void OpenSuperDetail()
    {
        if (!isSuperDetailOpen)
        {
            OnInfoButtonClicked();
        }
    }

    public void CloseSuperDetail()
    {
        if (isSuperDetailOpen)
        {
            OnInfoButtonClicked();
        }
    }

    private void OnInfoButtonClicked()
    {
        isSuperDetailOpen = !isSuperDetailOpen;

        if (isSuperDetailOpen)
        {
            HideGO1();
            ShowGO2();
        }
        else
        {
            ShowGO1();
            HideGO2();
        }
    }

    // =========================
    // GO1: LevelInfoPanel
    // =========================
    private void HideGO1()
    {
        if (go1CanvasGroup == null) return;

        go1Tween?.Kill();
        go1CanvasGroup.interactable = false;
        go1CanvasGroup.blocksRaycasts = false;
        
        go1Tween = go1CanvasGroup.DOFade(0f, duration)
            .SetEase(fadeEase)
            .SetUpdate(ignoreTimeScale);
    }

    private void ShowGO1()
    {
        if (go1CanvasGroup == null) return;

        go1Tween?.Kill();
        go1Tween = go1CanvasGroup.DOFade(1f, duration)
            .SetEase(fadeEase)
            .SetUpdate(ignoreTimeScale)
            .OnComplete(() =>
            {
                go1CanvasGroup.interactable = true;
                go1CanvasGroup.blocksRaycasts = true;
            });
    }

    // =========================
    // GO2: SuperDetail
    // =========================
    private void ShowGO2()
    {
        if (go2 == null) return;

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

    private void HideGO2()
    {
        if (go2 == null) return;

        fadeTween?.Kill();
        fadeTween = go2.DOFade(0f, duration)
            .SetEase(fadeEase)
            .SetUpdate(ignoreTimeScale)
            .OnComplete(() =>
            {
                go2.blocksRaycasts = false;
                go2.interactable = false;
            });
    }

    private void OnDestroy()
    {
        if (btnInfo != null)
            btnInfo.onClick.RemoveListener(OnInfoButtonClicked);

        go1Tween?.Kill();
        fadeTween?.Kill();
    }
}
