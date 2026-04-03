using UnityEngine;
using UnityEngine.UI;

public class PlayerVitalsUI : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private PlayerVitals vitals;

    [Header("UI Fills")]
    [SerializeField] private Image hpFill;
    [SerializeField] private Image oxygenFill;
    [SerializeField] private Image staminaFill;

    private void Awake()
    {
        if (vitals == null)
        {
            vitals = FindAnyObjectByType<PlayerVitals>();
        }
    }

    private void OnEnable()
    {
        if (vitals == null) return;

        vitals.OnHealthChanged += HandleHealth;
        vitals.OnOxygenChanged += HandleOxygen;
        vitals.OnStaminaChanged += HandleStamina;

        StartCoroutine(RefreshNextFrame());
    }

    private System.Collections.IEnumerator RefreshNextFrame()
    {
        yield return null; // đợi 1 frame
        RefreshAll();
    }

    private void OnDisable()
    {
        if (vitals == null) return;

        vitals.OnHealthChanged -= HandleHealth;
        vitals.OnOxygenChanged -= HandleOxygen;
        vitals.OnStaminaChanged -= HandleStamina;
    }

    private void RefreshAll()
    {
        SetFill(hpFill, vitals.HealthPercent);
        SetFill(oxygenFill, vitals.OxygenPercent);
        SetFill(staminaFill, vitals.StaminaPercent);
    }

    private void HandleHealth(float current, float max)
    {
        SetFill(hpFill, max <= 0f ? 0f : current / max);
    }

    private void HandleOxygen(float current, float max)
    {
        SetFill(oxygenFill, max <= 0f ? 0f : current / max);
    }

    private void HandleStamina(float current, float max)
    {
        SetFill(staminaFill, max <= 0f ? 0f : current / max);
    }

    private static void SetFill(Image img, float percent)
    {
        if (img == null) return;
        img.fillAmount = Mathf.Clamp01(percent);
    }
}
