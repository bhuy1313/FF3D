using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class OxygenMask : WornGearItem, IPlayerExternalOxygenSource
{
    [Header("Air Supply")]
    [SerializeField] private float maxMaskOxygen = 120f;
    [SerializeField] private float oxygenRechargePerSecond = 0f;
    [SerializeField] private bool rechargeWhileStowed;
    [SerializeField, Range(0f, 1f)] private float lowOxygenThreshold01 = 0.15f;

    [Header("Runtime")]
    [SerializeField] private float currentMaskOxygen;
    public bool IsSupplyingOxygen => IsGearEnabled && HasOxygenSupply;
    public bool HasOxygenSupply => currentMaskOxygen > 0.01f;
    public bool IsOxygenSupplyLow => OxygenSupplyPercent01 <= Mathf.Clamp01(lowOxygenThreshold01);
    public float OxygenSupplyPercent01 => Mathf.Approximately(maxMaskOxygen, 0f)
        ? 0f
        : Mathf.Clamp01(currentMaskOxygen / Mathf.Max(0.01f, maxMaskOxygen));

    protected override void OnGearAwake()
    {
        maxMaskOxygen = Mathf.Max(1f, maxMaskOxygen);
        if (currentMaskOxygen <= 0f)
        {
            currentMaskOxygen = maxMaskOxygen;
        }
    }

    protected override void OnGearValidate()
    {
        maxMaskOxygen = Mathf.Max(1f, maxMaskOxygen);
        oxygenRechargePerSecond = Mathf.Max(0f, oxygenRechargePerSecond);
        lowOxygenThreshold01 = Mathf.Clamp01(lowOxygenThreshold01);
        currentMaskOxygen = Mathf.Clamp(currentMaskOxygen, 0f, maxMaskOxygen);
    }

    protected override bool CanEnableGear(GameObject owner)
    {
        return HasOxygenSupply && owner != null && owner.GetComponent<PlayerVitals>() != null;
    }

    protected override void OnGearEnabled(GameObject owner)
    {
        BindToHolderVitals(owner);
    }

    protected override void OnGearDisabled(GameObject owner)
    {
        UnbindFromHolderVitals(owner != null ? owner : CurrentHolder);
    }

    protected override void OnWearHolderChanged(GameObject previousHolder, GameObject newHolder, bool gearWasEnabled)
    {
        if (!gearWasEnabled)
        {
            return;
        }

        if (previousHolder != null)
        {
            UnbindFromHolderVitals(previousHolder);
        }

        if (newHolder != null)
        {
            BindToHolderVitals(newHolder);
        }
    }

    protected override void OnWearTick(GameObject owner, bool equipped, float deltaTime)
    {
        if (IsGearEnabled && !HasOxygenSupply)
        {
            SetGearEnabled(false, owner);
        }

        if (oxygenRechargePerSecond <= 0f || deltaTime <= 0f)
        {
            return;
        }

        if (equipped || rechargeWhileStowed)
        {
            currentMaskOxygen = Mathf.Min(maxMaskOxygen, currentMaskOxygen + oxygenRechargePerSecond * deltaTime);
        }
    }

    public float ConsumeSuppliedOxygen(float amount)
    {
        if (!IsSupplyingOxygen || amount <= 0f)
        {
            return 0f;
        }

        float consumed = Mathf.Min(currentMaskOxygen, amount);
        currentMaskOxygen = Mathf.Max(0f, currentMaskOxygen - consumed);
        if (!HasOxygenSupply)
        {
            SetGearEnabled(false, CurrentHolder);
        }

        return consumed;
    }

    private void BindToHolderVitals(GameObject holder)
    {
        if (holder == null || !holder.TryGetComponent(out PlayerVitals vitals))
        {
            return;
        }

        vitals.BindExternalOxygenSource(this);
    }

    private void UnbindFromHolderVitals(GameObject holder)
    {
        if (holder == null || !holder.TryGetComponent(out PlayerVitals vitals))
        {
            return;
        }

        vitals.UnbindExternalOxygenSource(this);
    }
}
