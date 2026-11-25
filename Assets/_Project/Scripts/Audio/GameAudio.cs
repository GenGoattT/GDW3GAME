using System.Collections.Generic;
using UnityEngine;

namespace Project.Audio
{
    public class GameAudio : MonoBehaviour
    {
        private const string BankResource = "Audio/SoundBank";
        private const int PoolSize = 24;

        private static GameAudio instance;

        private readonly List<AudioSource> pool = new List<AudioSource>();
        private readonly Dictionary<SfxId, int> lastPick = new Dictionary<SfxId, int>();
        private readonly Dictionary<SfxId, float> lastPlayed = new Dictionary<SfxId, float>();
        private SoundBank bank;
        private int cursor;

        private static GameAudio Instance
        {
            get
            {
                if (instance != null) return instance;

                GameObject go = new GameObject("GameAudio");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<GameAudio>();
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        private void Awake()
        {
            bank = Resources.Load<SoundBank>(BankResource);
            if (bank == null) Debug.LogWarning("GameAudio: no SoundBank found at Resources/" + BankResource);

            for (int i = 0; i < PoolSize; i++)
            {
                AudioSource source = new GameObject("Voice " + i).AddComponent<AudioSource>();
                source.transform.SetParent(transform, false);
                source.playOnAwake = false;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.dopplerLevel = 0f;
                pool.Add(source);
            }
        }

        public static bool TryGetEntry(SfxId id, out SoundBank.Entry entry)
        {
            entry = null;
            GameAudio audio = Instance;
            return audio.bank != null && audio.bank.TryGet(id, out entry);
        }

        public static void Play(SfxId id) => Play(null, id, Vector3.zero, 1f, false);

        public static void Play(SfxId id, Vector3 position, float volumeScale = 1f) => Play(null, id, position, volumeScale, true);

        public static void Play(AudioClip overrideClip, SfxId id, Vector3 position, float volumeScale = 1f)
            => Play(overrideClip, id, position, volumeScale, true);

        private static void Play(AudioClip overrideClip, SfxId id, Vector3 position, float volumeScale, bool hasPosition)
        {
            GameAudio audio = Instance;
            SoundBank.Entry entry = null;
            bool known = audio.bank != null && audio.bank.TryGet(id, out entry);

            if (known && entry.cooldown > 0f)
            {
                if (audio.lastPlayed.TryGetValue(id, out float at) && Time.unscaledTime - at < entry.cooldown) return;
                audio.lastPlayed[id] = Time.unscaledTime;
            }

            AudioClip clip = overrideClip != null ? overrideClip : known ? audio.Pick(id, entry.clips) : null;
            if (clip == null) return;

            AudioSource source = audio.NextSource();
            bool spatial = hasPosition && known && entry.spatial;

            source.transform.position = spatial ? position : Vector3.zero;
            source.spatialBlend = spatial ? 1f : 0f;
            source.maxDistance = known ? entry.maxDistance : 80f;
            source.minDistance = 4f;
            source.pitch = known ? Random.Range(entry.pitch.x, entry.pitch.y) : 1f;
            source.volume = (known ? entry.volume : 1f) * volumeScale;
            source.clip = clip;
            source.Play();
        }

        private AudioClip Pick(SfxId id, AudioClip[] clips)
        {
            if (clips.Length == 1) return clips[0];

            lastPick.TryGetValue(id, out int last);
            int pick = Random.Range(0, clips.Length - 1);
            if (pick >= last) pick++;
            lastPick[id] = pick;
            return clips[pick];
        }

        private AudioSource NextSource()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                AudioSource candidate = pool[(cursor + i) % pool.Count];
                if (candidate.isPlaying) continue;

                cursor = (cursor + i + 1) % pool.Count;
                return candidate;
            }

            AudioSource oldest = pool[cursor];
            cursor = (cursor + 1) % pool.Count;
            return oldest;
        }
    }
}
