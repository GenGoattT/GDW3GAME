using System.Collections;
using Project.Dialogue;
using UnityEngine;

namespace Project.Audio
{
    public class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip track;
        [SerializeField, Range(0f, 1f)] private float volume = 0.12f;
        [SerializeField] private float fadeInTime = 2f;
        [SerializeField] private bool playOnStart = true;

        [Header("Ducking")]
        [SerializeField, Range(0f, 1f)] private float duckWhileTalking = 0.5f;
        [SerializeField] private float duckSpeed = 3f;

        private AudioSource source;
        private float duck = 1f;
        private bool fading;

        public AudioClip Track => track;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.ignoreListenerPause = true;
        }

        private void Start()
        {
            if (playOnStart) Play(track);
        }

        public void Play(AudioClip clip)
        {
            if (clip == null) return;

            StopAllCoroutines();
            source.clip = clip;
            source.Play();
            StartCoroutine(FadeTo(volume, fadeInTime));
        }

        public void Stop(float fadeOut = 1.5f)
        {
            StopAllCoroutines();
            StartCoroutine(FadeTo(0f, fadeOut, true));
        }

        public void SetVolume(float value)
        {
            volume = Mathf.Clamp01(value);
            if (!fading) source.volume = volume * duck;
        }

        private void Update()
        {
            DialogueDirector director = DialogueDirector.Instance;
            float target = director != null && director.IsBusy ? duckWhileTalking : 1f;
            duck = Mathf.MoveTowards(duck, target, duckSpeed * Time.unscaledDeltaTime);

            if (!fading && source.isPlaying) source.volume = volume * duck;
        }

        private IEnumerator FadeTo(float target, float time, bool stopAfter = false)
        {
            fading = true;
            float start = source.volume;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(start, target * duck, t / time);
                yield return null;
            }

            source.volume = target * duck;
            fading = false;
            if (stopAfter) source.Stop();
        }
    }
}
