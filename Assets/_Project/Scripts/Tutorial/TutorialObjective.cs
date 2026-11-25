using System.Collections;
using System.Collections.Generic;
using Project.Combat;
using Project.Dialogue;
using Project.Enemies;
using UnityEngine;
using UnityEngine.Events;

namespace Project.Tutorial
{
    public class TutorialObjective : PlayerZone
    {
        public enum BeginMode
        {
            OnZoneEnter,
            OnSceneStart,
            Manual
        }

        public enum PromptTiming
        {
            Immediately,
            WhenBriefingStarts,
            AfterBriefing
        }

        private enum Phase
        {
            Idle,
            Briefing,
            Active,
            Done
        }

        [Header("Objective")]
        [SerializeField] private string title = "DASH";
        [SerializeField] private string keyHint = "LSHIFT";
        [SerializeField] private PlayerAction action = PlayerAction.Dash;
        [SerializeField, Min(1)] private int requiredCount = 1;
        [SerializeField] private BeginMode beginOn = BeginMode.OnZoneEnter;
        [SerializeField] private PromptTiming showPrompt = PromptTiming.WhenBriefingStarts;
        [SerializeField, Min(0f)] private float promptDelay = 0.3f;

        [Header("Kill Targets")]
        [SerializeField] private List<BaseEnemy> killTargets = new List<BaseEnemy>();
        [SerializeField] private bool holdTargetsUntilBegin;
        [SerializeField] private bool wakeTargetsOnBegin;
        [SerializeField] private bool beginWhenTargetHurt = true;

        [Header("Dialogue")]
        [SerializeField] private DialogueSequence briefing;
        [SerializeField] private DialogueSequence debrief;
        [SerializeField] private bool dialogueOncePerSession = true;

        [Header("Result")]
        [SerializeField] private TutorialGate[] gatesToOpen = new TutorialGate[0];
        [SerializeField] private string setsFlagWhenDone;
        public UnityEvent onBegan;
        public UnityEvent onCompleted;

        private Phase phase = Phase.Idle;
        private int progress;
        private ObjectiveView.Row row;

        protected override Color GizmoColor => new Color(1f, 0.78f, 0.3f);

        public bool IsComplete => phase == Phase.Done;
        private bool TracksTargets => action == PlayerAction.Kill && killTargets.Count > 0;
        private int Target => TracksTargets ? killTargets.Count : requiredCount;

        private void Start()
        {
            if (holdTargetsUntilBegin) SetTargetsDormant(true);
            if (beginWhenTargetHurt) WatchTargets(true);
            if (beginOn == BeginMode.OnSceneStart) Begin();
        }

        private void OnDisable()
        {
            ActionFeed.Raised -= OnAction;
        }

        private void OnDestroy()
        {
            WatchTargets(false);
        }

        private void WatchTargets(bool watch)
        {
            foreach (BaseEnemy enemy in killTargets)
            {
                if (enemy == null) continue;
                Health health = enemy.GetComponent<Health>();
                if (health == null) continue;

                health.Damaged -= OnTargetHurt;
                if (watch) health.Damaged += OnTargetHurt;
            }
        }

        private void OnTargetHurt(float amount, Vector3 point, Vector3 direction)
        {
            if (phase == Phase.Idle) Begin();
        }

        protected override void OnPlayerEntered()
        {
            if (beginOn == BeginMode.OnZoneEnter) Begin();
        }

        public void Begin()
        {
            if (phase != Phase.Idle) return;

            phase = Phase.Briefing;
            ActionFeed.Raised += OnAction;
            if (wakeTargetsOnBegin) SetTargetsDormant(false);
            onBegan?.Invoke();

            if (showPrompt == PromptTiming.Immediately) ShowPrompt();
            System.Action onStarted = showPrompt == PromptTiming.WhenBriefingStarts ? ShowPromptSoon : (System.Action)null;
            PlayOnce(briefing, "briefing", onStarted, ShowPrompt);
        }

        private void ShowPromptSoon()
        {
            if (promptDelay <= 0f || !isActiveAndEnabled) ShowPrompt();
            else StartCoroutine(ShowPromptAfter(promptDelay));
        }

        private IEnumerator ShowPromptAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            ShowPrompt();
        }

        public void ForceComplete()
        {
            if (phase == Phase.Idle) Begin();
            if (phase != Phase.Done) Finish();
        }

        private void ShowPrompt()
        {
            if (phase != Phase.Briefing) return;

            phase = Phase.Active;
            row = ObjectiveView.Resolve().Add(title, keyHint, Target);
            Recount();
        }

        private void OnAction(PlayerAction raised, Object source)
        {
            if (raised != action || phase == Phase.Done || phase == Phase.Idle) return;

            if (TracksTargets)
            {
                if (source is BaseEnemy enemy && !killTargets.Contains(enemy)) return;
            }
            else
            {
                progress++;
            }

            Recount();
        }

        private void Recount()
        {
            if (TracksTargets) progress = CountDeadTargets();

            if (row != null) ObjectiveView.Resolve().SetProgress(row, progress);
            if (phase == Phase.Active && progress >= Target) Finish();
        }

        private void Finish()
        {
            phase = Phase.Done;
            ActionFeed.Raised -= OnAction;

            if (row != null) ObjectiveView.Resolve().Complete(row);
            foreach (TutorialGate gate in gatesToOpen)
            {
                if (gate != null) gate.Open();
            }

            DialogueMemory.SetFlag(setsFlagWhenDone);
            PlayOnce(debrief, "debrief", null, RaiseCompleted);
        }

        private void RaiseCompleted()
        {
            onCompleted?.Invoke();
        }

        private int CountDeadTargets()
        {
            int dead = 0;
            foreach (BaseEnemy enemy in killTargets)
            {
                if (enemy == null || enemy.IsEnemyDead) dead++;
            }
            return dead;
        }

        private void SetTargetsDormant(bool dormant)
        {
            foreach (BaseEnemy enemy in killTargets)
            {
                if (enemy != null && !enemy.IsEnemyDead) enemy.SetDormant(dormant);
            }
        }

        private void PlayOnce(DialogueSequence sequence, string part, System.Action onStarted, System.Action onDone)
        {
            string key = gameObject.scene.name + "/" + name + "/" + part;

            if (sequence == null || (dialogueOncePerSession && DialogueMemory.HasHeard(key)))
            {
                onStarted?.Invoke();
                onDone?.Invoke();
                return;
            }

            DialogueMemory.MarkHeard(key);
            DialogueDirector.Resolve().Play(sequence, onDone, onStarted);
        }
    }
}
