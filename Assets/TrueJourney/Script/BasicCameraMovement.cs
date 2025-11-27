using UnityEngine;
using System.Collections;

public class BasicCameraMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Horizontal movement speed (units per second)")]
    public float moveSpeed = 5f;

    [Tooltip("Vertical movement speed when pressing Space / LeftShift (units per second)")]
    public float verticalSpeed = 3f;

    [Tooltip("If true, WASD moves relative to the camera's forward/right. If false, uses world axes.")]
    public bool useLocalSpace = true;

    [Header("Rotation (RTS-style)")]
    [Tooltip("Góc cúi xuống (X). 50–65 là kiểu RTS phổ biến.")]
    [Range(30f, 80f)]
    public float tiltAngle = 60f;   // X

    [Tooltip("Góc xoay quanh trục Y. 30–60 là góc chéo kiểu isometric.")]
    [Range(0f, 360f)]
    public float yawAngle = 45f;    // Y

    void Start()
    {
        // Set góc nhìn kiểu RTS
        transform.rotation = Quaternion.Euler(tiltAngle, yawAngle, 0f);
    }

    void Update()
    {
        HandleMovement();
    }

    void HandleMovement()
    {
        // Horizontal input via WASD
        float inputForward = 0f;
        if (Input.GetKey(KeyCode.W)) inputForward += 1f;
        if (Input.GetKey(KeyCode.S)) inputForward -= 1f;

        float inputRight = 0f;
        if (Input.GetKey(KeyCode.D)) inputRight += 1f;
        if (Input.GetKey(KeyCode.A)) inputRight -= 1f;

        Vector3 move = Vector3.zero;

        if (useLocalSpace)
        {
            // Move relative to camera direction nhưng vẫn bám mặt phẳng ngang
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = transform.right;
            right.y = 0f;
            right.Normalize();

            move = forward * inputForward + right * inputRight;
        }
        else
        {
            move = Vector3.forward * inputForward + Vector3.right * inputRight;
        }

        if (move.sqrMagnitude > 1f) move.Normalize();

        Vector3 horizontalDelta = move * moveSpeed * Time.deltaTime;

        // Vertical controls: Space lên, LeftShift xuống
        float verticalDelta = 0f;
        if (Input.GetKey(KeyCode.Space)) verticalDelta += verticalSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.LeftShift)) verticalDelta -= verticalSpeed * Time.deltaTime;

        transform.position += horizontalDelta + new Vector3(0f, verticalDelta, 0f);
    }
}
