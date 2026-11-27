using Project.Audio;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Project.Combat;
using Project.Player;
using Project.Tutorial;

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
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private float hitFlashIntensity = 2f;
        [SerializeField] private float deathFallTime = 0.35f;
        [SerializeField] private float corpseLifetime = 4f;

        private Material bodyMat;
        private float flashTimer;
        private Renderer[] flashRenderers = new Renderer[0];
        private Material[][] restingMaterials;
        private Material[][] flashMaterials;

        protected NavMeshAgent Agent { get; private set; }
        protected Health EnemyHealth { get; private set; }
        protected Transform Target => target;
        protected bool IsAlerted { get; private set; }
        protected bool IsDead => EnemyHealth.IsDead;
        protected Color BaseColor { get; private set; } = Color.white;

        protected virtual Color RestingTint => BaseColor;

        public bool IsExternallyMoved { get; private set; }
        public bool IsDormant { get; private set; }
        public bool IsEnemyDead => EnemyHealth != null && EnemyHealth.IsDead;

        public void SetDormant(bool dormant)
        {
            IsDormant = dormant;
            if (dormant) return;

            if (IsAlerted) OnAlerted();
            else Alert();
        }

        public void BeginExternalMove()
        {
            IsExternallyMoved = true;
            if (Agent != null && Agent.enabled) Agent.enabled = false;
        }

        public void EndExternalMove()
        {
            IsExternallyMoved = false;
            if (IsDead || Agent == null) return;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                Agent.enabled = true;
                Agent.Warp(hit.position);
            }
            else
            {
                Agent.enabled = true;
            }
        }


        protected virtual void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            EnemyHealth = GetComponent<Health>();

            Agent.speed = moveSpeed;
            Agent.acceleration = acceleration;
            Agent.angularSpeed = turnSpeed;
            Agent.stoppingDistance = 0f;

            if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>();

            if (bodyRenderer != null)
            {
                bodyMat = bodyRenderer.material; 
                BaseColor = bodyMat.color;
            }

            BuildFlashMaterials();
        }

        protected virtual void OnDestroy()
        {
            if (flashMaterials == null) return;

            foreach (Material[] mats in flashMaterials)
                foreach (Material mat in mats)
                    Destroy(mat);
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

            if (IsDead || target == null || IsExternallyMoved || IsDormant) return;

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

        protected virtual void LateUpdate()
        {
            if (IsDead || IsExternallyMoved) return;

            Vector3 euler = transform.eulerAngles;
            if (Mathf.Abs(Mathf.DeltaAngle(euler.x, 0f)) > 0.01f || Mathf.Abs(Mathf.DeltaAngle(euler.z, 0f)) > 0.01f)
                transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
        }

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
            IsDormant = false;
            Alert();
            GameAudio.Play(SfxId.EnemyHit, hitPoint);

            if (flashRenderers.Length > 0)
            {
                SetFlash(true);
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
            GameAudio.Play(SfxId.EnemyDeath, transform.position);
            ActionFeed.Raise(PlayerAction.Kill, this);
            StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            Quaternion start = transform.rotation;
            Quaternion end = start * Quaternion.Euler(-90f, 0f, 0f);

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
            if (flashTimer > 0f) return;

            SetFlash(false);
            if (!IsDead) SetTint(RestingTint);
        }

        private void BuildFlashMaterials()
        {
            if (bodyRenderer != null)
            {
                flashRenderers = new[] { bodyRenderer };
            }
            else
            {
                flashRenderers = System.Array.FindAll(
                    GetComponentsInChildren<Renderer>(),
                    r => r is MeshRenderer || r is SkinnedMeshRenderer);
            }

            restingMaterials = new Material[flashRenderers.Length][];
            flashMaterials = new Material[flashRenderers.Length][];

            for (int i = 0; i < flashRenderers.Length; i++)
            {
                restingMaterials[i] = flashRenderers[i].sharedMaterials;
                flashMaterials[i] = new Material[restingMaterials[i].Length];

                for (int m = 0; m < restingMaterials[i].Length; m++)
                    flashMaterials[i][m] = CreateFlashMaterial(restingMaterials[i][m]);
            }
        }

        private Material CreateFlashMaterial(Material source)
        {
            if (source == null) return null;

            Material flash = new Material(source) { name = source.name + " (Flash)" };

            if (flash.HasProperty("_BaseColor")) flash.SetColor("_BaseColor", hitFlashColor);
            if (flash.HasProperty("_Color")) flash.SetColor("_Color", hitFlashColor);
            if (flash.HasProperty("_BaseMap")) flash.SetTexture("_BaseMap", null);
            if (flash.HasProperty("_MainTex")) flash.SetTexture("_MainTex", null);

            if (flash.HasProperty("_EmissionColor"))
            {
                flash.EnableKeyword("_EMISSION");
                flash.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                flash.SetColor("_EmissionColor", hitFlashColor * hitFlashIntensity);
            }

            return flash;
        }

        private void SetFlash(bool on)
        {
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                if (flashRenderers[i] == null) continue;
                flashRenderers[i].sharedMaterials = on ? flashMaterials[i] : restingMaterials[i];
            }
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
