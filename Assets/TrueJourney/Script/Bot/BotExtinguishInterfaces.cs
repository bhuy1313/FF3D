using UnityEngine;

namespace TrueJourney.BotBehavior
{
    public interface IBotExtinguisherItem
    {
        Rigidbody Rigidbody { get; }
        float ApplyWaterPerSecond { get; }
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
        bool IsBurning { get; }
        Vector3 GetWorldPosition();
    }
}
