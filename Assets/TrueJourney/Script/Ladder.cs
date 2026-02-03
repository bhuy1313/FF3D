using StarterAssets;
using UnityEngine;

[DisallowMultipleComponent]
public class Ladder : MonoBehaviour, IInteractable
{
    [SerializeField] private string requiredTag = "Player";
    [Tooltip("Additional height added to ladder top bound to stop climbing")]
    [SerializeField] private float topHeightOffset = 0.0f;

    public bool IsValidClimber(GameObject other)
    {
        if (string.IsNullOrWhiteSpace(requiredTag))
        {
            return true;
        }

        return other != null && other.CompareTag(requiredTag);
    }

    public float GetClimbMaxY()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            return col.bounds.max.y + topHeightOffset;
        }

        return transform.position.y + topHeightOffset;
    }

    public void Interact(GameObject interactor)
    {
        if (!IsValidClimber(interactor))
        {
            return;
        }

        FirstPersonController controller = interactor.GetComponentInParent<FirstPersonController>();
        if (controller != null)
        {
            controller.TryStartClimbFromInteract(this);
        }
    }
}
