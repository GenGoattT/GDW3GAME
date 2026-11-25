using Project.Audio;
using System.Collections;
using Project.Player;
using UnityEngine;
using UnityEngine.Events;

namespace Project.Tutorial
{
    public class TutorialGate : MonoBehaviour
    {
        [SerializeField] private float sinkDepth = 0f;
        [SerializeField] private float openTime = 1.1f;
        [SerializeField] private float rumbleStrength = 0.25f;
        [SerializeField] private bool hideWhenOpen = true;

        [Header("Inspector Hooks")]
        public UnityEvent onOpened;

        private Vector3 closedPosition;
        private bool open;

        public bool IsOpen => open;

        private void Awake()
        {
            closedPosition = transform.position;
        }

        public void Open()
        {
            if (open) return;
            open = true;
            StartCoroutine(Sink());
        }

        public void Close()
        {
            StopAllCoroutines();
            open = false;
            transform.position = closedPosition;
            SetSolid(true);
        }

        private IEnumerator Sink()
        {
            float depth = sinkDepth > 0f ? sinkDepth : MeasureHeight() + 0.5f;
            Vector3 target = closedPosition + Vector3.down * depth;

            GameAudio.Play(SfxId.GateOpen, closedPosition);
            PlayerCamera cam = FindFirstObjectByType<PlayerCamera>();
            if (cam != null) cam.Shake(rumbleStrength, openTime * 0.6f);

            for (float t = 0f; t < openTime; t += Time.deltaTime)
            {
                float k = t / openTime;
                float eased = k * k * (3f - 2f * k);
                Vector3 jitter = new Vector3(Mathf.Sin(t * 90f), 0f, Mathf.Cos(t * 77f)) * 0.04f * (1f - k);
                transform.position = Vector3.Lerp(closedPosition, target, eased) + jitter;
                yield return null;
            }

            transform.position = target;
            if (hideWhenOpen) SetSolid(false);
            onOpened?.Invoke();
        }

        private float MeasureHeight()
        {
            Collider col = GetComponentInChildren<Collider>();
            if (col != null) return col.bounds.size.y;

            Renderer rend = GetComponentInChildren<Renderer>();
            return rend != null ? rend.bounds.size.y : 5f;
        }

        private void SetSolid(bool solid)
        {
            foreach (Collider col in GetComponentsInChildren<Collider>(true)) col.enabled = solid;
            foreach (Renderer rend in GetComponentsInChildren<Renderer>(true)) rend.enabled = solid;
        }
    }
}
