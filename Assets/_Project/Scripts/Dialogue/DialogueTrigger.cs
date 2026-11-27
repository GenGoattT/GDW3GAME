using System.Collections;
using Project.Tutorial;
using UnityEngine;
using UnityEngine.Events;

namespace Project.Dialogue
{
    public class DialogueTrigger : PlayerZone
    {
        public enum ReplayRule
        {
            EveryTime,
            OncePerLife,
            OncePerSession
        }

        [SerializeField] private DialogueSequence sequence;
        [SerializeField] private ReplayRule replay = ReplayRule.OncePerSession;
        [SerializeField] private bool playOnStart;
        [SerializeField, Min(0f)] private float delay;

        [Header("Conditions")]
        [SerializeField] private string requiresFlag;
        [SerializeField] private string setsFlagWhenDone;
        [SerializeField] private string memoryKey;

        [Header("Inspector Hooks")]
        public UnityEvent onFinished;

        private bool playedThisLife;

        protected override Color GizmoColor => new Color(0.35f, 0.8f, 1f);

        private string Key => gameObject.scene.name + "/" + (string.IsNullOrEmpty(memoryKey) ? name : memoryKey);

        public DialogueSequence Sequence
        {
            get => sequence;
            set => sequence = value;
        }

        private void Start()
        {
            if (playOnStart) Play();
        }

        protected override void OnPlayerEntered()
        {
            if (!playOnStart) Play();
        }

        public void Play()
        {
            if (AlreadyHeardThisSession())
            {
                playedThisLife = true;
                Finish();
                return;
            }

            if (!CanPlay()) return;

            playedThisLife = true;
            if (replay == ReplayRule.OncePerSession) DialogueMemory.MarkHeard(Key);

            if (delay > 0f) StartCoroutine(PlayAfterDelay());
            else Send();
        }

        public void PlayIgnoringRules()
        {
            Send();
        }

        private bool AlreadyHeardThisSession()
        {
            return replay == ReplayRule.OncePerSession
                && !playedThisLife
                && DialogueMemory.HasFlag(requiresFlag)
                && DialogueMemory.HasHeard(Key);
        }

        private bool CanPlay()
        {
            if (!DialogueMemory.HasFlag(requiresFlag)) return false;

            switch (replay)
            {
                case ReplayRule.OncePerLife: return !playedThisLife;
                case ReplayRule.OncePerSession: return !playedThisLife && !DialogueMemory.HasHeard(Key);
                default: return true;
            }
        }

        private IEnumerator PlayAfterDelay()
        {
            yield return new WaitForSecondsRealtime(delay);
            Send();
        }

        private void Send()
        {
            DialogueDirector.Resolve().Play(sequence, Finish);
        }

        private void Finish()
        {
            DialogueMemory.SetFlag(setsFlagWhenDone);
            onFinished?.Invoke();
        }
    }
}
