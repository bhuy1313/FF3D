using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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
    [SerializeField] private FireSimulationManager fireSimulationManager;

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

    [Header("Linked Fires (Runtime, Read-Only)")]
    [SerializeField] private int linkedFireNodeCount;
    [SerializeField] private int linkedBurningFireNodeCount;
    [SerializeField] [TextArea(1, 6)] private string linkedFireSummary;

    public bool IsIsolated => isIsolated;
    public bool IsTransitionInProgress => isTransitionInProgress;
    public bool IsHazardActive => !isIsolated;
    public bool IsInteractionAvailable => !isTransitionInProgress;
    public string CurrentStateSummary => currentStateSummary;
    public string HazardDisplayName => ResolveHazardDisplayName();
    public FireHazardType HazardType => ResolveFireHazardType();
    public int LinkedFireNodeCount => linkedFireNodeCount;
    public int LinkedBurningFireNodeCount => linkedBurningFireNodeCount;

    private Coroutine transitionRoutine;
    private PlayerActionLock activePlayerLock;
    private FireSimulationManager subscribedManager;
    private readonly List<FireRuntimeNode> linkedFireNodeBuffer = new List<FireRuntimeNode>();

    private void Awake()
    {
        ResolveFireSimulationManager();
        ApplyIsolationState(startsIsolated, invokeEvents: false);
    }

    private void OnEnable()
    {
        BotRuntimeRegistry.RegisterHazardIsolationTarget(this);
        ResolveFireSimulationManager();
        SubscribeToManager();
        ApplyIsolationState(startsIsolated, invokeEvents: false);
        RefreshLinkedFireSummary();
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
        UnsubscribeFromManager();
        BotRuntimeRegistry.UnregisterHazardIsolationTarget(this);
    }

    private void OnValidate()
    {
        ResolveFireSimulationManager();
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

    public void SetFireSimulationManager(FireSimulationManager manager)
    {
        if (fireSimulationManager == manager)
        {
            return;
        }

        UnsubscribeFromManager();
        fireSimulationManager = manager;
        if (isActiveAndEnabled)
        {
            SubscribeToManager();
            RefreshLinkedFireSummary();
        }
    }

    public void GetLinkedFireNodes(List<FireRuntimeNode> buffer)
    {
        if (buffer == null)
        {
            return;
        }

        buffer.Clear();
        if (fireSimulationManager == null)
        {
            return;
        }

        fireSimulationManager.GetHazardLinkedNodes(buffer);
    }

    [ContextMenu("Log Linked Fires")]
    private void LogLinkedFires()
    {
        RefreshLinkedFireSummary();
        Debug.Log(
            $"[{nameof(HazardIsolationDevice)}] '{name}' linked fires: {linkedFireNodeCount} " +
            $"({linkedBurningFireNodeCount} burning)\n{linkedFireSummary}",
            this);
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

        fireSimulationManager?.SetActiveIncidentHazardType(fireHazardType);
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

        fireSimulationManager?.SetRuntimeHazardIsolation(isolated);

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

    private void ResolveFireSimulationManager()
    {
        if (fireSimulationManager == null)
        {
            fireSimulationManager = FindAnyObjectByType<FireSimulationManager>(FindObjectsInactive.Include);
        }
    }

    private void SubscribeToManager()
    {
        if (fireSimulationManager == null || subscribedManager == fireSimulationManager)
        {
            return;
        }

        UnsubscribeFromManager();
        fireSimulationManager.StateChanged += HandleManagerStateChanged;
        subscribedManager = fireSimulationManager;
    }

    private void UnsubscribeFromManager()
    {
        if (subscribedManager == null)
        {
            return;
        }

        subscribedManager.StateChanged -= HandleManagerStateChanged;
        subscribedManager = null;
    }

    private void HandleManagerStateChanged()
    {
        RefreshLinkedFireSummary();
    }

    private void RefreshLinkedFireSummary()
    {
        if (fireSimulationManager == null)
        {
            linkedFireNodeCount = 0;
            linkedBurningFireNodeCount = 0;
            linkedFireSummary = "<no FireSimulationManager>";
            return;
        }

        fireSimulationManager.GetHazardLinkedNodes(linkedFireNodeBuffer);
        linkedFireNodeCount = linkedFireNodeBuffer.Count;

        int burning = 0;
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < linkedFireNodeBuffer.Count; i++)
        {
            FireRuntimeNode node = linkedFireNodeBuffer[i];
            if (node == null)
            {
                continue;
            }

            if (node.IsBurning)
            {
                burning++;
            }

            if (sb.Length > 0)
            {
                sb.Append('\n');
            }

            sb.Append('[').Append(node.IncidentNodeKind).Append("] idx=").Append(node.Index)
                .Append(" heat=").Append(node.Heat.ToString("0.00"))
                .Append(node.IsBurning ? " (burning)" : " (idle)");
        }

        linkedBurningFireNodeCount = burning;
        linkedFireSummary = sb.Length > 0 ? sb.ToString() : "<no linked fires>";
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

}
