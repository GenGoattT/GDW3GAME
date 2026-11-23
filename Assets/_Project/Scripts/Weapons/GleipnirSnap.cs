using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Project.Combat;
using Project.Enemies;

public class GleipnirSnap : MonoBehaviour
{
    [Serializable]
    public class Settings
    {
        public float tensionTime = 0.09f;
        public float pullSpeed = 60f;
        public float minPullTime = 0.1f;
        public float maxPullTime = 0.45f;
        public float impactBaseDamage = 25f;
        public float impactDamagePerMeter = 1.6f;
        public float impactMaxDamage = 80f;
        public float sameEnemyDamage = 30f;
        public float pinTime = 0.25f;
        public float dropGravity = 40f;
        public float impactHitstop = 0.09f;
        public float killHitstop = 0.15f;
        public float tripwireDuration = 6f;
        public float tripwireDamage = 9f;
        public float tripwireTick = 0.2f;
        public float tripwireRadius = 0.35f;
        public LayerMask tripwireMask = ~0;
        public float wireWidth = 0.09f;
        public AudioClip tensionClip;
        public AudioClip impactClip;
        public AudioClip killClip;
        public AudioClip tripwireClip;
    }

    private class Pull
    {
        public Transform root;
        public BaseEnemy enemy;
        public IDamageable damageable;
        public Vector3 from;
        public Vector3 to;
        public float travel;
    }

    private Settings settings;
    private GleipnirStake a;
    private GleipnirStake b;
    private GleipnirWire wire;
    private Color color;

    public static void Begin(GleipnirStake first, GleipnirStake second, Settings settings)
    {
        GameObject go = new GameObject("GleipnirSnap");
        GleipnirSnap s = go.AddComponent<GleipnirSnap>();
        s.settings = settings;
        s.a = first;
        s.b = second;
        s.color = first.GlowColor;
        s.wire = GleipnirWire.Create("SnapWire", s.color, settings.wireWidth);
        s.wire.transform.SetParent(go.transform, false);
        s.StartCoroutine(s.Run());
    }

    private IEnumerator Run()
    {
        if (!Valid(a) || !Valid(b))
        {
            Finish();
            yield break;
        }

        a.Flare();
        b.Flare();
        wire.Twang(0.5f);
        ShardFX.Play(settings.tensionClip, b.transform.position, 1f);
        ShardFX.CameraShake(0.9f, 0.12f);

        float t = 0f;
        float startSag = Mathf.Min(1.5f, Vector3.Distance(a.WireAttach, b.WireAttach) * 0.08f);
        while (t < settings.tensionTime)
        {
            if (!Valid(a) || !Valid(b))
            {
                Finish();
                yield break;
            }
            float k = t / settings.tensionTime;
            wire.Draw(a.WireAttach, b.WireAttach, Mathf.Lerp(startSag, 0f, k * k), 1f, 1f + k * 1.5f, k);
            t += Time.deltaTime;
            yield return null;
        }

        wire.Twang(0.25f);
        ShardFX.Burst(a.WireAttach, Vector3.up, color, 10, 6f, 0.12f, 0.2f, 180f, 0f, true);
        ShardFX.Burst(b.WireAttach, Vector3.up, color, 10, 6f, 0.12f, 0.2f, 180f, 0f, true);

        bool aEnemy = a.State == GleipnirStake.StakeState.InEnemy;
        bool bEnemy = b.State == GleipnirStake.StakeState.InEnemy;

        if (aEnemy && bEnemy && a.VictimTransform == b.VictimTransform)
        {
            yield return Constrict();
        }
        else if (aEnemy && bEnemy)
        {
            yield return PullTogether();
        }
        else if (aEnemy || bEnemy)
        {
            GleipnirStake enemyStake = aEnemy ? a : b;
            GleipnirStake wallStake = aEnemy ? b : a;
            yield return PullToWall(enemyStake, wallStake);
        }
        else
        {
            yield return Tripwire();
        }

        Finish();
    }

    private static bool Valid(GleipnirStake s)
    {
        return s != null && s.IsValid;
    }

    private IEnumerator Constrict()
    {
        Vector3 p = (a.transform.position + b.transform.position) * 0.5f;
        IDamageable d = a.Victim;
        float t = 0f;
        while (t < 0.12f && Valid(a) && Valid(b))
        {
            wire.Draw(a.WireAttach, b.WireAttach, 0f, 1f, 2.5f, 1f);
            wire.Twang(0.08f);
            t += Time.deltaTime;
            yield return null;
        }

        if (d != null && !d.IsDead)
        {
            d.TakeDamage(settings.sameEnemyDamage, p, Vector3.up);
            ImpactFX(p, Vector3.up, d.IsDead, 1f);
        }
        a.Consume();
        b.Consume();
    }

    private Pull MakePull(GleipnirStake s, Vector3 targetCenter)
    {
        Transform root = s.Enemy != null ? s.Enemy.transform : s.VictimTransform;
        Bounds bounds = GetBounds(root);
        Vector3 offset = bounds.center - root.position;
        Pull p = new Pull
        {
            root = root,
            enemy = s.Enemy,
            damageable = s.Victim,
            from = root.position,
            to = targetCenter - offset
        };
        p.travel = Vector3.Distance(p.from, p.to);
        if (p.enemy != null) p.enemy.BeginExternalMove();
        return p;
    }

    private static float Radius(Transform root)
    {
        Bounds b = GetBounds(root);
        return Mathf.Max(0.3f, Mathf.Max(b.extents.x, b.extents.z));
    }

    private static Bounds GetBounds(Transform root)
    {
        bool has = false;
        Bounds b = new Bounds(root.position, Vector3.one);
        foreach (Collider c in root.GetComponentsInChildren<Collider>())
        {
            if (!c.enabled || c.isTrigger) continue;
            if (!has)
            {
                b = c.bounds;
                has = true;
            }
            else
            {
                b.Encapsulate(c.bounds);
            }
        }
        if (!has)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (r.GetComponentInParent<GleipnirStake>() != null) continue;
                if (!has)
                {
                    b = r.bounds;
                    has = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }
        }
        return b;
    }

    private IEnumerator PullToWall(GleipnirStake enemyStake, GleipnirStake wallStake)
    {
        Transform root = enemyStake.Enemy != null ? enemyStake.Enemy.transform : enemyStake.VictimTransform;
        Vector3 n = wallStake.SurfaceNormal;
        Vector3 targetCenter = wallStake.SurfacePoint + n * (Radius(root) + 0.05f);
        Pull p = MakePull(enemyStake, targetCenter);

        yield return MovePulls(new[] { p }, p.travel);

        Vector3 hitPoint = wallStake.SurfacePoint + n * 0.2f;
        float dmg = Mathf.Min(settings.impactMaxDamage, settings.impactBaseDamage + p.travel * settings.impactDamagePerMeter);
        bool killed = Smash(p, dmg, hitPoint, -n);
        ImpactFX(hitPoint, n, killed, Mathf.Clamp01(p.travel / 20f));

        enemyStake.Consume();
        wallStake.Break(true);

        yield return Settle(new[] { p }, n.y < 0.5f);
    }

    private IEnumerator PullTogether()
    {
        Transform ra = a.Enemy != null ? a.Enemy.transform : a.VictimTransform;
        Transform rb = b.Enemy != null ? b.Enemy.transform : b.VictimTransform;
        Vector3 ca = GetBounds(ra).center;
        Vector3 cb = GetBounds(rb).center;
        Vector3 dir = cb - ca;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();
        Vector3 mid = (ca + cb) * 0.5f;
        float raR = Radius(ra);
        float rbR = Radius(rb);
        Vector3 shift = dir * (raR - rbR) * 0.5f;

        Pull pa = MakePull(a, mid - dir * raR * 0.9f + shift);
        Pull pb = MakePull(b, mid + dir * rbR * 0.9f + shift);
        Pull[] pulls = { pa, pb };

        yield return MovePulls(pulls, Mathf.Max(pa.travel, pb.travel));

        float travel = pa.travel + pb.travel;
        float dmg = Mathf.Min(settings.impactMaxDamage, settings.impactBaseDamage + travel * settings.impactDamagePerMeter);
        Vector3 hitPoint = mid + shift;
        bool killedA = Smash(pa, dmg, hitPoint, dir);
        bool killedB = Smash(pb, dmg, hitPoint, -dir);
        ImpactFX(hitPoint, Vector3.up, killedA || killedB, Mathf.Clamp01(travel / 20f) + 0.3f);

        a.Consume();
        b.Consume();

        yield return Settle(pulls, true);
    }

    private IEnumerator MovePulls(Pull[] pulls, float maxTravel)
    {
        float duration = Mathf.Clamp(maxTravel / settings.pullSpeed, settings.minPullTime, settings.maxPullTime);
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float ease = k * k * k;

            foreach (Pull p in pulls)
            {
                if (p.root == null) continue;
                Vector3 prev = p.root.position;
                p.root.position = Vector3.LerpUnclamped(p.from, p.to, ease);
                Vector3 vel = p.root.position - prev;
                if (vel.sqrMagnitude > 0.0004f && UnityEngine.Random.value < 0.6f)
                    ShardFX.Burst(p.root.position + Vector3.up, -vel, color, 2, 3f, 0.12f, 0.25f, 20f, 0f, true);
            }

            if (a != null && b != null && !a.IsFinished && !b.IsFinished)
                wire.Draw(a.WireAttach, b.WireAttach, 0f, 1f, 2f - k, 0.8f);

            yield return null;
        }
    }

    private bool Smash(Pull p, float dmg, Vector3 point, Vector3 dir)
    {
        if (p.damageable == null || p.damageable.IsDead) return false;
        p.damageable.TakeDamage(dmg, point, dir);
        return p.damageable.IsDead;
    }

    private void ImpactFX(Vector3 p, Vector3 n, bool killed, float power)
    {
        wire.Hide();
        float s = 1f + power;
        ShardFX.Burst(p, n, color, Mathf.RoundToInt(40 * s), 20f * s, 0.24f, 0.45f, 70f, 1f, true);
        ShardFX.Burst(p, n, Color.white, 18, 14f, 0.16f, 0.3f, 80f, 0.6f, true);
        ShardFX.Burst(p, n, new Color(0.45f, 0.42f, 0.4f, 0.8f), 14, 4f, 0.8f, 0.8f, 70f, -0.15f, false);
        ShardFX.Burst(p, n, new Color(0.75f, 0.1f, 0.08f, 1f), 24, 10f, 0.2f, 0.6f, 90f, 2f, true);
        ShardFX.Ring(p, color, 0.6f, 6f * s, 0.4f, true);
        ShardFX.Flash(p, color, 16f, 14f, 0.3f);
        ShardFX.Play(settings.impactClip, p, 1f);
        ShardFX.CameraShakeAt(p, 3.5f * s, 0.35f, 45f);
        ShardFX.CameraPunch(108f, 0.06f);

        if (killed)
        {
            ShardFX.Burst(p, Vector3.up, new Color(1f, 0.35f, 0.25f, 1f), 45, 15f, 0.3f, 0.7f, 180f, 1.6f, true);
            ShardFX.Ring(p, Color.white, 1f, 9f, 0.5f, true);
            ShardFX.Play(settings.killClip, p, 1f);
            ShardFX.CameraShake(5f, 0.45f);
            HitStop.Do(settings.killHitstop);
        }
        else
        {
            HitStop.Do(settings.impactHitstop);
        }
    }

    private IEnumerator Settle(Pull[] pulls, bool pin)
    {
        if (pin) yield return new WaitForSeconds(settings.pinTime);

        List<Pull> falling = new List<Pull>();
        List<float> groundY = new List<float>();
        List<float> speeds = new List<float>();

        foreach (Pull p in pulls)
        {
            if (p.root == null) continue;
            float g = FindGround(p.root);
            if (float.IsNaN(g))
            {
                if (p.enemy != null) p.enemy.EndExternalMove();
                continue;
            }
            falling.Add(p);
            groundY.Add(g);
            speeds.Add(0f);
        }

        while (falling.Count > 0)
        {
            for (int i = falling.Count - 1; i >= 0; i--)
            {
                Pull p = falling[i];
                if (p.root == null)
                {
                    falling.RemoveAt(i);
                    groundY.RemoveAt(i);
                    speeds.RemoveAt(i);
                    continue;
                }

                speeds[i] += settings.dropGravity * Time.deltaTime;
                Vector3 pos = p.root.position;
                pos.y -= speeds[i] * Time.deltaTime;

                if (pos.y <= groundY[i])
                {
                    pos.y = groundY[i];
                    p.root.position = pos;
                    if (speeds[i] > 8f)
                    {
                        ShardFX.Burst(pos, Vector3.up, new Color(0.5f, 0.47f, 0.45f, 0.8f), 12, 4f, 0.6f, 0.6f, 80f, -0.1f, false);
                        ShardFX.CameraShakeAt(pos, 1.2f, 0.15f, 30f);
                    }
                    if (p.enemy != null) p.enemy.EndExternalMove();
                    falling.RemoveAt(i);
                    groundY.RemoveAt(i);
                    speeds.RemoveAt(i);
                    continue;
                }

                p.root.position = pos;
            }
            yield return null;
        }
    }

    private static float FindGround(Transform root)
    {
        Vector3 origin = GetBounds(root).center;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(root)) continue;
            if (h.collider.GetComponentInParent<GleipnirStake>() != null) continue;
            if (h.collider.GetComponentInParent<IDamageable>() != null) continue;
            return Mathf.Min(h.point.y, root.position.y);
        }
        return float.NaN;
    }

    private IEnumerator Tripwire()
    {
        Dictionary<IDamageable, float> nextHit = new Dictionary<IDamageable, float>();
        float t = 0f;
        ShardFX.Play(settings.tripwireClip, (a.WireAttach + b.WireAttach) * 0.5f, 1f);

        while (t < settings.tripwireDuration && Valid(a) && Valid(b))
        {
            Vector3 pa = a.WireAttach;
            Vector3 pb = b.WireAttach;
            float remaining = settings.tripwireDuration - t;
            float blink = remaining < 1.2f ? (Mathf.Repeat(Time.time * 10f, 1f) > 0.4f ? 1f : 0.3f) : 1f;
            float hum = 0.5f + 0.5f * Mathf.Sin(Time.time * 20f);
            wire.Draw(pa, pb, 0f, blink, 1.2f + 0.3f * hum, 0.3f + 0.3f * hum);
            wire.Twang(0.015f);

            Collider[] cols = Physics.OverlapCapsule(pa, pb, settings.tripwireRadius, settings.tripwireMask, QueryTriggerInteraction.Ignore);
            foreach (Collider c in cols)
            {
                IDamageable d = c.GetComponentInParent<IDamageable>();
                if (d == null || d.IsDead) continue;
                if (nextHit.TryGetValue(d, out float when) && Time.time < when) continue;
                nextHit[d] = Time.time + settings.tripwireTick;

                Vector3 cp = ClosestOnSegment(pa, pb, c.bounds.center);
                Vector3 dir = (c.bounds.center - cp);
                dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.up;
                d.TakeDamage(settings.tripwireDamage, cp, dir);
                bool killed = d.IsDead;

                wire.Twang(0.35f);
                ShardFX.Burst(cp, dir, new Color(0.75f, 0.1f, 0.08f, 1f), 12, 8f, 0.14f, 0.35f, 50f, 1.5f, true);
                ShardFX.Burst(cp, -dir, color, 10, 10f, 0.12f, 0.2f, 60f, 0f, true);
                ShardFX.Flash(cp, color, 4f, 4f, 0.1f);
                ShardFX.CameraShakeAt(cp, killed ? 2.5f : 0.8f, 0.12f, 35f);
                if (killed)
                {
                    ShardFX.Ring(cp, color, 0.4f, 4f, 0.3f, true);
                    HitStop.Do(0.06f);
                }
            }

            t += Time.deltaTime;
            yield return null;
        }

        if (a != null) a.Break(true);
        if (b != null) b.Break(true);
    }

    private static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        return a + ab * t;
    }

    private void Finish()
    {
        if (a != null && !a.IsFinished) a.Break(false);
        if (b != null && !b.IsFinished) b.Break(false);
        Destroy(gameObject);
    }
}
