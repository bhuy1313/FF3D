using System;
using TMPro;
using UnityEngine;

/*
Usage:
- RealTimeClockDisplay goes on any object and updates a TMP_Text with the current local system time.
*/
[DisallowMultipleComponent]
public class RealTimeClockDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text clockText;

    [Header("Format")]
    [SerializeField] private string timeFormat = "HH:mm:ss";

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private void Awake()
    {
        if (clockText == null)
        {
            Debug.LogWarning($"{nameof(RealTimeClockDisplay)} on {name}: clockText is not assigned.", this);
            return;
        }

        UpdateClockText();
    }

    private void Update()
    {
        if (clockText == null)
        {
            return;
        }

        UpdateClockText();
    }

    private void UpdateClockText()
    {
        string formatToUse = string.IsNullOrWhiteSpace(timeFormat) ? "HH:mm:ss" : timeFormat;
        clockText.text = DateTime.Now.ToString(formatToUse);

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(RealTimeClockDisplay)}: {clockText.text}", this);
        }
    }
}
