using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Project.Combat;
using Project.Player;

[RequireComponent(typeof(Weapon))]
public class GleipnirShotgun : MonoBehaviour, IWeaponPrimaryFire, IWeaponAltFire
{
    [SerializeField] private int wireCount = 7;
    [SerializeField] private float spreadAngle = 30f;
    [SerializeField] private float verticalJitter = 1.2f;
    [SerializeField] private float range = 16f;
    [SerializeField] private float damagePerWire = 7f;
    [SerializeField] private float falloffStart = 6f;
    [SerializeField] private float minDamageScale = 0.35f;
    [SerializeField] private float aimAssistRadius = 0.3f;
    [SerializeField] private float primaryCooldown = 0.8f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private Color wireColor = new Color(1f, 0.8f, 0.42f, 1f);
    [SerializeField] private float ribbonWidth = 0.13f;
    [SerializeField] private float ribbonWiggle = 0.35f;

    [SerializeField] private GleipnirStake.Settings stake = new GleipnirStake.Settings();
    [SerializeField] private GleipnirSnap.Settings snap = new GleipnirSnap.Settings();
    [SerializeField] private float stakeCooldown = 0.7f;
    [SerializeField] private float tetherWidth = 0.06f;

    [SerializeField] private float fireShake = 2.2f;
    [SerializeField] private float stakeShake = 1.8f;
    [SerializeField] private float fireFov = 97f;
    [SerializeField] private float recoilKick = 0.15f;
    [SerializeField] private float recoilPitch = 14f;
    [SerializeField] private float stakeRecoilKick = 0.2f;
    [SerializeField] private float stakeRecoilPitch = 18f;
    [SerializeField] private float recoilReturn = 13f;
    [SerializeField] private float swayAmount = 0.012f;
    [SerializeField] private float bobAmount = 0.016f;
    [SerializeField] private float equipTime = 0.3f;
    [SerializeField] private bool buildModel = true;

    [SerializeField] private AudioClip fireClip;
    [SerializeField] private AudioClip stakeFireClip;
    [SerializeField] private AudioClip equipClip;

    private struct WireHit
    {
        public float damage;
        public Vector3 point;
        public Vector3 dir;
        public int count;
    }

    private Weapon weapon;
    private PlayerMovement player;

    private Transform model;
    private Transform muzzle;
    private Transform spool;
    private Transform loadedStake;
    private readonly List<Material> ownedMaterials = new List<Material>();

    private GleipnirWire tether;
    private GleipnirStake flyingStake;
    private float stakeTimer;
    private float reloadAnim = 1f;

    private Vector3 recoilPos;
    private Vector3 recoilPosVel;
    private Vector3 recoilRot;
    private Vector3 recoilRotVel;
    private Vector2 sway;
    private float bobPhase;
    private float equipT;
    private float spoolSpeed;
    private float spoolAngle;

    public float PrimaryCooldown => primaryCooldown;

    private void Awake()
    {
        weapon = GetComponent<Weapon>();
        if (buildModel) BuildModel();
        if (muzzle != null) weapon.SetMuzzle(muzzle);
        tether = GleipnirWire.Create("GleipnirTether", wireColor, tetherWidth);
        tether.transform.SetParent(transform, false);
    }

    private void OnEnable()
    {
        equipT = 0f;
        ShardFX.Play(equipClip, transform.position, 0.8f);
    }

    private void BuildModel()
    {
        model = new GameObject("Model").transform;
        model.SetParent(transform, false);

        Material iron = Own(ShardFX.CreateLitMaterial(new Color(0.11f, 0.11f, 0.13f), 0.9f, 0.62f));
        Material wood = Own(ShardFX.CreateLitMaterial(new Color(0.24f, 0.13f, 0.07f), 0f, 0.35f));
        Material gold = Own(ShardFX.CreateSolidMaterial(Color.Lerp(wireColor, Color.white, 0.25f)));
        Material steel = Own(ShardFX.CreateLitMaterial(new Color(0.35f, 0.35f, 0.38f), 1f, 0.75f));

        Part(model, PrimitiveType.Cube, new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(0.11f, 0.12f, 0.26f), iron);
        Part(model, PrimitiveType.Cylinder, new Vector3(-0.028f, 0.025f, 0.3f), new Vector3(90f, 0f, 0f), new Vector3(0.05f, 0.2f, 0.05f), iron);
        Part(model, PrimitiveType.Cylinder, new Vector3(0.028f, 0.025f, 0.3f), new Vector3(90f, 0f, 0f), new Vector3(0.05f, 0.2f, 0.05f), iron);
        Part(model, PrimitiveType.Cylinder, new Vector3(0f, 0.025f, 0.47f), new Vector3(90f, 0f, 0f), new Vector3(0.122f, 0.012f, 0.066f), gold);
        Part(model, PrimitiveType.Cylinder, new Vector3(0f, 0.025f, 0.21f), new Vector3(90f, 0f, 0f), new Vector3(0.12f, 0.01f, 0.064f), gold);
        Part(model, PrimitiveType.Cube, new Vector3(-0.056f, 0f, 0f), Vector3.zero, new Vector3(0.004f, 0.02f, 0.2f), gold);
        Part(model, PrimitiveType.Cube, new Vector3(0.056f, 0f, 0f), Vector3.zero, new Vector3(0.004f, 0.02f, 0.2f), gold);
        Part(model, PrimitiveType.Cube, new Vector3(0f, -0.11f, -0.06f), new Vector3(18f, 0f, 0f), new Vector3(0.065f, 0.16f, 0.08f), wood);
        Part(model, PrimitiveType.Cube, new Vector3(0f, -0.02f, -0.2f), new Vector3(-5f, 0f, 0f), new Vector3(0.08f, 0.1f, 0.16f), wood);

        spool = new GameObject("Spool").transform;
        spool.SetParent(model, false);
        spool.localPosition = new Vector3(0f, 0.095f, 0.02f);
        Part(spool, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.09f, 0.032f, 0.09f), gold);
        Part(spool, PrimitiveType.Cylinder, new Vector3(-0.035f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.12f, 0.005f, 0.12f), steel);
        Part(spool, PrimitiveType.Cylinder, new Vector3(0.035f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.12f, 0.005f, 0.12f), steel);
        Part(spool, PrimitiveType.Cube, new Vector3(0f, 0.05f, 0f), Vector3.zero, new Vector3(0.06f, 0.012f, 0.012f), steel);

        loadedStake = new GameObject("LoadedStake").transform;
        loadedStake.SetParent(model, false);
        loadedStake.localPosition = new Vector3(0f, -0.045f, 0.27f);
        Part(loadedStake, PrimitiveType.Cylinder, Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(0.03f, 0.16f, 0.03f), steel);
        Part(loadedStake, PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.1f), new Vector3(90f, 0f, 0f), new Vector3(0.045f, 0.01f, 0.045f), gold);
        GameObject tip = new GameObject("Tip");
        tip.transform.SetParent(loadedStake, false);
        tip.transform.localPosition = new Vector3(0f, 0f, 0.2f);
        tip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        tip.transform.localScale = new Vector3(0.2f, 0.6f, 0.13f);
        tip.AddComponent<MeshFilter>().sharedMesh = ShardFX.BladeMesh;
        MeshRenderer tr = tip.AddComponent<MeshRenderer>();
        tr.sharedMaterial = steel;
        tr.shadowCastingMode = ShadowCastingMode.Off;
        tip.layer = gameObject.layer;

        muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(model, false);
        muzzle.localPosition = new Vector3(0f, 0.025f, 0.53f);
    }

    private Material Own(Material m)
    {
        ownedMaterials.Add(m);
        return m;
    }

    private void Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
    {
        GameObject p = GameObject.CreatePrimitive(type);
        Destroy(p.GetComponent<Collider>());
        p.layer = gameObject.layer;
        p.transform.SetParent(parent, false);
        p.transform.localPosition = pos;
        p.transform.localRotation = Quaternion.Euler(euler);
        p.transform.localScale = scale;
        Renderer r = p.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    private Camera GetCamera()
    {
        return weapon.Manager != null && weapon.Manager.AimCamera != null ? weapon.Manager.AimCamera : Camera.main;
    }

    private void ResolvePlayer()
    {
        if (player != null) return;
        if (weapon.Manager != null && weapon.Manager.PlayerRoot != null) player = weapon.Manager.PlayerRoot.GetComponent<PlayerMovement>();
        if (player == null) player = GetComponentInParent<PlayerMovement>();
        if (player == null) player = FindFirstObjectByType<PlayerMovement>();
    }

    private bool IsIgnored(Collider col)
    {
        if (player != null && col.transform.IsChildOf(player.transform)) return true;
        if (col.transform.IsChildOf(transform)) return true;
        return false;
    }

    private Vector3 MuzzlePos(Camera cam)
    {
        return muzzle != null ? muzzle.position : weapon.GetMuzzlePosition(cam);
    }

    public void FirePrimary()
    {
        Camera cam = GetCamera();
        if (cam == null) return;
        ResolvePlayer();

        Transform ct = cam.transform;
        Vector3 origin = ct.position;
        Vector3 muzzlePos = MuzzlePos(cam);
        Dictionary<IDamageable, WireHit> victims = new Dictionary<IDamageable, WireHit>();
        int n = Mathf.Max(1, wireCount);

        for (int i = 0; i < n; i++)
        {
            float u = n == 1 ? 0f : i / (float)(n - 1) * 2f - 1f;
            float yaw = u * spreadAngle * 0.5f + Random.Range(-1f, 1f) * spreadAngle * 0.03f;
            float pitch = Random.Range(-verticalJitter, verticalJitter);
            Vector3 dir = Quaternion.AngleAxis(yaw, ct.up) * Quaternion.AngleAxis(pitch, ct.right) * ct.forward;

            Trace(origin, dir, out Vector3 end, out Vector3 normal, out IDamageable victim, out float dist, out bool hitSomething);

            float extend = 0.035f + dist / range * 0.04f;
            GleipnirRibbon.Shoot(muzzlePos, end, wireColor, ribbonWidth, extend, 0.035f, 0.17f, ribbonWiggle);

            if (victim != null)
            {
                float k = dist <= falloffStart ? 1f : Mathf.Lerp(1f, minDamageScale, (dist - falloffStart) / Mathf.Max(0.01f, range - falloffStart));
                victims.TryGetValue(victim, out WireHit h);
                h.damage += damagePerWire * k;
                h.point = h.count == 0 ? end : Vector3.Lerp(h.point, end, 1f / (h.count + 1));
                h.dir = dir;
                h.count++;
                victims[victim] = h;

                ShardFX.Burst(end, -dir, wireColor, 6, 9f, 0.11f, 0.2f, 35f, 0.4f, true);
                ShardFX.Burst(end, dir, new Color(0.75f, 0.1f, 0.08f, 1f), 6, 7f, 0.13f, 0.35f, 30f, 1.5f, true);
            }
            else if (hitSomething)
            {
                ShardFX.Burst(end, normal, wireColor, 5, 6f, 0.08f, 0.18f, 50f, 1f, true);
                ShardFX.Burst(end, normal, new Color(0.5f, 0.47f, 0.45f, 0.6f), 2, 1.5f, 0.35f, 0.4f, 40f, -0.05f, false);
            }
        }

        int kills = 0;
        float total = 0f;
        foreach (KeyValuePair<IDamageable, WireHit> kv in victims)
        {
            if (kv.Key.IsDead) continue;
            kv.Key.TakeDamage(kv.Value.damage, kv.Value.point, kv.Value.dir);
            total += kv.Value.damage;
            if (kv.Key.IsDead)
            {
                kills++;
                ShardFX.Burst(kv.Value.point, Vector3.up, new Color(1f, 0.35f, 0.25f, 1f), 28, 12f, 0.24f, 0.55f, 160f, 1.5f, true);
                ShardFX.Ring(kv.Value.point, wireColor, 0.5f, 5f, 0.35f, true);
            }
            else if (kv.Value.count >= 4)
            {
                ShardFX.Ring(kv.Value.point, wireColor, 0.3f, 3f, 0.25f, true);
            }
        }

        Vector3 fwd = ct.forward;
        ShardFX.Burst(muzzlePos, fwd, wireColor, 26, 14f, 0.12f, 0.16f, spreadAngle * 0.6f, 0f, true);
        ShardFX.Burst(muzzlePos, fwd, Color.white, 6, 3f, 0.5f, 0.07f, 25f, 0f, false);
        ShardFX.Burst(muzzlePos, fwd + Vector3.up * 0.3f, new Color(0.6f, 0.57f, 0.55f, 0.35f), 5, 1.2f, 0.4f, 0.7f, 30f, -0.08f, false);
        ShardFX.Flash(muzzlePos, wireColor, 7f, 7f, 0.09f);
        ShardFX.Play(fireClip, muzzlePos, 1f);

        float hitBonus = victims.Count > 0 ? 0.8f : 0f;
        ShardFX.CameraShake(fireShake + hitBonus + kills * 1.2f, 0.18f);
        ShardFX.CameraPunch(fireFov, 0.04f);

        if (kills > 0) HitStop.Do(0.05f + 0.02f * Mathf.Min(kills, 3));
        else if (total >= 30f) HitStop.Do(0.03f);

        Kick(recoilKick, recoilPitch);
        spoolSpeed += 1400f;
    }

    private void Trace(Vector3 origin, Vector3 dir, out Vector3 end, out Vector3 normal, out IDamageable victim, out float dist, out bool hitSomething)
    {
        end = origin + dir * range;
        normal = -dir;
        victim = null;
        dist = range;
        hitSomething = false;

        RaycastHit[] hits = Physics.RaycastAll(origin, dir, range, hitMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
        {
            if (IsIgnored(h.collider)) continue;
            end = h.point;
            normal = h.normal;
            dist = h.distance;
            hitSomething = true;
            IDamageable d = h.collider.GetComponentInParent<IDamageable>();
            if (d != null && !d.IsDead) victim = d;
            break;
        }

        if (victim != null || aimAssistRadius <= 0f) return;

        RaycastHit[] near = Physics.SphereCastAll(origin, aimAssistRadius, dir, dist, hitMask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        foreach (RaycastHit h in near)
        {
            if (IsIgnored(h.collider)) continue;
            IDamageable d = h.collider.GetComponentInParent<IDamageable>();
            if (d == null || d.IsDead || h.distance >= best) continue;
            best = h.distance;
            victim = d;
            Bounds b = h.collider.bounds;
            float along = Mathf.Clamp(Vector3.Dot(b.center - origin, dir), 0f, dist);
            end = b.ClosestPoint(origin + dir * along);
            dist = Vector3.Distance(origin, end);
            normal = -dir;
            hitSomething = true;
        }
    }

    public void TryAltFire()
    {
        if (stakeTimer > 0f || flyingStake != null) return;

        Camera cam = GetCamera();
        if (cam == null) return;
        ResolvePlayer();

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 aimPoint = ray.origin + ray.direction * stake.range;
        RaycastHit[] hits = Physics.RaycastAll(ray, stake.range, hitMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
        {
            if (IsIgnored(h.collider)) continue;
            aimPoint = h.point;
            break;
        }

        Vector3 muzzlePos = MuzzlePos(cam);
        float travel = Vector3.Distance(muzzlePos, aimPoint);
        float time = travel / Mathf.Max(1f, stake.speed);
        aimPoint += Vector3.up * 0.5f * stake.gravity * time * time;

        Vector3 dir = aimPoint - muzzlePos;
        if (dir.sqrMagnitude < 0.0001f || Vector3.Dot(dir, ray.direction) <= 0f) dir = ray.direction;
        dir.Normalize();

        GleipnirStake s = GleipnirStake.Spawn(muzzlePos, dir, stake, hitMask, player != null ? player.transform : transform);
        GleipnirSnap.Settings snapSettings = snap;
        s.Landed += landed => ResolveLanding(landed, snapSettings);
        s.Landed += OnOwnStakeDone;
        s.Missed += OnOwnStakeDone;
        flyingStake = s;

        stakeTimer = stakeCooldown;
        reloadAnim = 0f;

        ShardFX.Burst(muzzlePos, dir, wireColor, 18, 10f, 0.13f, 0.2f, 15f, 0f, true);
        ShardFX.Burst(muzzlePos, dir, new Color(0.6f, 0.57f, 0.55f, 0.45f), 8, 2f, 0.5f, 0.6f, 25f, -0.08f, false);
        ShardFX.Flash(muzzlePos, wireColor, 6f, 6f, 0.1f);
        ShardFX.Play(stakeFireClip, muzzlePos, 1f);
        ShardFX.CameraShake(stakeShake, 0.2f);
        ShardFX.CameraPunch(fireFov - 2f, 0.05f);

        Kick(stakeRecoilKick, stakeRecoilPitch);
        spoolSpeed += 900f;
    }

    private void OnOwnStakeDone(GleipnirStake s)
    {
        if (this == null) return;
        if (flyingStake == s) flyingStake = null;
    }

    private static void ResolveLanding(GleipnirStake landed, GleipnirSnap.Settings snapSettings)
    {
        GleipnirStake armed = GleipnirStake.Armed;

        if (armed != null && armed != landed && armed.IsValid)
        {
            armed.SetArmedVisual(false);
            GleipnirStake.Armed = null;
            GleipnirSnap.Begin(armed, landed, snapSettings);
            return;
        }

        GleipnirStake.Armed = landed;
        landed.SetArmedVisual(true);
        ShardFX.Ring(landed.WireAttach, landed.GlowColor, 0.4f, 2.6f, 0.3f, true);
    }

    private void Kick(float back, float pitch)
    {
        recoilPos += new Vector3(Random.Range(-0.01f, 0.01f), back * 0.25f, -back);
        recoilRot += new Vector3(-pitch, Random.Range(-2.5f, 2.5f), Random.Range(-5f, 5f));
    }

    private void Update()
    {
        if (stakeTimer > 0f) stakeTimer -= Time.deltaTime;
        if (stakeTimer <= 0f && reloadAnim < 1f)
        {
            reloadAnim = Mathf.Min(1f, reloadAnim + Time.deltaTime / 0.14f);
            if (reloadAnim >= 1f)
            {
                recoilPos += new Vector3(0f, -0.012f, 0.02f);
                recoilRot += new Vector3(3f, 0f, 0f);
                if (loadedStake != null) ShardFX.Burst(loadedStake.position, Vector3.up, wireColor, 5, 1.5f, 0.05f, 0.15f, 60f, 0f, false);
            }
        }
    }

    private void LateUpdate()
    {
        AnimateModel();
        DrawTether();
    }

    private void AnimateModel()
    {
        if (model == null) return;
        float dt = Time.deltaTime;

        float stiff = recoilReturn * recoilReturn;
        float damp = 2f * 0.5f * recoilReturn;
        recoilPosVel += (-stiff * recoilPos - damp * recoilPosVel) * dt;
        recoilPos += recoilPosVel * dt;
        recoilRotVel += (-stiff * recoilRot - damp * recoilRotVel) * dt;
        recoilRot += recoilRotVel * dt;

        Vector2 look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        sway = Vector2.Lerp(sway, -look * swayAmount, 1f - Mathf.Exp(-10f * dt));
        sway = Vector2.ClampMagnitude(sway, 0.05f);

        Vector3 bob = Vector3.zero;
        ResolvePlayer();
        if (player != null && player.IsGrounded && !player.IsSliding)
        {
            float speed = player.HorizontalVelocity.magnitude;
            bobPhase += speed * dt * 0.9f;
            float amt = Mathf.Clamp01(speed / 9f) * bobAmount;
            bob = new Vector3(Mathf.Cos(bobPhase) * amt, -Mathf.Abs(Mathf.Sin(bobPhase)) * amt, 0f);
        }

        equipT = Mathf.Min(1f, equipT + dt / Mathf.Max(0.01f, equipTime));
        float e = 1f - equipT;
        float back = 1f + 2.70158f * Mathf.Pow(equipT - 1f, 3f) + 1.70158f * Mathf.Pow(equipT - 1f, 2f);
        Vector3 equipPos = new Vector3(0f, -0.28f, -0.05f) * (1f - back);
        Vector3 equipRot = new Vector3(40f, -10f, 15f) * e * e;

        model.localPosition = equipPos + recoilPos + new Vector3(sway.x, sway.y, 0f) + bob;
        model.localRotation = Quaternion.Euler(equipRot + recoilRot + new Vector3(sway.y * 120f, -sway.x * 120f, -sway.x * 200f));

        spoolSpeed *= Mathf.Exp(-3f * dt);
        spoolAngle += spoolSpeed * dt;
        if (spool != null) spool.localRotation = Quaternion.Euler(spoolAngle, 0f, 0f);

        if (loadedStake != null)
        {
            float r = reloadAnim;
            float ease = 1f - Mathf.Pow(1f - r, 3f);
            loadedStake.localPosition = new Vector3(0f, -0.045f, Mathf.Lerp(0.02f, 0.27f, ease));
            loadedStake.localScale = Vector3.one * (r <= 0f ? 0f : 1f);
        }
    }

    private void DrawTether()
    {
        if (tether == null) return;
        Camera cam = GetCamera();
        if (cam == null || muzzle == null)
        {
            tether.Hide();
            return;
        }

        GleipnirStake armed = GleipnirStake.Armed;
        bool armedValid = armed != null && armed.IsValid;
        bool flying = flyingStake != null && !flyingStake.IsFinished;
        Vector3 m = muzzle.position;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);

        if (flying && armedValid)
        {
            tether.Draw(armed.WireAttach, flyingStake.WireAttach, 0.15f, 0.95f, 1.4f, 0.5f);
            spoolSpeed = Mathf.Max(spoolSpeed, 1200f);
        }
        else if (armedValid)
        {
            float d = Vector3.Distance(m, armed.WireAttach);
            tether.Draw(m, armed.WireAttach, Mathf.Clamp(d * 0.06f, 0.1f, 2.2f), 0.35f + 0.2f * pulse, 0.8f, 0.1f * pulse);
        }
        else if (flying)
        {
            tether.Draw(m, flyingStake.WireAttach, 0.05f, 0.75f, 1.1f, 0.3f);
            spoolSpeed = Mathf.Max(spoolSpeed, 1200f);
        }
        else
        {
            tether.Hide();
        }
    }

    private void OnDestroy()
    {
        foreach (Material m in ownedMaterials)
        {
            if (m != null) Destroy(m);
        }
    }
}
