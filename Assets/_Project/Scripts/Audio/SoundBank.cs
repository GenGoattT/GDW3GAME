using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Audio
{
    public enum SfxId
    {
        None = 0,
        Footstep = 1,
        Jump = 2,
        Land = 3,
        Dash = 4,
        Slide = 5,
        WallJump = 6,
        WeaponSwap = 7,
        SlideLoop = 8,
        PistolFire = 20,
        ShotgunFire = 21,
        ShardThrow = 22,
        ShardZip = 23,
        ShardMelee = 24,
        ShardKill = 25,
        StakeFire = 26,
        StakeHitEnemy = 27,
        StakeHitWall = 28,
        SnapTension = 29,
        SnapImpact = 30,
        SnapKill = 31,
        Tripwire = 32,
        GleipnirEquip = 33,
        ShardEmbed = 34,
        ShardWall = 35,
        ShardExpire = 36,
        EnemyHit = 40,
        EnemyDeath = 41,
        PlayerHurt = 42,
        IceThrow = 50,
        IceShatter = 51,
        ClubSwing = 52,
        ClubSlam = 53,
        GateOpen = 60,
        WallSlam = 63,
        ObjectiveStart = 61,
        ObjectiveComplete = 62
    }

    [CreateAssetMenu(menuName = "Stories End/Audio/Sound Bank", fileName = "SoundBank")]
    public class SoundBank : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public SfxId id;
            public AudioClip[] clips = new AudioClip[0];
            [Range(0f, 1.5f)] public float volume = 1f;
            public Vector2 pitch = new Vector2(0.95f, 1.05f);
            public bool spatial;
            public float maxDistance = 80f;
            public float cooldown;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<SfxId, Entry> lookup;

        public bool TryGet(SfxId id, out Entry entry)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<SfxId, Entry>();
                foreach (Entry e in entries)
                {
                    if (e != null) lookup[e.id] = e;
                }
            }

            return lookup.TryGetValue(id, out entry) && entry.clips != null && entry.clips.Length > 0;
        }

        private void OnValidate()
        {
            lookup = null;
        }
    }
}
