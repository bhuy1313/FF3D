using UnityEngine;

[DisallowMultipleComponent]
public class HingedInteractable : MonoBehaviour, IInteractable, IOpenable
{
    [Header("Motion")]
    [SerializeField] private Transform hingeTransform;
    [SerializeField] private float openAngle = -90f;
    [SerializeField] private float animationSpeed = 6f;
    [SerializeField] private bool startsOpen;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;

    private Quaternion closedLocalRotation;
    private Vector3 closedLocalEulerAngles;
    private Quaternion targetLocalRotation;
    private bool isOpen;
    private bool initialized;
    private int currentOpenDirection = -1;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        InitializeState();
    }

    private void Update()
    {
        if (!initialized || hingeTransform == null)
            return;

        float t = 1f - Mathf.Exp(-animationSpeed * Time.deltaTime);
        hingeTransform.localRotation = Quaternion.Slerp(hingeTransform.localRotation, targetLocalRotation, t);
    }

    public void Interact(GameObject interactor)
    {
        if (!initialized)
            InitializeState();

        if (isOpen)
            Close();
        else
            Open(interactor);
    }

    private void InitializeState()
    {
        if (hingeTransform == null)
            hingeTransform = transform;

        closedLocalRotation = hingeTransform.localRotation;
        closedLocalEulerAngles = hingeTransform.localEulerAngles;
        
        currentOpenDirection = openAngle >= 0f ? 1 : -1;
        targetLocalRotation = startsOpen ? GetOpenLocalRotation(currentOpenDirection) : closedLocalRotation;
        hingeTransform.localRotation = targetLocalRotation;

        isOpen = startsOpen;
        initialized = true;
    }

    private Quaternion GetOpenLocalRotation(int direction)
    {
        float targetY = closedLocalEulerAngles.y + Mathf.Abs(openAngle) * direction;
        return Quaternion.Euler(closedLocalEulerAngles.x, targetY, closedLocalEulerAngles.z);
    }

    public void Open(GameObject interactor = null)
    {
        if (audioSource != null && openSound != null)
            audioSource.PlayOneShot(openSound);

        currentOpenDirection = DetermineOpenDirection(interactor);
        isOpen = true;
        targetLocalRotation = GetOpenLocalRotation(currentOpenDirection);
    }

    public void Close()
    {
        if (audioSource != null && closeSound != null)
            audioSource.PlayOneShot(closeSound);

        isOpen = false;
        targetLocalRotation = closedLocalRotation;
    }

    private int DetermineOpenDirection(GameObject interactor)
    {
        int defaultDirection = openAngle >= 0f ? 1 : -1;

        if (interactor == null)
            return defaultDirection;

        Vector3 lookDirection = Vector3.ProjectOnPlane(interactor.transform.forward, transform.up);
        if (TryGetOpenDirection(lookDirection, defaultDirection, out int lookSign))
            return lookSign;

        Vector3 toInteractor = Vector3.ProjectOnPlane(interactor.transform.position - transform.position, transform.up);
        if (TryGetOpenDirection(toInteractor, defaultDirection, out int positionSign))
            return positionSign;

        return defaultDirection;
    }

    private bool TryGetOpenDirection(Vector3 direction, int defaultDirection, out int sign)
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

        sign = defaultDirection * (facing >= 0f ? -1 : 1);
        return true;
    }
}