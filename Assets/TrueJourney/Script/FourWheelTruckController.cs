using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FourWheelTruckController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private string steerAxis = "Horizontal";
    [SerializeField] private string throttleAxis = "Vertical";
    [SerializeField] private KeyCode brakeKey = KeyCode.Space;
    [SerializeField] private KeyCode handbrakeKey = KeyCode.LeftShift;
    [SerializeField] private bool invertSteerInput = true;
    [SerializeField] private bool invertMotorInput = true;

    [Header("Drive")]
    [SerializeField] private bool allWheelDrive = true;
    [SerializeField] private float maxSteerAngle = 30f;
    [SerializeField] private float maxMotorTorque = 2400f;
    [SerializeField] private float brakeTorque = 3500f;
    [SerializeField] private float handbrakeTorque = 6000f;
    [SerializeField] private float maxSpeedKmh = 90f;
    [SerializeField] private float steeringResponse = 6f;

    [Header("Physics")]
    [SerializeField] private float downforce = 100f;
    [SerializeField] private Transform centerOfMass;
    [SerializeField] private bool autoConfigureRigidbody = true;
    [SerializeField] private float chassisMass = 2200f;
    [SerializeField] private Vector3 fallbackCenterOfMass = new Vector3(0f, -0.4f, 0f);

    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider frontLeftCollider;
    [SerializeField] private WheelCollider frontRightCollider;
    [SerializeField] private WheelCollider rearLeftCollider;
    [SerializeField] private WheelCollider rearRightCollider;

    [Header("Wheel Visuals")]
    [SerializeField] private Transform frontLeftVisual;
    [SerializeField] private Transform frontRightVisual;
    [SerializeField] private Transform rearLeftVisual;
    [SerializeField] private Transform rearRightVisual;

    [Header("Auto Setup Defaults")]
    [SerializeField] private float autoWheelRadius = 0.45f;
    [SerializeField] private float autoSuspensionDistance = 0.25f;
    [SerializeField] private float autoWheelMass = 40f;
    [SerializeField] private float autoSuspensionSpring = 35000f;
    [SerializeField] private float autoSuspensionDamper = 4500f;
    [SerializeField] private float autoSuspensionTarget = 0.5f;
    [SerializeField] private bool autoTuneSuspensionFromMass = true;
    [SerializeField] private bool autoRefreshWheelRadius = true;

    private Rigidbody cachedRigidbody;
    private float steerInput;
    private float throttleInput;
    private bool brakePressed;
    private bool handbrakePressed;
    private float currentSteerAngle;

    private Quaternion frontLeftVisualOffset = Quaternion.identity;
    private Quaternion frontRightVisualOffset = Quaternion.identity;
    private Quaternion rearLeftVisualOffset = Quaternion.identity;
    private Quaternion rearRightVisualOffset = Quaternion.identity;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();

        if (autoConfigureRigidbody)
        {
            if (cachedRigidbody.mass < 100f)
            {
                cachedRigidbody.mass = chassisMass;
            }

            cachedRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            cachedRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        if (centerOfMass != null)
        {
            cachedRigidbody.centerOfMass = transform.InverseTransformPoint(centerOfMass.position);
        }
        else if (autoConfigureRigidbody)
        {
            cachedRigidbody.centerOfMass = fallbackCenterOfMass;
        }

        CacheVisualOffsets();
    }

    private void Update()
    {
        steerInput = Input.GetAxis(steerAxis);
        throttleInput = Input.GetAxis(throttleAxis);
        brakePressed = Input.GetKey(brakeKey);
        handbrakePressed = Input.GetKey(handbrakeKey);
    }

    private void FixedUpdate()
    {
        if (!HasRequiredWheels())
        {
            return;
        }

        ApplySteering();
        ApplyDrive();
        ApplyDownforce();
    }

    private void LateUpdate()
    {
        UpdateWheelVisual(frontLeftCollider, frontLeftVisual, frontLeftVisualOffset);
        UpdateWheelVisual(frontRightCollider, frontRightVisual, frontRightVisualOffset);
        UpdateWheelVisual(rearLeftCollider, rearLeftVisual, rearLeftVisualOffset);
        UpdateWheelVisual(rearRightCollider, rearRightVisual, rearRightVisualOffset);
    }

    [ContextMenu("Auto Setup ForestFireTruck2 Wheels")]
    private void AutoSetupForestFireTruck2Wheels()
    {
        frontLeftVisual = FindDeepChild(transform, "FrontLeftW");
        frontRightVisual = FindDeepChild(transform, "FrontRightW");
        rearLeftVisual = FindDeepChild(transform, "BackLeftW");
        rearRightVisual = FindDeepChild(transform, "BackRightW");

        frontLeftCollider = EnsureWheelCollider(frontLeftCollider, frontLeftVisual, "FrontLeftCollider");
        frontRightCollider = EnsureWheelCollider(frontRightCollider, frontRightVisual, "FrontRightCollider");
        rearLeftCollider = EnsureWheelCollider(rearLeftCollider, rearLeftVisual, "RearLeftCollider");
        rearRightCollider = EnsureWheelCollider(rearRightCollider, rearRightVisual, "RearRightCollider");

        CacheVisualOffsets();
    }

    private void ApplySteering()
    {
        float steerDirection = invertSteerInput ? -1f : 1f;
        float targetSteerAngle = steerInput * steerDirection * maxSteerAngle;
        float steerStep = Mathf.Max(0.01f, steeringResponse) * maxSteerAngle * Time.fixedDeltaTime;
        currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetSteerAngle, steerStep);

        frontLeftCollider.steerAngle = currentSteerAngle;
        frontRightCollider.steerAngle = currentSteerAngle;
    }

    private void ApplyDrive()
    {
        float motorDirection = invertMotorInput ? -1f : 1f;
        float forwardSpeed = Vector3.Dot(cachedRigidbody.linearVelocity, transform.forward);
        float speedKmh = Mathf.Abs(forwardSpeed) * 3.6f;
        bool acceleratingIntoLimiter = speedKmh > maxSpeedKmh && Mathf.Sign(throttleInput * motorDirection) == Mathf.Sign(forwardSpeed);
        float throttle = acceleratingIntoLimiter ? 0f : throttleInput * motorDirection;
        if (brakePressed || handbrakePressed)
        {
            throttle = 0f;
        }

        float motorTorque = throttle * maxMotorTorque;
        float baseBrake = brakePressed ? brakeTorque : 0f;
        float rearBrake = baseBrake + (handbrakePressed ? handbrakeTorque : 0f);

        if (allWheelDrive)
        {
            frontLeftCollider.motorTorque = motorTorque;
            frontRightCollider.motorTorque = motorTorque;
        }
        else
        {
            frontLeftCollider.motorTorque = 0f;
            frontRightCollider.motorTorque = 0f;
        }

        rearLeftCollider.motorTorque = motorTorque;
        rearRightCollider.motorTorque = motorTorque;

        frontLeftCollider.brakeTorque = baseBrake;
        frontRightCollider.brakeTorque = baseBrake;
        rearLeftCollider.brakeTorque = rearBrake;
        rearRightCollider.brakeTorque = rearBrake;
    }

    private void ApplyDownforce()
    {
        if (downforce <= 0f)
        {
            return;
        }

        cachedRigidbody.AddForce(-transform.up * downforce * cachedRigidbody.linearVelocity.magnitude, ForceMode.Force);
    }

    private bool HasRequiredWheels()
    {
        return frontLeftCollider != null &&
               frontRightCollider != null &&
               rearLeftCollider != null &&
               rearRightCollider != null;
    }

    private void CacheVisualOffsets()
    {
        frontLeftVisualOffset = GetVisualOffset(frontLeftCollider, frontLeftVisual);
        frontRightVisualOffset = GetVisualOffset(frontRightCollider, frontRightVisual);
        rearLeftVisualOffset = GetVisualOffset(rearLeftCollider, rearLeftVisual);
        rearRightVisualOffset = GetVisualOffset(rearRightCollider, rearRightVisual);
    }

    private static Quaternion GetVisualOffset(WheelCollider wheelCollider, Transform visual)
    {
        if (wheelCollider == null || visual == null)
        {
            return Quaternion.identity;
        }

        return Quaternion.Inverse(wheelCollider.transform.rotation) * visual.rotation;
    }

    private static void UpdateWheelVisual(WheelCollider wheelCollider, Transform visual, Quaternion offset)
    {
        if (wheelCollider == null || visual == null)
        {
            return;
        }

        wheelCollider.GetWorldPose(out Vector3 position, out Quaternion rotation);
        visual.SetPositionAndRotation(position, rotation * offset);
    }

    private WheelCollider EnsureWheelCollider(WheelCollider current, Transform visual, string colliderName)
    {
        if (visual == null)
        {
            return current;
        }

        WheelCollider wheelCollider = current;
        if (wheelCollider == null)
        {
            Transform existing = FindDeepChild(transform, colliderName);
            if (existing != null)
            {
                wheelCollider = existing.GetComponent<WheelCollider>();
            }
        }

        if (wheelCollider == null)
        {
            GameObject colliderObject = new GameObject(colliderName);
            colliderObject.transform.SetParent(visual.parent, false);
            colliderObject.transform.localPosition = visual.localPosition;
            colliderObject.transform.localRotation = Quaternion.identity;
            wheelCollider = colliderObject.AddComponent<WheelCollider>();
        }

        if (autoRefreshWheelRadius)
        {
            wheelCollider.radius = EstimateWheelRadius(visual);
        }
        else if (wheelCollider.radius <= 0.01f)
        {
            wheelCollider.radius = EstimateWheelRadius(visual);
        }

        wheelCollider.mass = autoWheelMass;
        wheelCollider.suspensionDistance = autoSuspensionDistance;
        wheelCollider.forceAppPointDistance = 0.15f;

        JointSpring spring = wheelCollider.suspensionSpring;
        spring.spring = autoTuneSuspensionFromMass ? EstimateSuspensionSpring() : autoSuspensionSpring;
        spring.damper = autoTuneSuspensionFromMass ? spring.spring * 0.2f : autoSuspensionDamper;
        spring.targetPosition = autoSuspensionTarget;
        wheelCollider.suspensionSpring = spring;

        return wheelCollider;
    }

    private float EstimateWheelRadius(Transform visual)
    {
        Renderer renderer = visual.GetComponentInChildren<Renderer>();
        if (renderer == null)
        {
            return autoWheelRadius;
        }

        Vector3 extents = renderer.bounds.extents;
        float estimated = Mathf.Max(extents.x, extents.y, extents.z);
        return Mathf.Max(0.1f, estimated);
    }

    private float EstimateSuspensionSpring()
    {
        float mass = cachedRigidbody != null ? cachedRigidbody.mass : chassisMass;
        float travel = Mathf.Max(0.05f, autoSuspensionDistance);

        // Approximate spring so each wheel supports a quarter of the chassis at rest.
        float baseSpring = (mass * Physics.gravity.magnitude) / (travel * 4f);
        return Mathf.Max(5000f, baseSpring * 1.6f);
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
