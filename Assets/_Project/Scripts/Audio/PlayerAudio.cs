using Project.Combat;
using Project.Player;
using Project.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Audio
{
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerAudio : MonoBehaviour
    {
        [SerializeField] private float strideLength = 2.6f;
        [SerializeField] private float minStepSpeed = 2f;
        [SerializeField] private float minAirTimeForLand = 0.25f;
        [SerializeField] private float slideFadeIn = 12f;
        [SerializeField] private float slideFadeOut = 8f;

        private PlayerMovement movement;
        private Health health;
        private float strideProgress;
        private float airTime;
        private bool wasGrounded = true;
        private AudioSource slideSource;
        private float slideVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachToPlayers();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToPlayers();

        private static void AttachToPlayers()
        {
            foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
            {
                if (player.GetComponent<PlayerAudio>() == null) player.gameObject.AddComponent<PlayerAudio>();
            }
        }

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            health = GetComponent<Health>();
            BuildSlideSource();
        }

        private void BuildSlideSource()
        {
            if (!GameAudio.TryGetEntry(SfxId.SlideLoop, out SoundBank.Entry entry)) return;

            slideSource = gameObject.AddComponent<AudioSource>();
            slideSource.clip = entry.clips[0];
            slideSource.loop = true;
            slideSource.playOnAwake = false;
            slideSource.spatialBlend = 0f;
            slideSource.volume = 0f;
            slideVolume = entry.volume;
        }

        private void TickSlideLoop()
        {
            if (slideSource == null) return;

            bool sliding = movement.IsSliding && movement.IsGrounded;
            float target = sliding ? slideVolume * Mathf.Clamp01(movement.HorizontalVelocity.magnitude / 14f) : 0f;
            float rate = sliding ? slideFadeIn : slideFadeOut;
            slideSource.volume = Mathf.MoveTowards(slideSource.volume, target, rate * slideVolume * Time.deltaTime);

            if (slideSource.volume > 0.001f && !slideSource.isPlaying) slideSource.Play();
            else if (slideSource.volume <= 0.001f && slideSource.isPlaying) slideSource.Stop();
        }

        private void OnEnable()
        {
            ActionFeed.Raised += OnAction;
            if (health != null) health.Damaged += OnHurt;
        }

        private void OnDisable()
        {
            ActionFeed.Raised -= OnAction;
            if (health != null) health.Damaged -= OnHurt;
        }

        private void OnAction(PlayerAction action, Object source)
        {
            switch (action)
            {
                case PlayerAction.Jump: GameAudio.Play(SfxId.Jump); break;
                case PlayerAction.Dash: GameAudio.Play(SfxId.Dash); break;
                case PlayerAction.Slide: GameAudio.Play(SfxId.Slide); break;
                case PlayerAction.WallJump: GameAudio.Play(SfxId.WallJump); break;
                case PlayerAction.SwapWeapon: GameAudio.Play(SfxId.WeaponSwap); break;
            }
        }

        private void OnHurt(float amount, Vector3 point, Vector3 direction)
        {
            GameAudio.Play(SfxId.PlayerHurt);
        }

        private void Update()
        {
            bool grounded = movement.IsGrounded;

            if (!grounded)
            {
                airTime += Time.deltaTime;
            }
            else if (!wasGrounded)
            {
                if (airTime >= minAirTimeForLand)
                {
                    GameAudio.Play(SfxId.Land, transform.position, Mathf.Clamp01(airTime / 1.2f) * 0.6f + 0.4f);
                    strideProgress = 0f;
                }
                airTime = 0f;
            }

            wasGrounded = grounded;
            TickFootsteps(grounded);
            TickSlideLoop();
        }

        private void TickFootsteps(bool grounded)
        {
            float speed = movement.HorizontalVelocity.magnitude;
            if (!grounded || movement.IsSliding || movement.IsDashing || speed < minStepSpeed)
            {
                strideProgress = Mathf.Min(strideProgress, strideLength * 0.6f);
                return;
            }

            strideProgress += speed * Time.deltaTime;
            if (strideProgress < strideLength) return;

            strideProgress -= strideLength;
            GameAudio.Play(SfxId.Footstep, transform.position, Mathf.Clamp(speed / 12f, 0.5f, 1f));
        }
    }
}
