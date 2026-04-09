using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class ThermalCamera : Item, IInteractable, IPickupable, IUsable, IMovementWeightSource, IInventoryEquippable, IInventoryRuntimeTickable, IThermalVisionBatterySource
{
    [Header("Thermal Camera")]
    [SerializeField] private float movementWeightKg = 1.5f;
    [SerializeField] private bool autoEnableWhenEquipped;

    [Header("Battery")]
    [SerializeField] private float maxBatterySeconds = 180f;
    [SerializeField] private float batteryDrainPerSecond = 8f;
    [SerializeField] private float batteryRechargePerSecond = 2.5f;
    [SerializeField, Range(0f, 1f)] private float lowBatteryThreshold01 = 0.15f;
    [SerializeField] private bool rechargeWhileStowed = true;

    [Header("Runtime")]
    [SerializeField] private GameObject currentHolder;
    [SerializeField] private float currentBatterySeconds;

    private Rigidbody cachedRigidbody;

    public Rigidbody Rigidbody => cachedRigidbody;
    public float MovementWeightKg => Mathf.Max(0f, movementWeightKg);
    public bool HasBatteryCharge => currentBatterySeconds > 0.01f;
    public bool IsBatteryLow => BatteryPercent01 <= Mathf.Clamp01(lowBatteryThreshold01);
    public float BatteryPercent01 => Mathf.Approximately(maxBatterySeconds, 0f)
        ? 0f
        : Mathf.Clamp01(currentBatterySeconds / Mathf.Max(0.01f, maxBatterySeconds));

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        maxBatterySeconds = Mathf.Max(1f, maxBatterySeconds);
        if (currentBatterySeconds <= 0f)
        {
            currentBatterySeconds = maxBatterySeconds;
        }
    }

    private void OnValidate()
    {
        movementWeightKg = Mathf.Max(0f, movementWeightKg);
        maxBatterySeconds = Mathf.Max(1f, maxBatterySeconds);
        batteryDrainPerSecond = Mathf.Max(0f, batteryDrainPerSecond);
        batteryRechargePerSecond = Mathf.Max(0f, batteryRechargePerSecond);
        lowBatteryThreshold01 = Mathf.Clamp01(lowBatteryThreshold01);
        currentBatterySeconds = Mathf.Clamp(currentBatterySeconds, 0f, maxBatterySeconds);
    }

    public void Interact(GameObject interactor)
    {
    }

    public void OnPickup(GameObject picker)
    {
        currentHolder = picker;
    }

    public void Use(GameObject user)
    {
        GameObject resolvedUser = user != null ? user : currentHolder;
        ThermalVisionController controller = ThermalVisionController.GetOrCreate(resolvedUser);
        if (controller == null)
        {
            return;
        }

        controller.BindBatterySource(this);

        if (!HasBatteryCharge && !controller.IsThermalVisionActive)
        {
            controller.SetThermalVisionEnabled(false);
            return;
        }

        controller.ToggleThermalVision();
    }

    public void OnEquipped(GameObject owner)
    {
        currentHolder = owner;
        ThermalVisionController controller = ThermalVisionController.GetOrCreate(owner);
        controller?.BindBatterySource(this);

        if (autoEnableWhenEquipped)
        {
            if (HasBatteryCharge)
            {
                controller?.SetThermalVisionEnabled(true);
            }
        }
    }

    public void OnStowed(GameObject owner)
    {
        DisableThermalVision(owner != null ? owner : currentHolder, this);
    }

    public void RechargeBatteryToFull()
    {
        currentBatterySeconds = maxBatterySeconds;
    }

    public void OnInventoryTick(GameObject owner, bool isEquipped, float deltaTime)
    {
        if (owner != null)
        {
            currentHolder = owner;
        }

        ThermalVisionController controller = ResolveBoundController();
        bool isThermalActive = isEquipped && controller != null && controller.IsThermalVisionActive;

        if (controller != null && isEquipped)
        {
            controller.BindBatterySource(this);
        }

        if (isThermalActive)
        {
            DrainBattery(deltaTime);
            if (!HasBatteryCharge && controller != null)
            {
                controller.SetThermalVisionEnabled(false);
            }
            return;
        }

        if (isEquipped || rechargeWhileStowed)
        {
            RechargeBattery(deltaTime);
        }
    }

    private void DrainBattery(float deltaTime)
    {
        if (batteryDrainPerSecond <= 0f || deltaTime <= 0f)
        {
            return;
        }

        currentBatterySeconds = Mathf.Max(0f, currentBatterySeconds - batteryDrainPerSecond * deltaTime);
    }

    private void RechargeBattery(float deltaTime)
    {
        if (batteryRechargePerSecond <= 0f || deltaTime <= 0f)
        {
            return;
        }

        currentBatterySeconds = Mathf.Min(maxBatterySeconds, currentBatterySeconds + batteryRechargePerSecond * deltaTime);
    }

    private ThermalVisionController ResolveBoundController()
    {
        if (currentHolder == null)
        {
            return null;
        }

        return currentHolder.GetComponent<ThermalVisionController>();
    }

    private static void DisableThermalVision(GameObject target, IThermalVisionBatterySource batterySource)
    {
        if (target == null)
        {
            return;
        }

        ThermalVisionController controller = target.GetComponent<ThermalVisionController>();
        if (controller == null)
        {
            return;
        }

        controller.SetThermalVisionEnabled(false);
        if (batterySource != null)
        {
            controller.UnbindBatterySource(batterySource);
        }
    }

    public void OnDrop(GameObject dropper)
    {
        DisableThermalVision(dropper != null ? dropper : currentHolder, this);
        currentHolder = null;
    }
}
