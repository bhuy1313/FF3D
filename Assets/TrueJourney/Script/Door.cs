using UnityEngine;

[DisallowMultipleComponent]
public class Door : MonoBehaviour, IInteractable, IOpenable
{
    [SerializeField] private string doorChildName = "Door";
    [SerializeField] private float openAngle = -90f;
    [SerializeField] private float animationSpeed = 6f;
    [SerializeField] private bool startsOpen;

    private Transform doorTransform;
    private Quaternion closedLocalRotation;
    private Quaternion targetLocalRotation;
    private bool isOpen;
    private bool initialized;
    private int currentOpenDirection = -1;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        InitializeDoorState();
    }

    private void Update()
    {
        if (!initialized || doorTransform == null)
        {
            return;
        }

        float t = 1f - Mathf.Exp(-animationSpeed * Time.deltaTime);
        doorTransform.localRotation = Quaternion.Slerp(doorTransform.localRotation, targetLocalRotation, t);
    }

    public void Interact(GameObject interactor)
    {
        if (!initialized)
        {
            InitializeDoorState();
        }

        if (doorTransform == null)
        {
            return;
        }

        if (isOpen)
        {
            isOpen = false;
            targetLocalRotation = closedLocalRotation;
            return;
        }

        currentOpenDirection = DetermineOpenDirection(interactor);
        isOpen = true;
        targetLocalRotation = GetOpenLocalRotation(currentOpenDirection);
    }

    private void InitializeDoorState()
    {
        doorTransform = FindDoorTransform();
        if (doorTransform == null)
        {
            initialized = false;
            return;
        }

        closedLocalRotation = doorTransform.localRotation;
        currentOpenDirection = GetDefaultOpenDirection();
        isOpen = startsOpen;
        targetLocalRotation = isOpen ? GetOpenLocalRotation(currentOpenDirection) : closedLocalRotation;
        doorTransform.localRotation = targetLocalRotation;
        initialized = true;
    }

    private Transform FindDoorTransform()
    {
        if (!string.IsNullOrWhiteSpace(doorChildName))
        {
            Transform namedChild = transform.Find(doorChildName);
            if (namedChild != null)
            {
                return namedChild;
            }
        }

        return transform;
    }

    private Quaternion GetOpenLocalRotation(int direction)
    {
        return closedLocalRotation * Quaternion.Euler(0f, Mathf.Abs(openAngle) * direction, 0f);
    }

    private int DetermineOpenDirection(GameObject interactor)
    {
        if (interactor == null)
        {
            return GetDefaultOpenDirection();
        }

        Vector3 lookDirection = Vector3.ProjectOnPlane(interactor.transform.forward, transform.up);
        if (TryGetOpenDirection(lookDirection, out int lookSign))
        {
            return lookSign;
        }

        Vector3 toInteractor = Vector3.ProjectOnPlane(interactor.transform.position - transform.position, transform.up);
        if (TryGetOpenDirection(toInteractor, out int positionSign))
        {
            return positionSign;
        }

        return GetDefaultOpenDirection();
    }

    private bool TryGetOpenDirection(Vector3 direction, out int sign)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            sign = 0;
            return false;
        }

        float facing = Vector3.Dot(transform.forward, direction.normalized);
        if (Mathf.Abs(facing) <= 0.001f)
        {
            sign = 0;
            return false;
        }

        sign = GetDefaultOpenDirection() * (facing >= 0f ? -1 : 1);
        return true;
    }

    private int GetDefaultOpenDirection()
    {
        return openAngle >= 0f ? 1 : -1;
    }
}
