using StarterAssets;
using UnityEngine;

namespace FF3D.UI
{
    public class PlayerStatusUIController : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private FPSInteractionSystem interactionSystem;
        [SerializeField] private FirstPersonController firstPersonController;
        [SerializeField] private FPSInventorySystem inventorySystem;

        [Header("Container")]
        [SerializeField] private Transform statusRoot;

        [Header("Status Prefabs")]
        [SerializeField] private GameObject onCarryPrefab;
        [SerializeField] private GameObject limitedMovementPrefab;
        [SerializeField] private GameObject heavyPrefab;

        private const float BurdenEpsilonKg = 0.01f;

        private GameObject onCarryInstance;
        private GameObject limitedMovementInstance;
        private GameObject heavyInstance;

        private void Awake()
        {
            if (statusRoot == null)
            {
                statusRoot = transform;
            }

            ResolveSources();
            RefreshStatusIcons();
        }

        private void OnEnable()
        {
            ResolveSources();
            RefreshStatusIcons();
        }

        private void Update()
        {
            ResolveSources();
            RefreshStatusIcons();
        }

        private void ResolveSources()
        {
            if (interactionSystem == null)
            {
                interactionSystem = FindAnyObjectByType<FPSInteractionSystem>();
            }

            if (firstPersonController == null)
            {
                firstPersonController = interactionSystem != null
                    ? interactionSystem.GetComponent<FirstPersonController>()
                    : FindAnyObjectByType<FirstPersonController>();
            }

            if (inventorySystem == null)
            {
                inventorySystem = firstPersonController != null
                    ? firstPersonController.GetComponent<FPSInventorySystem>()
                    : FindAnyObjectByType<FPSInventorySystem>();
            }
        }

        private void RefreshStatusIcons()
        {
            if (interactionSystem == null)
            {
                UpdateStatusInstance(ref onCarryInstance, onCarryPrefab, false, "Status_OnCarry", 0);
                UpdateStatusInstance(ref limitedMovementInstance, limitedMovementPrefab, false, "Status_LimitedMovement", 1);
                UpdateStatusInstance(ref heavyInstance, heavyPrefab, false, "Status_Heavy", 2);
                return;
            }

            float burdenKg = Mathf.Max(0f, interactionSystem.CurrentMovementBurdenKg + GetHeldBulkyEquipmentWeightKg());
            bool movementPenaltyEnabled = firstPersonController != null && firstPersonController.EnableMovementWeightPenalty;
            bool hasMovementBurden = movementPenaltyEnabled && (burdenKg > BurdenEpsilonKg || IsHoldingBulkyEquipment());

            UpdateStatusInstance(ref onCarryInstance, onCarryPrefab, interactionSystem.IsCarryingVictim, "Status_OnCarry", 0);
            UpdateStatusInstance(ref limitedMovementInstance, limitedMovementPrefab, hasMovementBurden, "Status_LimitedMovement", 1);
            UpdateStatusInstance(ref heavyInstance, heavyPrefab, hasMovementBurden && burdenKg >= ResolveHeavyThresholdKg(), "Status_Heavy", 2);
        }

        private float ResolveHeavyThresholdKg()
        {
            if (firstPersonController == null)
            {
                return float.PositiveInfinity;
            }

            if (firstPersonController.SprintDisabledWeight > 0f)
            {
                return firstPersonController.SprintDisabledWeight;
            }

            return firstPersonController.WeightForMinimumSpeed > 0f
                ? firstPersonController.WeightForMinimumSpeed
                : float.PositiveInfinity;
        }

        private bool IsHoldingBulkyEquipment()
        {
            GameObject heldObject = inventorySystem != null ? inventorySystem.HeldObject : null;
            return heldObject != null && heldObject.GetComponent<IBulkyEquipment>() != null;
        }

        private float GetHeldBulkyEquipmentWeightKg()
        {
            GameObject heldObject = inventorySystem != null ? inventorySystem.HeldObject : null;
            if (heldObject == null || heldObject.GetComponent<IBulkyEquipment>() == null)
            {
                return 0f;
            }

            IMovementWeightSource weightSource = heldObject.GetComponent<IMovementWeightSource>();
            return weightSource != null ? Mathf.Max(0f, weightSource.MovementWeightKg) : 0f;
        }

        private void UpdateStatusInstance(ref GameObject instance, GameObject prefab, bool shouldExist, string instanceName, int siblingIndex)
        {
            if (!shouldExist)
            {
                DestroyStatusInstance(ref instance);
                return;
            }

            if (instance == null && prefab != null && statusRoot != null)
            {
                instance = Instantiate(prefab, statusRoot);
                instance.name = instanceName;
            }

            if (instance != null)
            {
                instance.transform.SetSiblingIndex(siblingIndex);
            }
        }

        private static void DestroyStatusInstance(ref GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }

            instance = null;
        }
    }
}
