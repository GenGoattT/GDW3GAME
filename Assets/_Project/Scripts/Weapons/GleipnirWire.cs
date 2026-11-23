using UnityEngine;
using UnityEngine.Rendering;

public class GleipnirWire : MonoBehaviour
{
    private const int Points = 28;

    private LineRenderer line;
    private Color color;
    private float width;
    private float vibration;
    private float phase;
    private readonly Keyframe[] keys = new Keyframe[Points];

    public static GleipnirWire Create(string name, Color color, float width)
    {
        GameObject go = new GameObject(name);
        GleipnirWire w = go.AddComponent<GleipnirWire>();
        w.color = color;
        w.width = width;
        w.line = go.AddComponent<LineRenderer>();
        w.line.useWorldSpace = true;
        w.line.positionCount = Points;
        w.line.alignment = LineAlignment.View;
        w.line.shadowCastingMode = ShadowCastingMode.Off;
        w.line.receiveShadows = false;
        w.line.numCapVertices = 2;
        w.line.sharedMaterial = ShardFX.SpriteMaterial;
        w.line.enabled = false;
        return w;
    }

    public void Twang(float amount)
    {
        vibration = Mathf.Max(vibration, amount);
    }

    public void Hide()
    {
        if (line != null) line.enabled = false;
    }

    public void Draw(Vector3 a, Vector3 b, float sag, float alpha, float widthScale, float brightness)
    {
        line.enabled = alpha > 0.001f;
        if (!line.enabled) return;

        Vector3 ab = b - a;
        Camera cam = Camera.main;
        Vector3 view = cam != null ? cam.transform.forward : Vector3.forward;
        Vector3 side = Vector3.Cross(ab.normalized, view);
        if (side.sqrMagnitude < 0.0001f) side = Vector3.Cross(ab.normalized, Vector3.up);
        side.Normalize();
        Vector3 up = Vector3.Cross(side, ab.normalized);

        float t0 = Time.unscaledTime;
        for (int i = 0; i < Points; i++)
        {
            float t = i / (float)(Points - 1);
            float bell = Mathf.Sin(t * Mathf.PI);
            Vector3 p = a + ab * t;
            p += Vector3.down * sag * 4f * t * (1f - t);
            p += side * Mathf.Sin(t * Mathf.PI * 5f + phase) * vibration * bell;
            p += up * Mathf.Sin(t * Mathf.PI * 3f - phase * 0.7f) * vibration * 0.5f * bell;
            line.SetPosition(i, p);

            float twist = 0.3f + 0.7f * Mathf.Abs(Mathf.Cos(t * 22f + t0 * 7f));
            keys[i] = new Keyframe(t, twist);
        }

        line.widthCurve = new AnimationCurve(keys);
        line.widthMultiplier = width * widthScale;

        Color c = Color.Lerp(color, Color.white, brightness);
        c.a = alpha;
        Color mid = Color.Lerp(color, Color.white, Mathf.Clamp01(brightness + 0.3f));
        mid.a = alpha;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(c, 0f), new GradientColorKey(mid, 0.5f), new GradientColorKey(c, 1f) },
            new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha, 1f) });
        line.colorGradient = g;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        phase += dt * 48f;
        vibration *= Mathf.Exp(-7f * dt);
    }
}

public class GleipnirRibbon : MonoBehaviour
{
    private const int Points = 18;

    private LineRenderer line;
    private Vector3 start;
    private Vector3 end;
    private Vector3 side;
    private Vector3 up;
    private Color color;
    private float width;
    private float extendTime;
    private float holdTime;
    private float fadeTime;
    private float wiggle;
    private float seed;
    private float timer;
    private readonly Keyframe[] keys = new Keyframe[Points];

    public static void Shoot(Vector3 start, Vector3 end, Color color, float width, float extendTime, float holdTime, float fadeTime, float wiggle)
    {
        GameObject go = new GameObject("GleipnirRibbon");
        GleipnirRibbon r = go.AddComponent<GleipnirRibbon>();
        r.start = start;
        r.end = end;
        r.color = color;
        r.width = width;
        r.extendTime = Mathf.Max(0.001f, extendTime);
        r.holdTime = holdTime;
        r.fadeTime = Mathf.Max(0.001f, fadeTime);
        r.wiggle = wiggle;
        r.seed = Random.value * 100f;

        Vector3 dir = (end - start).normalized;
        r.side = Vector3.Cross(dir, Vector3.up);
        if (r.side.sqrMagnitude < 0.0001f) r.side = Vector3.right;
        r.side.Normalize();
        r.up = Vector3.Cross(r.side, dir);

        r.line = go.AddComponent<LineRenderer>();
        r.line.useWorldSpace = true;
        r.line.positionCount = Points;
        r.line.alignment = LineAlignment.View;
        r.line.shadowCastingMode = ShadowCastingMode.Off;
        r.line.receiveShadows = false;
        r.line.numCapVertices = 3;
        r.line.sharedMaterial = ShardFX.SpriteMaterial;
        r.Render();
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= extendTime + holdTime + fadeTime)
        {
            Destroy(gameObject);
            return;
        }
        Render();
    }

    private void Render()
    {
        float grow = Mathf.Clamp01(timer / extendTime);
        float growEase = 1f - Mathf.Pow(1f - grow, 3f);
        float fade = Mathf.Clamp01((timer - extendTime - holdTime) / fadeTime);
        float tailPull = fade * fade;
        float settle = Mathf.Exp(-timer * 18f);

        Vector3 head = Vector3.Lerp(start, end, growEase);
        Vector3 tail = Vector3.Lerp(start, end, tailPull * 0.85f);

        for (int i = 0; i < Points; i++)
        {
            float t = i / (float)(Points - 1);
            Vector3 p = Vector3.Lerp(tail, head, t);
            float bell = Mathf.Sin(t * Mathf.PI);
            float w = wiggle * (0.35f + settle) * bell;
            p += side * Mathf.Sin(t * 11f + seed + timer * 60f) * w;
            p += up * Mathf.Cos(t * 7f + seed * 1.3f + timer * 45f) * w * 0.5f;
            line.SetPosition(i, p);

            float twist = 0.25f + 0.75f * Mathf.Abs(Mathf.Cos(t * 16f + seed + timer * 30f));
            float taper = Mathf.Lerp(0.6f, 1f, t);
            keys[i] = new Keyframe(t, twist * taper);
        }

        line.widthCurve = new AnimationCurve(keys);
        line.widthMultiplier = width * (1f - fade * 0.8f);

        float a = 1f - fade;
        Color hot = Color.Lerp(color, Color.white, 0.75f * (1f - fade));
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 0.6f), new GradientColorKey(hot, 1f) },
            new[] { new GradientAlphaKey(a * 0.5f, 0f), new GradientAlphaKey(a, 0.5f), new GradientAlphaKey(a, 1f) });
        line.colorGradient = g;
    }
}
