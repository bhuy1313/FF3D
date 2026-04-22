using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class Setting_UIScript
{
    private const string MinimapTypeSquareLocalizationKey = "setting.general.minimap.square";
    private const string MinimapTypeCircleLocalizationKey = "setting.general.minimap.circle";

    private void SaveChanges()
    {
        isFinalizingSave = true;
        Debug.Log($"Setting_UIScript[{GetInstanceLabel()}]: SaveChanges start.", this);

        SaveResolutionSelection();
        SaveVSyncSelection();
        SaveFpsSelection();
        SaveFovSelection();
        SaveMouseSensitivitySelection();
        SaveAudioSelection();
        SaveResponseSpeedSelection();
        SaveAutoQuestionSelection();
        SaveAutoValidateSelection();
        SaveMinimapToggleSelection();
        SaveMinimapTypeSelection();
        SaveControlBindingOverrides();
        SaveLanguageSelection();

        PlayerPrefs.Save();
        CaptureSnapshot("SaveChanges immediate");
        hasUnsavedChanges = false;
        LogSaveBaselineState("immediate");

        if (saveFinalizeCoroutine != null)
        {
            StopCoroutine(saveFinalizeCoroutine);
        }

        saveFinalizeCoroutine = StartCoroutine(FinalizeSaveChangesAfterFrame());
    }

    private void ConfigureResolutionDropdown(bool useSavedSelection)
    {
        supportedResolutions.Clear();

        if (resolutionDropdown == null)
        {
            return;
        }

        supportedResolutions.AddRange(DisplaySettingsService.GetSupportedResolutions());
        if (supportedResolutions.Count == 0)
        {
            return;
        }

        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>(supportedResolutions.Count);
        foreach (DisplaySettingsService.ResolutionOption option in supportedResolutions)
        {
            options.Add(new TMP_Dropdown.OptionData(option.Label));
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);

        int selectedIndex = DisplaySettingsService.GetRecommendedIndex(supportedResolutions);
        if (useSavedSelection && DisplaySettingsService.TryGetSavedResolution(out int savedWidth, out int savedHeight))
        {
            int savedIndex = DisplaySettingsService.FindBestIndex(supportedResolutions, savedWidth, savedHeight);
            if (savedIndex >= 0)
            {
                selectedIndex = savedIndex;
            }
        }

        SetResolutionDropdownValue(selectedIndex);
    }

    private void LoadSavedResolutionSelection()
    {
        if (resolutionDropdown == null || supportedResolutions.Count == 0)
        {
            return;
        }

        if (!DisplaySettingsService.TryGetSavedResolution(out int savedWidth, out int savedHeight))
        {
            return;
        }

        int savedIndex = DisplaySettingsService.FindBestIndex(supportedResolutions, savedWidth, savedHeight);
        if (savedIndex >= 0)
        {
            SetResolutionDropdownValue(savedIndex);
        }
    }

    private void SaveResolutionSelection()
    {
        if (resolutionDropdown == null || supportedResolutions.Count == 0)
        {
            return;
        }

        int selectedIndex = Mathf.Clamp(resolutionDropdown.value, 0, supportedResolutions.Count - 1);
        DisplaySettingsService.ResolutionOption selectedResolution = supportedResolutions[selectedIndex];
        bool hasSavedResolution = DisplaySettingsService.TryGetSavedResolution(out int savedWidth, out int savedHeight);
        if (!hasSavedResolution || savedWidth != selectedResolution.Width || savedHeight != selectedResolution.Height)
        {
            DisplaySettingsService.SaveResolution(selectedResolution.Width, selectedResolution.Height);
        }

        if (Screen.width != selectedResolution.Width || Screen.height != selectedResolution.Height)
        {
            DisplaySettingsService.ApplyResolution(selectedResolution);
        }
    }

    private void LoadSavedVSyncSelection()
    {
        if (vsyncToggle == null)
        {
            return;
        }

        bool vsyncEnabled = DisplaySettingsService.GetCurrentVSyncEnabled();
        DisplaySettingsService.TryGetSavedVSyncEnabled(out vsyncEnabled);
        vsyncToggle.SetIsOnWithoutNotify(vsyncEnabled);
    }

    private void SaveVSyncSelection()
    {
        if (vsyncToggle == null)
        {
            return;
        }

        bool hasSavedVSync = DisplaySettingsService.TryGetSavedVSyncEnabled(out bool savedVSyncEnabled);
        if (!hasSavedVSync || savedVSyncEnabled != vsyncToggle.isOn)
        {
            DisplaySettingsService.SaveVSyncEnabled(vsyncToggle.isOn);
        }

        if (DisplaySettingsService.GetCurrentVSyncEnabled() != vsyncToggle.isOn)
        {
            DisplaySettingsService.ApplyVSync(vsyncToggle.isOn);
        }
    }

    private void LoadSavedFpsSelection()
    {
        if (fpsToggle == null)
        {
            return;
        }

        if (!DisplaySettingsService.TryGetSavedShowFpsOverlay(out bool showFps))
        {
            return;
        }

        fpsToggle.SetIsOnWithoutNotify(showFps);
    }

    private void ConfigureFovSlider()
    {
        if (fovSlider == null)
        {
            return;
        }

        HideFovMilestoneLabels();
        fovSlider.minValue = GameplayFovSettings.MinFov;
        fovSlider.maxValue = GameplayFovSettings.MaxFov;
        fovSlider.wholeNumbers = false;

        if (fovSliderValueText != null)
        {
            fovSliderValueText.ConfigureDisplay(SliderPercentText.PercentMode.RawSliderValue, string.Empty, string.Empty, false);
        }
    }

    private void ConfigureMouseSensitivitySlider()
    {
        if (mouseSensitivitySlider == null)
        {
            return;
        }

        mouseSensitivitySlider.minValue = GameplayMouseSensitivitySettings.SliderMinValue;
        mouseSensitivitySlider.maxValue = GameplayMouseSensitivitySettings.SliderMaxValue;
        mouseSensitivitySlider.wholeNumbers = false;

        if (mouseSensitivitySliderValueText != null)
        {
            mouseSensitivitySliderValueText.ConfigureDisplay(
                SliderPercentText.PercentMode.CenterAsHundred,
                string.Empty,
                "%",
                true,
                0,
                200,
                false);
        }
    }

    private void ConfigureAudioSliders()
    {
        ConfigureAudioSlider(masterVolumeSlider, masterVolumeSliderValueText);
        ConfigureAudioSlider(musicVolumeSlider, musicVolumeSliderValueText);
        ConfigureAudioSlider(ambienceVolumeSlider, ambienceVolumeSliderValueText);
        ConfigureAudioSlider(sfxVolumeSlider, sfxVolumeSliderValueText);
        ConfigureAudioSlider(voiceVolumeSlider, voiceVolumeSliderValueText);
        ConfigureAudioSlider(uiVolumeSlider, uiVolumeSliderValueText);
    }

    private void ConfigureAudioSlider(Slider slider, SliderPercentText valueText)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = AudioVolumeSettings.MinVolume;
        slider.maxValue = AudioVolumeSettings.MaxVolume;
        slider.wholeNumbers = false;

        if (valueText != null)
        {
            valueText.ConfigureDisplay(
                SliderPercentText.PercentMode.NormalizeToRange,
                string.Empty,
                "%",
                true,
                0,
                100,
                true);
        }
    }

    private void HideFovMilestoneLabels()
    {
        Transform fovRoot = FindNamedPanelChild(panelGrap, "FOV");
        if (fovRoot == null)
        {
            return;
        }

        ThreeStepSlider threeStepSlider = fovRoot.GetComponentInChildren<ThreeStepSlider>(true);
        if (threeStepSlider != null)
        {
            threeStepSlider.enabled = false;
        }

        LocalizedText[] localizedTexts = fovRoot.GetComponentsInChildren<LocalizedText>(true);
        for (int i = 0; i < localizedTexts.Length; i++)
        {
            LocalizedText localizedText = localizedTexts[i];
            if (localizedText == null)
            {
                continue;
            }

            string key = localizedText.LocalizationKey;
            if (!string.Equals(key, "slider.triple.milestone.low", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(key, "slider.triple.milestone.medium", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(key, "slider.triple.milestone.high", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            localizedText.gameObject.SetActive(false);
        }
    }

    private void LoadSavedFovSelection()
    {
        if (fovSlider == null)
        {
            return;
        }

        fovSlider.SetValueWithoutNotify(GameplayFovSettings.GetSavedOrDefaultFov());
        if (fovSliderValueText != null)
        {
            fovSliderValueText.RefreshDisplay();
        }

        ApplyFovSliderPreview();
    }

    private void InitializeFovDefaultSelection()
    {
        if (fovSlider == null)
        {
            return;
        }

        fovSlider.SetValueWithoutNotify(GameplayFovSettings.DefaultFov);
        if (fovSliderValueText != null)
        {
            fovSliderValueText.RefreshDisplay();
        }

        ApplyFovSliderPreview();
    }

    private void LoadSavedMouseSensitivitySelection()
    {
        if (mouseSensitivitySlider == null)
        {
            return;
        }

        float savedSensitivity = GameplayMouseSensitivitySettings.GetSavedOrDefaultSensitivity();
        mouseSensitivitySlider.SetValueWithoutNotify(GameplayMouseSensitivitySettings.SensitivityToSliderValue(savedSensitivity));
        if (mouseSensitivitySliderValueText != null)
        {
            mouseSensitivitySliderValueText.RefreshDisplay();
        }

        ApplyMouseSensitivitySliderPreview();
    }

    private void LoadSavedAudioSelection()
    {
        LoadSavedAudioSelection(masterVolumeSlider, masterVolumeSliderValueText, AudioBus.Master);
        LoadSavedAudioSelection(musicVolumeSlider, musicVolumeSliderValueText, AudioBus.Music);
        LoadSavedAudioSelection(ambienceVolumeSlider, ambienceVolumeSliderValueText, AudioBus.Ambience);
        LoadSavedAudioSelection(sfxVolumeSlider, sfxVolumeSliderValueText, AudioBus.Sfx);
        LoadSavedAudioSelection(voiceVolumeSlider, voiceVolumeSliderValueText, AudioBus.Voice);
        LoadSavedAudioSelection(uiVolumeSlider, uiVolumeSliderValueText, AudioBus.Ui);
    }

    private void LoadSavedAudioSelection(Slider slider, SliderPercentText valueText, AudioBus bus)
    {
        if (slider == null)
        {
            return;
        }

        slider.SetValueWithoutNotify(AudioVolumeSettings.GetSavedOrDefaultVolume(bus));
        if (valueText != null)
        {
            valueText.RefreshDisplay();
        }

        ApplyAudioSliderPreview(bus, slider);
    }

    private void InitializeAudioDefaultSelection()
    {
        InitializeAudioDefaultSelection(masterVolumeSlider, masterVolumeSliderValueText, AudioBus.Master);
        InitializeAudioDefaultSelection(musicVolumeSlider, musicVolumeSliderValueText, AudioBus.Music);
        InitializeAudioDefaultSelection(ambienceVolumeSlider, ambienceVolumeSliderValueText, AudioBus.Ambience);
        InitializeAudioDefaultSelection(sfxVolumeSlider, sfxVolumeSliderValueText, AudioBus.Sfx);
        InitializeAudioDefaultSelection(voiceVolumeSlider, voiceVolumeSliderValueText, AudioBus.Voice);
        InitializeAudioDefaultSelection(uiVolumeSlider, uiVolumeSliderValueText, AudioBus.Ui);
    }

    private void InitializeAudioDefaultSelection(Slider slider, SliderPercentText valueText, AudioBus bus)
    {
        if (slider == null)
        {
            return;
        }

        slider.SetValueWithoutNotify(AudioVolumeSettings.GetDefaultVolume(bus));
        if (valueText != null)
        {
            valueText.RefreshDisplay();
        }
    }

    private void InitializeMouseSensitivityDefaultSelection()
    {
        if (mouseSensitivitySlider == null)
        {
            return;
        }

        mouseSensitivitySlider.SetValueWithoutNotify(
            GameplayMouseSensitivitySettings.SensitivityToSliderValue(GameplayMouseSensitivitySettings.DefaultSensitivity));
        if (mouseSensitivitySliderValueText != null)
        {
            mouseSensitivitySliderValueText.RefreshDisplay();
        }

        ApplyMouseSensitivitySliderPreview();
    }

    private void LoadSavedResponseSpeedSelection()
    {
        if (responseSpeedSlider == null)
        {
            return;
        }

        responseSpeedSlider.SetStep(CallPhaseResponseSpeedSettings.GetSavedOrDefaultStep(), false);
    }

    private void InitializeResponseSpeedDefaultSelection()
    {
        if (responseSpeedSlider == null)
        {
            return;
        }

        responseSpeedSlider.SetStep(CallPhaseResponseSpeedSettings.DefaultStep, false);
    }

    private void LoadSavedAutoQuestionSelection()
    {
        if (autoQuestionToggle == null)
        {
            return;
        }

        autoQuestionToggle.isOn = CallPhaseAutoQuestionSettings.GetSavedOrDefaultEnabled();
    }

    private void InitializeAutoQuestionDefaultSelection()
    {
        if (autoQuestionToggle == null)
        {
            return;
        }

        autoQuestionToggle.isOn = CallPhaseAutoQuestionSettings.DefaultEnabled;
    }

    private void LoadSavedAutoValidateSelection()
    {
        if (autoValidateToggle == null)
        {
            return;
        }

        autoValidateToggle.isOn = CallPhaseAutoValidateSettings.GetSavedOrDefaultEnabled();
    }

    private void InitializeAutoValidateDefaultSelection()
    {
        if (autoValidateToggle == null)
        {
            return;
        }

        autoValidateToggle.isOn = CallPhaseAutoValidateSettings.DefaultEnabled;
    }

    private void LoadSavedMinimapTypeSelection()
    {
        if (minimapTypeDropdown == null)
        {
            return;
        }

        SetMinimapTypeDropdownValue((int)MinimapDisplaySettings.GetSavedOrDefaultType());
        ApplyMinimapTypePreview();
    }

    private void InitializeMinimapTypeDefaultSelection()
    {
        if (minimapTypeDropdown == null)
        {
            return;
        }

        RefreshMinimapTypeDropdownOptions();
        SetMinimapTypeDropdownValue((int)MinimapDisplaySettings.DefaultType);
    }

    private void SaveFpsSelection()
    {
        if (fpsToggle == null)
        {
            return;
        }

        bool hasSavedFpsPreference = DisplaySettingsService.TryGetSavedShowFpsOverlay(out bool savedShowFps);
        if (!hasSavedFpsPreference || savedShowFps != fpsToggle.isOn)
        {
            DisplaySettingsService.SaveShowFpsOverlay(fpsToggle.isOn);
        }

        FPSOverlayRuntimeController.SetOverlayVisible(fpsToggle.isOn);
    }

    private void SaveFovSelection()
    {
        if (fovSlider == null)
        {
            return;
        }

        float selectedValue = GameplayFovSettings.ClampFov(fovSlider.value);
        bool hasSavedValue = GameplayFovSettings.TryGetSavedFov(out float savedValue);
        if (!hasSavedValue || !Mathf.Approximately(savedValue, selectedValue))
        {
            GameplayFovSettings.SaveFov(selectedValue);
        }

        GameplayFovRuntimeApplier.ApplyFov(selectedValue);
    }

    private void SaveMouseSensitivitySelection()
    {
        if (mouseSensitivitySlider == null)
        {
            return;
        }

        float selectedSensitivity = GameplayMouseSensitivitySettings.ClampSensitivity(
            GameplayMouseSensitivitySettings.SliderValueToSensitivity(mouseSensitivitySlider.value));
        bool hasSavedValue = GameplayMouseSensitivitySettings.TryGetSavedSensitivity(out float savedSensitivity);
        if (!hasSavedValue || !Mathf.Approximately(savedSensitivity, selectedSensitivity))
        {
            GameplayMouseSensitivitySettings.SaveSensitivity(selectedSensitivity);
        }

        GameplayMouseSensitivityRuntimeApplier.ApplySensitivity(selectedSensitivity);
    }

    private void SaveAudioSelection()
    {
        SaveAudioSelection(masterVolumeSlider, AudioBus.Master);
        SaveAudioSelection(musicVolumeSlider, AudioBus.Music);
        SaveAudioSelection(ambienceVolumeSlider, AudioBus.Ambience);
        SaveAudioSelection(sfxVolumeSlider, AudioBus.Sfx);
        SaveAudioSelection(voiceVolumeSlider, AudioBus.Voice);
        SaveAudioSelection(uiVolumeSlider, AudioBus.Ui);
    }

    private void SaveAudioSelection(Slider slider, AudioBus bus)
    {
        if (slider == null)
        {
            return;
        }

        AudioService.SetBusVolume(bus, slider.value, true);
    }

    private void SaveResponseSpeedSelection()
    {
        if (responseSpeedSlider == null)
        {
            return;
        }

        int selectedStep = responseSpeedSlider.GetCurrentStep();
        bool hasSavedStep = CallPhaseResponseSpeedSettings.TryGetSavedStep(out int savedStep);
        if (!hasSavedStep || savedStep != selectedStep)
        {
            CallPhaseResponseSpeedSettings.SaveStep(selectedStep);
        }
    }

    private void SaveAutoQuestionSelection()
    {
        if (autoQuestionToggle == null)
        {
            return;
        }

        bool selectedValue = autoQuestionToggle.isOn;
        bool hasSavedValue = CallPhaseAutoQuestionSettings.TryGetSavedEnabled(out bool savedValue);
        if (!hasSavedValue || savedValue != selectedValue)
        {
            CallPhaseAutoQuestionSettings.SaveEnabled(selectedValue);
        }
    }

    private void SaveAutoValidateSelection()
    {
        if (autoValidateToggle == null)
        {
            return;
        }

        bool selectedValue = autoValidateToggle.isOn;
        bool hasSavedValue = CallPhaseAutoValidateSettings.TryGetSavedEnabled(out bool savedValue);
        if (!hasSavedValue || savedValue != selectedValue)
        {
            CallPhaseAutoValidateSettings.SaveEnabled(selectedValue);
        }
    }

    private void LoadSavedMinimapToggleSelection()
    {
        if (minimapToggle == null)
        {
            return;
        }

        minimapToggle.isOn = MinimapDisplaySettings.GetSavedOrDefaultEnabled();
    }

    private void InitializeMinimapToggleDefaultSelection()
    {
        if (minimapToggle == null)
        {
            return;
        }

        minimapToggle.isOn = MinimapDisplaySettings.DefaultEnabled;
    }

    private void SaveMinimapToggleSelection()
    {
        if (minimapToggle == null)
        {
            return;
        }

        bool selectedValue = minimapToggle.isOn;
        bool hasSavedValue = MinimapDisplaySettings.TryGetSavedEnabled(out bool savedValue);
        if (!hasSavedValue || savedValue != selectedValue)
        {
            MinimapDisplaySettings.SaveEnabled(selectedValue);
        }

        MinimapDisplayRuntime.ApplyEnabled(selectedValue);
    }

    private void SaveMinimapTypeSelection()
    {
        if (minimapTypeDropdown == null)
        {
            return;
        }

        MinimapDisplayType selectedType = GetSelectedMinimapDisplayType();
        bool hasSavedValue = MinimapDisplaySettings.TryGetSavedType(out MinimapDisplayType savedType);
        if (!hasSavedValue || savedType != selectedType)
        {
            MinimapDisplaySettings.SaveType(selectedType);
        }

        MinimapDisplayRuntime.ApplyType(selectedType);
    }

    private void SaveLanguageSelection()
    {
        if (LanguageManager.Instance == null)
        {
            return;
        }

        if (languageToggleBinder != null && languageToggleBinder.TryGetSelectedLanguage(out AppLanguage selectedLanguage))
        {
            if (LanguageManager.Instance.CurrentLanguage != selectedLanguage)
            {
                LanguageManager.Instance.SetLanguage(selectedLanguage, true, true);
            }

            return;
        }

        LanguageManager.Instance.SetLanguage(LanguageManager.Instance.CurrentLanguage, true, false);
    }

    private void OnFpsToggleValueChanged(bool _)
    {
        ApplyFpsTogglePreview();
    }

    private void OnFovSliderValueChanged(float _)
    {
        ApplyFovSliderPreview();
    }

    private void OnMouseSensitivitySliderValueChanged(float _)
    {
        ApplyMouseSensitivitySliderPreview();
    }

    private void OnMinimapToggleValueChanged(bool _)
    {
        ApplyMinimapTogglePreview();
    }

    private void OnMinimapTypeDropdownValueChanged(int _)
    {
        ApplyMinimapTypePreview();
    }

    private void ApplyFpsTogglePreview()
    {
        if (fpsToggle == null)
        {
            return;
        }

        FPSOverlayRuntimeController.SetOverlayVisible(fpsToggle.isOn);
    }

    private void ApplyFovSliderPreview()
    {
        if (fovSlider == null)
        {
            return;
        }

        RefreshFovValueText();
        GameplayFovRuntimeApplier.ApplyFov(fovSlider.value);
    }

    private void ApplyMouseSensitivitySliderPreview()
    {
        if (mouseSensitivitySlider == null)
        {
            return;
        }

        GameplayMouseSensitivityRuntimeApplier.ApplySensitivity(
            GameplayMouseSensitivitySettings.SliderValueToSensitivity(mouseSensitivitySlider.value));
    }

    private void ApplyAudioSliderPreview(AudioBus bus, Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        AudioService.SetBusVolume(bus, slider.value, false);
    }

    private void ApplyMinimapTogglePreview()
    {
        if (minimapToggle == null)
        {
            return;
        }

        MinimapDisplayRuntime.ApplyEnabled(minimapToggle.isOn);
    }

    private void ApplyMinimapTypePreview()
    {
        if (minimapTypeDropdown == null)
        {
            return;
        }

        MinimapDisplayRuntime.ApplyType(GetSelectedMinimapDisplayType());
    }

    private void RefreshFovValueText()
    {
        if (fovValueText == null || fovSlider == null)
        {
            return;
        }

        fovValueText.text = Mathf.RoundToInt(GameplayFovSettings.ClampFov(fovSlider.value)).ToString();
    }

    private void SetResolutionDropdownValue(int index)
    {
        if (resolutionDropdown == null || supportedResolutions.Count == 0)
        {
            return;
        }

        int clampedIndex = Mathf.Clamp(index, 0, supportedResolutions.Count - 1);
        resolutionDropdown.SetValueWithoutNotify(clampedIndex);
        resolutionDropdown.RefreshShownValue();
    }

    private void RefreshMinimapTypeDropdownOptions()
    {
        if (minimapTypeDropdown == null)
        {
            return;
        }

        int currentValue = minimapTypeDropdown.value;
        minimapTypeDropdown.ClearOptions();
        minimapTypeDropdown.AddOptions(new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData(LanguageManager.Tr(MinimapTypeSquareLocalizationKey, "Hình vuông")),
            new TMP_Dropdown.OptionData(LanguageManager.Tr(MinimapTypeCircleLocalizationKey, "Hình tròn"))
        });
        minimapTypeDropdown.SetValueWithoutNotify(Mathf.Clamp(currentValue, 0, 1));
        minimapTypeDropdown.RefreshShownValue();
    }

    private void SetMinimapTypeDropdownValue(int index)
    {
        if (minimapTypeDropdown == null)
        {
            return;
        }

        RefreshMinimapTypeDropdownOptions();
        int optionCount = Mathf.Max(1, minimapTypeDropdown.options.Count);
        int clampedIndex = Mathf.Clamp(index, 0, optionCount - 1);
        minimapTypeDropdown.SetValueWithoutNotify(clampedIndex);
        minimapTypeDropdown.RefreshShownValue();
    }

    private MinimapDisplayType GetSelectedMinimapDisplayType()
    {
        if (minimapTypeDropdown == null)
        {
            return MinimapDisplaySettings.DefaultType;
        }

        return MinimapDisplaySettings.ClampType(minimapTypeDropdown.value);
    }
}
