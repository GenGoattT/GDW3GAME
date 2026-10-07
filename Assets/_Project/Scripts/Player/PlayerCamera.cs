using UnityEngine;

namespace Project.Player
{
    public class PlayerCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform playerBody;
        [SerializeField] private Camera cam;

        [Header("Look")]
        [SerializeField] private float mouseSensitivity = 2.5f;
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        [Header("Tilt Feel")]
        [SerializeField] private float strafeTiltAmount = 3f;
        [SerializeField] private float slideTiltAmount = 8f;
        [SerializeField] private float tiltSpeed = 8f;
        [SerializeField] private float slideTiltSpeed = 22f;

        [Header("Dash FOV Kick")]
        [SerializeField] private float baseFov = 90f;
        [SerializeField] private float dashFov = 100f;
        [SerializeField] private float fovLerpSpeed = 10f;
        [SerializeField] private float dashFovHoldTime = 0.18f;

        [Header("Slide FOV Kick")]
        [SerializeField] private float slideFov = 96f;
        [SerializeField] private float slideFovHoldTime = 0.12f;

        [Header("Wall Jump Kick")]
        [SerializeField] private float wallJumpFov = 98f;
        [SerializeField] private float wallJumpFovHoldTime = 0.1f;
        [SerializeField] private float wallJumpTiltKick = 9f;

        private float pitch;
        private float currentTilt;
        private float targetFov;
        private bool isSliding;

        private void Awake()
        {
            if (cam == null) cam = GetComponent<Camera>();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            targetFov = baseFov;
            cam.fieldOfView = baseFov;
        }

        private void Update()
        {
            HandleLook();
            HandleTilt();
            HandleFov();
        }

        private void HandleLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            transform.localRotation = Quaternion.Euler(pitch, 0f, currentTilt);
            playerBody.Rotate(Vector3.up * mouseX);
        }

        private void HandleTilt()
        {
            float strafe = Input.GetAxisRaw("Horizontal");
            float amount = isSliding ? slideTiltAmount : strafeTiltAmount;
            float speed = isSliding ? slideTiltSpeed : tiltSpeed;
            float target = -strafe * amount;
            currentTilt = Mathf.Lerp(currentTilt, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
        }

        private void HandleFov()
        {
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.deltaTime * fovLerpSpeed);
        }

        public void OnDash()
        {
            targetFov = dashFov;
            CancelInvoke(nameof(ResetFov));
            Invoke(nameof(ResetFov), dashFovHoldTime);
        }

        private void ResetFov()
        {
            targetFov = baseFov;
        }

        public void OnSlideStart()
        {
            isSliding = true;
            targetFov = Mathf.Max(targetFov, slideFov);
            CancelInvoke(nameof(ResetFov));
            Invoke(nameof(ResetFov), slideFovHoldTime);
        }

        public void OnWallJump(Vector3 wallNormal)
        {
            targetFov = Mathf.Max(targetFov, wallJumpFov);
            CancelInvoke(nameof(ResetFov));
            Invoke(nameof(ResetFov), wallJumpFovHoldTime);
            currentTilt = -Vector3.Dot(wallNormal, transform.right) * wallJumpTiltKick;
        }

        public void OnSlideEnd()
        {
            isSliding = false;
        }
    }
}
