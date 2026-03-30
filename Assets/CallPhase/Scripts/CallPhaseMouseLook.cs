using UnityEngine;

/*
Usage:
- Attach this script to a Call Phase controller/manager object (not the Main Camera).
- Assign targetRig to a dedicated local pivot/rig used for subtle Call Phase view motion.
- Popup systems can pause/resume the effect by calling SetSuppressed(true/false).
*/
[DisallowMultipleComponent]
public class CallPhaseMouseLook : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform targetRig;

    [Header("Activation")]
    [SerializeField] private bool effectEnabled = true;
    [SerializeField] private bool suppressWhilePopupOpen = true;
    [SerializeField] private bool isSuppressed = false;

    [Header("Edge Detection")]
    [SerializeField] private float edgeThresholdPercent = 0.12f;
    [SerializeField] private bool ignoreOutOfBoundsMouse = true;

    [Header("Pan Limits")]
    [SerializeField] private float maxLocalOffsetX = 0.05f;
    [SerializeField] private float maxLocalOffsetY = 0.03f;

    [Header("Optional Rotation")]
    [SerializeField] private bool useRotation = false;
    [SerializeField] private float maxPitch = 1.5f;
    [SerializeField] private float maxYaw = 2.0f;

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothTime = 0.12f;
    [SerializeField] private float rotationSmoothTime = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private Vector3 defaultLocalPosition;
    private Quaternion defaultLocalRotation;

    private Vector2 smoothedPositionInfluence;
    private Vector2 smoothedPositionInfluenceVelocity;
    private Vector2 smoothedRotationInfluence;
    private Vector2 smoothedRotationInfluenceVelocity;

    private bool defaultsCached;
    private bool smoothResetRequested;

    private void Awake()
    {
        if (targetRig == null)
        {
            Debug.LogWarning($"{nameof(CallPhaseMouseLook)} on {name}: Target rig is not assigned.", this);
            return;
        }

        CacheDefaults();
    }

    private void Update()
    {
        if (targetRig == null)
        {
            return;
        }

        if (!defaultsCached)
        {
            CacheDefaults();
        }

        bool shouldReturnToCenter =
            smoothResetRequested ||
            !effectEnabled ||
            (suppressWhilePopupOpen && isSuppressed);

        Vector2 desiredInfluence = Vector2.zero;
        bool allowRotation = useRotation;

        if (!shouldReturnToCenter)
        {
            Vector3 mousePosition = Input.mousePosition;

            if (ignoreOutOfBoundsMouse && IsMouseOutOfBounds(mousePosition))
            {
                shouldReturnToCenter = true;
                allowRotation = false;
            }
            else
            {
                desiredInfluence = CalculateEdgeInfluence(mousePosition);
            }
        }
        else
        {
            allowRotation = false;
        }

        if (shouldReturnToCenter)
        {
            desiredInfluence = Vector2.zero;
        }

        ApplySmoothedTransform(desiredInfluence, allowRotation);

        if (smoothResetRequested && IsCentered())
        {
            smoothResetRequested = false;
        }
    }

    public void SetEffectEnabled(bool value)
    {
        effectEnabled = value;
        if (!effectEnabled)
        {
            smoothResetRequested = false;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(CallPhaseMouseLook)}: effectEnabled = {effectEnabled}", this);
        }
    }

    public void SetSuppressed(bool value)
    {
        isSuppressed = value;
        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(CallPhaseMouseLook)}: isSuppressed = {isSuppressed}", this);
        }
    }

    public void ResetViewInstant()
    {
        if (targetRig == null)
        {
            return;
        }

        if (!defaultsCached)
        {
            CacheDefaults();
        }

        smoothResetRequested = false;

        smoothedPositionInfluence = Vector2.zero;
        smoothedPositionInfluenceVelocity = Vector2.zero;
        smoothedRotationInfluence = Vector2.zero;
        smoothedRotationInfluenceVelocity = Vector2.zero;

        targetRig.localPosition = defaultLocalPosition;
        targetRig.localRotation = defaultLocalRotation;
    }

    public void ResetViewSmooth()
    {
        if (targetRig == null)
        {
            return;
        }

        if (!defaultsCached)
        {
            CacheDefaults();
        }

        smoothResetRequested = true;
    }

    private void CacheDefaults()
    {
        defaultLocalPosition = targetRig.localPosition;
        defaultLocalRotation = targetRig.localRotation;
        defaultsCached = true;
    }

    private void ApplySmoothedTransform(Vector2 desiredInfluence, bool allowRotation)
    {
        float deltaTime = Time.unscaledDeltaTime;

        float safePositionSmoothTime = Mathf.Max(0.0001f, positionSmoothTime);
        float safeRotationSmoothTime = Mathf.Max(0.0001f, rotationSmoothTime);

        smoothedPositionInfluence = Vector2.SmoothDamp(
            smoothedPositionInfluence,
            desiredInfluence,
            ref smoothedPositionInfluenceVelocity,
            safePositionSmoothTime,
            Mathf.Infinity,
            deltaTime);

        Vector3 offset = new Vector3(
            smoothedPositionInfluence.x * Mathf.Abs(maxLocalOffsetX),
            smoothedPositionInfluence.y * Mathf.Abs(maxLocalOffsetY),
            0f);

        targetRig.localPosition = defaultLocalPosition + offset;

        Vector2 desiredRotationInfluence = allowRotation ? desiredInfluence : Vector2.zero;

        smoothedRotationInfluence = Vector2.SmoothDamp(
            smoothedRotationInfluence,
            desiredRotationInfluence,
            ref smoothedRotationInfluenceVelocity,
            safeRotationSmoothTime,
            Mathf.Infinity,
            deltaTime);

        Quaternion rotationOffset = Quaternion.Euler(
            -smoothedRotationInfluence.y * Mathf.Abs(maxPitch),
            smoothedRotationInfluence.x * Mathf.Abs(maxYaw),
            0f);

        targetRig.localRotation = defaultLocalRotation * rotationOffset;
    }

    private Vector2 CalculateEdgeInfluence(Vector3 mousePosition)
    {
        float width = Mathf.Max(1f, Screen.width);
        float height = Mathf.Max(1f, Screen.height);

        float clampedThreshold = Mathf.Clamp(edgeThresholdPercent, 0.001f, 0.49f);
        float thresholdX = width * clampedThreshold;
        float thresholdY = height * clampedThreshold;

        float influenceX = 0f;
        if (mousePosition.x <= thresholdX)
        {
            float t = Mathf.Clamp01(mousePosition.x / thresholdX);
            influenceX = -(1f - t);
        }
        else if (mousePosition.x >= width - thresholdX)
        {
            float t = Mathf.Clamp01((mousePosition.x - (width - thresholdX)) / thresholdX);
            influenceX = t;
        }

        float influenceY = 0f;
        if (mousePosition.y <= thresholdY)
        {
            float t = Mathf.Clamp01(mousePosition.y / thresholdY);
            influenceY = -(1f - t);
        }
        else if (mousePosition.y >= height - thresholdY)
        {
            float t = Mathf.Clamp01((mousePosition.y - (height - thresholdY)) / thresholdY);
            influenceY = t;
        }

        return new Vector2(Mathf.Clamp(influenceX, -1f, 1f), Mathf.Clamp(influenceY, -1f, 1f));
    }

    private static bool IsMouseOutOfBounds(Vector3 mousePosition)
    {
        return mousePosition.x < 0f ||
               mousePosition.y < 0f ||
               mousePosition.x > Screen.width ||
               mousePosition.y > Screen.height;
    }

    private bool IsCentered()
    {
        if (targetRig == null)
        {
            return true;
        }

        const float positionEpsilon = 0.0001f;
        const float angleEpsilon = 0.05f;

        bool isPositionCentered = (targetRig.localPosition - defaultLocalPosition).sqrMagnitude <= positionEpsilon * positionEpsilon;
        bool isRotationCentered = Quaternion.Angle(targetRig.localRotation, defaultLocalRotation) <= angleEpsilon;
        bool areInfluencesNearZero = smoothedPositionInfluence.sqrMagnitude <= 0.000001f && smoothedRotationInfluence.sqrMagnitude <= 0.000001f;

        return isPositionCentered && isRotationCentered && areInfluencesNearZero;
    }
}
