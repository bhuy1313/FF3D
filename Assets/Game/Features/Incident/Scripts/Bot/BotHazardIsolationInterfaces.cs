using UnityEngine;

namespace TrueJourney.BotBehavior
{
    public interface IBotHazardIsolationTarget
    {
        bool IsHazardActive { get; }
        bool IsInteractionAvailable { get; }
        FireHazardType HazardType { get; }
        Vector3 GetWorldPosition();
    }
}
