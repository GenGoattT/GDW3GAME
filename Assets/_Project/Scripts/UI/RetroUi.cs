using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI
{
    public static class RetroUi
    {
        public const string DitherShaderName = "Ultrakillstyle/UIDither";
        private const string FontResourcePath = "Fonts & Materials/Oswald Bold SDF";

        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int EdgeXId = Shader.PropertyToID("_EdgeFadeX");
        private static readonly int EdgeYId = Shader.PropertyToID("_EdgeFadeY");
        private static readonly int ScanlineId = Shader.PropertyToID("_ScanlineStrength");

        public static Canvas CreateOverlayCanvas(Transform parent, string name, int sortingOrder)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void Stretch(RectTransform rect, float padX = 0f, float padY = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(-padX, -padY);
            rect.offsetMax = new Vector2(padX, padY);
        }

        public static Image CreateDitherImage(Transform parent, string name, Shader shader, Color color, float edgeX, float edgeY, float scanlines = 0.15f)
        {
            RectTransform rect = CreateRect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            if (shader == null) shader = Shader.Find(DitherShaderName);
            if (shader != null)
            {
                Material mat = new Material(shader) { name = name + " (Dither)" };
                mat.SetFloat(EdgeXId, edgeX);
                mat.SetFloat(EdgeYId, edgeY);
                mat.SetFloat(ScanlineId, scanlines);
                image.material = mat;
            }

            return image;
        }

        public static void SetFade(Graphic graphic, float fade)
        {
            if (graphic != null && graphic.material != null && graphic.material.HasProperty(FadeId))
                graphic.material.SetFloat(FadeId, fade);
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(parent, name);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font != null ? font : DefaultFont();
            text.fontSize = size;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.richText = true;
            return text;
        }

        public static void ApplyHardShadow(TextMeshProUGUI text, Color shadow, float offset)
        {
            Material mat = text.fontMaterial;
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, shadow);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, offset);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -offset);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.1f);
            text.fontMaterial = mat;
        }

        public static TMP_FontAsset DefaultFont()
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>(FontResourcePath);
            return font != null ? font : TMP_Settings.defaultFontAsset;
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }
}
