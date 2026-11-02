using UnityEngine;

public class BulletTracer : MonoBehaviour
{
    private static Material fallbackMaterial;

    private LineRenderer line;
    private Color color;
    private float startWidth;
    private float duration;
    private float timer;

    public static void Spawn(Vector3 start, Vector3 end, Material material, Color color, float width, float duration)
    {
        GameObject go = new GameObject("BulletTracer");
        BulletTracer tracer = go.AddComponent<BulletTracer>();
        tracer.Init(start, end, material, color, width, duration);
    }

    private void Init(Vector3 start, Vector3 end, Material material, Color tracerColor, float width, float tracerDuration)
    {
        color = tracerColor;
        startWidth = width;
        duration = Mathf.Max(0.01f, tracerDuration);
        timer = 0f;

        if (material == null)
        {
            if (fallbackMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null) fallbackMaterial = new Material(shader);
            }
            material = fallbackMaterial;
        }

        line = gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        if (material != null) line.sharedMaterial = material;

        ApplyLook(1f);
    }

    private void Update()
    {
        timer += Time.deltaTime;
        float t = timer / duration;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        ApplyLook(1f - t);
    }

    private void ApplyLook(float strength)
    {
        Color c = color;
        c.a *= strength;
        line.startColor = c;
        line.endColor = c;
        line.startWidth = startWidth * strength;
        line.endWidth = startWidth * strength;
    }
}
