using UnityEngine;

namespace TrueJourney.BotBehavior
{
    public interface IRescuableTarget
    {
        bool NeedsRescue { get; }
        bool IsRescueInProgress { get; }
        GameObject ActiveRescuer { get; }
        bool IsCarried { get; }
        Vector3 GetWorldPosition();
        bool TryBeginCarry(GameObject rescuer, Transform carryAnchor);
        void CompleteRescueAt(Vector3 dropPosition);
    }

    public interface ISafeZoneTarget
    {
        Vector3 GetWorldPosition();
    }
}
