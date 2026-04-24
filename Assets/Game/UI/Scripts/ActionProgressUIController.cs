using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ActionProgressUIController : MonoBehaviour
{
    [Header("UI Container")]
    [Tooltip("The main container object that will be toggled on/off")]
    [SerializeField] private GameObject uiContainer;

    [Header("UI References")]
    [SerializeField] private Image progressFillImage;
    [SerializeField] private TMP_Text timerText;

    [Header("Localization")]
    [SerializeField] private string progressPercentLocalizationKey = "mission.hud.progress.percent";
    [SerializeField] private string progressPercentFallbackFormat = "{0:0}%";

    private void Start()
    {
        if (uiContainer != null)
        {
            uiContainer.SetActive(false);
        }
    }

    private void Awake()
    {
        PlayerContinuousActionBus.OnActionStarted += HandleActionStarted;
        PlayerContinuousActionBus.OnActionProgressed += HandleActionProgressed;
        PlayerContinuousActionBus.OnActionEnded += HandleActionEnded;
    }

    private void OnDestroy()
    {
        PlayerContinuousActionBus.OnActionStarted -= HandleActionStarted;
        PlayerContinuousActionBus.OnActionProgressed -= HandleActionProgressed;
        PlayerContinuousActionBus.OnActionEnded -= HandleActionEnded;
    }

    private void HandleActionStarted(string actionText)
    {
        if (uiContainer != null)
        {
            uiContainer.SetActive(true);
        }

        if (progressFillImage != null)
        {
            progressFillImage.fillAmount = 0f;
        }

        if (timerText != null)
        {
            timerText.text = BuildProgressText(0f);
        }
    }

    private void HandleActionProgressed(float progress)
    {
        if (progressFillImage != null)
        {
            progressFillImage.fillAmount = progress;
        }

        if (timerText != null)
        {
            timerText.text = BuildProgressText(progress);
        }
    }

    private void HandleActionEnded(bool success)
    {
        if (uiContainer != null)
        {
            uiContainer.SetActive(false);
        }
    }

    private string BuildProgressText(float progress)
    {
        float percent = Mathf.Clamp01(progress) * 100f;
        return MissionLocalization.Format(progressPercentLocalizationKey, progressPercentFallbackFormat, percent);
    }
}
