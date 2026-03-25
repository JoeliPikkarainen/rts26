using UnityEngine;

public class PlayerCameraController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraPivot;   // Pivot at player
    public Transform playerCamera;  // Actual camera

    [Header("Settings")]
    public float mouseSensitivity = 500f;
    public float followSpeed = 10f;

    [Header("Camera Positioning")]
    [Tooltip("Distance from pivot")]
    public float distanceBehind = 3f;

    [Range(-80f, 80f)]
    [Tooltip("Initial vertical angle (looking up/down)")]
    public float initialAngle = 15f;

    private float yaw = 0f;   // Horizontal rotation
    private float pitch = 0f; // Vertical rotation

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        yaw = transform.eulerAngles.y;       // initialize horizontal rotation
        pitch = initialAngle;                // initialize vertical rotation

        UpdateCameraPosition();
    }

    void Update()
    {
        // Mouse input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        yaw += mouseX;             // horizontal rotation
        pitch -= mouseY;           // vertical rotation (invert if needed)
        pitch = Mathf.Clamp(pitch, 5f, 65f); // limit vertical

        // Rotate pivot with pitch only (vertical)
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void LateUpdate()
    {
        // Smoothly follow the player
        cameraPivot.position = Vector3.Lerp(
            cameraPivot.position,
            transform.position,
            followSpeed * Time.deltaTime
        );

        // Only update camera position based on yaw/pitch, don't rotate parent
        UpdateCameraPosition();
    }

    private void UpdateCameraPosition()
    {
        // Create rotation for camera positioning (yaw + pitch)
        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);
        
        // Orbit camera around the pivot
        Vector3 offset = cameraRotation * Vector3.forward * distanceBehind;
        playerCamera.position = cameraPivot.position - offset;

        // Look at the pivot
        playerCamera.LookAt(cameraPivot.position);
    }
}