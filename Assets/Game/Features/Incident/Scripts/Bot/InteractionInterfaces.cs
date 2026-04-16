using UnityEngine;

public interface IInteractable
{
    void Interact(GameObject interactor);
}

public interface IUsable
{
    void Use(GameObject user);
}

public interface IPickupable
{
    Rigidbody Rigidbody { get; }
    void OnPickup(GameObject picker);
    void OnDrop(GameObject dropper);
}

public interface IInventoryEquippable
{
    void OnEquipped(GameObject owner);
    void OnStowed(GameObject owner);
}

public interface IInventoryRuntimeTickable
{
    void OnInventoryTick(GameObject owner, bool isEquipped, float deltaTime);
}

public interface IInventorySelectionBlocker
{
    bool BlocksInventorySelectionChange(GameObject owner);
}

public interface IJumpActionBlocker
{
    bool BlocksJumpAction(GameObject owner);
}

public interface IGrabbable
{
    //None
}

public interface IMovementWeightSource
{
    float MovementWeightKg { get; }
}

public interface ICustomGrabPlacement
{
    bool TryGetGrabPlacementPose(Transform aimTransform, LayerMask placementMask, float maxDistance, out Vector3 position, out Quaternion rotation);
    void OnGrabStarted();
    void OnGrabCancelled();
    void OnGrabPlaced(Vector3 position, Quaternion rotation);
}

public interface IDamageable
{
    void TakeDamage(float amount, GameObject source, Vector3 hitPoint, Vector3 hitNormal);
}

public interface IEventListener
{
    void OnEventTriggered(GameObject eventSource, GameObject instigator);
}

public interface IOpenable
{
    bool IsOpen { get; }
}

public interface IPryOpenable
{
    bool CanBePriedOpen { get; }
    bool TryPryOpen(GameObject interactor);
}

public interface ISmokeVentPoint : IOpenable
{
    float SmokeVentilationRelief { get; }
    float FireDraftRisk { get; }
}

public enum ThermalSignatureCategory
{
    Fire = 0,
    VictimStable = 1,
    VictimUrgent = 2,
    VictimCritical = 3
}

public interface IThermalSignatureSource
{
    bool HasThermalSignature { get; }
    Vector3 GetThermalSignatureWorldPosition();
    float GetThermalSignatureStrength();
    ThermalSignatureCategory ThermalSignatureCategory { get; }
}

public interface IThermalVisionBatterySource
{
    bool HasBatteryCharge { get; }
    bool IsBatteryLow { get; }
    float BatteryPercent01 { get; }
}

public interface IPlayerExternalOxygenSource
{
    bool IsSupplyingOxygen { get; }
    bool HasOxygenSupply { get; }
    bool IsOxygenSupplyLow { get; }
    float OxygenSupplyPercent01 { get; }
    float ConsumeSuppliedOxygen(float amount);
}

public struct FallImpactData
{
    public GameObject Actor;
    public Vector3 ImpactPosition;
    public float FallDistance;
    public float LandingSpeed;
    public float DownwardVelocity;
}

public struct FallImpactResponse
{
    public bool PreventDamage;
    public float DamageMultiplier;
    public bool OverrideVerticalVelocity;
    public float VerticalVelocity;

    public static FallImpactResponse Default => new FallImpactResponse
    {
        PreventDamage = false,
        DamageMultiplier = 1f,
        OverrideVerticalVelocity = false,
        VerticalVelocity = 0f
    };
}

public interface IFallImpactResponder
{
    bool TryHandleFallImpact(FallImpactData impactData, ref FallImpactResponse response);
}
