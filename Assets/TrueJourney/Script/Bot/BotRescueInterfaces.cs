using UnityEngine;

namespace TrueJourney.BotBehavior
{
    public interface IRescuableTarget
    {
        bool NeedsRescue { get; }
        bool IsRescueInProgress { get; }
        GameObject ActiveRescuer { get; }
        bool IsCarried { get; }
        bool RequiresStabilization { get; }
        float RescuePriority { get; }
        Vector3 GetWorldPosition();
        Transform GetCarryRightHandHoldPoint();
        Transform GetCarryLeftHandHoldPoint();
        bool TryStabilize(GameObject rescuer);
        bool TryBeginCarry(GameObject rescuer, Transform carryAnchor);
        void CompleteRescueAt(Vector3 dropPosition);
    }

    public interface ISafeZoneTarget
    {
        Vector3 GetWorldPosition();
        Vector3 GetDropPoint(Vector3 fallbackPosition);
        bool ContainsPoint(Vector3 worldPosition);
    }
}
