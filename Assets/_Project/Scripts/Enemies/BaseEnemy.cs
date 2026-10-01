using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Project.Combat;
using Project.Player;

namespace Project.Enemies
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public abstract class BaseEnemy : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Leave empty to auto-find the PlayerMovement in the scene.")]
        [SerializeField] private Transform target;
        [Tooltip("Optional. Used for the hit flash and tints.")]
        [SerializeField] private Renderer bodyRenderer;

        [Header("Awareness")]
        [Tooltip("ULTRAKILL enemies are alerted from the start. Turn off to wait until the player is in aggroRange or shoots it.")]
        [SerializeField] private bool startAggro = true;
        [SerializeField] private float aggroRange = 40f;

        [Header("Movement")]
        [SerializeField] protected float moveSpeed = 10f;
        [SerializeField] protected float acceleration = 40f;
        [SerializeField] protected float turnSpeed = 720f;

        [Header("Feedback")]
        [SerializeField] private float hitFlashTime = 0.08f;
        [SerializeField] private float deathFallTime = 0.35f;
        [SerializeField] private float corpseLifetime = 4f;

        private Material bodyMat;
        private float flashTimer;

        protected NavMeshAgent Agent { get; private set; }
        protected Health EnemyHealth { get; private set; }
        protected Transform Target => target;
        protected bool IsAlerted { get; private set; }
        protected bool IsDead => EnemyHealth.IsDead;
        protected Color BaseColor { get; private set; } = Color.white;

        protected virtual Color RestingTint => BaseColor;


        protected virtual void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            EnemyHealth = GetComponent<Health>();

            Agent.speed = moveSpeed;
            Agent.acceleration = acceleration;
            Agent.angularSpeed = turnSpeed;
            Agent.stoppingDistance = 0f;

            if (bodyRenderer != null)
            {
                bodyMat = bodyRenderer.material; 
                BaseColor = bodyMat.color;
            }
        }

        protected virtual void OnEnable()
        {
            EnemyHealth.Damaged += HandleDamaged;
            EnemyHealth.Died += HandleDeath;
        }

        protected virtual void OnDisable()
        {
            EnemyHealth.Damaged -= HandleDamaged;
            EnemyHealth.Died -= HandleDeath;
        }

        protected virtual void Start()
        {
            if (target == null)
            {
                PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
                if (player != null) target = player.transform;
            }

            if (startAggro) Alert();
        }

        protected virtual void Update()
        {
            UpdateFlash();

            if (IsDead || target == null) return;

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;

            if (!IsAlerted)
            {
                if (dist <= aggroRange) Alert();
                return;
            }

            Tick(toTarget, dist);
        }

      // hooks

        protected abstract void Tick(Vector3 toTarget, float distanceToTarget);

        protected virtual void OnAlerted() { }

        protected virtual void OnDamaged(float amount, Vector3 hitPoint, Vector3 hitDirection) { }

        protected virtual void OnDeath() { }


        protected void Alert()
        {
            if (IsAlerted) return;
            IsAlerted = true;
            OnAlerted();
        }

        private void HandleDamaged(float amount, Vector3 hitPoint, Vector3 hitDirection)
        {
            Alert(); // getting shot wakes it up

            if (bodyMat != null)
            {
                bodyMat.color = Color.white;
                flashTimer = hitFlashTime;
            }

            OnDamaged(amount, hitPoint, hitDirection);
        }

        private void HandleDeath()
        {
            Agent.enabled = false;
            foreach (Collider col in GetComponentsInChildren<Collider>())
                col.enabled = false;

            SetTint(BaseColor);
            OnDeath();
            StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            Quaternion start = transform.rotation;
            Quaternion end = start * Quaternion.Euler(-90f, 0f, 0f); // topple backwards

            for (float t = 0f; t < deathFallTime; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.Slerp(start, end, t / deathFallTime);
                yield return null;
            }

            transform.rotation = end;
            Destroy(gameObject, corpseLifetime);
        }

        private void UpdateFlash()
        {
            if (flashTimer <= 0f) return;

            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && !IsDead)
                SetTint(RestingTint);
        }

        protected void SetTint(Color c)
        {
            if (bodyMat != null) bodyMat.color = c;
        }

        protected void RotateTowards(Vector3 flatDir, float degreesPerSecond)
        {
            if (flatDir.sqrMagnitude < 0.0001f) return;
            Quaternion want = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, degreesPerSecond * Time.deltaTime);
        }
    }
}
