using UnityEngine;
using Project.Combat;

namespace Project.Enemies
{
    public class DraugrEnemy : BaseEnemy
    {
        private enum State { Chase, Windup, Recover }

        [Header("Chase")]
        [SerializeField] private float repathInterval = 0.1f;
        [Tooltip("How far from the player it stops walking. Keep it a bit under Attack Range so it doesn't walk into the camera.")]
        [SerializeField] private float stopDistance = 2f;

        [Header("Attack")]
        [Tooltip("How close (flat distance) before it starts swinging.")]
        [SerializeField] private float attackRange = 2f;
        [Tooltip("Delay between starting the swing and the hit landing.")]
        [SerializeField] private float windupTime = 0.3f;
        [SerializeField] private float recoverTime = 0.6f;
        [SerializeField] private float damage = 20f;

        [Header("Hit Check")]
        [SerializeField] private float hitRadius = 1f;
        [SerializeField] private float hitForwardOffset = 1f;
        [SerializeField] private float hitHeight = 1f;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Attack Telegraph")]
        [SerializeField] private Color windupTint = new Color(1f, 0.35f, 0.25f);

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string moveScaleParameter = "MoveScale";
        [SerializeField] private string attackSpeedParameter = "AttackSpeed";
        [Tooltip("Movement speed the Running clip was authored for. Faster agents speed the clip up so the feet don't slide.")]
        [SerializeField] private float runAnimationSpeed = 6f;
        [SerializeField] private string locomotionState = "Locomotion";
        [SerializeField] private string attackState = "Attack";
        [SerializeField] private string stunState = "Stun";
        [SerializeField] private float speedDampTime = 0.1f;
        [SerializeField] private float crossFadeTime = 0.08f;
        [Tooltip("Stops the animation from turning or sliding the model away from the enemy's facing.")]
        [SerializeField] private bool lockAnimatedRoot = true;

        [Header("Body Facing")]
        [Tooltip("Counter-rotates the model so the chest and hips face the player even when an animation twists the body.")]
        [SerializeField] private bool alignBodyToTarget = true;
        [SerializeField] private float maxBodyCorrection = 90f;
        [SerializeField] private string leftShoulderBone = "shoulder.L";
        [SerializeField] private string rightShoulderBone = "shoulder.R";
        [SerializeField] private string leftHipBone = "thigh.L";
        [SerializeField] private string rightHipBone = "thigh.R";

        private State state = State.Chase;
        private float stateTimer;
        private float repathTimer;

        private int speedHash;
        private int moveScaleHash;
        private int attackSpeedHash;
        private bool hasMoveScale;
        private bool hasAttackSpeed;
        private float attackClipLength;
        private int locomotionHash;
        private int attackHash;
        private int stunHash;
        private bool wasExternallyMoved;

        private Transform animRoot;
        private Vector3 animRootPosition;
        private Quaternion animRootRotation;

        private Transform leftShoulder;
        private Transform rightShoulder;
        private Transform leftHip;
        private Transform rightHip;
        private float bodySide = 1f;

        protected override Color RestingTint => state == State.Windup ? windupTint : BaseColor;

        protected override void Awake()
        {
            base.Awake();
            Agent.stoppingDistance = stopDistance;
            Agent.updateRotation = false;
            Agent.updatePosition = false;

            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animRoot = animator.transform;
                animRootPosition = animRoot.localPosition;
                animRootRotation = animRoot.localRotation;

                leftShoulder = FindBone(animRoot, leftShoulderBone);
                rightShoulder = FindBone(animRoot, rightShoulderBone);
                leftHip = FindBone(animRoot, leftHipBone);
                rightHip = FindBone(animRoot, rightHipBone);

                if (TryGetBodyForward(out Vector3 restForward) && Vector3.Dot(restForward, transform.forward) < 0f)
                    bodySide = -1f;
            }

            speedHash = Animator.StringToHash(speedParameter);
            moveScaleHash = Animator.StringToHash(moveScaleParameter);
            attackSpeedHash = Animator.StringToHash(attackSpeedParameter);

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (AnimatorControllerParameter param in animator.parameters)
                {
                    if (param.nameHash == moveScaleHash) hasMoveScale = true;
                    if (param.nameHash == attackSpeedHash) hasAttackSpeed = true;
                }

                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                {
                    if (clip.name == attackState) attackClipLength = clip.length;
                }
            }
            locomotionHash = Animator.StringToHash(locomotionState);
            attackHash = Animator.StringToHash(attackState);
            stunHash = Animator.StringToHash(stunState);
        }

        protected override void Update()
        {
            base.Update();
            UpdateAnimation();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            SyncToAgent();

            if (lockAnimatedRoot && animRoot != null && animRoot != transform)
            {
                animRoot.localPosition = animRootPosition;
                animRoot.localRotation = animRootRotation;
            }

            FaceTarget();
            AlignBody();
        }

        private void AlignBody()
        {
            if (!alignBodyToTarget || IsDead || animRoot == null || animRoot == transform) return;

            animRoot.localRotation = animRootRotation;
            if (!TryGetBodyForward(out Vector3 bodyForward)) return;

            float twist = Vector3.SignedAngle(transform.forward, bodyForward, Vector3.up);
            twist = Mathf.Clamp(twist, -maxBodyCorrection, maxBodyCorrection);
            animRoot.localRotation = Quaternion.Euler(0f, -twist, 0f) * animRootRotation;
        }

        private bool TryGetBodyForward(out Vector3 forward)
        {
            forward = Vector3.zero;
            if (leftShoulder == null || rightShoulder == null || leftHip == null || rightHip == null) return false;

            Vector3 side = (leftShoulder.position - rightShoulder.position) + (leftHip.position - rightHip.position);
            side.y = 0f;
            if (side.sqrMagnitude < 0.0001f) return false;

            forward = Vector3.Cross(Vector3.up, side.normalized) * bodySide;
            return true;
        }

        private static Transform FindBone(Transform root, string boneName)
        {
            if (root.name == boneName) return root;

            foreach (Transform child in root)
            {
                Transform found = FindBone(child, boneName);
                if (found != null) return found;
            }
            return null;
        }

        private void SyncToAgent()
        {
            if (IsDead || IsExternallyMoved || !Agent.enabled || !Agent.isOnNavMesh) return;

            if (Agent.updatePosition) Agent.updatePosition = false;
            if (Agent.updateRotation) Agent.updateRotation = false;

            transform.position = Agent.nextPosition;
        }

        private void FaceTarget()
        {
            if (IsDead || IsExternallyMoved || Target == null) return;

            Vector3 toTarget = FlatDirectionToTarget();
            if (toTarget.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toTarget, Vector3.up);
        }

        private Vector3 FlatDirectionToTarget()
        {
            if (Target == null) return transform.forward;

            Vector3 toTarget = Target.position - transform.position;
            toTarget.y = 0f;
            return toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;
        }

        private void UpdateAnimation()
        {
            if (animator == null || IsDead) return;

            if (IsExternallyMoved != wasExternallyMoved)
            {
                wasExternallyMoved = IsExternallyMoved;
                PlayState(IsExternallyMoved ? stunHash : locomotionHash);
            }

            float speed = Agent.enabled && !IsExternallyMoved ? Agent.velocity.magnitude : 0f;
            animator.SetFloat(speedHash, speed, speedDampTime, Time.deltaTime);
            if (hasMoveScale) animator.SetFloat(moveScaleHash, Mathf.Max(1f, speed / runAnimationSpeed));
        }

        private void PlayState(int stateHash)
        {
            if (animator == null || !animator.HasState(0, stateHash)) return;
            animator.CrossFadeInFixedTime(stateHash, crossFadeTime, 0, 0f);
        }

        protected override void OnDeath()
        {
            PlayState(stunHash);
        }

        protected override void OnAlerted()
        {
            state = State.Chase;
            repathTimer = 0f;
        }

        protected override void Tick(Vector3 toTarget, float dist)
        {
            switch (state)
            {
                case State.Chase: TickChase(dist); break;
                case State.Windup: TickWindup(); break;
                case State.Recover: TickRecover(); break;
            }
        }

        private void TickChase(float dist)
        {
            KeepChasing();

            if (dist <= attackRange)
                BeginWindup();
        }

        private void KeepChasing()
        {
            repathTimer -= Time.deltaTime;
            if (repathTimer > 0f || !Agent.isOnNavMesh) return;

            Agent.isStopped = false;
            Agent.SetDestination(Target.position);
            repathTimer = repathInterval;
        }

        private void BeginWindup()
        {
            state = State.Windup;
            stateTimer = windupTime;

            SetTint(windupTint);

            if (hasAttackSpeed && attackClipLength > 0f)
                animator.SetFloat(attackSpeedHash, attackClipLength * 0.9f / (windupTime + recoverTime));

            PlayState(attackHash);
        }

        private void TickWindup()
        {
            KeepChasing();

            stateTimer -= Time.deltaTime;
            if (stateTimer > 0f) return;

            Bite(FlatDirectionToTarget());
            state = State.Recover;
            stateTimer = recoverTime;
            SetTint(BaseColor);
        }

        private void TickRecover()
        {
            KeepChasing();

            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
                state = State.Chase;
        }

        private bool Bite(Vector3 facing)
        {
            Vector3 center = Agent.isOnNavMesh ? Agent.nextPosition : transform.position;
            center += facing * hitForwardOffset + Vector3.up * hitHeight;

            Collider[] hits = Physics.OverlapSphere(center, hitRadius, hitMask, QueryTriggerInteraction.Ignore);
            foreach (Collider col in hits)
            {
                if (col.transform.IsChildOf(transform)) continue;
                if (col.GetComponentInParent<BaseEnemy>() != null) continue;

                IDamageable dmg = col.GetComponentInParent<IDamageable>();
                if (dmg == null || dmg.IsDead) continue;

                dmg.TakeDamage(damage, center, facing);
                return true;
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            Gizmos.color = Color.red;
            Vector3 center = transform.position + transform.forward * hitForwardOffset + Vector3.up * hitHeight;
            Gizmos.DrawWireSphere(center, hitRadius);
        }
    }
}
