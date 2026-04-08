using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rescuable))]
public class VictimCondition : MonoBehaviour
{
    public enum TriageState
    {
        Stable = 0,
        Urgent = 1,
        Critical = 2,
        Deceased = 3
    }

    [Header("Condition")]
    [SerializeField] private float maxCondition = 100f;
    [SerializeField] private float passiveDeteriorationPerSecond = 0f;
    [SerializeField] private float smokeDamageMultiplier = 1f;
    [SerializeField, Range(0f, 100f), Tooltip("Condition percentage at or below this value becomes Urgent.")]
    private float urgentThreshold = 65f;
    [SerializeField, Range(0f, 100f), Tooltip("Condition percentage at or below this value becomes Critical.")]
    private float criticalThreshold = 30f;
    [SerializeField] private bool stopPassiveDeteriorationWhenStabilized = true;
    [SerializeField] private bool stopConditionLossWhenExtracted = true;
    [SerializeField] private bool allowTreatmentWhenUrgent = true;
    [SerializeField] private bool allowTreatmentWhenCritical = true;
    [SerializeField] private bool requireStabilizationBeforeCarryWhenUrgent = false;
    [SerializeField] private bool requireStabilizationBeforeCarryWhenCritical = false;
    [SerializeField] private float carriedUnstabilizedDeteriorationMultiplier = 1.85f;

    [Header("Runtime")]
    [SerializeField] private float currentCondition = 100f;
    [SerializeField] private TriageState triageState = TriageState.Stable;
    [SerializeField] private bool isStabilized;
    [SerializeField] private bool isExtracted;

    [Header("Animation")]
    [SerializeField] private Animator victimAnimator;
    [SerializeField] private string urgentAnimatorParameter = "IsUrgent";
    [SerializeField] private string criticalAnimatorParameter = "IsCritical";
    [SerializeField] private string deceasedAnimatorParameter = "IsDeceased";
    [SerializeField] private string carriedAnimatorParameter = "IsCarried";

    [Header("Events")]
    [SerializeField] private UnityEvent onConditionChanged;
    [SerializeField] private UnityEvent onVictimDeceased;

    public float CurrentCondition => currentCondition;
    public float MaxCondition => maxCondition;
    public float NormalizedCondition => maxCondition <= 0f ? 0f : currentCondition / maxCondition;
    public float CurrentConditionPercent => NormalizedCondition * 100f;
    public float UrgentThresholdPercent => urgentThreshold;
    public float CriticalThresholdPercent => criticalThreshold;
    public TriageState CurrentTriageState => triageState;
    public bool IsAlive => triageState != TriageState.Deceased;
    public bool IsStabilized => isStabilized;
    public bool IsExtracted => isExtracted;
    public bool CanBeStabilized => IsAlive && !isExtracted && !isStabilized;
    public bool CanReceiveStabilizationTreatment => CanBeStabilized && IsTreatmentAllowedAtCurrentTriageState();
    public bool RequiresStabilization => !isStabilized && (
        (requireStabilizationBeforeCarryWhenUrgent && triageState == TriageState.Urgent) ||
        (requireStabilizationBeforeCarryWhenCritical && triageState == TriageState.Critical));
    public bool CanBeginCarry => IsAlive && !RequiresStabilization;

    public event Action<TriageState> OnTriageStateChanged;
    public event Action OnConditionContextChanged;
    public event Action OnVictimStabilized;
    public event Action OnVictimExtracted;

    private Rescuable rescuable;

    private void Awake()
    {
        CacheReferences();
        ClampSettings();
        currentCondition = Mathf.Clamp(currentCondition, 0f, maxCondition);
        RefreshTriageState(raiseEvents: false);
        SyncRuntimeFlags();
        SyncTriageAnimation();
    }

    private void OnEnable()
    {
        CacheReferences();
        if (rescuable != null)
            rescuable.RescueCompleted += HandleRescueCompleted;

        SyncRuntimeFlags();
        SyncTriageAnimation();
    }

    private void OnDisable()
    {
        if (rescuable != null)
            rescuable.RescueCompleted -= HandleRescueCompleted;
    }

    private void Start()
    {
        SyncTriageAnimation();
    }

    private void OnValidate()
    {
        CacheReferences();
        ClampSettings();
        currentCondition = Mathf.Clamp(currentCondition, 0f, maxCondition);
        RefreshTriageState(raiseEvents: false);
        SyncRuntimeFlags();

        if (Application.isPlaying)
            SyncTriageAnimation();
    }

    private void Update()
    {
        SyncTriageAnimation();

        if (!IsAlive || passiveDeteriorationPerSecond <= 0f)
            return;

        if (isExtracted && stopConditionLossWhenExtracted)
            return;

        if (isStabilized && stopPassiveDeteriorationWhenStabilized)
            return;

        float deteriorationPerSecond = passiveDeteriorationPerSecond;
        if (ShouldApplyCarryDeterioration())
        {
            deteriorationPerSecond *= Mathf.Max(1f, carriedUnstabilizedDeteriorationMultiplier);
        }

        ApplyConditionDamage(deteriorationPerSecond * Time.deltaTime);
    }

    public void ApplySmokeExposure(float amount)
    {
        if (amount <= 0f || !CanLoseCondition())
            return;

        ApplyConditionDamage(amount * Mathf.Max(0f, smokeDamageMultiplier));
    }

    public void ApplyConditionDamage(float amount)
    {
        if (amount <= 0f || !CanLoseCondition())
            return;

        currentCondition = Mathf.Max(0f, currentCondition - amount);
        RefreshTriageState(raiseEvents: true);
    }

    public void RestoreCondition(float amount)
    {
        if (amount <= 0f || !IsAlive)
            return;

        currentCondition = Mathf.Min(maxCondition, currentCondition + amount);
        RefreshTriageState(raiseEvents: true);
    }

    public void Stabilize(float restoreAmount = 0f)
    {
        if (!IsAlive)
            return;

        bool stateChanged = !isStabilized;
        isStabilized = true;

        if (restoreAmount > 0f)
            currentCondition = Mathf.Min(maxCondition, currentCondition + restoreAmount);

        if (restoreAmount > 0f)
            RefreshTriageState(raiseEvents: true);
        else if (stateChanged)
            NotifyConditionContextChanged();

        if (stateChanged)
            OnVictimStabilized?.Invoke();
    }

    public float GetRescuePriorityScore()
    {
        float severityScore;
        switch (triageState)
        {
            case TriageState.Critical:
                severityScore = 90f;
                break;
            case TriageState.Urgent:
                severityScore = 60f;
                break;
            case TriageState.Stable:
                severityScore = 30f;
                break;
            default:
                severityScore = 5f;
                break;
        }

        severityScore += (1f - NormalizedCondition) * 20f;

        if (isStabilized)
            severityScore -= 15f;

        if (isExtracted)
            severityScore -= 100f;

        return severityScore;
    }

    private void RefreshTriageState(bool raiseEvents)
    {
        TriageState nextState = ResolveTriageState();
        bool stateChanged = triageState != nextState;
        triageState = nextState;

        if (!IsAlive)
            isStabilized = false;

        SyncTriageAnimation();

        if (!raiseEvents)
            return;

        onConditionChanged?.Invoke();
        OnConditionContextChanged?.Invoke();
        if (stateChanged)
            OnTriageStateChanged?.Invoke(triageState);

        if (triageState == TriageState.Deceased)
            onVictimDeceased?.Invoke();
    }

    private TriageState ResolveTriageState()
    {
        if (currentCondition <= 0f)
            return TriageState.Deceased;

        float currentConditionPercent = CurrentConditionPercent;
        if (currentConditionPercent <= criticalThreshold)
            return TriageState.Critical;

        if (currentConditionPercent <= urgentThreshold)
            return TriageState.Urgent;

        return TriageState.Stable;
    }

    private void ClampSettings()
    {
        maxCondition = Mathf.Max(1f, maxCondition);
        passiveDeteriorationPerSecond = Mathf.Max(0f, passiveDeteriorationPerSecond);
        smokeDamageMultiplier = Mathf.Max(0f, smokeDamageMultiplier);
        criticalThreshold = Mathf.Clamp(criticalThreshold, 0f, 100f);
        urgentThreshold = Mathf.Clamp(urgentThreshold, criticalThreshold, 100f);
        carriedUnstabilizedDeteriorationMultiplier = Mathf.Max(1f, carriedUnstabilizedDeteriorationMultiplier);
    }

    private bool CanLoseCondition()
    {
        if (!IsAlive)
            return false;

        return !isExtracted || !stopConditionLossWhenExtracted;
    }

    private bool IsTreatmentAllowedAtCurrentTriageState()
    {
        switch (triageState)
        {
            case TriageState.Critical:
                return allowTreatmentWhenCritical;
            case TriageState.Urgent:
                return allowTreatmentWhenUrgent;
            default:
                return false;
        }
    }

    private bool ShouldApplyCarryDeterioration()
    {
        if (isStabilized || rescuable == null || !rescuable.IsCarried)
        {
            return false;
        }

        return triageState == TriageState.Urgent || triageState == TriageState.Critical;
    }

    private void CacheReferences()
    {
        if (rescuable == null)
            rescuable = GetComponent<Rescuable>();

        if (victimAnimator == null)
        {
            victimAnimator = GetComponent<Animator>();
            if (victimAnimator == null)
                victimAnimator = GetComponentInChildren<Animator>(true);
            if (victimAnimator == null)
                victimAnimator = GetComponentInParent<Animator>();
        }
    }

    private void HandleRescueCompleted()
    {
        SetExtractedState(true, raiseEvents: true);
        SyncTriageAnimation();
    }

    private void SyncRuntimeFlags()
    {
        if (!IsAlive)
            isStabilized = false;

        if (rescuable != null && rescuable.IsRescued)
            SetExtractedState(true, raiseEvents: false);
        else if (!isExtracted && isStabilized && !IsAlive)
            isStabilized = false;
    }

    private void SetExtractedState(bool extracted, bool raiseEvents)
    {
        bool extractedChanged = isExtracted != extracted;
        bool stabilizedChanged = false;

        isExtracted = extracted;
        if (isExtracted && IsAlive && !isStabilized)
        {
            isStabilized = true;
            stabilizedChanged = true;
        }

        if (!raiseEvents || (!extractedChanged && !stabilizedChanged))
            return;

        NotifyConditionContextChanged();
        if (stabilizedChanged)
            OnVictimStabilized?.Invoke();
        if (extractedChanged)
            OnVictimExtracted?.Invoke();
    }

    private void NotifyConditionContextChanged()
    {
        onConditionChanged?.Invoke();
        OnConditionContextChanged?.Invoke();
    }

    private void SyncTriageAnimation()
    {
        if (!Application.isPlaying)
            return;

        CacheReferences();
        if (!CanDriveVictimAnimator())
            return;

        ApplyTriageAnimatorParameters();
    }

    private void ApplyTriageAnimatorParameters()
    {
        SetAnimatorBoolParameter(carriedAnimatorParameter, rescuable != null && rescuable.IsCarried);
        SetAnimatorBoolParameter(urgentAnimatorParameter, triageState == TriageState.Urgent);
        SetAnimatorBoolParameter(criticalAnimatorParameter, triageState == TriageState.Critical);
        SetAnimatorBoolParameter(deceasedAnimatorParameter, triageState == TriageState.Deceased);
    }

    private void SetAnimatorBoolParameter(string parameterName, bool value)
    {
        if (string.IsNullOrWhiteSpace(parameterName) || !CanDriveVictimAnimator() || !HasAnimatorBoolParameter(parameterName))
            return;

        victimAnimator.SetBool(parameterName, value);
    }

    private bool CanDriveVictimAnimator()
    {
        return victimAnimator != null &&
               victimAnimator.runtimeAnimatorController != null &&
               victimAnimator.isActiveAndEnabled &&
               victimAnimator.gameObject.activeInHierarchy &&
               victimAnimator.isInitialized;
    }

    private bool HasAnimatorBoolParameter(string parameterName)
    {
        AnimatorControllerParameter[] parameters = victimAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Bool &&
                string.Equals(parameter.name, parameterName, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
