using UnityEngine;

namespace Project.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraHolder;
        [SerializeField] private PlayerCamera playerCamera;

        [Header("Input (Legacy Input Manager)")]
        [Tooltip("Jump uses the default 'Jump' input axis (Space).")]
        [SerializeField] private KeyCode dashKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode slideKey = KeyCode.LeftControl;

        [Header("Ground Movement")]
        [SerializeField] private float moveSpeed = 9f;
        [SerializeField] private float groundAcceleration = 14f;
        [SerializeField] private float groundFriction = 8f;

        [Header("Air Movement")]
        [Tooltip("Air movement uses Quake-style acceleration, which is what makes strafing/bunny-hopping gain speed like in Ultrakill.")]
        [SerializeField] private float airAcceleration = 50f;
        [SerializeField] private float airSpeedCap = 1.6f; // multiplier of moveSpeed reachable purely from air-strafing
        [SerializeField] private float airControl = 1f;

        [Header("Jump")]
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float jumpBufferTime = 0.12f;
        [SerializeField] private float gravity = -28f;
        [SerializeField] private float fallMultiplier = 1.6f;

        [Header("Dash")]
        [SerializeField] private float dashSpeed = 22f;
        [SerializeField] private float dashDuration = 0.18f;
        [SerializeField] private int maxDashCharges = 1;
        [Tooltip("Charges also slowly regenerate mid-air on this cooldown so you're never permanently stuck without one during a long fall. Landing always fully refills.")]
        [SerializeField] private float dashRegenTime = 0.6f;
        [SerializeField] private float dashUpwardBoost = 1f;

        [Header("Speed Limit")]
        [Tooltip("Hard cap on horizontal speed, applied every frame after all movement/slide/dash logic. Set to 0 to disable.")]
        [SerializeField] private float maxHorizontalSpeed = 16f;

        [Header("Slide")]
        [SerializeField] private float slideSpeedBoost = 1.35f;
        [SerializeField] private float slideFriction = 1.5f;
        [SerializeField] private float minSlideSpeed = 4f;
        [SerializeField] private float slideHopBoost = 1.1f;
        [SerializeField] private float standingHeight = 2f;
        [SerializeField] private float slidingHeight = 1f;
        [SerializeField] private float crouchTransitionSpeed = 10f;

        private CharacterController controller;

        private Vector3 velocity;
        private float verticalVelocity;

        private bool isGrounded;
        private bool wasGroundedLastFrame;
        private float coyoteTimer;
        private float jumpBufferTimer;

        private bool isSliding;
        private float currentHeight;

        private int dashCharges;
        private bool isDashing;
        private float dashTimer;
        private Vector3 dashDirection;
        private float dashRegenTimer;

        public bool IsSliding => isSliding;
        public bool IsDashing => isDashing;
        public bool IsGrounded => isGrounded;
        public Vector3 HorizontalVelocity => new Vector3(velocity.x, 0f, velocity.z);

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            currentHeight = standingHeight;
            controller.height = currentHeight;
            controller.center = new Vector3(0f, currentHeight * 0.5f, 0f);
            dashCharges = maxDashCharges;
        }

        private void Update()
        {
            CheckGround();
            HandleTimers();
            HandleSlideInput();
            HandleDashInput();

            if (isDashing)
            {
                DashMove();
            }
            else if (isSliding)
            {
                SlideMove();
            }
            else
            {
                NormalMove();
            }

            ApplyGravity();
            ClampHorizontalSpeed();
            UpdateCrouchHeight();

            controller.Move(velocity * Time.deltaTime);

            wasGroundedLastFrame = isGrounded;
        }

        private void CheckGround()
        {
            isGrounded = controller.isGrounded;

            if (isGrounded)
            {
                coyoteTimer = coyoteTime;
                if (!wasGroundedLastFrame)
                {
                    dashCharges = maxDashCharges;
                }
            }
            else
            {
                coyoteTimer -= Time.deltaTime;
            }
        }

        private void HandleTimers()
        {
            if (Input.GetButtonDown("Jump"))
                jumpBufferTimer = jumpBufferTime;
            else
                jumpBufferTimer -= Time.deltaTime;

            if (dashCharges < maxDashCharges)
            {
                dashRegenTimer -= Time.deltaTime;
                if (dashRegenTimer <= 0f)
                {
                    dashCharges++;
                    dashRegenTimer = dashRegenTime;
                }
            }
        }

        private Vector3 GetWishDir()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Debug.Log(h);
            Debug.Log(v);
            Vector3 wish = transform.right * h + transform.forward * v;
            wish.y = 0f;
            return wish.sqrMagnitude > 1f ? wish.normalized : wish;
        }

        private void NormalMove()
        {
            Vector3 wishDir = GetWishDir();

            if (isGrounded)
            {
                ApplyFriction(groundFriction);
                Accelerate(wishDir, moveSpeed, groundAcceleration);
            }
            else
            {
                Accelerate(wishDir, moveSpeed * airSpeedCap, airAcceleration * airControl);
            }

            TryJump();
        }

        private void TryJump()
        {
            if (jumpBufferTimer > 0f && coyoteTimer > 0f)
            {
                verticalVelocity = jumpForce;
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
            }
        }

        private void Accelerate(Vector3 wishDir, float wishSpeed, float accel)
        {
            if (wishDir.sqrMagnitude < 0.0001f) return;

            Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
            float currentSpeed = Vector3.Dot(horizontalVel, wishDir);
            float addSpeed = wishSpeed - currentSpeed;
            if (addSpeed <= 0f) return;

            float accelSpeed = accel * wishSpeed * Time.deltaTime;
            accelSpeed = Mathf.Min(accelSpeed, addSpeed);

            horizontalVel += wishDir * accelSpeed;
            velocity.x = horizontalVel.x;
            velocity.z = horizontalVel.z;
        }

        private void ApplyFriction(float friction)
        {
            Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
            float speed = horizontalVel.magnitude;
            if (speed < 0.001f)
            {
                velocity.x = 0f;
                velocity.z = 0f;
                return;
            }

            float drop = speed * friction * Time.deltaTime;
            float newSpeed = Mathf.Max(speed - drop, 0f);
            float scale = newSpeed / speed;

            velocity.x *= scale;
            velocity.z *= scale;
        }

        private void HandleSlideInput()
        {
            bool slideKeyHeld = Input.GetKey(slideKey);
            Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);

            if (!isSliding && slideKeyHeld && isGrounded && horizontalVel.magnitude > minSlideSpeed * 0.6f)
            {
                StartSlide();
                return;
            }

            if (isSliding)
            {
                bool wantsToJumpOut = jumpBufferTimer > 0f && coyoteTimer > 0f;
                bool tooSlow = horizontalVel.magnitude < minSlideSpeed;
                bool keyReleased = !slideKeyHeld;
                bool leftGround = !isGrounded;

                if (wantsToJumpOut || tooSlow || keyReleased || leftGround)
                {
                    if (wantsToJumpOut && isGrounded)
                    {
                        Vector3 boosted = horizontalVel.normalized * (horizontalVel.magnitude * slideHopBoost);
                        velocity.x = boosted.x;
                        velocity.z = boosted.z;
                    }
                    EndSlide();
                }
            }
        }

        private void StartSlide()
        {
            isSliding = true;
            Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
            float boostedSpeed = Mathf.Max(horizontalVel.magnitude * slideSpeedBoost, moveSpeed * slideSpeedBoost);
            Vector3 dir = horizontalVel.sqrMagnitude > 0.01f ? horizontalVel.normalized : transform.forward;
            velocity.x = dir.x * boostedSpeed;
            velocity.z = dir.z * boostedSpeed;

            if (playerCamera != null) playerCamera.OnSlideStart();
        }

        private void EndSlide()
        {
            isSliding = false;
            if (playerCamera != null) playerCamera.OnSlideEnd();
        }

        private void SlideMove()
        {
            ApplyFriction(slideFriction);

            // Light steering while sliding, doesn't override the momentum feel.
            Vector3 wishDir = GetWishDir();
            Accelerate(wishDir, moveSpeed * slideSpeedBoost, groundAcceleration * 0.25f);
        }

        private void HandleDashInput()
        {
            if (isDashing)
            {
                dashTimer -= Time.deltaTime;
                if (dashTimer <= 0f)
                {
                    isDashing = false;
                    Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
                    float cap = moveSpeed * airSpeedCap;
                    if (horizontalVel.magnitude > cap)
                    {
                        Vector3 clamped = horizontalVel.normalized * cap;
                        velocity.x = clamped.x;
                        velocity.z = clamped.z;
                    }
                }
                return;
            }

            if (Input.GetKeyDown(dashKey) && dashCharges > 0)
            {
                StartDash();
            }
        }

        private void StartDash()
        {
            Vector3 wishDir = GetWishDir();
            dashDirection = wishDir.sqrMagnitude > 0.01f ? wishDir : transform.forward;
            dashDirection.y = 0f;
            dashDirection.Normalize();

            isDashing = true;
            dashTimer = dashDuration;
            dashCharges--;
            dashRegenTimer = dashRegenTime;

            if (isSliding) EndSlide();

            if (!isGrounded)
                verticalVelocity = Mathf.Max(verticalVelocity, dashUpwardBoost);

            if (playerCamera != null) playerCamera.OnDash();
        }

        private void DashMove()
        {
            velocity.x = dashDirection.x * dashSpeed;
            velocity.z = dashDirection.z * dashSpeed;
            verticalVelocity = Mathf.Lerp(verticalVelocity, 0f, Time.deltaTime * 4f);
        }

        private void ApplyGravity()
        {
            if (isDashing)
            {
                velocity.y = verticalVelocity;
                return;
            }

            if (isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f; // keeps CharacterController firmly groundeds
            }
            else
            {
                float multiplier = verticalVelocity < 0f ? fallMultiplier : 1f;
                verticalVelocity += gravity * multiplier * Time.deltaTime;
            }

            velocity.y = verticalVelocity;
        }

        private void ClampHorizontalSpeed()
        {
            if (maxHorizontalSpeed <= 0f) return;
            if (isDashing) return; // dash is allowed to briefly exceed the cap for its snap

            Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
            if (horizontalVel.magnitude > maxHorizontalSpeed)
            {
                Vector3 clamped = horizontalVel.normalized * maxHorizontalSpeed;
                velocity.x = clamped.x;
                velocity.z = clamped.z;
            }
        }

        private void UpdateCrouchHeight()
        {
            float targetHeight = isSliding ? slidingHeight : standingHeight;
            currentHeight = Mathf.Lerp(currentHeight, targetHeight, Time.deltaTime * crouchTransitionSpeed);
            controller.height = currentHeight;
            controller.center = new Vector3(0f, currentHeight * 0.5f, 0f);

            if (cameraHolder != null)
            {
                Vector3 pos = cameraHolder.localPosition;
                pos.y = Mathf.Lerp(pos.y, currentHeight * 0.85f, Time.deltaTime * crouchTransitionSpeed);
                cameraHolder.localPosition = pos;
            }
        }
    }
}
