using Project.Audio;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using Project.Combat;
using Project.Enemies;

public class GleipnirStake : MonoBehaviour
{
    public enum StakeState { Flying, InEnemy, InWall }

    [Serializable]
    public class Settings
    {
        public float speed = 95f;
        public float gravity = 7f;
        public float range = 140f;
        public float radius = 0.22f;
        public float damage = 12f;
        public float armedLifetime = 15f;
        public float expireWarningTime = 3f;
        public float size = 1f;
        public Color glowColor = new Color(1f, 0.78f, 0.35f, 1f);
        public Color ironColor = new Color(0.16f, 0.15f, 0.17f, 1f);
        public float lightIntensity = 3f;
        public float lightRange = 5f;
        public float markerScreenSize = 0.05f;
        public AudioClip enemyHitClip;
        public AudioClip wallHitClip;
    }

    public static GleipnirStake Armed { get; set; }

    public StakeState State { get; private set; }
    public IDamageable Victim { get; private set; }
    public Transform VictimTransform { get; private set; }
    public BaseEnemy Enemy { get; private set; }
    public Vector3 SurfaceNormal { get; private set; }
    public Vector3 SurfacePoint => anchor != null ? anchor.TransformPoint(anchorLocalPoint) : transform.position;
    public Vector3 WireAttach => transform.position - transform.forward * 0.45f * settings.size;
    public Color GlowColor => settings.glowColor;
    public bool IsFinished => finished;

    public bool IsValid
    {
        get
        {
            if (finished || State == StakeState.Flying || anchor == null) return false;
            if (State == StakeState.InEnemy) return VictimTransform != null && Victim != null && !Victim.IsDead;
            return true;
        }
    }

    public event Action<GleipnirStake> Landed;
    public event Action<GleipnirStake> Missed;

    private Settings settings;
    private Vector3 velocity;
    private LayerMask mask;
    private Transform ignoreRoot;
    private float travelled;
    private float armedTimer;
    private bool finished;
    private bool broken;

    private Transform anchor;
    private Vector3 anchorLocalPos;
    private Vector3 anchorLocalDir;
    private Vector3 anchorLocalPoint;
    private Vector3 anchorLocalNormal;

    private Transform visualRoot;
    private Transform glow;
    private Material glowMaterial;
    private Transform marker;
    private Renderer markerRenderer;
    private Material markerMaterial;
    private Material ironMaterial;
    private Material bandMaterial;
    private Light stakeLight;
    private TrailRenderer trail;
    private float wobble;

    public static GleipnirStake Spawn(Vector3 position, Vector3 dir, Settings settings, LayerMask mask, Transform ignoreRoot)
    {
        GameObject go = new GameObject("GleipnirStake");
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(dir));
        GleipnirStake s = go.AddComponent<GleipnirStake>();
        s.settings = settings;
        s.velocity = dir.normalized * settings.speed;
        s.mask = mask;
        s.ignoreRoot = ignoreRoot;
        s.BuildVisual();
        return s;
    }

    private void BuildVisual()
    {
        float k = settings.size;
        visualRoot = new GameObject("Visual").transform;
        visualRoot.SetParent(transform, false);

        ironMaterial = ShardFX.CreateLitMaterial(settings.ironColor, 0.85f, 0.55f);
        bandMaterial = ShardFX.CreateSolidMaterial(Color.Lerp(settings.glowColor, Color.white, 0.35f));

        AddPart(PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.15f * k), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.09f, 0.42f, 0.09f) * k, ironMaterial);
        AddPart(PrimitiveType.Cube, new Vector3(0f, 0f, -0.6f * k), Quaternion.Euler(0f, 0f, 45f), new Vector3(0.17f, 0.17f, 0.08f) * k, ironMaterial);
        AddPart(PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.42f * k), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.13f, 0.03f, 0.13f) * k, bandMaterial);
        AddPart(PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.05f * k), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.115f, 0.02f, 0.115f) * k, bandMaterial);

        GameObject tip = new GameObject("Tip");
        tip.transform.SetParent(visualRoot, false);
        tip.transform.localPosition = new Vector3(0f, 0f, 0.32f * k);
        tip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        tip.transform.localScale = new Vector3(0.45f, 1.3f, 0.32f) * k;
        tip.AddComponent<MeshFilter>().sharedMesh = ShardFX.BladeMesh;
        MeshRenderer tr = tip.AddComponent<MeshRenderer>();
        tr.sharedMaterial = ironMaterial;
        tr.shadowCastingMode = ShadowCastingMode.Off;

        Color gc = settings.glowColor;
        gc.a = 0.45f;
        GameObject g = ShardFX.CreateBillboard("Glow", transform, ShardFX.SoftDot, gc, 1.3f * k, false);
        g.transform.localPosition = new Vector3(0f, 0f, -0.42f * k);
        glow = g.transform;
        glowMaterial = g.GetComponent<Renderer>().sharedMaterial;

        GameObject m = ShardFX.CreateBillboard("Marker", transform, ShardFX.RingTexture, settings.glowColor, 1f, true);
        m.transform.localPosition = new Vector3(0f, 0f, -0.42f * k);
        marker = m.transform;
        markerRenderer = m.GetComponent<Renderer>();
        markerMaterial = markerRenderer.sharedMaterial;
        markerRenderer.enabled = false;

        stakeLight = gameObject.AddComponent<Light>();
        stakeLight.type = LightType.Point;
        stakeLight.color = settings.glowColor;
        stakeLight.range = settings.lightRange;
        stakeLight.intensity = settings.lightIntensity;
        stakeLight.shadows = LightShadows.None;

        trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.16f;
        trail.minVertexDistance = 0.05f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        trail.widthMultiplier = 0.22f * k;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.sharedMaterial = ShardFX.SpriteMaterial;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(settings.glowColor, 0.3f), new GradientColorKey(settings.glowColor, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.4f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = grad;
    }

    private void AddPart(PrimitiveType type, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
    {
        GameObject p = GameObject.CreatePrimitive(type);
        Destroy(p.GetComponent<Collider>());
        p.transform.SetParent(visualRoot, false);
        p.transform.localPosition = pos;
        p.transform.localRotation = rot;
        p.transform.localScale = scale;
        Renderer r = p.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
    }

    public void SetArmedVisual(bool armed)
    {
        if (markerRenderer != null) markerRenderer.enabled = armed;
        armedTimer = 0f;
    }

    private void Update()
    {
        if (finished) return;

        if (State == StakeState.Flying)
        {
            Fly();
            return;
        }

        if (!IsValid)
        {
            Break(false);
            return;
        }

        if (Armed == this)
        {
            armedTimer += Time.deltaTime;
            if (armedTimer >= settings.armedLifetime)
            {
                Armed = null;
                Break(true);
                return;
            }
        }

        AnimateIdle();
    }

    private void Fly()
    {
        float dt = Time.deltaTime;
        velocity += Vector3.down * settings.gravity * dt;
        Vector3 step = velocity * dt;
        float dist = step.magnitude;
        Vector3 dir = step / Mathf.Max(0.0001f, dist);
        transform.rotation = Quaternion.LookRotation(dir);

        RaycastHit[] hits = Physics.SphereCastAll(transform.position, settings.radius, dir, dist, mask, QueryTriggerInteraction.Ignore);
        if (hits.Length > 0)
        {
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit h in hits)
            {
                if (ignoreRoot != null && h.collider.transform.IsChildOf(ignoreRoot)) continue;
                if (h.collider.GetComponentInParent<GleipnirStake>() != null) continue;
                Impact(h, dir);
                return;
            }
        }

        transform.position += step;
        travelled += dist;
        if (travelled >= settings.range)
        {
            finished = true;
            Missed?.Invoke(this);
            Destroy(gameObject);
        }
    }

    private void Impact(RaycastHit hit, Vector3 dir)
    {
        Vector3 point = hit.distance <= 0f ? hit.collider.ClosestPoint(transform.position) : hit.point;
        Vector3 normal = hit.distance <= 0f ? -dir : hit.normal;
        IDamageable d = hit.collider.GetComponentInParent<IDamageable>();
        bool enemy = d != null && !d.IsDead;
        float k = settings.size;

        Vector3 pos = point - dir * (enemy ? 0.05f : 0.12f) * k;
        transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
        anchor = hit.collider.transform;
        anchorLocalPos = anchor.InverseTransformPoint(pos);
        anchorLocalDir = anchor.InverseTransformDirection(dir);
        anchorLocalPoint = anchor.InverseTransformPoint(point);
        anchorLocalNormal = anchor.InverseTransformDirection(normal);
        SurfaceNormal = normal;
        trail.emitting = false;
        wobble = 1f;

        Color c = settings.glowColor;

        if (enemy)
        {
            d.TakeDamage(settings.damage, point, dir);

            ShardFX.Burst(point, -dir, new Color(0.75f, 0.1f, 0.08f, 1f), 18, 9f, 0.16f, 0.4f, 40f, 1.4f, true);
            ShardFX.Burst(point, -dir, c, 14, 12f, 0.14f, 0.3f, 30f, 0.5f, true);
            ShardFX.Ring(point, c, 0.3f, 2.4f, 0.25f, true);
            ShardFX.Flash(point, c, 6f, 6f, 0.15f);
            GameAudio.Play(settings.enemyHitClip, SfxId.StakeHitEnemy, point, 1f);
            ShardFX.CameraShake(1.4f, 0.15f);
            HitStop.Do(0.035f);

            if (d.IsDead)
            {
                finished = true;
                Missed?.Invoke(this);
                Break(true);
                return;
            }

            Victim = d;
            VictimTransform = ((Component)d).transform;
            Enemy = VictimTransform.GetComponentInParent<BaseEnemy>();
            State = StakeState.InEnemy;
            Landed?.Invoke(this);
            return;
        }

        ShardFX.Burst(point, normal, c, 22, 11f, 0.13f, 0.35f, 45f, 1.6f, true);
        ShardFX.Burst(point, normal, new Color(0.55f, 0.52f, 0.5f, 0.8f), 10, 3f, 0.5f, 0.6f, 50f, -0.1f, false);
        ShardFX.Ring(point, c, 0.2f, 1.8f, 0.2f, false);
        ShardFX.Flash(point, c, 4f, 5f, 0.12f);
        GameAudio.Play(settings.wallHitClip, SfxId.StakeHitWall, point, 1f);
        ShardFX.CameraShakeAt(point, 1f, 0.12f, 30f);

        State = StakeState.InWall;
        Landed?.Invoke(this);
    }

    private void LateUpdate()
    {
        if (finished) return;

        if (State != StakeState.Flying)
        {
            if (anchor == null)
            {
                Break(false);
                return;
            }
            Quaternion rot = Quaternion.LookRotation(anchor.TransformDirection(anchorLocalDir));
            transform.SetPositionAndRotation(anchor.TransformPoint(anchorLocalPos), rot);
            SurfaceNormal = anchor.TransformDirection(anchorLocalNormal).normalized;

            if (wobble > 0.001f)
            {
                wobble *= Mathf.Exp(-9f * Time.deltaTime);
                float w = Mathf.Sin(Time.time * 55f) * wobble * 9f;
                visualRoot.localRotation = Quaternion.Euler(w, w * 0.6f, 0f);
            }
        }

        Camera cam = Camera.main;
        if (cam == null) return;
        glow.rotation = cam.transform.rotation;
        marker.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, Time.time * -120f);

        if (markerRenderer.enabled)
        {
            float dist = Vector3.Distance(cam.transform.position, marker.position);
            float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.1f;
            marker.localScale = Vector3.one * Mathf.Max(0.5f, dist * settings.markerScreenSize) * pulse;
        }
    }

    private void AnimateIdle()
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 7f);
        bool visible = true;

        if (Armed == this)
        {
            float remaining = settings.armedLifetime - armedTimer;
            if (remaining < settings.expireWarningTime)
            {
                float rate = Mathf.Lerp(14f, 3f, remaining / settings.expireWarningTime);
                visible = Mathf.Repeat(Time.time * rate, 1f) > 0.35f;
            }
        }

        Color mc = Color.Lerp(settings.glowColor, Color.white, 0.3f * pulse);
        mc.a = visible ? 1f : 0.15f;
        markerMaterial.color = mc;

        Color gc = settings.glowColor;
        gc.a = (0.3f + 0.3f * pulse) * (visible ? 1f : 0.3f);
        glowMaterial.color = gc;
        stakeLight.intensity = settings.lightIntensity * (0.5f + 0.5f * pulse) * (visible ? 1f : 0.25f);
    }

    public void Flare()
    {
        if (glowMaterial == null) return;
        Color c = Color.Lerp(settings.glowColor, Color.white, 0.6f);
        c.a = 1f;
        glowMaterial.color = c;
        glow.localScale = Vector3.one * 2.4f * settings.size;
        stakeLight.intensity = settings.lightIntensity * 3f;
        wobble = 1.2f;
    }

    public void Break(bool big)
    {
        if (broken) return;
        broken = true;
        bool wasFinished = finished;
        finished = true;
        if (Armed == this) Armed = null;

        Vector3 p = transform.position;
        Color c = settings.glowColor;
        ShardFX.Burst(p, Vector3.up, c, big ? 24 : 12, big ? 8f : 5f, 0.14f, 0.45f, 180f, 1.5f, true);
        ShardFX.Burst(p, Vector3.up, settings.ironColor, big ? 10 : 6, 5f, 0.12f, 0.6f, 180f, 2.5f, false);
        if (big) ShardFX.Ring(p, c, 0.3f, 2f, 0.25f, false);
        if (!wasFinished && State == StakeState.Flying) Missed?.Invoke(this);
        Destroy(gameObject);
    }

    public void Consume()
    {
        if (broken) return;
        broken = true;
        finished = true;
        if (Armed == this) Armed = null;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Armed == this) Armed = null;
        if (ironMaterial != null) Destroy(ironMaterial);
        if (bandMaterial != null) Destroy(bandMaterial);
        if (glowMaterial != null) Destroy(glowMaterial);
        if (markerMaterial != null) Destroy(markerMaterial);
    }
}
