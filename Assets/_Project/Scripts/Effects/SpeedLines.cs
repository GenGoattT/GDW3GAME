using UnityEngine;
using Project.Player;
using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;
using MinMaxGradient = UnityEngine.ParticleSystem.MinMaxGradient;

namespace Project.Effects
{
    public class SpeedLines : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMovement player;
        [SerializeField] private Material lineMaterial;

        [Header("Look")]
        [SerializeField] private Color lineColor = new Color(0.82f, 0.93f, 1f, 0.9f);
        [SerializeField] private Vector2 lineWidth = new Vector2(0.02f, 0.05f);
        [SerializeField] private float lineLength = 40f;
        [SerializeField] private float velocityStretch = 0.03f;
        [SerializeField] private Vector2 lifetime = new Vector2(0.16f, 0.28f);
        [SerializeField] private Vector2 travelSpeed = new Vector2(30f, 55f);

        [Header("Ring Shape")]
        [SerializeField] private float ringDistance = 6f;
        [SerializeField] private float ringRadius = 7f;
        [SerializeField, Range(0.1f, 1f)] private float ringThickness = 0.7f;
        [SerializeField] private float ringJitter = 1.5f;

        [Header("Response")]
        [SerializeField] private float dashIntensity = 1f;
        [SerializeField] private float slideIntensity = 0.7f;
        [SerializeField] private float fadeInSpeed = 40f;
        [SerializeField] private float fadeOutSpeed = 9f;
        [SerializeField] private float maxEmissionRate = 320f;
        [SerializeField] private int dashBurstCount = 70;
        [SerializeField] private int slideBurstCount = 30;
        [SerializeField] private int maxParticles = 600;

        private ParticleSystem system;
        private Camera viewCamera;
        private Texture2D ownedTexture;
        private Material ownedMaterial;
        private float intensity;
        private bool wasDashing;
        private bool wasSliding;
        private float lastAspect;

        private void Awake()
        {
            viewCamera = GetComponent<Camera>();
            if (viewCamera == null) viewCamera = GetComponentInParent<Camera>();

            if (player == null) player = FindFirstObjectByType<PlayerMovement>();

            if (lineMaterial == null)
            {
                ownedTexture = VFXTextures.CreateSoftDot(64, 0.05f, 1.5f);
                ownedMaterial = VFXMaterials.CreateParticleMaterial(ownedTexture, true, "SpeedLine");
                lineMaterial = ownedMaterial;
            }

            Build();
        }

        private void Build()
        {
            GameObject go = new GameObject("SpeedLinesFX");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = new MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new MinMaxCurve(1f, 4f);
            main.startSize = new MinMaxCurve(lineWidth.x, lineWidth.y);
            main.startRotation = 0f;
            main.gravityModifier = 0f;
            main.maxParticles = maxParticles;

            Color dim = lineColor;
            dim.a = lineColor.a * 0.35f;
            main.startColor = new MinMaxGradient(dim, lineColor);

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = ringRadius;
            shape.radiusThickness = ringThickness;
            shape.arc = 360f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
            shape.position = new Vector3(0f, 0f, ringDistance);
            shape.rotation = Vector3.zero;
            shape.randomPositionAmount = ringJitter;
            shape.scale = new Vector3(CurrentAspect(), 1f, 1f);

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new MinMaxCurve(0f, 0f);
            velocity.y = new MinMaxCurve(0f, 0f);
            velocity.z = new MinMaxCurve(-travelSpeed.y, -travelSpeed.x);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
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
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(1f, 0.65f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new MinMaxGradient(fade);

            ParticleSystemRenderer rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.lengthScale = lineLength;
            rend.velocityScale = velocityStretch;
            rend.cameraVelocityScale = 0f;
            rend.sharedMaterial = lineMaterial;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            rend.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            rend.sortingOrder = 100;

            system.Play();
        }

        private float CurrentAspect()
        {
            float aspect = viewCamera != null ? viewCamera.aspect : 1.78f;
            lastAspect = Mathf.Clamp(aspect, 1f, 2.4f);
            return lastAspect;
        }

        private void Update()
        {
            if (system == null) return;

            bool dashing = player != null && player.IsDashing;
            bool sliding = player != null && player.IsSliding;

            float target = 0f;
            if (dashing) target = dashIntensity;
            else if (sliding) target = slideIntensity;

            float rate = target > intensity ? fadeInSpeed : fadeOutSpeed;
            intensity = Mathf.MoveTowards(intensity, target, rate * Time.unscaledDeltaTime);

            if (dashing && !wasDashing)
            {
                system.Emit(Mathf.RoundToInt(dashBurstCount * dashIntensity));
            }
            else if (sliding && !wasSliding && !dashing)
            {
                system.Emit(Mathf.RoundToInt(slideBurstCount * slideIntensity));
            }

            wasDashing = dashing;
            wasSliding = sliding;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = maxEmissionRate * intensity;

            if (viewCamera != null && !Mathf.Approximately(Mathf.Clamp(viewCamera.aspect, 1f, 2.4f), lastAspect))
            {
                ParticleSystem.ShapeModule shape = system.shape;
                shape.scale = new Vector3(CurrentAspect(), 1f, 1f);
            }
        }

        private void OnDestroy()
        {
            if (ownedMaterial != null) Destroy(ownedMaterial);
            if (ownedTexture != null) Destroy(ownedTexture);
        }
    }
}
