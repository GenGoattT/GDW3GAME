using UnityEngine;
using Project.Combat;
using Project.Player;

namespace Project.Enemies
{
    public class JotunnEnemy : BaseEnemy
    {
        private enum State { Chase, ClubWindup, ClubRecover, ThrowWindup, ThrowRecover }

        [Header("Chase")]
        [SerializeField] private float repathInterval = 0.15f;

        [Header("Club")]
        [SerializeField] private float attackRange = 3.2f;
        [SerializeField] private float clubWindupTime = 0.75f;
        [SerializeField] private float clubRecoverTime = 1.1f;
        [SerializeField] private float clubDamage = 35f;
        [SerializeField] private float clubTurnSpeed = 90f;

        [Header("Club Hit Check")]
        [SerializeField] private float hitRadius = 1.6f;
        [SerializeField] private float hitForwardOffset = 2.2f;
        [SerializeField] private float hitHeight = 1.5f;
        [SerializeField] private LayerMask hitMask = ~0;

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
        [SerializeField] private float meleePhaseMin = 5f;
        [SerializeField] private float meleePhaseMax = 8f;
        [SerializeField] private float throwRetryDelay = 1f;

        [Header("Telegraph")]
        [SerializeField] private Color clubTint = new Color(1f, 0.35f, 0.25f);
        [SerializeField] private Color throwTint = new Color(0.45f, 0.85f, 1f);

        private State state = State.Chase;
        private float stateTimer;
        private float repathTimer;
        private float meleeTimer;
        private int volleyRemaining;
        private IceBlockProjectile heldBlock;
        private PlayerMovement playerMovement;

        protected override Color RestingTint
        {
            get
            {
                if (state == State.ClubWindup) return clubTint;
                if (state == State.ThrowWindup) return throwTint;
                return BaseColor;
            }
        }

        private Vector3 ThrowOrigin => throwPoint != null
            ? throwPoint.position
            : transform.position + Vector3.up * 3f + transform.forward * 0.8f;

        private void Reset()
        {
            moveSpeed = 3.5f;
            acceleration = 10f;
            turnSpeed = 140f;
        }

        protected override void Start()
        {
            base.Start();
            if (Target != null) playerMovement = Target.GetComponent<PlayerMovement>();
        }

        protected override void OnAlerted()
        {
            state = State.Chase;
            repathTimer = 0f;
            meleeTimer = 0f;
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
                case State.ClubWindup: TickClubWindup(toTarget); break;
                case State.ClubRecover: TickClubRecover(); break;
                case State.ThrowWindup: TickThrowWindup(toTarget); break;
                case State.ThrowRecover: TickThrowRecover(); break;
            }
        }

        private void TickChase(float dist)
        {
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

            if (dist <= attackRange)
                BeginClubWindup();
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

        private void BeginClubWindup()
        {
            state = State.ClubWindup;
            stateTimer = clubWindupTime;
            StopAgent();
            SetTint(clubTint);
        }

        private void TickClubWindup(Vector3 toTarget)
        {
            RotateTowards(toTarget, clubTurnSpeed);

            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                SwingClub();
                state = State.ClubRecover;
                stateTimer = clubRecoverTime;
                SetTint(BaseColor);
            }
        }

        private void TickClubRecover()
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                ResumeAgent();
                state = State.Chase;
            }
        }

        private void SwingClub()
        {
            Vector3 center = transform.position
                             + transform.forward * hitForwardOffset
                             + Vector3.up * hitHeight;

            Collider[] hits = Physics.OverlapSphere(center, hitRadius, hitMask, QueryTriggerInteraction.Ignore);
            foreach (Collider col in hits)
            {
                if (col.transform.IsChildOf(transform)) continue;
                if (col.GetComponentInParent<BaseEnemy>() != null) continue;

                IDamageable dmg = col.GetComponentInParent<IDamageable>();
                if (dmg == null || dmg.IsDead) continue;

                dmg.TakeDamage(clubDamage, center, transform.forward);
                break;
            }
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
            heldBlock = null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            Gizmos.color = Color.red;
            Vector3 center = transform.position + transform.forward * hitForwardOffset + Vector3.up * hitHeight;
            Gizmos.DrawWireSphere(center, hitRadius);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, minThrowDistance);
            Gizmos.DrawWireSphere(transform.position, maxThrowDistance);
        }
    }
}
