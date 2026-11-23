using UnityEngine;

public class HitStop : MonoBehaviour
{
    private static HitStop instance;

    private bool active;
    private float until;
    private float scale;
    private float restore = 1f;

    public static void Do(float duration, float timeScale = 0.03f)
    {
        if (duration <= 0f) return;
        if (Time.timeScale <= 0f) return;

        if (instance == null)
        {
            GameObject go = new GameObject("HitStop");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<HitStop>();
        }

        instance.Begin(duration, timeScale);
    }

    private void Begin(float duration, float timeScale)
    {
        if (!active)
        {
            restore = Time.timeScale;
            active = true;
        }
        else if (!Mathf.Approximately(Time.timeScale, scale))
        {
            return;
        }

        scale = timeScale;
        until = Mathf.Max(until, Time.unscaledTime + duration);
        Time.timeScale = scale;
    }

    private void Update()
    {
        if (active && Time.unscaledTime >= until) End();
    }

    private void End()
    {
        active = false;
        if (Mathf.Approximately(Time.timeScale, scale)) Time.timeScale = restore;
    }

    private void OnDisable()
    {
        if (active) End();
    }
}
