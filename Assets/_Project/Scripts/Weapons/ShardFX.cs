using UnityEngine;
using UnityEngine.Rendering;
using Project.Effects;

public static class ShardFX
{
    private static Texture2D softDot;
    private static Texture2D ring;
    private static Material spriteMaterial;
    private static Material particleMaterial;
    private static Mesh bladeMesh;

    public static Texture2D SoftDot
    {
        get
        {
            if (softDot == null) softDot = VFXTextures.CreateSoftDot(64, 0.05f, 1.6f);
            return softDot;
        }
    }

    public static Texture2D RingTexture
    {
        get
        {
            if (ring != null) return ring;
            int size = 128;
            ring = new Texture2D(size, size, TextureFormat.RGBA32, true);
            ring.name = "ShardRing";
            ring.wrapMode = TextureWrapMode.Clamp;
            Color32[] px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c;
                    float dy = (y - c) / c;
                    float diamond = Mathf.Abs(dx) + Mathf.Abs(dy);
                    float edge = Mathf.Abs(diamond - 0.78f);
                    float a = Mathf.Clamp01(1f - edge / 0.09f);
                    a = a * a * (3f - 2f * a);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            ring.SetPixels32(px);
            ring.Apply(true);
            return ring;
        }
    }

    public static Material SpriteMaterial
    {
        get
        {
            if (spriteMaterial == null) spriteMaterial = new Material(Shader.Find("Sprites/Default")) { name = "ShardSprite" };
            return spriteMaterial;
        }
    }

    public static Material ParticleMaterial
    {
        get
        {
            if (particleMaterial != null) return particleMaterial;
            particleMaterial = new Material(Shader.Find("Sprites/Default")) { name = "ShardParticles" };
            particleMaterial.mainTexture = SoftDot;
            return particleMaterial;
        }
    }

    public static Material CreateSolidMaterial(Color color)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Unlit/Color");
        if (s == null) s = Shader.Find("Sprites/Default");
        Material m = new Material(s) { name = "ShardCore" };
        SetColor(m, color);
        return m;
    }

    public static Material CreateGlowMaterial(Texture tex, Color color, bool alwaysOnTop)
    {
        Material m;
        if (alwaysOnTop)
        {
            Shader ui = Shader.Find("UI/Default");
            m = new Material(ui != null ? ui : Shader.Find("Sprites/Default"));
            m.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            m.renderQueue = 4000;
        }
        else
        {
            m = new Material(Shader.Find("Sprites/Default"));
        }
        m.name = "ShardGlow";
        m.mainTexture = tex;
        m.color = color;
        return m;
    }

    public static void SetColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    public static Mesh BladeMesh
    {
        get
        {
            if (bladeMesh != null) return bladeMesh;

            Vector3 tip = new Vector3(0f, 0f, 0.6f);
            Vector3 tail = new Vector3(0f, 0f, -0.4f);
            Vector3 l = new Vector3(-0.15f, 0f, 0.05f);
            Vector3 r = new Vector3(0.15f, 0f, 0.05f);
            Vector3 u = new Vector3(0f, 0.05f, 0.08f);
            Vector3 d = new Vector3(0f, -0.05f, 0.08f);

            Vector3[][] tris =
            {
                new[] { tip, u, r }, new[] { tip, r, d }, new[] { tip, d, l }, new[] { tip, l, u },
                new[] { tail, r, u }, new[] { tail, d, r }, new[] { tail, l, d }, new[] { tail, u, l },
            };

            Vector3[] verts = new Vector3[24];
            int[] idx = new int[24];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    verts[i * 3 + j] = tris[i][j];
                    idx[i * 3 + j] = i * 3 + j;
                }
            }

            bladeMesh = new Mesh { name = "ShardBlade", vertices = verts, triangles = idx };
            bladeMesh.RecalculateNormals();
            bladeMesh.RecalculateBounds();
            return bladeMesh;
        }
    }

    public static GameObject CreateBillboard(string name, Transform parent, Texture tex, Color color, float size, bool alwaysOnTop)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Object.Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.localScale = Vector3.one * size;
        Renderer r = q.GetComponent<Renderer>();
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sharedMaterial = CreateGlowMaterial(tex, color, alwaysOnTop);
        return q;
    }

    public static void Burst(Vector3 pos, Vector3 dir, Color color, int count, float speed, float size, float life, float cone, float gravity, bool streaks)
    {
        if (count <= 0) return;

        GameObject go = new GameObject("ShardBurst");
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir.sqrMagnitude > 0.0001f ? dir : Vector3.up));
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.2f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.45f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.35f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.7f));
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = cone;
        shape.radius = 0.05f;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.sharedMaterial = ParticleMaterial;
        psr.shadowCastingMode = ShadowCastingMode.Off;
        psr.receiveShadows = false;
        if (streaks)
        {
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.velocityScale = 0.035f;
            psr.lengthScale = 1.5f;
        }

        ps.Play();
    }

    public static void Ring(Vector3 pos, Color color, float startSize, float endSize, float duration, bool alwaysOnTop)
    {
        GameObject go = new GameObject("ShardRing");
        go.transform.position = pos;
        ShardFXFader f = go.AddComponent<ShardFXFader>();
        GameObject q = CreateBillboard("Quad", go.transform, RingTexture, color, startSize, alwaysOnTop);
        f.Init(q.transform, q.GetComponent<Renderer>().sharedMaterial, null, color, startSize, endSize, duration, 0f);
    }

    public static void Flash(Vector3 pos, Color color, float intensity, float range, float duration)
    {
        GameObject go = new GameObject("ShardFlash");
        go.transform.position = pos;
        Light pointLight = go.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = color;
        pointLight.range = range;
        pointLight.intensity = intensity;
        pointLight.shadows = LightShadows.None;
        ShardFXFader f = go.AddComponent<ShardFXFader>();
        f.Init(null, null, pointLight, color, 1f, 1f, duration, intensity);
    }

    public static void Play(AudioClip clip, Vector3 pos, float volume)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, pos, volume);
    }

    private static Project.Player.PlayerCamera cachedCamera;

    private static Project.Player.PlayerCamera PlayerCam
    {
        get
        {
            if (cachedCamera == null) cachedCamera = Object.FindFirstObjectByType<Project.Player.PlayerCamera>();
            return cachedCamera;
        }
    }

    public static void CameraShake(float strength, float duration)
    {
        if (PlayerCam != null) PlayerCam.Shake(strength, duration);
    }

    public static void CameraShakeAt(Vector3 pos, float strength, float duration, float falloff)
    {
        if (PlayerCam == null) return;
        float d = Vector3.Distance(PlayerCam.transform.position, pos);
        float k = Mathf.Clamp01(1f - d / Mathf.Max(0.01f, falloff));
        if (k > 0.05f) PlayerCam.Shake(strength * (0.35f + 0.65f * k), duration);
    }

    public static void CameraPunch(float fov, float hold)
    {
        if (PlayerCam != null) PlayerCam.PunchFov(fov, hold);
    }

    public static Material CreateLitMaterial(Color color, float metallic, float smoothness)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        Material m = new Material(s) { name = "WeaponMetal" };
        SetColor(m, color);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        return m;
    }
}

public class ShardFXFader : MonoBehaviour
{
    private Transform quad;
    private Material material;
    private Light pointLight;
    private Color color;
    private float startSize;
    private float endSize;
    private float duration;
    private float startIntensity;
    private float timer;

    public void Init(Transform quad, Material material, Light pointLight, Color color, float startSize, float endSize, float duration, float startIntensity)
    {
        this.quad = quad;
        this.material = material;
        this.pointLight = pointLight;
        this.color = color;
        this.startSize = startSize;
        this.endSize = endSize;
        this.duration = Mathf.Max(0.01f, duration);
        this.startIntensity = startIntensity;
    }

    private void LateUpdate()
    {
        timer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(timer / duration);
        float ease = 1f - (1f - t) * (1f - t);

        if (quad != null)
        {
            quad.localScale = Vector3.one * Mathf.Lerp(startSize, endSize, ease);
            Camera cam = Camera.main;
            if (cam != null) quad.rotation = cam.transform.rotation;
        }

        if (material != null)
        {
            Color c = color;
            c.a *= 1f - t;
            material.color = c;
        }

        if (pointLight != null) pointLight.intensity = startIntensity * (1f - ease);

        if (t >= 1f)
        {
            if (material != null) Destroy(material);
            Destroy(gameObject);
        }
    }
}
