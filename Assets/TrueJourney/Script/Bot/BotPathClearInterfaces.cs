using UnityEngine;

namespace TrueJourney.BotBehavior
{
    public interface IBotBreakTool
    {
        Rigidbody Rigidbody { get; }
        float PreferredBreakDistance { get; }
        float MaxBreakDistance { get; }
        float UseCooldown { get; }
        bool IsHeld { get; }
        bool IsHeldBy(GameObject requester);
        bool IsAvailableTo(GameObject requester);
        bool TryClaim(GameObject requester);
        void ReleaseClaim(GameObject requester);
        void UseOnTarget(GameObject user, IBotBreakableTarget target);
    }

    public interface IBotBreakableTarget
    {
        bool IsBroken { get; }
        bool CanBeClearedByBot { get; }
        Vector3 GetWorldPosition();
        void TakeBreakDamage(float amount, GameObject source, Vector3 hitPoint, Vector3 hitNormal);
    }
}
