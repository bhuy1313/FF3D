using UnityEngine;

namespace StarterAssets
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonAnimatorParameterDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterController characterController;
        [Tooltip("Velocity is converted into this local space before being sent to the animator.")]
        [SerializeField] private Transform velocityReference;

        [Header("Animator Parameters")]
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string horizontalSpeedParameter = "HorizontalSpeed";
        [SerializeField] private string velocityXParameter = "VelocityX";
        [SerializeField] private string velocityYParameter = "VelocityY";
        [SerializeField] private string velocityZParameter = "VelocityZ";
        [SerializeField] private float parameterDampTime = 0.1f;

        [Header("Runtime Debug")]
        [SerializeField] private Vector3 worldVelocity;
        [SerializeField] private Vector3 localVelocity;
        [SerializeField] private float speed;
        [SerializeField] private float horizontalSpeed;

        private int _speedHash;
        private int _horizontalSpeedHash;
        private int _velocityXHash;
        private int _velocityYHash;
        private int _velocityZHash;

        public Vector3 WorldVelocity => worldVelocity;
        public Vector3 LocalVelocity => localVelocity;
        public float Speed => speed;
        public float HorizontalSpeed => horizontalSpeed;

        private void Reset()
        {
            AutoAssignReferences();
            CacheParameterHashes();
        }

        private void Awake()
        {
            AutoAssignReferences();
            CacheParameterHashes();
        }

        private void LateUpdate()
        {
            UpdateVelocityState();
            PushAnimatorParameters();
        }

        private void OnValidate()
        {
            AutoAssignReferences();
            CacheParameterHashes();
        }

        private void AutoAssignReferences()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (velocityReference == null)
            {
                velocityReference = transform;
            }
        }

        private void CacheParameterHashes()
        {
            _speedHash = ToHash(speedParameter);
            _horizontalSpeedHash = ToHash(horizontalSpeedParameter);
            _velocityXHash = ToHash(velocityXParameter);
            _velocityYHash = ToHash(velocityYParameter);
            _velocityZHash = ToHash(velocityZParameter);
        }

        private void UpdateVelocityState()
        {
            if (characterController == null)
            {
                worldVelocity = Vector3.zero;
                localVelocity = Vector3.zero;
                speed = 0f;
                horizontalSpeed = 0f;
                return;
            }

            worldVelocity = characterController.velocity;

            Transform reference = velocityReference != null ? velocityReference : transform;
            localVelocity = reference.InverseTransformDirection(worldVelocity);
            speed = worldVelocity.magnitude;
            horizontalSpeed = new Vector3(worldVelocity.x, 0f, worldVelocity.z).magnitude;
        }

        private void PushAnimatorParameters()
        {
            if (animator == null)
            {
                return;
            }

            SetFloatIfValid(_speedHash, speed);
            // SetFloatIfValid(_horizontalSpeedHash, horizontalSpeed);
            // SetFloatIfValid(_velocityXHash, localVelocity.x);
            // SetFloatIfValid(_velocityYHash, localVelocity.y);
            // SetFloatIfValid(_velocityZHash, localVelocity.z);
        }

        private void SetFloatIfValid(int parameterHash, float value)
        {
            if (parameterHash == 0)
            {
                return;
            }

            animator.SetFloat(parameterHash, value, parameterDampTime, Time.deltaTime);
        }

        private static int ToHash(string parameterName)
        {
            return string.IsNullOrWhiteSpace(parameterName) ? 0 : Animator.StringToHash(parameterName);
        }
    }
}
