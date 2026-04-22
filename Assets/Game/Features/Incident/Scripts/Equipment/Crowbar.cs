using TrueJourney.BotBehavior;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class Crowbar : Tool
{
    protected override BreakToolKind DefaultToolKind => BreakToolKind.Crowbar;

    public override void Use(GameObject user)
    {
        if (TryGetUseHit(user, out UseHit hit) &&
            TryFindPryOpenable(hit.Collider, out IPryOpenable pryOpenable) &&
            pryOpenable.TryPryOpen(user != null ? user : gameObject))
        {
            return;
        }

        base.Use(user);
    }

    private static bool TryFindPryOpenable(Collider collider, out IPryOpenable pryOpenable)
    {
        pryOpenable = null;
        if (collider == null)
        {
            return false;
        }

        if (collider.TryGetComponent(out pryOpenable))
        {
            return true;
        }

        if (collider.attachedRigidbody != null &&
            collider.attachedRigidbody.TryGetComponent(out pryOpenable))
        {
            return true;
        }

        Transform parent = collider.transform.parent;
        while (parent != null)
        {
            if (parent.TryGetComponent(out pryOpenable))
            {
                return true;
            }

            parent = parent.parent;
        }

        return false;
    }
}
