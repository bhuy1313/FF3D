using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class SmokeAlarmDetector : MonoBehaviour, IMissionSignalResettable
{
    [Header("Detection")]
    [SerializeField] private bool autoCollectChildHazards = true;
    [SerializeField] private SmokeHazard[] linkedSmokeHazards = System.Array.Empty<SmokeHazard>();
    [SerializeField, Range(0f, 1f)] private float triggerSmokeThreshold = 0.18f;
    [SerializeField, Range(0f, 1f)] private float clearSmokeThreshold = 0.08f;
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private bool allowAutomaticResetAfterClear = true;
    [SerializeField] private float triggerDelaySeconds = 0.4f;

    [Header("Mission Signal")]
    [SerializeField] private IncidentMissionSystem missionSystem;
    [SerializeField] private string alarmSignalKey = "smoke-alarm";

    [Header("Alarm")]
    [SerializeField] private AudioSource alarmAudioSource;
    [SerializeField] private AudioClip alarmStartClip;
    [SerializeField] private AudioClip alarmLoopClip;
    [SerializeField] private bool loopAlarmAudio = true;
    [SerializeField] private Renderer[] indicatorRenderers = System.Array.Empty<Renderer>();
    [SerializeField] private Light[] indicatorLights = System.Array.Empty<Light>();
    [SerializeField] private string indicatorColorProperty = "_BaseColor";
    [SerializeField] private Color idleColor = new Color(0.18f, 0.92f, 0.22f, 1f);
    [SerializeField] private Color alarmColor = new Color(1f, 0.15f, 0.12f, 1f);
    [SerializeField] private float idleLightIntensity = 0.15f;
    [SerializeField] private float alarmLightIntensity = 2.4f;
    [SerializeField] private float alarmLightBlinkSpeed = 10f;

    [Header("Events")]
    [SerializeField] private UnityEvent onAlarmTriggered;
    [SerializeField] private UnityEvent onAlarmCleared;

    [Header("Runtime")]
    [SerializeField] private bool isAlarmTriggered;
    [SerializeField] private bool hasRaisedSignal;
    [SerializeField, Range(0f, 1f)] private float currentDetectedSmokeDensity;

    private Coroutine pendingTriggerRoutine;

    public bool IsAlarmTriggered => isAlarmTriggered;
    public float CurrentDetectedSmokeDensity => currentDetectedSmokeDensity;

    private void Awake()
    {
        ResolveMissionSystem();
        ResolveLinkedHazards();
        ApplyPresentation(immediate: true);
    }

    private void OnEnable()
    {
        ResolveLinkedHazards();
        ApplyPresentation(immediate: true);
    }

    private void OnDisable()
    {
        if (pendingTriggerRoutine != null)
        {
            StopCoroutine(pendingTriggerRoutine);
            pendingTriggerRoutine = null;
        }

        StopAlarmAudio();
    }

    private void OnValidate()
    {
        triggerSmokeThreshold = Mathf.Clamp01(triggerSmokeThreshold);
        clearSmokeThreshold = Mathf.Clamp(clearSmokeThreshold, 0f, triggerSmokeThreshold);
        triggerDelaySeconds = Mathf.Max(0f, triggerDelaySeconds);
        idleLightIntensity = Mathf.Max(0f, idleLightIntensity);
        alarmLightIntensity = Mathf.Max(0f, alarmLightIntensity);
        alarmLightBlinkSpeed = Mathf.Max(0f, alarmLightBlinkSpeed);
        currentDetectedSmokeDensity = Mathf.Clamp01(currentDetectedSmokeDensity);
        ResolveLinkedHazards();
        ApplyPresentation(immediate: true);
    }

    private void Update()
    {
        ResolveLinkedHazards();
        currentDetectedSmokeDensity = EvaluateHighestSmokeDensity();

        if (!isAlarmTriggered)
        {
            if (currentDetectedSmokeDensity >= triggerSmokeThreshold)
            {
                TryBeginTrigger();
            }
        }
        else if (allowAutomaticResetAfterClear && currentDetectedSmokeDensity <= clearSmokeThreshold)
        {
            ClearAlarm();
        }

        ApplyPresentation(immediate: false);
    }

    public void ResetMissionSignalState()
    {
        hasRaisedSignal = false;
        ClearAlarm();
    }

    [ContextMenu("Trigger Alarm")]
    public void TriggerAlarm()
    {
        TriggerAlarmInternal();
    }

    [ContextMenu("Clear Alarm")]
    public void ClearAlarm()
    {
        if (pendingTriggerRoutine != null)
        {
            StopCoroutine(pendingTriggerRoutine);
            pendingTriggerRoutine = null;
        }

        if (!isAlarmTriggered)
        {
            return;
        }

        isAlarmTriggered = false;
        StopAlarmAudio();
        ApplyPresentation(immediate: true);
        onAlarmCleared?.Invoke();
    }

    private void TryBeginTrigger()
    {
        if (isAlarmTriggered || pendingTriggerRoutine != null)
        {
            return;
        }

        if (triggerOnce && hasRaisedSignal)
        {
            return;
        }

        if (triggerDelaySeconds <= 0.01f)
        {
            TriggerAlarmInternal();
            return;
        }

        pendingTriggerRoutine = StartCoroutine(TriggerAfterDelay());
    }

    private IEnumerator TriggerAfterDelay()
    {
        yield return new WaitForSeconds(triggerDelaySeconds);
        pendingTriggerRoutine = null;

        if (currentDetectedSmokeDensity >= triggerSmokeThreshold)
        {
            TriggerAlarmInternal();
        }
    }

    private void TriggerAlarmInternal()
    {
        if (isAlarmTriggered)
        {
            return;
        }

        if (triggerOnce && hasRaisedSignal)
        {
            return;
        }

        isAlarmTriggered = true;
        RaiseMissionSignal();
        PlayAlarmAudio();
        ApplyPresentation(immediate: true);
        onAlarmTriggered?.Invoke();
    }

    private void RaiseMissionSignal()
    {
        if (hasRaisedSignal || string.IsNullOrWhiteSpace(alarmSignalKey))
        {
            return;
        }

        ResolveMissionSystem();
        if (missionSystem != null && missionSystem.NotifySignal(alarmSignalKey))
        {
            hasRaisedSignal = true;
        }
    }

    private void ResolveMissionSystem()
    {
        if (missionSystem == null)
        {
            missionSystem = FindAnyObjectByType<IncidentMissionSystem>();
        }
    }

    private void ResolveLinkedHazards()
    {
        List<SmokeHazard> resolved = new List<SmokeHazard>();

        if (linkedSmokeHazards != null)
        {
            for (int i = 0; i < linkedSmokeHazards.Length; i++)
            {
                SmokeHazard hazard = linkedSmokeHazards[i];
                if (hazard != null && !resolved.Contains(hazard))
                {
                    resolved.Add(hazard);
                }
            }
        }

        if (autoCollectChildHazards)
        {
            SmokeHazard[] childHazards = GetComponentsInChildren<SmokeHazard>(true);
            for (int i = 0; i < childHazards.Length; i++)
            {
                SmokeHazard hazard = childHazards[i];
                if (hazard != null && !resolved.Contains(hazard))
                {
                    resolved.Add(hazard);
                }
            }
        }

        linkedSmokeHazards = resolved.ToArray();
    }

    private float EvaluateHighestSmokeDensity()
    {
        float highestDensity = 0f;
        if (linkedSmokeHazards == null)
        {
            return highestDensity;
        }

        for (int i = 0; i < linkedSmokeHazards.Length; i++)
        {
            SmokeHazard hazard = linkedSmokeHazards[i];
            if (hazard == null)
            {
                continue;
            }

            highestDensity = Mathf.Max(highestDensity, hazard.CurrentSmokeDensity);
        }

        return Mathf.Clamp01(highestDensity);
    }

    private void ApplyPresentation(bool immediate)
    {
        Color targetColor = isAlarmTriggered ? alarmColor : idleColor;
        float targetBaseIntensity = isAlarmTriggered ? alarmLightIntensity : idleLightIntensity;
        float blinkMultiplier = 1f;

        if (isAlarmTriggered && !immediate && alarmLightBlinkSpeed > 0f)
        {
            blinkMultiplier = Mathf.Lerp(0.3f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.time * alarmLightBlinkSpeed));
        }

        for (int i = 0; i < indicatorLights.Length; i++)
        {
            Light indicator = indicatorLights[i];
            if (indicator == null)
            {
                continue;
            }

            indicator.color = targetColor;
            indicator.intensity = targetBaseIntensity * blinkMultiplier;
            indicator.enabled = indicator.intensity > 0f;
        }

        for (int i = 0; i < indicatorRenderers.Length; i++)
        {
            Renderer indicator = indicatorRenderers[i];
            if (indicator == null)
            {
                continue;
            }

            ApplyRendererColor(indicator, targetColor);
        }
    }

    private void ApplyRendererColor(Renderer targetRenderer, Color color)
    {
        Material material = Application.isPlaying ? targetRenderer.material : targetRenderer.sharedMaterial;
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(indicatorColorProperty))
        {
            material.SetColor(indicatorColorProperty, color);
            return;
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private void PlayAlarmAudio()
    {
        if (alarmAudioSource == null)
        {
            return;
        }

        if (alarmStartClip != null)
        {
            alarmAudioSource.PlayOneShot(alarmStartClip);
        }

        if (alarmLoopClip == null)
        {
            return;
        }

        alarmAudioSource.clip = alarmLoopClip;
        alarmAudioSource.loop = loopAlarmAudio;
        alarmAudioSource.Play();
    }

    private void StopAlarmAudio()
    {
        if (alarmAudioSource == null)
        {
            return;
        }

        if (alarmAudioSource.isPlaying)
        {
            alarmAudioSource.Stop();
        }

        if (alarmAudioSource.clip == alarmLoopClip)
        {
            alarmAudioSource.clip = null;
        }
    }
}
