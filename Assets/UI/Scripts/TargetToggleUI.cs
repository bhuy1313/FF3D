using StarterAssets;
using UnityEngine;

public class TargetToggleUI : MonoBehaviour
{
    [Header("Target Source")]
    [SerializeField] private FPSInteractionSystem interactionSystem;
    [SerializeField] private GameObject targetOverride; // nếu muốn gán trực tiếp

    [Header("UI Objects")]
    [SerializeField] private GameObject uiOnTarget;
    [SerializeField] private GameObject uiOnNoTarget;

    private void Update()
    {
        GameObject currentTarget = targetOverride;

        if (interactionSystem != null)
        {
            currentTarget = interactionSystem.CurrentTarget;
        }

        bool hasTarget = currentTarget != null;

        if (uiOnTarget != null) uiOnTarget.SetActive(hasTarget);
        if (uiOnNoTarget != null) uiOnNoTarget.SetActive(!hasTarget);
    }
}
