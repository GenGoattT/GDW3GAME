using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Dialogue
{
    [DefaultExecutionOrder(-100)]
    public class DialogueDirector : MonoBehaviour
    {
        private class Request
        {
            public DialogueSequence sequence;
            public Action onDone;
            public Action onStarted;
            public float queuedAt;
            public bool wrapUp;
            public bool started;
        }

        public static DialogueDirector Instance { get; private set; }

        [SerializeField] private MonoBehaviour viewBehaviour;
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private AudioSource blipSource;

        [Header("Pacing")]
        [SerializeField] private float revealCharsPerSecond = 70f;
        [SerializeField] private float readingCharsPerSecond = 21f;
        [SerializeField] private float minimumHold = 1.2f;
        [SerializeField] private float lingerAfterLine = 0.3f;
        [SerializeField] private float gapBetweenSequences = 0.15f;
        [SerializeField] private KeyCode skipKey = KeyCode.None;

        [Header("Queue")]
        [SerializeField] private bool newerLinesTakeOver = true;
        [SerializeField] private float ambientMaxWait = 2.5f;
        [SerializeField] private float storyMaxWait = 10f;

        public event Action<DialogueLine> LineStarted;
        public event Action<DialogueSequence> SequenceFinished;

        private readonly List<Request> queue = new List<Request>();
        private IDialogueView view;
        private Request current;
        private Coroutine runner;
        private bool skipRequested;

        public bool IsBusy => current != null;
        public DialogueSequence Current => current?.sequence;

        public static DialogueDirector Resolve()
        {
            if (Instance != null) return Instance;

            Instance = FindFirstObjectByType<DialogueDirector>();
            if (Instance == null) Instance = new GameObject("DialogueDirector").AddComponent<DialogueDirector>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            view = viewBehaviour as IDialogueView ?? GetComponentInChildren<IDialogueView>(true);
            if (view == null) view = gameObject.AddComponent<CaptionView>();

            if (voiceSource == null) voiceSource = CreateSource("Voice");
            if (blipSource == null) blipSource = CreateSource("Blips");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (skipKey != KeyCode.None && current != null && Input.GetKeyDown(skipKey))
                skipRequested = true;

            if (current == null && runner == null && queue.Count > 0)
                StartNext();
        }

        public void Play(DialogueSequence sequence, Action onDone = null, Action onStarted = null)
        {
            if (sequence == null || sequence.IsEmpty)
            {
                onStarted?.Invoke();
                onDone?.Invoke();
                return;
            }

            Request request = new Request
            {
                sequence = sequence,
                onDone = onDone,
                onStarted = onStarted,
                queuedAt = Time.unscaledTime
            };

            if (current == null && runner == null)
            {
                Begin(request);
                return;
            }

            DialoguePriority incoming = sequence.Priority;
            DialoguePriority playing = current != null ? current.sequence.Priority : DialoguePriority.Ambient;
            bool canCut = current != null && current.sequence.Interruptible;

            if (canCut && incoming > playing)
            {
                Interrupt();
                Begin(request);
                return;
            }

            if (canCut && newerLinesTakeOver && incoming == playing)
            {
                current.wrapUp = true;
                Enqueue(request, true);
                return;
            }

            Enqueue(request, false);
        }

        public void StopAll()
        {
            List<Request> pending = new List<Request>(queue);
            queue.Clear();
            Interrupt();
            view.Close();
            foreach (Request r in pending) Release(r);
        }

        private void Enqueue(Request request, bool toFront)
        {
            int slot = queue.FindIndex(r => toFront
                ? r.sequence.Priority <= request.sequence.Priority
                : r.sequence.Priority < request.sequence.Priority);

            if (slot < 0) queue.Add(request);
            else queue.Insert(slot, request);
        }

        private void StartNext()
        {
            while (queue.Count > 0)
            {
                Request next = queue[0];
                queue.RemoveAt(0);

                if (IsStale(next))
                {
                    Release(next);
                    continue;
                }

                Begin(next);
                return;
            }

            view.Close();
        }

        private bool IsStale(Request request)
        {
            float waited = Time.unscaledTime - request.queuedAt;
            switch (request.sequence.Priority)
            {
                case DialoguePriority.Ambient: return waited > ambientMaxWait;
                case DialoguePriority.Story: return waited > storyMaxWait;
                default: return false;
            }
        }

        private void Begin(Request request)
        {
            current = request;
            request.started = true;
            runner = StartCoroutine(RunSequence(request));
            request.onStarted?.Invoke();
        }

        private void Release(Request request)
        {
            if (!request.started) request.onStarted?.Invoke();
            request.onDone?.Invoke();
        }

        private void Interrupt()
        {
            if (runner != null) StopCoroutine(runner);
            runner = null;
            voiceSource.Stop();

            Request cut = current;
            current = null;
            if (cut != null) Release(cut);
        }

        private IEnumerator RunSequence(Request request)
        {
            view.Open();

            foreach (DialogueLine line in request.sequence.Lines)
            {
                if (request.wrapUp) break;
                if (line.pauseBefore > 0f) yield return new WaitForSecondsRealtime(line.pauseBefore);
                yield return RunLine(line, request);
            }

            current = null;
            SequenceFinished?.Invoke(request.sequence);
            Release(request);

            if (current == null && queue.Count > 0)
                yield return new WaitForSecondsRealtime(gapBetweenSequences);

            runner = null;
            if (current == null) StartNext();
        }

        private IEnumerator RunLine(DialogueLine line, Request owner)
        {
            string text = line.text ?? string.Empty;
            float reveal = text.Length / Mathf.Max(1f, revealCharsPerSecond);
            float hold = HoldTimeFor(line, text);

            skipRequested = false;
            view.ShowLine(line, reveal);
            LineStarted?.Invoke(line);

            if (line.voice != null) voiceSource.PlayOneShot(line.voice);

            int lastBlipAt = 0;
            float elapsed = 0f;

            while (elapsed < hold)
            {
                if (skipRequested)
                {
                    skipRequested = false;
                    if (elapsed < reveal) elapsed = reveal;
                    else break;
                }

                if (owner.wrapUp && line.voice == null && elapsed >= reveal + minimumHold * 0.5f) break;
                if (line.voice == null) lastBlipAt = TickBlips(line.speaker, lastBlipAt);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            voiceSource.Stop();
        }

        private float HoldTimeFor(DialogueLine line, string text)
        {
            if (line.holdOverride > 0f) return line.holdOverride;
            if (line.voice != null) return line.voice.length + lingerAfterLine;

            float reading = text.Length / Mathf.Max(1f, readingCharsPerSecond);
            return Mathf.Max(minimumHold, reading) + lingerAfterLine;
        }

        private int TickBlips(DialogueSpeaker speaker, int lastBlipAt)
        {
            if (speaker == null || speaker.Blip == null) return lastBlipAt;

            int shown = view.VisibleCharacters;
            if (shown - lastBlipAt < speaker.CharactersPerBlip) return lastBlipAt;

            blipSource.pitch = UnityEngine.Random.Range(speaker.BlipPitch.x, speaker.BlipPitch.y);
            blipSource.PlayOneShot(speaker.Blip);
            return shown;
        }

        private AudioSource CreateSource(string label)
        {
            AudioSource source = new GameObject(label).AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
