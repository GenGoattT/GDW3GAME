using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Effects
{
    public class WinterEnvironment : MonoBehaviour
    {
        [SerializeField] private bool applyOnStart = true;

        [Header("Fog")]
        [SerializeField] private bool applyFog = true;
        [SerializeField] private Color fogColor = new Color(0.72f, 0.8f, 0.88f, 1f);
        [SerializeField] private float fogDensity = 0.01f;

        [Header("Ambient")]
        [SerializeField] private bool applyAmbient = true;
        [SerializeField] private Color ambientSky = new Color(0.55f, 0.65f, 0.8f, 1f);
        [SerializeField] private Color ambientEquator = new Color(0.42f, 0.48f, 0.58f, 1f);
        [SerializeField] private Color ambientGround = new Color(0.25f, 0.28f, 0.34f, 1f);

        [Header("Camera")]
        [SerializeField] private bool applyCameraBackground = true;
        [SerializeField] private Camera targetCamera;

        [Header("Sun")]
        [SerializeField] private bool applySunTint = true;
        [SerializeField] private Light sun;
        [SerializeField] private Color sunColor = new Color(0.85f, 0.9f, 1f, 1f);

        private void Start()
        {
            if (applyOnStart) Apply();
        }

        [ContextMenu("Apply Now")]
        public void Apply()
        {
            if (applyFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity;
            }

            if (applyAmbient)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = ambientSky;
                RenderSettings.ambientEquatorColor = ambientEquator;
                RenderSettings.ambientGroundColor = ambientGround;
            }

            if (applyCameraBackground)
            {
                Camera cam = targetCamera != null ? targetCamera : Camera.main;
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = fogColor;
                }
            }

            if (applySunTint)
            {
                Light light = sun != null ? sun : RenderSettings.sun;
                if (light != null) light.color = sunColor;
            }
        }
    }
}
