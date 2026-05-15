using UnityEngine;

[DisallowMultipleComponent]
public class PlayerTraversalStaminaConfig : MonoBehaviour
{
    [Header("Traversal Stamina")]
    [Tooltip("Stamina used per second while sprinting")]
    [SerializeField] private float sprintStaminaCostPerSecond = 12f;
    [Tooltip("Minimum stamina required to start sprinting")]
    [SerializeField] private float sprintMinStamina = 5f;
    [Tooltip("Stamina spent when climbing over a window")]
    [SerializeField] private float climbOverStaminaCost = 10f;

    public float SprintStaminaCostPerSecond => Mathf.Max(0f, sprintStaminaCostPerSecond);
    public float SprintMinStamina => Mathf.Max(0f, sprintMinStamina);
    public float ClimbOverStaminaCost => Mathf.Max(0f, climbOverStaminaCost);

    private void OnValidate()
    {
        sprintStaminaCostPerSecond = Mathf.Max(0f, sprintStaminaCostPerSecond);
        sprintMinStamina = Mathf.Max(0f, sprintMinStamina);
        climbOverStaminaCost = Mathf.Max(0f, climbOverStaminaCost);
    }
}
