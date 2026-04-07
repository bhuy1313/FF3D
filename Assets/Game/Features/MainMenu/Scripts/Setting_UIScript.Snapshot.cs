using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class Setting_UIScript
{
    private void CaptureDefaultState()
    {
        toggleDefaults.Clear();
        sliderDefaults.Clear();
        dropdownDefaults.Clear();
        tmpDropdownDefaults.Clear();
        inputFieldDefaults.Clear();
        tmpInputFieldDefaults.Clear();

        CapturePanelDefaults(panelGen);
        CapturePanelDefaults(panelGrap);
        CapturePanelDefaults(panelAudio);
        CapturePanelDefaults(panelCont);

        hasDefaultState = true;
    }

    private void CapturePanelDefaults(GameObject panelRoot)
    {
        if (panelRoot == null)
        {
            return;
        }

        Toggle[] toggles = panelRoot.GetComponentsInChildren<Toggle>(true);
        foreach (Toggle toggle in toggles)
        {
            if (toggle != null && !toggleDefaults.ContainsKey(toggle))
            {
                toggleDefaults.Add(toggle, toggle.isOn);
            }
        }

        Slider[] sliders = panelRoot.GetComponentsInChildren<Slider>(true);
        foreach (Slider slider in sliders)
        {
            if (slider != null && !sliderDefaults.ContainsKey(slider))
            {
                sliderDefaults.Add(slider, slider.value);
            }
        }

        Dropdown[] dropdowns = panelRoot.GetComponentsInChildren<Dropdown>(true);
        foreach (Dropdown dropdown in dropdowns)
        {
            if (dropdown != null && !dropdownDefaults.ContainsKey(dropdown))
            {
                dropdownDefaults.Add(dropdown, dropdown.value);
            }
        }

        TMP_Dropdown[] tmpDropdowns = panelRoot.GetComponentsInChildren<TMP_Dropdown>(true);
        foreach (TMP_Dropdown dropdown in tmpDropdowns)
        {
            if (dropdown != null && !tmpDropdownDefaults.ContainsKey(dropdown))
            {
                tmpDropdownDefaults.Add(dropdown, dropdown.value);
            }
        }

        InputField[] inputFields = panelRoot.GetComponentsInChildren<InputField>(true);
        foreach (InputField inputField in inputFields)
        {
            if (inputField != null && !inputFieldDefaults.ContainsKey(inputField))
            {
                inputFieldDefaults.Add(inputField, inputField.text);
            }
        }

        TMP_InputField[] tmpInputFields = panelRoot.GetComponentsInChildren<TMP_InputField>(true);
        foreach (TMP_InputField inputField in tmpInputFields)
        {
            if (inputField != null && !tmpInputFieldDefaults.ContainsKey(inputField))
            {
                tmpInputFieldDefaults.Add(inputField, inputField.text);
            }
        }
    }

    private void CaptureSnapshot(string reason = null)
    {
        toggleSnapshot.Clear();
        sliderSnapshot.Clear();
        dropdownSnapshot.Clear();
        tmpDropdownSnapshot.Clear();
        inputFieldSnapshot.Clear();
        tmpInputFieldSnapshot.Clear();

        CapturePanelSnapshot(panelGen);
        CapturePanelSnapshot(panelGrap);
        CapturePanelSnapshot(panelAudio);
        CapturePanelSnapshot(panelCont);

        if (LanguageManager.Instance != null)
        {
            languageSnapshot = LanguageManager.Instance.CurrentLanguage;
        }

        hasSnapshot = true;

        if (vsyncToggle != null && toggleSnapshot.TryGetValue(vsyncToggle, out bool vsyncSnapshotValue))
        {
            Debug.Log(
                $"Setting_UIScript[{GetInstanceLabel()}]: CaptureSnapshot reason='{reason ?? "<none>"}' VSync current={vsyncToggle.isOn} snapshot={vsyncSnapshotValue}",
                this);
        }
    }

    private void CapturePanelSnapshot(GameObject panelRoot)
    {
        if (panelRoot == null)
        {
            return;
        }

        Toggle[] toggles = panelRoot.GetComponentsInChildren<Toggle>(true);
        foreach (Toggle toggle in toggles)
        {
            if (toggle != null && !toggleSnapshot.ContainsKey(toggle))
            {
                toggleSnapshot.Add(toggle, toggle.isOn);
            }
        }

        Slider[] sliders = panelRoot.GetComponentsInChildren<Slider>(true);
        foreach (Slider slider in sliders)
        {
            if (slider != null && !sliderSnapshot.ContainsKey(slider))
            {
                sliderSnapshot.Add(slider, slider.value);
            }
        }

        Dropdown[] dropdowns = panelRoot.GetComponentsInChildren<Dropdown>(true);
        foreach (Dropdown dropdown in dropdowns)
        {
            if (dropdown != null && !dropdownSnapshot.ContainsKey(dropdown))
            {
                dropdownSnapshot.Add(dropdown, dropdown.value);
            }
        }

        TMP_Dropdown[] tmpDropdowns = panelRoot.GetComponentsInChildren<TMP_Dropdown>(true);
        foreach (TMP_Dropdown dropdown in tmpDropdowns)
        {
            if (dropdown != null && !tmpDropdownSnapshot.ContainsKey(dropdown))
            {
                tmpDropdownSnapshot.Add(dropdown, dropdown.value);
            }
        }

        InputField[] inputFields = panelRoot.GetComponentsInChildren<InputField>(true);
        foreach (InputField inputField in inputFields)
        {
            if (inputField != null && !inputFieldSnapshot.ContainsKey(inputField))
            {
                inputFieldSnapshot.Add(inputField, inputField.text);
            }
        }

        TMP_InputField[] tmpInputFields = panelRoot.GetComponentsInChildren<TMP_InputField>(true);
        foreach (TMP_InputField inputField in tmpInputFields)
        {
            if (inputField != null && !tmpInputFieldSnapshot.ContainsKey(inputField))
            {
                tmpInputFieldSnapshot.Add(inputField, inputField.text);
            }
        }
    }

    private bool HasDifferencesFromSnapshot()
    {
        return TryGetDifferenceDescription(out _);
    }

    private bool TryGetDifferenceDescription(out string description)
    {
        description = null;

        if (!hasSnapshot)
        {
            return false;
        }

        foreach (KeyValuePair<Toggle, bool> entry in toggleSnapshot)
        {
            if (entry.Key == null)
            {
                description = "Toggle snapshot entry is missing.";
                return true;
            }

            if (entry.Key.isOn != entry.Value)
            {
                description = $"Toggle [{GetHierarchyPath(entry.Key.transform)}] current={entry.Key.isOn} snapshot={entry.Value}";
                return true;
            }
        }

        foreach (KeyValuePair<Slider, float> entry in sliderSnapshot)
        {
            if (entry.Key == null)
            {
                description = "Slider snapshot entry is missing.";
                return true;
            }

            if (!Mathf.Approximately(entry.Key.value, entry.Value))
            {
                description = $"Slider [{GetHierarchyPath(entry.Key.transform)}] current={entry.Key.value} snapshot={entry.Value}";
                return true;
            }
        }

        foreach (KeyValuePair<Dropdown, int> entry in dropdownSnapshot)
        {
            if (entry.Key == null)
            {
                description = "Dropdown snapshot entry is missing.";
                return true;
            }

            if (entry.Key.value != entry.Value)
            {
                description = $"Dropdown [{GetHierarchyPath(entry.Key.transform)}] current={entry.Key.value} snapshot={entry.Value}";
                return true;
            }
        }

        foreach (KeyValuePair<TMP_Dropdown, int> entry in tmpDropdownSnapshot)
        {
            if (entry.Key == null)
            {
                description = "TMP_Dropdown snapshot entry is missing.";
                return true;
            }

            if (entry.Key.value != entry.Value)
            {
                description = $"TMP_Dropdown [{GetHierarchyPath(entry.Key.transform)}] current={entry.Key.value} snapshot={entry.Value}";
                return true;
            }
        }

        foreach (KeyValuePair<InputField, string> entry in inputFieldSnapshot)
        {
            if (entry.Key == null)
            {
                description = "InputField snapshot entry is missing.";
                return true;
            }

            if (!string.Equals(entry.Key.text, entry.Value, StringComparison.Ordinal))
            {
                description = $"InputField [{GetHierarchyPath(entry.Key.transform)}] current='{entry.Key.text}' snapshot='{entry.Value}'";
                return true;
            }
        }

        foreach (KeyValuePair<TMP_InputField, string> entry in tmpInputFieldSnapshot)
        {
            if (entry.Key == null)
            {
                description = "TMP_InputField snapshot entry is missing.";
                return true;
            }

            if (!string.Equals(entry.Key.text, entry.Value, StringComparison.Ordinal))
            {
                description = $"TMP_InputField [{GetHierarchyPath(entry.Key.transform)}] current='{entry.Key.text}' snapshot='{entry.Value}'";
                return true;
            }
        }

        if (LanguageManager.Instance != null && LanguageManager.Instance.CurrentLanguage != languageSnapshot)
        {
            description = $"Language current={LanguageManager.Instance.CurrentLanguage} snapshot={languageSnapshot}";
            return true;
        }

        return false;
    }

    private void RestoreSnapshot()
    {
        if (!hasSnapshot)
        {
            return;
        }

        isRestoringSnapshot = true;

        foreach (KeyValuePair<Toggle, bool> entry in toggleSnapshot)
        {
            if (entry.Key != null && entry.Key.isOn != entry.Value)
            {
                entry.Key.isOn = entry.Value;
            }
        }

        foreach (KeyValuePair<Slider, float> entry in sliderSnapshot)
        {
            if (entry.Key != null && !Mathf.Approximately(entry.Key.value, entry.Value))
            {
                entry.Key.value = entry.Value;
            }
        }

        foreach (KeyValuePair<Dropdown, int> entry in dropdownSnapshot)
        {
            if (entry.Key != null && entry.Key.value != entry.Value)
            {
                entry.Key.value = entry.Value;
            }
        }

        foreach (KeyValuePair<TMP_Dropdown, int> entry in tmpDropdownSnapshot)
        {
            if (entry.Key != null && entry.Key.value != entry.Value)
            {
                entry.Key.value = entry.Value;
            }
        }

        foreach (KeyValuePair<InputField, string> entry in inputFieldSnapshot)
        {
            if (entry.Key != null && !string.Equals(entry.Key.text, entry.Value, StringComparison.Ordinal))
            {
                entry.Key.text = entry.Value;
            }
        }

        foreach (KeyValuePair<TMP_InputField, string> entry in tmpInputFieldSnapshot)
        {
            if (entry.Key != null && !string.Equals(entry.Key.text, entry.Value, StringComparison.Ordinal))
            {
                entry.Key.text = entry.Value;
            }
        }

        if (LanguageManager.Instance != null && LanguageManager.Instance.CurrentLanguage != languageSnapshot)
        {
            LanguageManager.Instance.SetLanguage(languageSnapshot, false, true);
        }

        isRestoringSnapshot = false;
        hasUnsavedChanges = false;
    }

    private void RestoreDefaults()
    {
        if (!hasDefaultState)
        {
            return;
        }

        isRestoringSnapshot = true;

        foreach (KeyValuePair<Toggle, bool> entry in toggleDefaults)
        {
            if (entry.Key != null && entry.Key.isOn != entry.Value)
            {
                entry.Key.isOn = entry.Value;
            }
        }

        foreach (KeyValuePair<Slider, float> entry in sliderDefaults)
        {
            if (entry.Key != null && !Mathf.Approximately(entry.Key.value, entry.Value))
            {
                entry.Key.value = entry.Value;
            }
        }

        foreach (KeyValuePair<Dropdown, int> entry in dropdownDefaults)
        {
            if (entry.Key != null && entry.Key.value != entry.Value)
            {
                entry.Key.value = entry.Value;
            }
        }

        foreach (KeyValuePair<TMP_Dropdown, int> entry in tmpDropdownDefaults)
        {
            if (entry.Key != null && entry.Key.value != entry.Value)
            {
                entry.Key.value = entry.Value;
            }
        }

        foreach (KeyValuePair<InputField, string> entry in inputFieldDefaults)
        {
            if (entry.Key != null && !string.Equals(entry.Key.text, entry.Value, StringComparison.Ordinal))
            {
                entry.Key.text = entry.Value;
            }
        }

        foreach (KeyValuePair<TMP_InputField, string> entry in tmpInputFieldDefaults)
        {
            if (entry.Key != null && !string.Equals(entry.Key.text, entry.Value, StringComparison.Ordinal))
            {
                entry.Key.text = entry.Value;
            }
        }

        if (languageToggleBinder != null)
        {
            languageToggleBinder.SetSelectedLanguage(defaultLanguage);
        }
        else if (LanguageManager.Instance != null && LanguageManager.Instance.CurrentLanguage != defaultLanguage)
        {
            LanguageManager.Instance.SetLanguage(defaultLanguage, false, true);
        }

        isRestoringSnapshot = false;
        hasUnsavedChanges = HasDifferencesFromSnapshot();
    }

    private AppLanguage GetConfirmationLanguage()
    {
        if (confirmUsesSavedLanguage && hasSnapshot)
        {
            return languageSnapshot;
        }

        if (LanguageManager.Instance != null)
        {
            return LanguageManager.Instance.CurrentLanguage;
        }

        if (languageToggleBinder != null && languageToggleBinder.TryGetSelectedLanguage(out AppLanguage selectedLanguage))
        {
            return selectedLanguage;
        }

        return AppLanguage.Vietnamese;
    }

    private string LocalizeText(string key, string fallback, AppLanguage language)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return fallback;
        }

        return LanguageManager.Tr(key, language, fallback);
    }

    private void RefreshDirtyState()
    {
        if (isFinalizingSave)
        {
            hasUnsavedChanges = false;
            return;
        }

        hasUnsavedChanges = HasDifferencesFromSnapshot();
    }

    private IEnumerator FinalizeSaveChangesAfterFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        CaptureSnapshot("SaveChanges finalized");
        isFinalizingSave = false;
        RefreshDirtyState();
        saveFinalizeCoroutine = null;
        LogSaveBaselineState("finalized");

        if (hasUnsavedChanges && TryGetDifferenceDescription(out string remainingDifference))
        {
            Debug.LogWarning($"Setting_UIScript: State is still dirty after SaveChanges. {remainingDifference}", this);
        }
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return "<null>";
        }

        List<string> segments = new List<string>();
        Transform current = target;
        while (current != null)
        {
            segments.Add(current.name);
            current = current.parent;
        }

        segments.Reverse();
        return string.Join("/", segments);
    }

    private void LogSaveBaselineState(string stage)
    {
        if (vsyncToggle != null && toggleSnapshot.TryGetValue(vsyncToggle, out bool vsyncSnapshotValue))
        {
            Debug.Log(
                $"Setting_UIScript[{GetInstanceLabel()}]: Save baseline ({stage}) VSync current={vsyncToggle.isOn} snapshot={vsyncSnapshotValue} savedPref={ReadBoolPref(DisplaySettingsService.VSyncEnabledKey)} currentRuntimeVSync={DisplaySettingsService.GetCurrentVSyncEnabled()}",
                this);
        }

        if (resolutionDropdown != null && tmpDropdownSnapshot.TryGetValue(resolutionDropdown, out int resolutionSnapshotValue))
        {
            Debug.Log(
                $"Setting_UIScript[{GetInstanceLabel()}]: Save baseline ({stage}) Resolution current={resolutionDropdown.value} snapshot={resolutionSnapshotValue}",
                this);
        }
    }

    private static string ReadBoolPref(string key)
    {
        if (!PlayerPrefs.HasKey(key))
        {
            return "<missing>";
        }

        return PlayerPrefs.GetInt(key, 0) != 0 ? "true" : "false";
    }
}
