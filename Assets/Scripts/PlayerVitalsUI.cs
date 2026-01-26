using UnityEngine;
using UnityEngine.UI;

public class PlayerVitalsUI : MonoBehaviour
{
    [Header("HP")]
    [Min(0f)] public float hp = 100f;
    [Min(0.0001f)] public float hpMax = 100f;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Image hpFill;

    [Header("Oxygen")]
    [Min(0f)] public float oxygen = 100f;
    [Min(0.0001f)] public float oxygenMax = 100f;
    [SerializeField] private Slider oxygenSlider;
    [SerializeField] private Image oxygenFill;

    [Header("Stamina")]
    [Min(0f)] public float stamina = 100f;
    [Min(0.0001f)] public float staminaMax = 100f;
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private Image staminaFill;

    private void Awake()
    {
        SetupSlider(hpSlider);
        SetupSlider(oxygenSlider);
        SetupSlider(staminaSlider);
        Refresh();
    }

    private void OnValidate()
    {
        hpMax = Mathf.Max(0.0001f, hpMax);
        oxygenMax = Mathf.Max(0.0001f, oxygenMax);
        staminaMax = Mathf.Max(0.0001f, staminaMax);
        Refresh();
    }

    public void SetHP(float current, float max = -1f)
    {
        hp = current;
        if (max > 0f) hpMax = max;
        Refresh();
    }

    public void SetOxygen(float current, float max = -1f)
    {
        oxygen = current;
        if (max > 0f) oxygenMax = max;
        Refresh();
    }

    public void SetStamina(float current, float max = -1f)
    {
        stamina = current;
        if (max > 0f) staminaMax = max;
        Refresh();
    }

    public void Refresh()
    {
        SetBar(hpSlider, hpFill, hp, hpMax);
        SetBar(oxygenSlider, oxygenFill, oxygen, oxygenMax);
        SetBar(staminaSlider, staminaFill, stamina, staminaMax);
    }

    private static void SetupSlider(Slider slider)
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
    }

    private static void SetBar(Slider slider, Image fill, float current, float max)
    {
        var value01 = Mathf.Clamp01(current / Mathf.Max(0.0001f, max));
        if (slider != null) slider.value = value01;
        if (fill != null) fill.fillAmount = value01;
    }
}
