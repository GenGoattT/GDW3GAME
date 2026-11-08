using System.Collections.Generic;
using UnityEngine;
using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;
using MinMaxGradient = UnityEngine.ParticleSystem.MinMaxGradient;

namespace Project.Effects
{
    public class SnowfallController : MonoBehaviour
    {
        [System.Serializable]
        public class Layer
        {
            public string label = "Layer";
            public int targetCount = 800;
            public float areaSize = 50f;
            public float columnHeight = 26f;
            public Vector2 sizeRange = new Vector2(0.12f, 0.22f);
            public Vector2 fallSpeedRange = new Vector2(1.4f, 2.4f);
            public Vector2 lifetimeRange = new Vector2(6f, 9f);
            public float alpha = 0.7f;
            public float sway = 0.6f;
            public float swayFrequency = 0.2f;
            public float windScale = 1f;
            public float spin = 0f;
            public float maxScreenSize = 0.03f;
            public bool useFlakeTexture = false;
            public bool collide = false;
        }

        private class LayerRuntime
        {
            public Layer config;
            public ParticleSystem system;
            public Transform root;
            public float baseRate;
        }

        [Header("References")]
        [SerializeField] private Transform follow;
        [SerializeField] private Material dotMaterial;
        [SerializeField] private Material flakeMaterial;

        [Header("Look")]
        [SerializeField, Range(0f, 2f)] private float intensity = 1f;
        [SerializeField] private Color snowColor = new Color(0.96f, 0.98f, 1f, 1f);
        [SerializeField] private float verticalBelow = 6f;

        [Header("Wind")]
        [SerializeField] private Vector3 windDirection = new Vector3(1f, 0f, 0.35f);
        [SerializeField] private float windStrength = 1.4f;
        [SerializeField, Range(0f, 1f)] private float gustAmount = 0.6f;
        [SerializeField] private float gustSpeed = 0.11f;

        [Header("Following")]
        [SerializeField] private float leadTime = 1f;
        [SerializeField] private float maxLead = 14f;

        [Header("Shelter")]
        [SerializeField] private bool reduceUnderCover = true;
        [SerializeField] private float shelterCheckHeight = 18f;
        [SerializeField, Range(0f, 1f)] private float shelteredAmount = 0.08f;
        [SerializeField] private LayerMask shelterMask = ~0;

        [Header("Collision")]
        [SerializeField] private LayerMask collisionMask = ~0;

        [Header("Layers")]
        [SerializeField] private Layer[] layers = DefaultLayers();

        private readonly List<LayerRuntime> runtimes = new List<LayerRuntime>();
        private Texture2D ownedDotTexture;
        private Texture2D ownedFlakeTexture;
        private Material ownedDotMaterial;
        private Material ownedFlakeMaterial;

        private Vector3 lastFollowPosition;
        private Vector3 smoothedVelocity;
        private float shelter = 1f;
        private float shelterTarget = 1f;
        private float shelterTimer;

        public float Intensity
        {
            get { return intensity; }
            set { intensity = Mathf.Clamp(value, 0f, 2f); }
        }

        private static Layer[] DefaultLayers()
        {
            return new Layer[]
            {
                new Layer
                {
                    label = "Far",
                    targetCount = 1500,
                    areaSize = 90f,
                    columnHeight = 34f,
                    sizeRange = new Vector2(0.07f, 0.14f),
                    fallSpeedRange = new Vector2(1f, 1.8f),
                    lifetimeRange = new Vector2(8f, 12f),
                    alpha = 0.6f,
                    sway = 0.35f,
                    swayFrequency = 0.15f,
                    windScale = 1f,
                    maxScreenSize = 0.012f
                },
                new Layer
                {
                    label = "Mid",
                    targetCount = 700,
                    areaSize = 55f,
                    columnHeight = 26f,
                    sizeRange = new Vector2(0.12f, 0.22f),
                    fallSpeedRange = new Vector2(1.4f, 2.4f),
                    lifetimeRange = new Vector2(6f, 9f),
                    alpha = 0.75f,
                    sway = 0.6f,
                    swayFrequency = 0.2f,
                    windScale = 1.1f,
                    maxScreenSize = 0.03f,
                    collide = true
                },
                new Layer
                {
                    label = "Near",
                    targetCount = 110,
                    areaSize = 24f,
                    columnHeight = 18f,
                    sizeRange = new Vector2(0.28f, 0.55f),
                    fallSpeedRange = new Vector2(2f, 3.2f),
                    lifetimeRange = new Vector2(4f, 6f),
                    alpha = 0.4f,
                    sway = 0.9f,
                    swayFrequency = 0.25f,
                    windScale = 1.25f,
                    spin = 60f,
                    maxScreenSize = 0.07f,
                    useFlakeTexture = true,
                    collide = true
                }
            };
        }

        private void Awake()
        {
            if (follow == null)
            {
                PlayerCameraAnchor();
            }

            if (follow != null)
            {
                lastFollowPosition = follow.position;
                transform.position = follow.position;
            }

            EnsureMaterials();

            for (int i = 0; i < layers.Length; i++)
            {
                BuildLayer(layers[i], i);
            }
        }

        private void PlayerCameraAnchor()
        {
            Project.Player.PlayerMovement player = FindFirstObjectByType<Project.Player.PlayerMovement>();
            if (player != null)
            {
                follow = player.transform;
                return;
            }

            if (Camera.main != null) follow = Camera.main.transform;
        }

        private void EnsureMaterials()
        {
            if (dotMaterial == null)
            {
                ownedDotTexture = VFXTextures.CreateSoftDot(64, 0.02f, 1.6f);
                ownedDotMaterial = VFXMaterials.CreateParticleMaterial(ownedDotTexture, false, "SnowDot");
                dotMaterial = ownedDotMaterial;
            }

            if (flakeMaterial == null)
            {
                ownedFlakeTexture = VFXTextures.CreateSnowflake(128);
                ownedFlakeMaterial = VFXMaterials.CreateParticleMaterial(ownedFlakeTexture, false, "SnowFlake");
                flakeMaterial = ownedFlakeMaterial;
            }
        }

        private void BuildLayer(Layer layer, int index)
        {
            GameObject go = new GameObject("Snow_" + layer.label);
            go.transform.SetParent(transform, false);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float meanLife = (layer.lifetimeRange.x + layer.lifetimeRange.y) * 0.5f;

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = new MinMaxCurve(layer.lifetimeRange.x, layer.lifetimeRange.y);
            main.startSpeed = 0f;
            main.startSize = new MinMaxCurve(layer.sizeRange.x, layer.sizeRange.y);
            main.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0f;
            main.maxParticles = Mathf.CeilToInt(layer.targetCount * 1.6f);

            Color high = snowColor;
            high.a = layer.alpha;
            Color low = snowColor;
            low.a = layer.alpha * 0.45f;
            main.startColor = new MinMaxGradient(low, high);

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            float baseRate = layer.targetCount / meanLife;
            emission.rateOverTime = baseRate * intensity;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(layer.areaSize, layer.columnHeight, layer.areaSize);
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            ApplyVelocity(velocity, layer, Vector3.zero);

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.separateAxes = true;
            noise.strengthX = layer.sway;
            noise.strengthY = layer.sway * 0.25f;
            noise.strengthZ = layer.sway;
            noise.frequency = layer.swayFrequency;
            noise.scrollSpeed = 0.3f;
            noise.damping = true;
            noise.octaveCount = 2;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            if (layer.spin > 0f)
            {
                ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
                rotation.enabled = true;
                float radians = layer.spin * Mathf.Deg2Rad;
                rotation.z = new MinMaxCurve(-radians, radians);
            }

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.1f),
                    new GradientAlphaKey(1f, 0.78f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new MinMaxGradient(fade);

            if (layer.collide)
            {
                ParticleSystem.CollisionModule collision = ps.collision;
                collision.enabled = true;
                collision.type = ParticleSystemCollisionType.World;
                collision.mode = ParticleSystemCollisionMode.Collision3D;
                collision.quality = ParticleSystemCollisionQuality.Low;
                collision.collidesWith = collisionMask;
                collision.dampen = 1f;
                collision.bounce = 0f;
                collision.lifetimeLoss = 1f;
                collision.radiusScale = 0.2f;
                collision.maxCollisionShapes = 128;
                collision.enableDynamicColliders = false;
                collision.sendCollisionMessages = false;
            }

            ParticleSystemRenderer rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.sharedMaterial = layer.useFlakeTexture ? flakeMaterial : dotMaterial;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            rend.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            rend.maxParticleSize = layer.maxScreenSize;
            rend.sortingOrder = index;

            LayerRuntime runtime = new LayerRuntime();
            runtime.config = layer;
            runtime.system = ps;
            runtime.root = go.transform;
            runtime.baseRate = baseRate;
            runtimes.Add(runtime);

            go.transform.localPosition = new Vector3(0f, layer.columnHeight * 0.5f - verticalBelow, 0f);
            ps.Play();
        }

        private static void ApplyVelocity(ParticleSystem.VelocityOverLifetimeModule velocity, Layer layer, Vector3 wind)
        {
            float scaled = layer.windScale;
            float xa = wind.x * scaled * 0.8f;
            float xb = wind.x * scaled * 1.2f;
            float za = wind.z * scaled * 0.8f;
            float zb = wind.z * scaled * 1.2f;

            velocity.x = new MinMaxCurve(Mathf.Min(xa, xb), Mathf.Max(xa, xb));
            velocity.y = new MinMaxCurve(-layer.fallSpeedRange.y, -layer.fallSpeedRange.x);
            velocity.z = new MinMaxCurve(Mathf.Min(za, zb), Mathf.Max(za, zb));
        }

        private void LateUpdate()
        {
            if (follow == null) return;

            float dt = Time.deltaTime;
            Vector3 position = follow.position;

            if (dt > 0.0001f)
            {
                Vector3 v = (position - lastFollowPosition) / dt;
                v.y = 0f;
                if (v.sqrMagnitude > 2500f) v = Vector3.zero;
                smoothedVelocity = Vector3.Lerp(smoothedVelocity, v, 1f - Mathf.Exp(-6f * dt));
            }

            lastFollowPosition = position;
            transform.position = position;

            UpdateShelter(position, dt);

            Vector3 lead = Vector3.ClampMagnitude(smoothedVelocity * leadTime, maxLead);
            Vector3 wind = CurrentWind();

            for (int i = 0; i < runtimes.Count; i++)
            {
                LayerRuntime rt = runtimes[i];
                Layer layer = rt.config;

                Vector3 layerLead = Vector3.ClampMagnitude(lead, layer.areaSize * 0.3f);
                rt.root.localPosition = new Vector3(layerLead.x, layer.columnHeight * 0.5f - verticalBelow, layerLead.z);

                ParticleSystem.VelocityOverLifetimeModule velocity = rt.system.velocityOverLifetime;
                ApplyVelocity(velocity, layer, wind);

                ParticleSystem.EmissionModule emission = rt.system.emission;
                emission.rateOverTime = rt.baseRate * intensity * shelter;
            }
        }

        private Vector3 CurrentWind()
        {
            Vector3 dir = new Vector3(windDirection.x, 0f, windDirection.z);
            if (dir.sqrMagnitude < 0.0001f) return Vector3.zero;

            float gust = Mathf.PerlinNoise(Time.time * gustSpeed, 3.7f);
            float strength = windStrength * Mathf.Lerp(1f - gustAmount, 1f + gustAmount, gust);
            return dir.normalized * strength;
        }

        private void UpdateShelter(Vector3 position, float dt)
        {
            if (!reduceUnderCover)
            {
                shelter = 1f;
                return;
            }

            shelterTimer -= dt;
            if (shelterTimer <= 0f)
            {
                shelterTimer = 0.2f;
                bool covered = Physics.Raycast(position + Vector3.up * 1.2f, Vector3.up, shelterCheckHeight, shelterMask, QueryTriggerInteraction.Ignore);
                shelterTarget = covered ? shelteredAmount : 1f;
            }

            shelter = Mathf.MoveTowards(shelter, shelterTarget, dt * 1.5f);
        }

        private void OnDestroy()
        {
            if (ownedDotMaterial != null) Destroy(ownedDotMaterial);
            if (ownedFlakeMaterial != null) Destroy(ownedFlakeMaterial);
            if (ownedDotTexture != null) Destroy(ownedDotTexture);
            if (ownedFlakeTexture != null) Destroy(ownedFlakeTexture);
        }
    }
}
