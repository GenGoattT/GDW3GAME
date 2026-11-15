using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[ImageEffectAllowedInSceneView]
public class UltrakillstylePostEffect : MonoBehaviour
{
    public Shader shader;

    [Range(120, 1080)] public int targetHeight = 270;
    [Range(2, 64)] public float colorSteps = 24f;
    [Range(0f, 2f)] public float ditherStrength = 1f;
    [Range(0f, 2f)] public float saturation = 1.25f;
    [Range(0.5f, 2f)] public float contrast = 1.15f;
    [Range(0.5f, 2f)] public float brightness = 1.05f;
    [Range(0f, 3f)] public float vignetteStrength = 0.9f;
    public Color shadowTint = new Color(0.85f, 0.88f, 1f, 1f);
    public Color highlightTint = new Color(1f, 0.97f, 0.9f, 1f);

    Material material;
    Camera cam;

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        cam.allowMSAA = false;

        if (shader == null)
        {
            shader = Shader.Find("Hidden/Ultrakillstyle");
        }
    }

    void OnDisable()
    {
        if (material != null)
        {
            DestroyImmediate(material);
            material = null;
        }
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (shader == null || !shader.isSupported)
        {
            Graphics.Blit(source, destination);
            return;
        }

        if (material == null)
        {
            material = new Material(shader);
            material.hideFlags = HideFlags.HideAndDontSave;
        }

        int height = Mathf.Max(1, targetHeight);
        int width = Mathf.Max(1, Mathf.RoundToInt(height * ((float)source.width / source.height)));

        RenderTexture low = RenderTexture.GetTemporary(width, height, 0, source.format);
        low.filterMode = FilterMode.Point;
        source.filterMode = FilterMode.Point;
        Graphics.Blit(source, low);

        material.SetVector("_LowRes", new Vector4(width, height, 1f / width, 1f / height));
        material.SetFloat("_ColorSteps", colorSteps);
        material.SetFloat("_DitherStrength", ditherStrength);
        material.SetFloat("_Saturation", saturation);
        material.SetFloat("_Contrast", contrast);
        material.SetFloat("_Brightness", brightness);
        material.SetFloat("_VignetteStrength", vignetteStrength);
        material.SetColor("_ShadowTint", shadowTint);
        material.SetColor("_HighlightTint", highlightTint);

        Graphics.Blit(low, destination, material);
        RenderTexture.ReleaseTemporary(low);
    }
}
