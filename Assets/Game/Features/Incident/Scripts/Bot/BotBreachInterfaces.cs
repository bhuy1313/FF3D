using UnityEngine;

namespace TrueJourney.BotBehavior
{
    public interface IBotPryTarget
    {
        bool CanBePriedOpen { get; }
        bool IsPryInProgress { get; }
        bool IsBreached { get; }
        Vector3 GetWorldPosition();
        bool TryPryOpen(GameObject interactor);
    }
}
