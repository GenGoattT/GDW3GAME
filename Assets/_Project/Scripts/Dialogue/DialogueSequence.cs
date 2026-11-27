using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Dialogue
{
    public enum DialoguePriority
    {
        Ambient = 0,
        Story = 1,
        Critical = 2
    }

    [Serializable]
    public class DialogueLine
    {
        public DialogueSpeaker speaker;
        [TextArea(2, 4)] public string text;
        public AudioClip voice;
        [Min(0f)] public float pauseBefore;
        [Min(0f)] public float holdOverride;
    }

    [CreateAssetMenu(menuName = "Stories End/Dialogue/Sequence", fileName = "Seq_")]
    public class DialogueSequence : ScriptableObject
    {
        [SerializeField] private DialoguePriority priority = DialoguePriority.Story;
        [SerializeField] private bool interruptible = true;
        [SerializeField] private List<DialogueLine> lines = new List<DialogueLine>();

        public DialoguePriority Priority => priority;
        public bool Interruptible => interruptible;
        public IReadOnlyList<DialogueLine> Lines => lines;
        public bool IsEmpty => lines == null || lines.Count == 0;
    }
}
