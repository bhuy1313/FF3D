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
    DryChemical = 1,
    CO2 = 2
}

public enum FireExtinguisherType
{
    Water = 0,
    DryChemical = 1,
    CO2 = 2
}

public enum FireSuppressionOutcome
{
    SafeEffective = 0,
    SafeLimited = 1,
    UnsafeWorsens = 2
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
        GameObject CurrentHolder { get; }
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
        void ApplyWater(float amount, GameObject sourceUser, FireSuppressionAgent suppressionAgent);
        bool HasActiveFires { get; }
        Vector3 GetClosestActiveFirePosition(Vector3 fromPosition);
        Vector3 GetWorldCenter();
    }

    public interface IFireTarget
    {
        void ApplyWater(float amount);
        void ApplySuppression(float amount, FireSuppressionAgent agent);
        void ApplySuppression(float amount, FireSuppressionAgent agent, GameObject sourceUser);
        bool IsBurning { get; }
        FireHazardType FireType { get; }
        FireSuppressionOutcome EvaluateSuppressionOutcome(FireSuppressionAgent agent);
        Vector3 GetWorldPosition();
        float GetWorldRadius();
    }
}
