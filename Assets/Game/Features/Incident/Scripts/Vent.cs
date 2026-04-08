using UnityEngine;

public class Vent : MonoBehaviour, IInteractable, IGrabbable, IOpenable, ISmokeVentPoint, IMovementWeightSource
{
    [SerializeField] private bool startsOpen;
    [SerializeField] private bool allowToggle = true;
    [SerializeField] private float smokeVentilationReliefWhenOpen = 0.35f;
    [SerializeField] private float fireDraftRiskWhenOpen;
    [SerializeField] private float movementWeightKg = 12f;

    private Rigidbody rb;
    private bool isOpen;

    public bool IsOpen => isOpen;
    public float SmokeVentilationRelief => isOpen ? smokeVentilationReliefWhenOpen : 0f;
    public float FireDraftRisk => isOpen ? fireDraftRiskWhenOpen : 0f;
    public float MovementWeightKg => Mathf.Max(0f, movementWeightKg);

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody component missing from Vent object.");
            return;
        }

        ApplyVentState();
    }

    private void Reset()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        ApplyVentState();
    }

    private void OnValidate()
    {
        smokeVentilationReliefWhenOpen = Mathf.Max(0f, smokeVentilationReliefWhenOpen);
        fireDraftRiskWhenOpen = Mathf.Max(0f, fireDraftRiskWhenOpen);
        movementWeightKg = Mathf.Max(0f, movementWeightKg);

        if (Application.isPlaying)
            return;

        isOpen = startsOpen;
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        ApplyVentState();
    }

    public void Interact(GameObject interactor)
    {
        isOpen = allowToggle ? !isOpen : true;
        ApplyVentState();
    }

    private void ApplyVentState()
    {
        if (rb == null)
            return;

        if (!Application.isPlaying)
            isOpen = startsOpen;

        rb.isKinematic = !isOpen;
    }
}
