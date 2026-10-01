using UnityEngine;
using Project.Combat;

namespace Project.Enemies
{
    public class DraugrEnemy : BaseEnemy
    {
        private enum State { Chase, Windup, Recover }

        [Header("Chase")]
        [SerializeField] private float repathInterval = 0.1f;

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

        private State state = State.Chase;
        private float stateTimer;
        private float repathTimer;

        protected override Color RestingTint => state == State.Windup ? windupTint : BaseColor;

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
                case State.Windup: TickWindup(toTarget); break;
                case State.Recover: TickRecover(); break;
            }
        }

        private void TickChase(float dist)
        {
            repathTimer -= Time.deltaTime;
            if (repathTimer <= 0f)
            {
                Agent.SetDestination(Target.position);
                repathTimer = repathInterval;
            }

            if (dist <= attackRange)
                BeginWindup();
        }

        private void BeginWindup()
        {
            state = State.Windup;
            stateTimer = windupTime;

            Agent.isStopped = true;
            Agent.velocity = Vector3.zero;
            Agent.updateRotation = false;

            SetTint(windupTint);
        }

        private void TickWindup(Vector3 toTarget)
        {
            RotateTowards(toTarget, turnSpeed); 

            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                Swing();
                state = State.Recover;
                stateTimer = recoverTime;
                SetTint(BaseColor);
            }
        }

        private void TickRecover()
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                Agent.isStopped = false;
                Agent.updateRotation = true;
                repathTimer = 0f;
                state = State.Chase;
            }
        }

        // combat

        private void Swing()
        {
            Vector3 center = transform.position
                             + transform.forward * hitForwardOffset
                             + Vector3.up * hitHeight;

            Collider[] hits = Physics.OverlapSphere(center, hitRadius, hitMask, QueryTriggerInteraction.Ignore);
            foreach (Collider col in hits)
            {
                if (col.transform.IsChildOf(transform)) continue;             // ignore self
                if (col.GetComponentInParent<BaseEnemy>() != null) continue;  // no friendly fire

                IDamageable dmg = col.GetComponentInParent<IDamageable>();
                if (dmg == null || dmg.IsDead) continue;

                dmg.TakeDamage(damage, center, transform.forward);
                break; // one hit per swing
            }
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
