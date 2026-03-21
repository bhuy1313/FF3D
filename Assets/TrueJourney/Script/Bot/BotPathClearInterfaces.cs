using UnityEngine;

namespace TrueJourney.BotBehavior
{
    public enum BreakToolKind
    {
        None = 0,
        FireAxe = 1,
        SledgeHammer = 2,
        ChainSaw = 3
    }

    public interface IBotBreakTool
    {
        BreakToolKind ToolKind { get; }
        Rigidbody Rigidbody { get; }
        float PreferredBreakDistance { get; }
        float MaxBreakDistance { get; }
        float UseCooldown { get; }
        bool IsHeld { get; }
        bool IsHeldBy(GameObject requester);
        bool IsAvailableTo(GameObject requester);
        bool TryClaim(GameObject requester);
        void ReleaseClaim(GameObject requester);
        bool UseOnTarget(GameObject user, IBotBreakableTarget target);
    }

    public interface IBotBreakableTarget
    {
        bool IsBroken { get; }
        bool CanBeClearedByBot { get; }
        bool IsBreakInProgress { get; }
        GameObject ActiveBreaker { get; }
        Vector3 GetWorldPosition();
        bool IsOnSameSide(Vector3 pointA, Vector3 pointB);
        bool SupportsBreakTool(BreakToolKind toolKind);
        bool TryStartBreak(GameObject breaker, BreakToolKind toolKind);
    }
}
