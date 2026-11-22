using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Project.Combat;
using Project.Player;

[RequireComponent(typeof(Weapon))]
public class ShardAltFire : MonoBehaviour, IWeaponAltFire
{
    [SerializeField] private SwordShard.Settings shard = new SwordShard.Settings();
    [SerializeField] private float shardCooldown = 0.5f;
    [SerializeField] private LayerMask hitMask = ~0;

    [SerializeField] private float zipSpeed = 80f;
    [SerializeField] private float zipMaxDuration = 0.7f;
    [SerializeField] private float arriveDistance = 1.8f;
    [SerializeField] private float stallForgiveDistance = 3.5f;
    [SerializeField] private float meleeDamage = 30f;
    [SerializeField] private float hitstopDuration = 0.08f;
    [SerializeField] private float killHitstopDuration = 0.15f;
    [SerializeField] private float hitstopScale = 0.03f;
    [SerializeField] private float bounceUp = 10f;
    [SerializeField] private float bounceBack = 5f;
    [SerializeField] private float cancelCarrySpeed = 18f;
    [SerializeField] private float afterZipCooldown = 0.3f;
    [SerializeField] private string meleeAnimationState = "Melee";
    [SerializeField] private GameObject meleeImpactEffect;

    [SerializeField] private float fireShake = 0.8f;
    [SerializeField] private float embedShake = 1.2f;
    [SerializeField] private float zipShake = 0.6f;
    [SerializeField] private float meleeShake = 3f;
    [SerializeField] private float killShake = 4.5f;
    [SerializeField] private float zipFov = 120f;
    [SerializeField] private float meleeFov = 112f;
    [SerializeField] private float tetherWidth = 0.05f;

    [SerializeField] private AudioClip fireClip;
    [SerializeField] private AudioClip zipClip;
    [SerializeField] private AudioClip meleeClip;
    [SerializeField] private AudioClip killClip;

    private Weapon weapon;
    private PlayerMovement player;
    private PlayerCamera playerCamera;
    private SwordShard activeShard;
    private LineRenderer tether;
    private float cooldownTimer;
    private bool zipping;
    private bool hitstopping;
    private float timeScaleBeforeHitstop = 1f;

    public bool HasEmbeddedShard => activeShard != null && activeShard.IsEmbeddedInLivingEnemy;
    public bool IsZipping => zipping;

    private void Awake()
    {
        weapon = GetComponent<Weapon>();
        BuildTether();
    }

    private void BuildTether()
    {
        GameObject go = new GameObject("ShardTether");
        go.transform.SetParent(transform, false);
        tether = go.AddComponent<LineRenderer>();
        tether.useWorldSpace = true;
        tether.positionCount = 12;
        tether.alignment = LineAlignment.View;
        tether.shadowCastingMode = ShadowCastingMode.Off;
        tether.receiveShadows = false;
        tether.sharedMaterial = ShardFX.SpriteMaterial;
        tether.numCapVertices = 2;
        tether.enabled = false;
    }

    private void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
    }

    private void LateUpdate()
    {
        UpdateTether();
    }

    private void UpdateTether()
    {
        bool show = HasEmbeddedShard;
        tether.enabled = show;
        if (!show) return;

        Camera cam = GetCamera();
        if (cam == null)
        {
            tether.enabled = false;
            return;
        }

        Vector3 a = weapon.GetMuzzlePosition(cam);
        Vector3 b = activeShard.transform.position;
        Vector3 side = Vector3.Cross((b - a).normalized, cam.transform.forward).normalized;
        float wobble = zipping ? 0f : 0.15f;
        int n = tether.positionCount;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1);
            float wave = Mathf.Sin(t * Mathf.PI) * Mathf.Sin(Time.time * 14f + t * 9f) * wobble;
            tether.SetPosition(i, Vector3.Lerp(a, b, t) + side * wave);
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
        Color c = activeShard.ShardColor;
        Color near = c;
        near.a = zipping ? 0.9f : 0.08f;
        Color far = Color.Lerp(c, Color.white, 0.4f);
        far.a = zipping ? 1f : 0.35f + 0.25f * pulse;
        tether.startColor = near;
        tether.endColor = far;
        float w = tetherWidth * (zipping ? 2.5f : 1f);
        tether.startWidth = w * 0.4f;
        tether.endWidth = w;
    }

    private Camera GetCamera()
    {
        return weapon.Manager != null && weapon.Manager.AimCamera != null ? weapon.Manager.AimCamera : Camera.main;
    }

    private bool ResolvePlayer()
    {
        if (player != null) return true;

        WeaponManager wm = weapon.Manager;
        if (wm != null && wm.PlayerRoot != null) player = wm.PlayerRoot.GetComponent<PlayerMovement>();
        if (player == null) player = GetComponentInParent<PlayerMovement>();
        if (player == null) player = FindFirstObjectByType<PlayerMovement>();
        if (player == null) return false;

        playerCamera = player.GetComponentInChildren<PlayerCamera>();
        if (playerCamera == null) playerCamera = FindFirstObjectByType<PlayerCamera>();
        return true;
    }

    private void Shake(float strength, float duration)
    {
        if (playerCamera != null) playerCamera.Shake(strength, duration);
    }

    public void TryAltFire()
    {
        if (zipping) return;
        if (!ResolvePlayer()) return;

        if (HasEmbeddedShard)
        {
            StartCoroutine(ZipRoutine(activeShard));
            return;
        }

        if (activeShard != null && activeShard.State == SwordShard.ShardState.Flying) return;
        if (cooldownTimer > 0f) return;

        FireShard();
    }

    private void FireShard()
    {
        Camera cam = GetCamera();
        if (cam == null) return;

        cooldownTimer = shardCooldown;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 aimPoint = ray.origin + ray.direction * shard.range;

        RaycastHit[] hits = Physics.RaycastAll(ray, shard.range, hitMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (RaycastHit h in hits)
        {
            if (IsIgnored(h.collider)) continue;
            aimPoint = h.point;
            break;
        }

        Vector3 muzzle = weapon.GetMuzzlePosition(cam);
        Vector3 dir = aimPoint - muzzle;
        if (dir.sqrMagnitude < 0.0001f || Vector3.Dot(dir, ray.direction) <= 0f) dir = ray.direction;
        dir.Normalize();

        weapon.PlayAnimationState("Fire");

        ShardFX.Burst(muzzle, dir, shard.color, 14, 9f, 0.12f, 0.18f, 18f, 0f, true);
        ShardFX.Burst(muzzle, dir, Color.white, 4, 2f, 0.45f, 0.08f, 10f, 0f, false);
        ShardFX.Flash(muzzle, shard.color, 5f, 5f, 0.08f);
        ShardFX.Play(fireClip, muzzle, 0.9f);
        Shake(fireShake, 0.12f);

        activeShard = SwordShard.Spawn(muzzle, dir, shard, hitMask, player.transform);
        activeShard.Embedded += OnShardEmbedded;
    }

    private void OnShardEmbedded(SwordShard s)
    {
        Shake(embedShake, 0.15f);
    }

    private IEnumerator ZipRoutine(SwordShard target)
    {
        zipping = true;
        player.LockMovement();

        CharacterController controller = player.Controller;
        Transform root = player.transform;
        Vector3 lastDir = target.transform.position - (root.position + controller.center);
        lastDir = lastDir.sqrMagnitude > 0.0001f ? lastDir.normalized : root.forward;

        if (playerCamera != null)
        {
            playerCamera.OnDash();
            playerCamera.PunchFov(zipFov, zipMaxDuration);
        }
        Shake(zipShake, 0.25f);
        ShardFX.Play(zipClip, root.position, 1f);
        ShardFX.Burst(root.position + controller.center, -lastDir, target.ShardColor, 20, 12f, 0.15f, 0.3f, 25f, 0f, true);

        float elapsed = 0f;
        float stalled = 0f;
        bool arrived = false;

        while (elapsed < zipMaxDuration)
        {
            if (target == null || !target.IsEmbeddedInLivingEnemy) break;

            Vector3 center = root.position + controller.center;
            Vector3 to = target.transform.position - center;
            float dist = to.magnitude;
            if (dist > 0.0001f) lastDir = to / dist;

            if (dist <= arriveDistance)
            {
                arrived = true;
                break;
            }

            float step = Mathf.Min(zipSpeed * Time.deltaTime, dist - arriveDistance * 0.5f);
            Vector3 before = root.position;
            controller.Move(lastDir * step);
            float moved = (root.position - before).magnitude;

            if (moved < step * 0.2f)
            {
                stalled += Time.deltaTime;
                if (stalled >= 0.06f)
                {
                    arrived = dist <= stallForgiveDistance;
                    break;
                }
            }
            else
            {
                stalled = 0f;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!arrived && elapsed >= zipMaxDuration && target != null && target.IsEmbeddedInLivingEnemy)
        {
            Vector3 center = root.position + controller.center;
            arrived = (target.transform.position - center).magnitude <= stallForgiveDistance;
        }

        if (arrived && target != null && target.IsEmbeddedInLivingEnemy)
        {
            bool killed = Melee(target, lastDir);
            Vector3 flatBack = new Vector3(-lastDir.x, 0f, -lastDir.z);
            flatBack = flatBack.sqrMagnitude > 0.0001f ? flatBack.normalized : -root.forward;
            player.UnlockMovement(flatBack * bounceBack + Vector3.up * bounceUp, true);
            StartCoroutine(HitstopRoutine(killed ? killHitstopDuration : hitstopDuration));
        }
        else
        {
            if (target != null && target.State == SwordShard.ShardState.Embedded) target.Shatter(false);
            player.UnlockMovement(lastDir * cancelCarrySpeed, false);
        }

        if (activeShard == target) activeShard = null;
        cooldownTimer = afterZipCooldown;
        zipping = false;
    }

    private bool Melee(SwordShard target, Vector3 dir)
    {
        Vector3 point = target.transform.position;
        Color c = target.ShardColor;
        IDamageable victim = target.Victim;
        target.Shatter(true);

        weapon.PlayAnimationState(meleeAnimationState);
        victim.TakeDamage(meleeDamage, point, dir);
        bool killed = victim.IsDead;

        ShardFX.Burst(point, -dir, c, 45, 22f, 0.25f, 0.45f, 55f, 0.8f, true);
        ShardFX.Burst(point, dir, Color.white, 25, 18f, 0.18f, 0.3f, 40f, 0.5f, true);
        ShardFX.Burst(point, -dir, Color.white, 6, 2f, 1.2f, 0.12f, 30f, 0f, false);
        ShardFX.Ring(point, c, 0.5f, 5f, 0.35f, true);
        ShardFX.Flash(point, c, 14f, 12f, 0.25f);
        ShardFX.Play(meleeClip, point, 1f);

        if (killed)
        {
            Color hot = new Color(1f, 0.35f, 0.25f, 1f);
            ShardFX.Burst(point, Vector3.up, hot, 40, 14f, 0.3f, 0.6f, 180f, 1.5f, true);
            ShardFX.Ring(point, Color.white, 0.8f, 8f, 0.45f, true);
            ShardFX.Play(killClip, point, 1f);
        }

        Shake(killed ? killShake : meleeShake, killed ? 0.4f : 0.3f);
        if (playerCamera != null) playerCamera.PunchFov(meleeFov, 0.08f);

        if (meleeImpactEffect != null)
        {
            GameObject fx = Instantiate(meleeImpactEffect, point, Quaternion.LookRotation(-dir));
            Destroy(fx, 3f);
        }

        return killed;
    }

    private IEnumerator HitstopRoutine(float duration)
    {
        if (duration <= 0f || hitstopping || Time.timeScale <= 0f) yield break;

        hitstopping = true;
        timeScaleBeforeHitstop = Time.timeScale;
        Time.timeScale = hitstopScale;
        yield return new WaitForSecondsRealtime(duration);
        EndHitstop();
    }

    private void EndHitstop()
    {
        if (!hitstopping) return;
        hitstopping = false;
        if (Mathf.Approximately(Time.timeScale, hitstopScale)) Time.timeScale = timeScaleBeforeHitstop;
    }

    private bool IsIgnored(Collider col)
    {
        if (player != null && col.transform.IsChildOf(player.transform)) return true;
        if (col.transform.IsChildOf(transform)) return true;
        return false;
    }

    private void OnDisable()
    {
        EndHitstop();
        if (tether != null) tether.enabled = false;

        if (zipping)
        {
            zipping = false;
            if (player != null) player.UnlockMovement(Vector3.zero, false);
        }

        if (activeShard != null && activeShard.State != SwordShard.ShardState.Stuck) activeShard.Consume();
        activeShard = null;
    }
}
