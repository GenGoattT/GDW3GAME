using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Effects
{
    public static class VFXTextures
    {
        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float Gauss(float x, float sigma)
        {
            return Mathf.Exp(-(x * x) / (2f * sigma * sigma));
        }

        private static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float abx = bx - ax;
            float aby = by - ay;
            float t = Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / (abx * abx + aby * aby));
            float cx = ax + abx * t - px;
            float cy = ay + aby * t - py;
            return Mathf.Sqrt(cx * cx + cy * cy);
        }

        private static Texture2D NewTexture(int size, string name)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
            tex.name = name;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Color32 ToPixel(float alpha)
        {
            byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
            return new Color32(255, 255, 255, a);
        }

        public static Texture2D CreateSoftDot(int size, float core, float power)
        {
            Texture2D tex = NewTexture(size, "SoftDot");
            Color32[] pixels = new Color32[size * size];
            float c = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c;
                    float dy = (y - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 1f - Smooth(core, 1f, d);
                    a = Mathf.Pow(Mathf.Clamp01(a), power);
                    pixels[y * size + x] = ToPixel(a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(true);
            return tex;
        }

        public static Texture2D CreateSnowflake(int size)
        {
            Texture2D tex = NewTexture(size, "Snowflake");
            Color32[] pixels = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            float sector = Mathf.PI / 3f;
            float spurAngle = 55f * Mathf.Deg2Rad;
            float spurCos = Mathf.Cos(spurAngle);
            float spurSin = Mathf.Sin(spurAngle);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c;
                    float dy = (y - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    if (d >= 1f)
                    {
                        pixels[y * size + x] = ToPixel(0f);
                        continue;
                    }

                    float angle = Mathf.Atan2(dy, dx);
                    float folded = Mathf.Abs(Mathf.Repeat(angle + sector * 0.5f, sector) - sector * 0.5f);
                    float along = d * Mathf.Cos(folded);
                    float across = d * Mathf.Sin(folded);

                    float arm = Gauss(across, 0.05f) * (1f - Smooth(0.7f, 0.92f, along));

                    float spurA = Gauss(SegmentDistance(along, across, 0.34f, 0f, 0.34f + spurCos * 0.3f, spurSin * 0.3f), 0.04f);
                    float spurB = Gauss(SegmentDistance(along, across, 0.58f, 0f, 0.58f + spurCos * 0.22f, spurSin * 0.22f), 0.04f);

                    float a = Mathf.Max(arm, Mathf.Max(spurA, spurB) * 0.85f);
                    a = Mathf.Max(a, Gauss(d, 0.11f));
                    a = Mathf.Max(a, Gauss(d, 0.4f) * 0.32f);
                    a *= 1f - Smooth(0.82f, 1f, d);

                    pixels[y * size + x] = ToPixel(a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(true);
            return tex;
        }
    }

    public static class VFXMaterials
    {
        public static Material CreateParticleMaterial(Texture2D texture, bool additive, string name)
        {
            Material material = new Material(FindShader(additive));
            material.name = name;
            material.mainTexture = texture;
            material.color = Color.white;
            return material;
        }

        private static Shader FindShader(bool additive)
        {
            Shader shader = null;

            if (GraphicsSettings.currentRenderPipeline == null)
            {
                shader = Shader.Find(additive ? "Legacy Shaders/Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended");
            }

            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            return shader;
        }
    }
}
