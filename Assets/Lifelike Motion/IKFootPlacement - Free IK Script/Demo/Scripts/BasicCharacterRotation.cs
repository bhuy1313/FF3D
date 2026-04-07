namespace LifelikeMotion.IKFootPlacement
{
    using UnityEngine;

    public class BasicCharacterRotation : MonoBehaviour
    {
        [SerializeField] private float mouseSensitivity = 1.5f;
        [SerializeField] private float smoothing = 0;

        private Vector3 rotation;
        private Animator animator;
        private float mouseX;
        private float mouseY;
        private float rotationX = 0;
        private float rotationX_target = 0;
        private float rotationY_target = 0;
        private bool receiveInput = true;

        private void Start()
        {
            animator = GetComponent<Animator>();
            rotation.y = transform.eulerAngles.y;
        }

        private void Update()
        {
            GetInputData();
            ApplyRotation();
        }

        private void ApplyRotation()
        {
            if (smoothing <= 0)
            {
                rotation.y += mouseX * mouseSensitivity;

                rotationY_target = rotation.y;
                rotationX_target += mouseY * mouseSensitivity;
                rotationX_target = Mathf.Clamp(rotationX_target, -90, 90);
                rotationX = rotationX_target;

                float _rotation_Angle = rotationX_target / 90f;
                animator.SetFloat("Rotation_Angle", _rotation_Angle);

                transform.localEulerAngles = rotation;
            }
            else if (smoothing > 0)
            {
                rotationY_target += mouseX * mouseSensitivity;

                rotation.y = Mathf.Lerp(rotation.y, rotationY_target, Time.deltaTime / smoothing);
                rotationX_target += mouseY * mouseSensitivity;
                rotationX_target = Mathf.Clamp(rotationX_target, -90, 90);

                rotationX = Mathf.Lerp(rotationX, rotationX_target, Time.deltaTime / smoothing);
                float _rotation_Angle = rotationX / 90f;
                animator.SetFloat("Rotation_Angle", _rotation_Angle);

                transform.localEulerAngles = rotation;
            }
        }
        private void GetInputData()
        {
            if (receiveInput)
            {
                mouseX = Input.GetAxis("Mouse X");
                mouseY = Input.GetAxis("Mouse Y");
            }
        }
    }

}