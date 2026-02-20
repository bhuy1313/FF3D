using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Slider))]
public class SliderPercentText : MonoBehaviour
{
    [System.Serializable]
    public class IntEvent : UnityEvent<int> { }

    public enum PercentMode
    {
        NormalizeToRange,
        RawSliderValue,
        CenterAsHundred
    }

    [Header("References")]
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text percentText;

    [Header("Behavior")]
    [SerializeField] private PercentMode percentMode = PercentMode.NormalizeToRange;
    [SerializeField] private bool applyOnStart = true;

    [Header("Center Mode (100% at Center)")]
    [SerializeField] private bool useSliderMidpointAsCenter = true;
    [SerializeField] private float centerValue = 0.5f;
    [SerializeField] private int centerBasePercent = 100;
    [SerializeField, Min(0)] private int centerMaxDeltaPercent = 100;

    [Header("Events")]
    [SerializeField] private IntEvent onPercentChanged;

    private int lastPercent = -1;

    private void Awake()
    {
        if (slider == null)
        {
            slider = GetComponent<Slider>();
        }
    }

    private void OnEnable()
    {
        if (slider == null)
        {
            slider = GetComponent<Slider>();
        }

        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.RemoveListener(OnSliderChanged);
        slider.onValueChanged.AddListener(OnSliderChanged);

        if (applyOnStart)
        {
            UpdatePercentUI(true);
        }
    }

    private void OnDisable()
    {
        if (slider != null)
        {
            slider.onValueChanged.RemoveListener(OnSliderChanged);
        }
    }

    private void OnValidate()
    {
        if (slider == null)
        {
            slider = GetComponent<Slider>();
        }

        if (!Application.isPlaying)
        {
            UpdatePercentUI(false);
        }
    }

    private void OnSliderChanged(float _)
    {
        UpdatePercentUI(true);
    }

    public int GetCurrentPercent()
    {
        if (slider == null)
        {
            return 0;
        }

        float rawPercent;
        if (percentMode == PercentMode.NormalizeToRange)
        {
            float min = slider.minValue;
            float max = slider.maxValue;
            rawPercent = Mathf.Approximately(min, max)
                ? 0f
                : Mathf.InverseLerp(min, max, slider.value) * 100f;
        }
        else if (percentMode == PercentMode.CenterAsHundred)
        {
            float min = slider.minValue;
            float max = slider.maxValue;
            float center = useSliderMidpointAsCenter ? (min + max) * 0.5f : centerValue;
            center = Mathf.Clamp(center, min, max);

            float signedT = 0f;
            if (slider.value >= center)
            {
                float rightRange = max - center;
                signedT = Mathf.Approximately(rightRange, 0f) ? 0f : (slider.value - center) / rightRange;
            }
            else
            {
                float leftRange = center - min;
                signedT = Mathf.Approximately(leftRange, 0f) ? 0f : (slider.value - center) / leftRange;
            }

            rawPercent = centerBasePercent + (signedT * centerMaxDeltaPercent);
            int minPercent = centerBasePercent - centerMaxDeltaPercent;
            int maxPercent = centerBasePercent + centerMaxDeltaPercent;
            return Mathf.Clamp(Mathf.RoundToInt(rawPercent), minPercent, maxPercent);
        }
        else
        {
            rawPercent = slider.value;
        }

        return Mathf.Clamp(Mathf.RoundToInt(rawPercent), 0, 100);
    }

    private void UpdatePercentUI(bool invokeEvent)
    {
        int percent = GetCurrentPercent();

        if (percentText != null)
        {
            percentText.text = percent + "%";
        }

        if (!invokeEvent || percent == lastPercent)
        {
            lastPercent = percent;
            return;
        }

        lastPercent = percent;
        onPercentChanged?.Invoke(percent);
    }
}
