using Project.Audio;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using Project.Combat;

public class SwordShard : MonoBehaviour
{
    public enum ShardState { Flying, Embedded, Stuck }

    [Serializable]
    public class Settings
    {
        public float speed = 130f;
        public float range = 250f;
        public float radius = 0.3f;
        public float damage = 3f;
        public float embedLifetime = 10f;
        public float stuckLifetime = 2.5f;
        public float size = 1.4f;
        public Color color = new Color(0.35f, 0.85f, 1f, 1f);
        public float glowSize = 1.8f;
        public float lightIntensity = 4f;
        public float lightRange = 6f;
        public float trailWidth = 0.35f;
        public float trailTime = 0.22f;
        public float markerScreenSize = 0.06f;
        public float expireWarningTime = 2.5f;
        public GameObject visualPrefab;
        public AudioClip embedClip;
        public AudioClip wallClip;
        public AudioClip expireClip;
    }

    public ShardState State { get; private set; }
    public IDamageable Victim { get; private set; }
    public Transform VictimTransform { get; private set; }
    public Color ShardColor => settings.color;

    public bool IsEmbeddedInLivingEnemy =>
        State == ShardState.Embedded && VictimTransform != null && Victim != null && !Victim.IsDead;

    public event Action<SwordShard> Embedded;
    public event Action<SwordShard> HitWall;

    private Settings settings;
    private Vector3 direction;
    private LayerMask mask;
    private Transform ignoreRoot;

    private Transform spinRoot;
    private Transform glow;
    private Material glowMaterial;
    private Transform marker;
    private Material markerMaterial;
    private Renderer markerRenderer;
    private Light shardLight;
    private TrailRenderer trail;
    private Material coreMaterial;
    private Material shellMaterial;

    private Transform anchor;
    private Vector3 anchorLocalPos;
    private Vector3 anchorLocalDir;

    private float travelled;
    private float timer;
    private float spin;
    private bool finished;

    public static SwordShard Spawn(Vector3 position, Vector3 dir, Settings settings, LayerMask mask, Transform ignoreRoot)
    {
        GameObject go = new GameObject("SwordShard");
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(dir));
        SwordShard shard = go.AddComponent<SwordShard>();
        shard.settings = settings;
        shard.direction = dir.normalized;
        shard.mask = mask;
        shard.ignoreRoot = ignoreRoot;
        shard.BuildVisual();
        return shard;
    }

    private void BuildVisual()
    {
        Color c = settings.color;
        Color core = Color.Lerp(c, Color.white, 0.6f);

        spinRoot = new GameObject("Spin").transform;
        spinRoot.SetParent(transform, false);

        if (settings.visualPrefab != null)
        {
            GameObject v = Instantiate(settings.visualPrefab, spinRoot);
            v.transform.localPosition = Vector3.zero;
            v.transform.localRotation = Quaternion.identity;
            foreach (Collider col in v.GetComponentsInChildren<Collider>()) Destroy(col);
        }
        else
        {
            coreMaterial = ShardFX.CreateSolidMaterial(core);
            CreateBlade("Core", Vector3.one * settings.size, coreMaterial);

            Color shell = c;
            shell.a = 0.45f;
            shellMaterial = ShardFX.CreateGlowMaterial(null, shell, false);
            CreateBlade("Shell", new Vector3(1.6f, 2.2f, 1.2f) * settings.size, shellMaterial);
        }

        Color glowColor = c;
        glowColor.a = 0.55f;
        GameObject g = ShardFX.CreateBillboard("Glow", transform, ShardFX.SoftDot, glowColor, settings.glowSize, false);
        glow = g.transform;
        glowMaterial = g.GetComponent<Renderer>().sharedMaterial;

        GameObject m = ShardFX.CreateBillboard("Marker", transform, ShardFX.RingTexture, core, 1f, true);
        marker = m.transform;
        markerRenderer = m.GetComponent<Renderer>();
        markerMaterial = markerRenderer.sharedMaterial;
        markerRenderer.enabled = false;

        shardLight = gameObject.AddComponent<Light>();
        shardLight.type = LightType.Point;
        shardLight.color = c;
        shardLight.range = settings.lightRange;
        shardLight.intensity = settings.lightIntensity;
        shardLight.shadows = LightShadows.None;

        trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = settings.trailTime;
        trail.minVertexDistance = 0.05f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        trail.widthMultiplier = settings.trailWidth;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.sharedMaterial = ShardFX.SpriteMaterial;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(c, 0.25f), new GradientColorKey(c, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.3f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = grad;
    }

    private void CreateBlade(string name, Vector3 scale, Material mat)
    {
        GameObject b = new GameObject(name);
        b.transform.SetParent(spinRoot, false);
        b.transform.localScale = scale;
        b.AddComponent<MeshFilter>().sharedMesh = ShardFX.BladeMesh;
        MeshRenderer r = b.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    private void Update()
    {
        if (finished) return;
        switch (State)
        {
            case ShardState.Flying: Fly(); break;
            case ShardState.Embedded: TickEmbedded(); break;
            case ShardState.Stuck: TickStuck(); break;
        }
    }

    private void LateUpdate()
    {
        if (finished) return;
        if (State != ShardState.Flying)
        {
            if (anchor == null)
            {
                if (State == ShardState.Embedded) Shatter(false);
                else Destroy(gameObject);
                return;
            }
            transform.SetPositionAndRotation(
                anchor.TransformPoint(anchorLocalPos),
                Quaternion.LookRotation(anchor.TransformDirection(anchorLocalDir)));
        }

        Camera cam = Camera.main;
        if (cam == null) return;
        Quaternion face = cam.transform.rotation;
        glow.rotation = face;
        marker.rotation = face * Quaternion.Euler(0f, 0f, Time.time * 90f);

        if (markerRenderer.enabled)
        {
            float dist = Vector3.Distance(cam.transform.position, transform.position);
            float pulse = 1f + Mathf.Sin(Time.time * 10f) * 0.12f;
            marker.localScale = Vector3.one * Mathf.Max(0.6f, dist * settings.markerScreenSize) * pulse;
        }
    }

    private void Fly()
    {
        spin += 1440f * Time.deltaTime;
        spinRoot.localRotation = Quaternion.Euler(0f, 0f, spin);

        float step = settings.speed * Time.deltaTime;
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, settings.radius, direction, step, mask, QueryTriggerInteraction.Ignore);

        if (hits.Length > 0)
        {
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit h in hits)
            {
                if (ignoreRoot != null && h.collider.transform.IsChildOf(ignoreRoot)) continue;
                Impact(h);
                return;
            }
        }

        transform.position += direction * step;
        travelled += step;
        if (travelled >= settings.range) Destroy(gameObject);
    }

    private void Impact(RaycastHit hit)
    {
        Vector3 point = hit.distance <= 0f ? hit.collider.ClosestPoint(transform.position) : hit.point;
        Vector3 normal = hit.distance <= 0f ? -direction : hit.normal;
        IDamageable d = hit.collider.GetComponentInParent<IDamageable>();
        bool enemy = d != null && !d.IsDead;

        Vector3 pos = point + direction * (enemy ? 0.15f * settings.size : 0.3f * settings.size);
        transform.position = pos;
        anchor = hit.collider.transform;
        anchorLocalPos = anchor.InverseTransformPoint(pos);
        anchorLocalDir = anchor.InverseTransformDirection(direction);
        trail.emitting = false;
        spinRoot.localRotation = Quaternion.Euler(0f, 0f, 45f);
        timer = 0f;

        Color c = settings.color;

        if (enemy)
        {
            d.TakeDamage(settings.damage, point, direction);

            ShardFX.Burst(point, -direction, c, 26, 16f, 0.22f, 0.35f, 35f, 0.6f, true);
            ShardFX.Burst(point, normal, Color.white, 8, 6f, 0.5f, 0.15f, 60f, 0f, false);
            ShardFX.Ring(point, c, 0.3f, 3.2f, 0.3f, true);
            ShardFX.Flash(point, c, 8f, 8f, 0.2f);
            GameAudio.Play(settings.embedClip, SfxId.ShardEmbed, point, 1f);

            if (d.IsDead)
            {
                Shatter(true);
                return;
            }

            Victim = d;
            VictimTransform = ((Component)d).transform;
            State = ShardState.Embedded;
            markerRenderer.enabled = true;
            Embedded?.Invoke(this);
            return;
        }

        ShardFX.Burst(point, normal, c, 14, 9f, 0.14f, 0.3f, 40f, 1f, true);
        ShardFX.Flash(point, c, 3f, 4f, 0.12f);
        GameAudio.Play(settings.wallClip, SfxId.ShardWall, point, 0.8f);
        State = ShardState.Stuck;
        HitWall?.Invoke(this);
    }

    private void TickEmbedded()
    {
        timer += Time.deltaTime;
        if (!IsEmbeddedInLivingEnemy)
        {
            Shatter(false);
            return;
        }
        if (timer >= settings.embedLifetime)
        {
            GameAudio.Play(settings.expireClip, SfxId.ShardExpire, transform.position, 0.8f);
            Shatter(true);
            return;
        }

        float remaining = settings.embedLifetime - timer;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
        bool visible = true;

        if (remaining < settings.expireWarningTime)
        {
            float rate = Mathf.Lerp(16f, 4f, remaining / settings.expireWarningTime);
            visible = Mathf.Repeat(Time.time * rate, 1f) > 0.35f;
        }

        Color mc = Color.Lerp(settings.color, Color.white, 0.4f + 0.4f * pulse);
        mc.a = visible ? 1f : 0.15f;
        markerMaterial.color = mc;

        Color gc = settings.color;
        gc.a = (0.35f + 0.35f * pulse) * (visible ? 1f : 0.3f);
        glowMaterial.color = gc;
        glow.localScale = Vector3.one * settings.glowSize * (0.9f + 0.3f * pulse);
        shardLight.intensity = settings.lightIntensity * (0.5f + 0.6f * pulse) * (visible ? 1f : 0.2f);
    }

    private void TickStuck()
    {
        timer += Time.deltaTime;
        float fade = Mathf.Clamp01((settings.stuckLifetime - timer) / 0.5f);
        spinRoot.localScale = Vector3.one * fade;
        glow.localScale = Vector3.one * settings.glowSize * 0.6f * fade;
        shardLight.intensity = settings.lightIntensity * 0.4f * fade;
        if (timer >= settings.stuckLifetime) Destroy(gameObject);
    }

    public void Shatter(bool big)
    {
        if (finished) return;
        finished = true;
        Vector3 p = transform.position;
        Color c = settings.color;
        ShardFX.Burst(p, Vector3.up, c, big ? 30 : 14, big ? 10f : 6f, big ? 0.2f : 0.14f, 0.5f, 180f, 1.2f, true);
        ShardFX.Burst(p, Vector3.up, Color.white, big ? 10 : 5, 3f, 0.35f, 0.25f, 180f, 0f, false);
        if (big) ShardFX.Ring(p, c, 0.4f, 2.2f, 0.25f, false);
        Destroy(gameObject);
    }

    public void Consume()
    {
        finished = true;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (coreMaterial != null) Destroy(coreMaterial);
        if (shellMaterial != null) Destroy(shellMaterial);
        if (glowMaterial != null) Destroy(glowMaterial);
        if (markerMaterial != null) Destroy(markerMaterial);
    }
}
