using System;
using System.Collections;
using TrueJourney.BotBehavior;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class HazardIsolationDevice : MonoBehaviour, IInteractable, IBotHazardIsolationTarget
{
    private enum IsolationHazardType
    {
        None = 0,
        Electrical = 1,
        Gas = 2
    }

    [Header("Isolation")]
    [SerializeField] private bool startsIsolated;
    [SerializeField] private bool allowToggleAfterIsolation;
    [SerializeField] private IsolationHazardType hazardType = IsolationHazardType.None;
    [SerializeField] private bool applyHazardTypeToLinkedFires = true;
    [SerializeField] private bool autoCollectChildFires = true;
    [SerializeField] private Fire[] linkedFires = new Fire[0];

    [Header("Interaction")]
    [SerializeField] private float interactionDuration = 0.65f;
    [SerializeField] private bool lockPlayerWhileInteracting = true;

    [Header("Presentation")]
    [SerializeField] private string hazardDisplayName = string.Empty;
    [SerializeField] private Renderer[] indicatorRenderers = new Renderer[0];
    [SerializeField] private Light[] indicatorLights = new Light[0];
    [SerializeField] private GameObject[] activeStateObjects = new GameObject[0];
    [SerializeField] private GameObject[] isolatedStateObjects = new GameObject[0];
    [SerializeField] private string indicatorColorProperty = "_BaseColor";
    [SerializeField] private Color activeIndicatorColor = new Color(1f, 0.32f, 0.18f, 1f);
    [SerializeField] private Color isolatedIndicatorColor = new Color(0.28f, 1f, 0.42f, 1f);
    [SerializeField] private float activeLightIntensity = 2.2f;
    [SerializeField] private float isolatedLightIntensity = 1.25f;
    [SerializeField] private AudioSource stateAudioSource;
    [SerializeField] private AudioClip isolatedAudioClip;
    [SerializeField] private AudioClip reactivatedAudioClip;

    [Header("Mission Signals")]
    [SerializeField] private IncidentMissionSystem missionSystem;
    [SerializeField] private string isolatedSignalKey;
    [SerializeField] private string reactivatedSignalKey;

    [Header("Events")]
    [SerializeField] private UnityEvent onHazardIsolated;
    [SerializeField] private UnityEvent onHazardReactivated;

    [Header("Runtime")]
    [SerializeField] private bool isIsolated;
    [SerializeField] private bool isTransitionInProgress;
    [SerializeField] private string currentStateSummary;

    public bool IsIsolated => isIsolated;
    public bool IsTransitionInProgress => isTransitionInProgress;
    public bool IsHazardActive => !isIsolated;
    public bool IsInteractionAvailable => !isTransitionInProgress;
    public string CurrentStateSummary => currentStateSummary;
    public string HazardDisplayName => ResolveHazardDisplayName();
    public FireHazardType HazardType => ResolveFireHazardType();

    private Coroutine transitionRoutine;
    private PlayerActionLock activePlayerLock;

    private void Awake()
    {
        ResolveLinkedFires();
        ApplyIsolationState(startsIsolated, invokeEvents: false);
    }

    private void OnEnable()
    {
        BotRuntimeRegistry.RegisterHazardIsolationTarget(this);
        ResolveLinkedFires();
        ApplyIsolationState(startsIsolated, invokeEvents: false);
    }

    private void OnDisable()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        isTransitionInProgress = false;
        ReleasePlayerLock();
        BotRuntimeRegistry.UnregisterHazardIsolationTarget(this);
    }

    private void OnValidate()
    {
        ResolveLinkedFires();
        ApplyLinkedFireConfiguration();
        interactionDuration = Mathf.Max(0f, interactionDuration);
        activeLightIntensity = Mathf.Max(0f, activeLightIntensity);
        isolatedLightIntensity = Mathf.Max(0f, isolatedLightIntensity);
        RefreshPresentation();
    }

    public void Interact(GameObject interactor)
    {
        if (isTransitionInProgress)
        {
            return;
        }

        if (!allowToggleAfterIsolation && isIsolated)
        {
            return;
        }

        bool nextState = !isIsolated;
        if (interactionDuration <= 0.01f)
        {
            ApplyIsolationState(nextState, invokeEvents: true);
            return;
        }

        transitionRoutine = StartCoroutine(PerformInteractionAfterDelay(interactor, nextState));
    }

    [ContextMenu("Isolate Hazard")]
    public void IsolateHazard()
    {
        ApplyIsolationState(true, invokeEvents: true);
    }

    [ContextMenu("Reactivate Hazard")]
    public void ReactivateHazard()
    {
        if (!allowToggleAfterIsolation)
        {
            return;
        }

        ApplyIsolationState(false, invokeEvents: true);
    }

    public Vector3 GetWorldPosition()
    {
        return transform.position;
    }

    public void SetLinkedFires(Fire[] fires)
    {
        linkedFires = fires ?? Array.Empty<Fire>();
        ApplyLinkedFireConfiguration();
    }

    public void SetRuntimeHazardType(FireHazardType fireHazardType)
    {
        switch (fireHazardType)
        {
            case FireHazardType.Electrical:
                hazardType = IsolationHazardType.Electrical;
                break;
            case FireHazardType.GasFed:
                hazardType = IsolationHazardType.Gas;
                break;
            default:
                hazardType = IsolationHazardType.None;
                break;
        }

        ApplyLinkedFireConfiguration();
        RefreshPresentation();
    }

    public void SetRuntimeIsolationState(bool isolated, bool invokeEvents)
    {
        ApplyIsolationState(isolated, invokeEvents);
    }

    private void ApplyIsolationState(bool isolated, bool invokeEvents)
    {
        transitionRoutine = null;
        ReleasePlayerLock();
        bool changed = isIsolated != isolated;
        isIsolated = isolated;
        isTransitionInProgress = false;
        ApplyLinkedFireConfiguration();

        if (linkedFires == null)
        {
            linkedFires = new Fire[0];
        }

        for (int i = 0; i < linkedFires.Length; i++)
        {
            Fire fire = linkedFires[i];
            if (fire != null)
            {
                fire.SetHazardSourceIsolated(isolated);
            }
        }

        RefreshPresentation();

        if (!invokeEvents || !changed)
        {
            return;
        }

        if (isIsolated)
        {
            RaiseMissionSignal(isolatedSignalKey);
            PlayStateAudio(isolatedAudioClip);
            onHazardIsolated?.Invoke();
        }
        else
        {
            RaiseMissionSignal(reactivatedSignalKey);
            PlayStateAudio(reactivatedAudioClip);
            onHazardReactivated?.Invoke();
        }
    }

    private IEnumerator PerformInteractionAfterDelay(GameObject interactor, bool nextState)
    {
        isTransitionInProgress = true;
        currentStateSummary = BuildTransitionSummary(nextState);
        RefreshPresentation();
        AcquirePlayerLock(interactor);

        float waitSeconds = Mathf.Max(0.01f, interactionDuration);
        yield return new WaitForSeconds(waitSeconds);

        transitionRoutine = null;
        ReleasePlayerLock();
        ApplyIsolationState(nextState, invokeEvents: true);
    }

    private void ResolveLinkedFires()
    {
        System.Collections.Generic.List<Fire> resolvedFires = new System.Collections.Generic.List<Fire>();
        AddUniqueNonNullFires(linkedFires, resolvedFires);

        if (autoCollectChildFires)
        {
            Fire[] childFires = GetComponentsInChildren<Fire>(true);
            AddUniqueNonNullFires(childFires, resolvedFires);
        }

        linkedFires = resolvedFires.ToArray();
        RefreshPresentation();
    }

    private void ApplyLinkedFireConfiguration()
    {
        if (!applyHazardTypeToLinkedFires || hazardType == IsolationHazardType.None || linkedFires == null)
        {
            return;
        }

        FireHazardType fireHazardType = ResolveFireHazardType();
        for (int i = 0; i < linkedFires.Length; i++)
        {
            Fire fire = linkedFires[i];
            if (fire != null)
            {
                fire.SetFireHazardType(fireHazardType);
            }
        }
    }

    private FireHazardType ResolveFireHazardType()
    {
        switch (hazardType)
        {
            case IsolationHazardType.Electrical:
                return FireHazardType.Electrical;
            case IsolationHazardType.Gas:
                return FireHazardType.GasFed;
            default:
                return FireHazardType.OrdinaryCombustibles;
        }
    }

    private void RefreshPresentation()
    {
        currentStateSummary = isTransitionInProgress
            ? currentStateSummary
            : BuildStateSummary(isIsolated);

        ApplyIndicatorVisuals();
        SetObjectsActive(activeStateObjects, !isIsolated);
        SetObjectsActive(isolatedStateObjects, isIsolated);
    }

    private void ApplyIndicatorVisuals()
    {
        Color targetColor = isIsolated ? isolatedIndicatorColor : activeIndicatorColor;
        float targetIntensity = isIsolated ? isolatedLightIntensity : activeLightIntensity;

        for (int i = 0; i < indicatorLights.Length; i++)
        {
            Light indicatorLight = indicatorLights[i];
            if (indicatorLight == null)
            {
                continue;
            }

            indicatorLight.color = targetColor;
            indicatorLight.intensity = targetIntensity;
            indicatorLight.enabled = targetIntensity > 0f;
        }

        for (int i = 0; i < indicatorRenderers.Length; i++)
        {
            Renderer indicatorRenderer = indicatorRenderers[i];
            if (indicatorRenderer == null)
            {
                continue;
            }

            ApplyRendererColor(indicatorRenderer, targetColor);
        }
    }

    private void ApplyRendererColor(Renderer targetRenderer, Color color)
    {
        if (targetRenderer == null)
        {
            return;
        }

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

    private void AcquirePlayerLock(GameObject interactor)
    {
        if (!lockPlayerWhileInteracting || interactor == null)
        {
            return;
        }

        activePlayerLock = PlayerActionLock.GetOrCreate(interactor);
        activePlayerLock?.AcquireFullLock();
    }

    private void ReleasePlayerLock()
    {
        if (activePlayerLock == null)
        {
            return;
        }

        activePlayerLock.ReleaseFullLock();
        activePlayerLock = null;
    }

    private void PlayStateAudio(AudioClip clip)
    {
        if (stateAudioSource == null || clip == null)
        {
            return;
        }

        stateAudioSource.PlayOneShot(clip);
    }

    private string ResolveHazardDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(hazardDisplayName))
        {
            return hazardDisplayName.Trim();
        }

        return hazardType switch
        {
            IsolationHazardType.Electrical => "Electrical Panel",
            IsolationHazardType.Gas => "Gas Shutoff Valve",
            _ => "Hazard Control"
        };
    }

    private string BuildStateSummary(bool isolated)
    {
        return isolated
            ? $"{ResolveHazardDisplayName()}: isolated"
            : $"{ResolveHazardDisplayName()}: active";
    }

    private string BuildTransitionSummary(bool isolating)
    {
        return isolating
            ? $"{ResolveHazardDisplayName()}: isolating..."
            : $"{ResolveHazardDisplayName()}: reactivating...";
    }

    private static void SetObjectsActive(GameObject[] targets, bool active)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            GameObject target = targets[i];
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }

    private void RaiseMissionSignal(string signalKey)
    {
        if (string.IsNullOrWhiteSpace(signalKey))
        {
            return;
        }

        ResolveMissionSystem();
        missionSystem?.NotifySignal(signalKey);
    }

    private void ResolveMissionSystem()
    {
        if (missionSystem == null)
        {
            missionSystem = FindAnyObjectByType<IncidentMissionSystem>();
        }
    }

    private static void AddUniqueNonNullFires(
        Fire[] source,
        System.Collections.Generic.List<Fire> destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        for (int i = 0; i < source.Length; i++)
        {
            Fire fire = source[i];
            if (fire == null || destination.Contains(fire))
            {
                continue;
            }

            destination.Add(fire);
        }
    }
}
