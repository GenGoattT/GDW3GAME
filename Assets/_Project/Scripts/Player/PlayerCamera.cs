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

        private float shakeStrength;
        private float shakeDuration;
        private float shakeTimer;

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

            Vector3 shake = GetShakeOffset();
            transform.localRotation = Quaternion.Euler(pitch + shake.x, shake.y, currentTilt + shake.z);
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

        public void Shake(float strength, float duration)
        {
            if (strength >= shakeStrength * (shakeDuration > 0f ? shakeTimer / shakeDuration : 0f))
            {
                shakeStrength = strength;
                shakeDuration = Mathf.Max(0.01f, duration);
                shakeTimer = shakeDuration;
            }
        }

        public void PunchFov(float peakFov, float hold)
        {
            Punch(peakFov, 0.8f, hold, 0f, 0f);
        }

        private Vector3 GetShakeOffset()
        {
            if (shakeTimer <= 0f) return Vector3.zero;
            shakeTimer -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(shakeTimer / shakeDuration);
            float amp = shakeStrength * k * k;
            float t = Time.unscaledTime * 38f;
            return new Vector3(
                (Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 2f * amp,
                (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * amp,
                (Mathf.PerlinNoise(t, 3.3f) - 0.5f) * 2f * amp * 0.6f);
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
