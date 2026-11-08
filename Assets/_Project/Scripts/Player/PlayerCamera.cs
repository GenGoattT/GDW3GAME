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

        [Header("FOV Response")]
        [SerializeField] private float baseFov = 90f;
        [SerializeField] private float fovAttackSpeed = 38f;
        [SerializeField] private float fovReleaseSpeed = 7f;

        [Header("Dash FOV Punch")]
        [SerializeField] private float dashPunchFov = 114f;
        [SerializeField, Range(0f, 1f)] private float dashPunchSnap = 0.75f;
        [SerializeField] private float dashPunchHold = 0.2f;
        [SerializeField] private float dashRecoilDip = 6f;
        [SerializeField] private float dashRecoilTime = 0.1f;

        [Header("Slide FOV Punch")]
        [SerializeField] private float slidePunchFov = 106f;
        [SerializeField, Range(0f, 1f)] private float slidePunchSnap = 0.7f;
        [SerializeField] private float slidePunchHold = 0.14f;
        [SerializeField] private float slideSustainFov = 98f;

        [Header("Wall Jump Punch")]
        [SerializeField] private float wallJumpPunchFov = 110f;
        [SerializeField, Range(0f, 1f)] private float wallJumpPunchSnap = 0.7f;
        [SerializeField] private float wallJumpPunchHold = 0.12f;
        [SerializeField] private float wallJumpTiltKick = 9f;

        private float pitch;
        private float currentTilt;
        private bool isSliding;

        private float holdTimer;
        private float holdFov;
        private float pendingDip;
        private float pendingDipTime;
        private float dipAmount;
        private float dipTimer;

        private void Awake()
        {
            if (cam == null) cam = GetComponent<Camera>();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

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
            float dt = Time.deltaTime;

            if (holdTimer > 0f)
            {
                holdTimer -= dt;
                if (holdTimer <= 0f && pendingDip > 0f)
                {
                    dipAmount = pendingDip;
                    dipTimer = pendingDipTime;
                    pendingDip = 0f;
                    pendingDipTime = 0f;
                }
            }

            if (dipTimer > 0f) dipTimer -= dt;

            float rest = isSliding ? slideSustainFov : baseFov;
            if (dipTimer > 0f) rest -= dipAmount;

            float target = holdTimer > 0f ? Mathf.Max(holdFov, rest) : rest;
            float speed = target > cam.fieldOfView ? fovAttackSpeed : fovReleaseSpeed;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, target, 1f - Mathf.Exp(-speed * dt));
        }

        private void Punch(float peakFov, float snap, float hold, float dip, float dipTime)
        {
            holdFov = holdTimer > 0f ? Mathf.Max(holdFov, peakFov) : peakFov;
            holdTimer = Mathf.Max(holdTimer, hold);

            float snapped = Mathf.Lerp(cam.fieldOfView, peakFov, snap);
            if (snapped > cam.fieldOfView) cam.fieldOfView = snapped;

            dipTimer = 0f;
            if (dip > 0f)
            {
                pendingDip = Mathf.Max(pendingDip, dip);
                pendingDipTime = Mathf.Max(pendingDipTime, dipTime);
            }
        }

        public void OnDash()
        {
            Punch(dashPunchFov, dashPunchSnap, dashPunchHold, dashRecoilDip, dashRecoilTime);
        }

        public void OnSlideStart()
        {
            isSliding = true;
            Punch(slidePunchFov, slidePunchSnap, slidePunchHold, 0f, 0f);
        }

        public void OnWallJump(Vector3 wallNormal)
        {
            Punch(wallJumpPunchFov, wallJumpPunchSnap, wallJumpPunchHold, 0f, 0f);
            currentTilt = -Vector3.Dot(wallNormal, transform.right) * wallJumpTiltKick;
        }

        public void OnSlideEnd()
        {
            isSliding = false;
        }
    }
}
