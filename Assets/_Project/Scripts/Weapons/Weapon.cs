using UnityEngine;
using Project.Combat;

public class Weapon : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator Animator;
    [Tooltip("Optional. Where the tracer visually starts (the barrel tip). If empty, a spot in front of the camera is used.")]
    [SerializeField] private Transform muzzlePoint;
    private WeaponManager WeaponManager;

    public WeaponInstanceData WeaponInstance;

    [SerializeField] private WeaponIds Id;
    [SerializeField] private string Name;
    [SerializeField] private int CurrentAmmo;
    [SerializeField] private int MaxAmmo;

    [Header("Firing (Ultrakill style hitscan)")]
    [SerializeField] private float damage = 10f;
    [Tooltip("Seconds between shots.")]
    [SerializeField] private float fireCooldown = 0.25f;
    [Tooltip("If you click slightly before the cooldown ends, the shot still fires the moment it's ready.")]
    [SerializeField] private float inputBufferTime = 0.15f;
    [SerializeField] private float range = 500f;
    [Tooltip("Small forgiveness radius so shots near an enemy still hit it. 0 = perfectly precise ray.")]
    [SerializeField] private float aimAssistRadius = 0.2f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private string fireAnimationState = "Fire";

    [Header("Effects")]
    [SerializeField] private Material tracerMaterial;
    [SerializeField] private Color tracerColor = new Color(1f, 0.85f, 0.4f, 1f);
    [SerializeField] private float tracerWidth = 0.05f;
    [SerializeField] private float tracerDuration = 0.12f;
    [Tooltip("Optional. Spawned where the bullet lands.")]
    [SerializeField] private GameObject impactEffect;

    private float cooldownTimer;
    private float bufferTimer;
    private int fireStateHash;

    private void Awake()
    {
        if (Animator == null) Animator = GetComponentInChildren<Animator>();
        fireStateHash = Animator.StringToHash(fireAnimationState);
    }

    void Start()
    {
        if (WeaponInstance == null)
        {

        }
        else
        {

        }
    }

    void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
        if (bufferTimer > 0f) bufferTimer -= Time.deltaTime;

        if (bufferTimer > 0f && cooldownTimer <= 0f)
        {
            PlayerFires();
        }
    }

    public void PlayerFires()
    {
        cooldownTimer = fireCooldown;
        bufferTimer = 0f;

        PlayFireAnimation();
        FireHitscan();
    }

    public void PlayerTriesToFire()
    {
        if (cooldownTimer <= 0f)
        {
            PlayerFires();
        }
        else
        {
            bufferTimer = inputBufferTime;
        }
    }

    private void PlayFireAnimation()
    {
        if (Animator == null) return;
        if (!Animator.HasState(0, fireStateHash)) return;
        Animator.Play(fireStateHash, 0, 0f);
    }

    private void FireHitscan()
    {
        Camera cam = WeaponManager != null ? WeaponManager.AimCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("Weapon: no camera found to aim with.");
            return;
        }

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        Vector3 endPoint = ray.origin + ray.direction * range;
        Vector3 hitNormal = -ray.direction;
        float wallDistance = range;
        IDamageable victim = null;

        RaycastHit[] hits = Physics.RaycastAll(ray, range, hitMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit h in hits)
        {
            if (IsIgnored(h.collider)) continue;

            endPoint = h.point;
            hitNormal = h.normal;
            wallDistance = h.distance;

            IDamageable d = h.collider.GetComponentInParent<IDamageable>();
            if (d != null && !d.IsDead) victim = d;
            break;
        }

        if (victim == null && aimAssistRadius > 0f)
        {
            RaycastHit[] near = Physics.SphereCastAll(ray.origin, aimAssistRadius, ray.direction, wallDistance, hitMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;

            foreach (RaycastHit h in near)
            {
                if (IsIgnored(h.collider)) continue;

                IDamageable d = h.collider.GetComponentInParent<IDamageable>();
                if (d == null || d.IsDead) continue;
                if (h.distance >= best) continue;

                best = h.distance;
                victim = d;

                Bounds b = h.collider.bounds;
                float along = Mathf.Clamp(Vector3.Dot(b.center - ray.origin, ray.direction), 0f, wallDistance);
                endPoint = b.ClosestPoint(ray.origin + ray.direction * along);
                hitNormal = -ray.direction;
            }
        }

        if (victim != null)
        {
            victim.TakeDamage(damage, endPoint, ray.direction);
        }

        Vector3 muzzle = muzzlePoint != null
            ? muzzlePoint.position
            : cam.transform.position + cam.transform.right * 0.25f - cam.transform.up * 0.2f + cam.transform.forward * 0.6f;

        BulletTracer.Spawn(muzzle, endPoint, tracerMaterial, tracerColor, tracerWidth, tracerDuration);

        if (impactEffect != null)
        {
            GameObject fx = Instantiate(impactEffect, endPoint, Quaternion.LookRotation(hitNormal));
            Destroy(fx, 3f);
        }
    }

    private bool IsIgnored(Collider col)
    {
        Transform playerRoot = WeaponManager != null ? WeaponManager.PlayerRoot : null;
        if (playerRoot != null && col.transform.IsChildOf(playerRoot)) return true;
        if (col.transform.IsChildOf(transform)) return true;
        return false;
    }

    public void SetWeaponManagerReference(WeaponManager weaponManager)
    {
        WeaponManager = weaponManager;
    }
    public WeaponInstanceData ConvertToWeaponInstanceData()
    {
        WeaponInstanceData w = new WeaponInstanceData(Id, CurrentAmmo, Name);

        return w;
    }
    public bool DoesGunAllowInputs()
    {
        return cooldownTimer <= 0f;
    }

    public void StartAllowingPlayerInput()
    {
        cooldownTimer = 0f;
    }
}
