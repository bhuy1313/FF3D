using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Slider))]
public class ThreeStepSlider : MonoBehaviour
{
    [System.Serializable]
    public class IntEvent : UnityEvent<int> { }

    [Header("References")]
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text valueLabel;

    [Header("Step Labels")]
    [SerializeField] private string lowLabel = "Low";
    [SerializeField] private string mediumLabel = "Medium";
    [SerializeField] private string highLabel = "High";

    [Header("Options")]
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool invokeOnStart = true;

    [Header("Events")]
    [SerializeField] private IntEvent onStepChanged;

    private bool isUpdating;
    private int lastStep = -1;

    private void Awake()
    {
        if (slider == null)
        {
            slider = GetComponent<Slider>();
        }

        ConfigureSlider();
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

        slider.onValueChanged.RemoveListener(OnSliderValueChanged);
        slider.onValueChanged.AddListener(OnSliderValueChanged);

        if (applyOnStart)
        {
            SnapAndRefresh(invokeOnStart, invokeOnStart);
        }
    }

    private void OnDisable()
    {
        if (slider != null)
        {
            slider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }
    }

    private void OnValidate()
    {
        if (slider == null)
        {
            slider = GetComponent<Slider>();
        }

        ConfigureSlider();

        if (!Application.isPlaying)
        {
            SnapWithoutNotify();
        }
    }

    private void OnSliderValueChanged(float _)
    {
        SnapAndRefresh(true, false);
    }

    public void AddStepChangedListener(UnityAction<int> listener)
    {
        if (listener == null)
        {
            return;
        }

        onStepChanged ??= new IntEvent();
        onStepChanged.RemoveListener(listener);
        onStepChanged.AddListener(listener);
    }

    public void RemoveStepChangedListener(UnityAction<int> listener)
    {
        if (listener == null || onStepChanged == null)
        {
            return;
        }

        onStepChanged.RemoveListener(listener);
    }

    public int GetCurrentStep()
    {
        if (slider == null)
        {
            return 0;
        }

        return Mathf.Clamp(Mathf.RoundToInt(slider.value), 0, 2);
    }

    public string GetCurrentLabel()
    {
        return GetLabel(GetCurrentStep());
    }

    public void SetStep(int step, bool invokeEvent = true)
    {
        if (slider == null)
        {
            return;
        }

        int clampedStep = Mathf.Clamp(step, 0, 2);
        slider.SetValueWithoutNotify(clampedStep);
        RefreshLabel(clampedStep);
        NotifyStepIfNeeded(clampedStep, invokeEvent, invokeEvent);
    }

    private void ConfigureSlider()
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 2f;
        slider.wholeNumbers = true;
    }

    private void SnapWithoutNotify()
    {
        if (slider == null)
        {
            return;
        }

        int step = GetCurrentStep();
        slider.SetValueWithoutNotify(step);
        RefreshLabel(step);
    }

    private void SnapAndRefresh(bool invokeEvent, bool forceNotify)
    {
        if (slider == null || isUpdating)
        {
            return;
        }

        int step = GetCurrentStep();
        float snappedValue = step;

        if (!Mathf.Approximately(slider.value, snappedValue))
        {
            isUpdating = true;
            slider.SetValueWithoutNotify(snappedValue);
            isUpdating = false;
        }

        RefreshLabel(step);
        NotifyStepIfNeeded(step, invokeEvent, forceNotify);
    }

    private void RefreshLabel(int step)
    {
        if (valueLabel == null)
        {
            return;
        }

        valueLabel.text = GetLabel(step);
    }

    private string GetLabel(int step)
    {
        switch (Mathf.Clamp(step, 0, 2))
        {
            case 0: return lowLabel;
            case 1: return mediumLabel;
            default: return highLabel;
        }
    }

    private void NotifyStepIfNeeded(int step, bool invokeEvent, bool forceNotify)
    {
        if (!invokeEvent)
        {
            lastStep = step;
            return;
        }

        if (!forceNotify && step == lastStep)
        {
            return;
        }

        lastStep = step;
        onStepChanged?.Invoke(step);
    }
}
