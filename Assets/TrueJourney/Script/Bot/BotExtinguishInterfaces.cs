using UnityEngine;

public enum FireHazardType
{
    OrdinaryCombustibles = 0,
    Electrical = 1,
    FlammableLiquid = 2,
    GasFed = 3
}

public enum FireSuppressionAgent
{
    Water = 0,
    DryChemical = 1
}

namespace TrueJourney.BotBehavior
{
    public interface IBotExtinguisherItem
    {
        Rigidbody Rigidbody { get; }
        float ApplyWaterPerSecond { get; }
        FireSuppressionAgent SuppressionAgent { get; }
        float PreferredSprayDistance { get; }
        float MaxSprayDistance { get; }
        float MaxVerticalReach { get; }
        float BallisticLaunchSpeed { get; }
        float BallisticGravityMultiplier { get; }
        bool RequiresPreciseAim { get; }
        bool HasUsableCharge { get; }
        bool IsHeld { get; }
        GameObject ClaimOwner { get; }
        bool IsAvailableTo(GameObject requester);
        bool TryClaim(GameObject requester);
        void ReleaseClaim(GameObject requester);
        void SetExternalAimDirection(Vector3 worldDirection, GameObject user);
        void ClearExternalAimDirection(GameObject user);
        void SetExternalSprayState(bool enable, GameObject user);
    }

    public interface IFireGroupTarget
    {
        void ApplyWater(float amount);
        bool HasActiveFires { get; }
        Vector3 GetClosestActiveFirePosition(Vector3 fromPosition);
        Vector3 GetWorldCenter();
    }

    public interface IFireTarget
    {
        void ApplyWater(float amount);
        void ApplySuppression(float amount, FireSuppressionAgent agent);
        bool IsBurning { get; }
        FireHazardType FireType { get; }
        Vector3 GetWorldPosition();
        float GetWorldRadius();
    }
}
