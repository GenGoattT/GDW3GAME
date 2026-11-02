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
        [SerializeField] private float airSpeedCap = 1.6f;
        [SerializeField] private float airControl = 1f;

        [Header("Jump")]
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float jumpBufferTime = 0.12f;
        [SerializeField] private float gravity = -28f;
        [SerializeField] private float fallMultiplier = 1.6f;

        [Header("Wall Jump")]
        [SerializeField] private float wallJumpForce = 9.5f;
        [SerializeField] private float wallJumpPush = 11f;
        [SerializeField] private int maxWallJumps = 3;
        [SerializeField] private float wallContactGrace = 0.12f;
        [SerializeField] private float wallMaxNormalY = 0.3f;

        [Header("Dash")]
        [SerializeField] private float dashSpeed = 22f;
        [SerializeField] private float dashDuration = 0.18f;
        [SerializeField] public int maxDashCharges = 1; //Changed this to public so that the HUD can access it for the dash cooldown display.
        [Tooltip("Charges also slowly regenerate mid-air on this cooldown so you're never permanently stuck without one during a long fall. Landing always fully refills.")]
        [SerializeField] public float dashRegenTime = 0.6f; //Changed this to public so that the HUD can access it for the dash cooldown display.
        [SerializeField] private float dashUpwardBoost = 1f;

        [Header("Speed Limit")]
        [Tooltip("Hard cap on horizontal speed, applied every frame after all movement/slide/dash logic. Set to 0 to disable.")]
        [SerializeField] private float maxHorizontalSpeed = 16f;

        [Header("Slide")]
        [SerializeField] private float slideSpeedBoost = 1.35f;
        [SerializeField] private float slideImpulse = 14f;
        [SerializeField] private float slideFriction = 1.5f;
        [SerializeField] private float minSlideSpeed = 4f;
        [SerializeField] private float slideHopBoost = 1.1f;
        [SerializeField] private float standingHeight = 2f;
        [SerializeField] private float slidingHeight = 1f;
        [SerializeField] private float slideEnterSnap = 0.7f;
        [SerializeField] private float slideEnterHeightSpeed = 40f;
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

        public int dashCharges; //Changed this to public so that the HUD can access it for the dash cooldown display.
        private bool isDashing;
        private float dashTimer;
        private Vector3 dashDirection;
        public float dashRegenTimer; //Changed this to public so that the HUD can access it for the dash cooldown display.

        private Vector3 wallNormal;
        private float wallContactTimer;
        private int wallJumpsUsed;

        public bool IsSliding => isSliding;
        public bool IsDashing => isDashing;
        public bool IsGrounded => isGrounded;
        public int WallJumpsRemaining => Mathf.Max(0, maxWallJumps - wallJumpsUsed);
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
            HandleWallJump();

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
                    wallJumpsUsed = 0;
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

            wallContactTimer -= Time.deltaTime;

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
            Vector3 dir = horizontalVel.sqrMagnitude > 0.01f ? horizontalVel.normalized : transform.forward;
            float entrySpeed = Mathf.Max(horizontalVel.magnitude * slideSpeedBoost, slideImpulse);

            velocity.x = dir.x * entrySpeed;
            velocity.z = dir.z * entrySpeed;

            currentHeight = Mathf.Lerp(currentHeight, slidingHeight, slideEnterSnap);

            if (cameraHolder != null)
            {
                Vector3 pos = cameraHolder.localPosition;
                pos.y = Mathf.Lerp(pos.y, currentHeight * 0.85f, slideEnterSnap);
                cameraHolder.localPosition = pos;
            }

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

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (isGrounded) return;

            Vector3 n = hit.normal;
            if (n.y > wallMaxNormalY || n.y < -wallMaxNormalY) return;

            wallNormal = new Vector3(n.x, 0f, n.z).normalized;
            wallContactTimer = wallContactGrace;
        }

        private void HandleWallJump()
        {
            if (isGrounded || coyoteTimer > 0f) return;
            if (jumpBufferTimer <= 0f) return;
            if (wallContactTimer <= 0f) return;
            if (wallJumpsUsed >= maxWallJumps) return;

            Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
            float into = Vector3.Dot(horizontalVel, wallNormal);
            if (into < 0f) horizontalVel -= wallNormal * into;
            horizontalVel += wallNormal * wallJumpPush;

            velocity.x = horizontalVel.x;
            velocity.z = horizontalVel.z;
            verticalVelocity = wallJumpForce;

            jumpBufferTimer = 0f;
            wallContactTimer = 0f;
            wallJumpsUsed++;
            isDashing = false;

            if (playerCamera != null) playerCamera.OnWallJump(wallNormal);
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
                verticalVelocity = -2f;
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
            if (isDashing) return;

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
            float speed = isSliding ? slideEnterHeightSpeed : crouchTransitionSpeed;
            float t = 1f - Mathf.Exp(-speed * Time.deltaTime);

            currentHeight = Mathf.Lerp(currentHeight, targetHeight, t);
            controller.height = currentHeight;
            controller.center = new Vector3(0f, currentHeight * 0.5f, 0f);

            if (cameraHolder != null)
            {
                Vector3 pos = cameraHolder.localPosition;
                pos.y = Mathf.Lerp(pos.y, currentHeight * 0.85f, t);
                cameraHolder.localPosition = pos;
            }
        }
    }
}
