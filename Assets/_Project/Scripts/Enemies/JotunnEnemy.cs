using Project.Audio;
using UnityEngine;
using Project.Combat;
using Project.Player;

namespace Project.Enemies
{
    public class JotunnEnemy : BaseEnemy
    {
        private enum State { Chase, SwingWindup, SwingStrike, Exhausted, ThrowWindup, ThrowRecover }

        [Header("Chase")]
        [SerializeField] private float repathInterval = 0.15f;

        [Header("Club")]
        [SerializeField] private Transform club;
        [SerializeField] private Vector3 shoulderOffset = new Vector3(1.9f, 3f, 0.4f);
        [SerializeField] private float clubLength = 7f;
        [SerializeField] private Color clubColor = new Color(0.32f, 0.27f, 0.24f);
        [SerializeField] private Vector2 restPose = new Vector2(55f, 25f);

        [Header("Wild Swings")]
        [SerializeField] private float attackRange = 6.5f;
        [SerializeField] private Vector2Int swingsPerCombo = new Vector2Int(2, 4);
        [SerializeField, Range(0f, 1f)] private float slamChance = 0.5f;
        [SerializeField] private float firstWindupTime = 0.6f;
        [SerializeField] private float chainWindupTime = 0.22f;
        [SerializeField] private float strikeTime = 0.28f;
        [SerializeField] private float slamWindupTime = 0.5f;
        [SerializeField] private float exhaustedTime = 1.4f;
        [SerializeField] private float swingJitter = 20f;
        [SerializeField] private float lungeSpeed = 4f;
        [SerializeField] private float abandonRangeMultiplier = 1.8f;
        [SerializeField] private float clubTurnSpeed = 240f;

        [Header("Club Damage")]
        [SerializeField] private float clubDamage = 18f;
        [SerializeField] private float slamDamage = 35f;
        [SerializeField] private float hitRadius = 1.9f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private float slamShake = 0.7f;

        [Header("Ice Throw")]
        [SerializeField] private IceBlockProjectile iceBlockPrefab;
        [SerializeField] private Transform throwPoint;
        [SerializeField] private float minThrowDistance = 8f;
        [SerializeField] private float maxThrowDistance = 45f;
        [SerializeField] private float throwWindupTime = 0.9f;
        [SerializeField] private float throwRecoverTime = 0.9f;
        [SerializeField] private int blocksPerVolley = 1;
        [SerializeField] private float volleyInterval = 0.5f;
        [SerializeField] private float projectileSpeed = 22f;
        [SerializeField] private float projectileGravityScale = 0.4f;
        [SerializeField] private float leadFactor = 0.6f;
        [SerializeField] private float aimHeight = 1f;
        [SerializeField] private float throwTurnSpeed = 120f;
        [SerializeField] private LayerMask lineOfSightMask = ~0;

        [Header("Attack Cycle")]
        [SerializeField] private float meleePhaseMin = 4f;
        [SerializeField] private float meleePhaseMax = 7f;
        [SerializeField] private float throwRetryDelay = 1f;

        [Header("Telegraph")]
        [SerializeField] private Color clubTint = new Color(1f, 0.35f, 0.25f);
        [SerializeField] private Color throwTint = new Color(0.45f, 0.85f, 1f);

        private State state = State.Chase;
        private float stateTimer;
        private float stateLength;
        private float repathTimer;
        private float meleeTimer;
        private int volleyRemaining;
        private IceBlockProjectile heldBlock;
        private PlayerMovement playerMovement;
        private PlayerCamera playerCamera;

        private int swingsLeft;
        private int swingSide = 1;
        private bool slamming;
        private bool swingLanded;
        private Vector2 pose;
        private Vector2 poseFrom;
        private Vector2 windupPose;
        private Vector2 strikeEnd;
        private bool ownsClub;

        protected override Color RestingTint
        {
            get
            {
                if (state == State.SwingWindup) return clubTint;
                if (state == State.ThrowWindup) return throwTint;
                return BaseColor;
            }
        }

        private Vector3 ThrowOrigin => throwPoint != null
            ? throwPoint.position
            : transform.position + Vector3.up * 3f + transform.forward * 0.8f;

        private Vector3 Shoulder => transform.position + transform.rotation * shoulderOffset;

        private void Reset()
        {
            moveSpeed = 2.4f;
            acceleration = 6f;
            turnSpeed = 100f;
        }

        protected override void Start()
        {
            base.Start();
            if (Target != null)
            {
                playerMovement = Target.GetComponent<PlayerMovement>();
                playerCamera = Target.GetComponentInChildren<PlayerCamera>();
            }

            pose = restPose;
            if (club == null) BuildClub();
            ApplyClubPose();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (ownsClub && club != null) Destroy(club.gameObject);
        }

        protected override void OnAlerted()
        {
            state = State.Chase;
            repathTimer = 0f;
            meleeTimer = Random.Range(0.5f, 1.5f);
        }

        protected override void OnDeath()
        {
            if (heldBlock != null) Destroy(heldBlock.gameObject);
        }

        protected override void Tick(Vector3 toTarget, float dist)
        {
            switch (state)
            {
                case State.Chase: TickChase(dist); break;
                case State.SwingWindup: TickSwingWindup(toTarget); break;
                case State.SwingStrike: TickSwingStrike(toTarget, dist); break;
                case State.Exhausted: TickExhausted(); break;
                case State.ThrowWindup: TickThrowWindup(toTarget); break;
                case State.ThrowRecover: TickThrowRecover(); break;
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

            bool swinging = state == State.SwingWindup || state == State.SwingStrike;
            if (!swinging || IsDead) pose = Vector2.Lerp(pose, restPose, 1f - Mathf.Exp(-5f * Time.deltaTime));

            ApplyClubPose();
        }

        private void TickChase(float dist)
        {
            if (dist <= attackRange)
            {
                BeginCombo();
                return;
            }

            meleeTimer -= Time.deltaTime;
            if (meleeTimer <= 0f)
            {
                if (CanThrow(dist))
                {
                    BeginThrow();
                    return;
                }
                meleeTimer = throwRetryDelay;
            }

            repathTimer -= Time.deltaTime;
            if (repathTimer <= 0f)
            {
                Agent.SetDestination(Target.position);
                repathTimer = repathInterval;
            }
        }

        private void StopAgent()
        {
            Agent.isStopped = true;
            Agent.velocity = Vector3.zero;
            Agent.updateRotation = false;
        }

        private void ResumeAgent()
        {
            Agent.isStopped = false;
            Agent.updateRotation = true;
            repathTimer = 0f;
        }

        private void BeginCombo()
        {
            StopAgent();
            swingsLeft = Random.Range(swingsPerCombo.x, swingsPerCombo.y + 1);
            swingSide = Random.value < 0.5f ? 1 : -1;
            BeginSwing(firstWindupTime);
        }

        private void BeginSwing(float windup)
        {
            slamming = swingsLeft <= 0;
            swingLanded = false;

            if (slamming)
            {
                windupPose = new Vector2(-110f, Random.Range(-8f, 8f));
                strikeEnd = new Vector2(78f, 0f);
                windup = slamWindupTime;
            }
            else
            {
                float jitter = Random.Range(-swingJitter, swingJitter);
                windupPose = new Vector2(-30f + jitter * 0.5f, (110f + jitter) * swingSide);
                strikeEnd = new Vector2(40f, (-100f + jitter) * swingSide);
            }

            poseFrom = pose;
            state = State.SwingWindup;
            stateTimer = stateLength = windup;
            SetTint(clubTint);
        }

        private void TickSwingWindup(Vector3 toTarget)
        {
            RotateTowards(toTarget, clubTurnSpeed);

            stateTimer -= Time.deltaTime;
            float k = 1f - Mathf.Clamp01(stateTimer / stateLength);
            pose = Vector2.Lerp(poseFrom, windupPose, k * k * (3f - 2f * k));

            if (stateTimer > 0f) return;

            state = State.SwingStrike;
            stateTimer = stateLength = strikeTime;
            SetTint(BaseColor);
            GameAudio.Play(SfxId.ClubSwing, Shoulder, slamming ? 1f : 0.8f);
        }

        private void TickSwingStrike(Vector3 toTarget, float dist)
        {
            stateTimer -= Time.deltaTime;
            float k = 1f - Mathf.Clamp01(stateTimer / stateLength);
            pose = StrikePose(k * k);

            if (!slamming && toTarget.sqrMagnitude > 0.01f)
                Agent.Move(toTarget.normalized * lungeSpeed * Time.deltaTime);

            if (!swingLanded && k > 0.2f) swingLanded = CheckClubHit();

            if (stateTimer > 0f) return;

            if (slamming) OnSlamLanded();

            if (slamming || dist > attackRange * abandonRangeMultiplier)
            {
                state = State.Exhausted;
                stateTimer = exhaustedTime;
                return;
            }

            swingsLeft--;
            swingSide = -swingSide;

            if (swingsLeft <= 0 && Random.value > slamChance)
            {
                state = State.Exhausted;
                stateTimer = exhaustedTime;
                return;
            }

            BeginSwing(chainWindupTime);
        }

        private Vector2 StrikePose(float t)
        {
            if (slamming) return Vector2.Lerp(windupPose, strikeEnd, t);

            float yaw = Mathf.Lerp(windupPose.y, strikeEnd.y, t);
            float pitch = t < 0.5f
                ? Mathf.Lerp(windupPose.x, 58f, t * 2f)
                : Mathf.Lerp(58f, strikeEnd.x, (t - 0.5f) * 2f);
            return new Vector2(pitch, yaw);
        }

        private void TickExhausted()
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer > 0f) return;

            ResumeAgent();
            state = State.Chase;
        }

        private bool CheckClubHit()
        {
            Vector3 origin = Shoulder;
            Vector3 dir = ClubRotation * Vector3.forward;
            float damage = slamming ? slamDamage : clubDamage;

            for (int i = 0; i < 3; i++)
            {
                float along = clubLength * (0.45f + 0.275f * i);
                float radius = hitRadius * (0.6f + 0.2f * i);
                Vector3 point = origin + dir * along;

                foreach (Collider col in Physics.OverlapSphere(point, radius, hitMask, QueryTriggerInteraction.Ignore))
                {
                    if (col.transform.IsChildOf(transform)) continue;
                    if (club != null && col.transform.IsChildOf(club)) continue;
                    if (col.GetComponentInParent<BaseEnemy>() != null) continue;

                    IDamageable target = col.GetComponentInParent<IDamageable>();
                    if (target == null || target.IsDead) continue;

                    target.TakeDamage(damage, point, (point - transform.position).normalized);
                    return true;
                }
            }

            return false;
        }

        private void OnSlamLanded()
        {
            GameAudio.Play(SfxId.ClubSlam, transform.position + transform.rotation * Vector3.forward * clubLength * 0.6f);
            if (playerCamera == null || Target == null) return;

            float dist = Vector3.Distance(Target.position, transform.position);
            float falloff = Mathf.Clamp01(1f - dist / (attackRange * 3f));
            if (falloff > 0f) playerCamera.Shake(slamShake * falloff, 0.3f);
        }

        private bool CanThrow(float dist)
        {
            if (iceBlockPrefab == null) return false;
            if (dist < minThrowDistance || dist > maxThrowDistance) return false;
            return HasLineOfSight();
        }

        private bool HasLineOfSight()
        {
            Vector3 from = ThrowOrigin;
            Vector3 to = Target.position + Vector3.up * aimHeight;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.01f) return true;

            RaycastHit[] hits = Physics.RaycastAll(from, delta / length, length, lineOfSightMask, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;
                if (hit.transform.IsChildOf(Target)) continue;
                if (hit.collider.GetComponentInParent<BaseEnemy>() != null) continue;
                return false;
            }
            return true;
        }

        private void BeginThrow()
        {
            state = State.ThrowWindup;
            stateTimer = throwWindupTime;
            volleyRemaining = Mathf.Max(1, blocksPerVolley);
            StopAgent();
            SpawnHeldBlock();
            SetTint(throwTint);
        }

        private void TickThrowWindup(Vector3 toTarget)
        {
            RotateTowards(toTarget, throwTurnSpeed);

            stateTimer -= Time.deltaTime;
            if (stateTimer > 0f) return;

            ReleaseBlock();
            volleyRemaining--;

            if (volleyRemaining > 0)
            {
                stateTimer = volleyInterval;
                SpawnHeldBlock();
            }
            else
            {
                state = State.ThrowRecover;
                stateTimer = throwRecoverTime;
                SetTint(BaseColor);
            }
        }

        private void TickThrowRecover()
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                ResumeAgent();
                meleeTimer = Random.Range(meleePhaseMin, meleePhaseMax);
                state = State.Chase;
            }
        }

        private void SpawnHeldBlock()
        {
            if (heldBlock != null) return;
            heldBlock = Instantiate(iceBlockPrefab, ThrowOrigin, Quaternion.identity);
            heldBlock.Hold(throwPoint != null ? throwPoint : transform);
        }

        private void ReleaseBlock()
        {
            if (heldBlock == null) return;

            Vector3 origin = heldBlock.transform.position;
            Vector3 aim = Target.position + Vector3.up * aimHeight;
            float flightTime = Mathf.Max(0.25f, Vector3.Distance(origin, aim) / projectileSpeed);

            if (playerMovement != null)
                aim += playerMovement.HorizontalVelocity * (flightTime * leadFactor);

            Vector3 gravity = Physics.gravity * projectileGravityScale;
            Vector3 velocity = (aim - origin - 0.5f * gravity * flightTime * flightTime) / flightTime;

            heldBlock.Launch(velocity, gravity, transform);
            GameAudio.Play(SfxId.IceThrow, origin);
            heldBlock = null;
        }

        private Quaternion ClubRotation => transform.rotation * Quaternion.Euler(pose.x, pose.y, 0f);

        private void ApplyClubPose()
        {
            if (club == null) return;
            club.SetPositionAndRotation(Shoulder, ClubRotation);
        }

        private void BuildClub()
        {
            club = new GameObject(name + "_Club").transform;
            ownsClub = true;

            Material source = GetComponentInChildren<Renderer>() != null ? GetComponentInChildren<Renderer>().sharedMaterial : null;
            Material mat = source != null ? new Material(source) : null;
            if (mat != null)
            {
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", clubColor);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", clubColor);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
            }

            AddClubPart("Handle", new Vector3(0f, 0f, clubLength * 0.36f), new Vector3(0.35f, 0.35f, clubLength * 0.72f), 0f, mat);
            AddClubPart("Head", new Vector3(0f, 0f, clubLength * 0.84f), new Vector3(1.1f, 1.1f, clubLength * 0.32f), 45f, mat);
        }

        private void AddClubPart(string label, Vector3 localPos, Vector3 scale, float roll, Material mat)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = label;
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(club, false);
            part.transform.localPosition = localPos;
            part.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
            part.transform.localScale = scale;
            if (mat != null) part.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + transform.rotation * shoulderOffset, 0.4f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, minThrowDistance);
            Gizmos.DrawWireSphere(transform.position, maxThrowDistance);
        }
    }
}
