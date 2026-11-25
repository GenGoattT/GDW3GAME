using System.Collections.Generic;
using UnityEngine;

namespace Project.Dialogue
{
    public static class DialogueMemory
    {
        private static readonly HashSet<string> heard = new HashSet<string>();
        private static readonly HashSet<string> flags = new HashSet<string>();

        public static bool HasHeard(string key) => !string.IsNullOrEmpty(key) && heard.Contains(key);
        public static void MarkHeard(string key)
        {
            if (!string.IsNullOrEmpty(key)) heard.Add(key);
        }

        public static bool HasFlag(string flag) => string.IsNullOrEmpty(flag) || flags.Contains(flag);
        public static void SetFlag(string flag)
        {
            if (!string.IsNullOrEmpty(flag)) flags.Add(flag);
        }

        public static void ClearFlag(string flag) => flags.Remove(flag);

        public static void Forget()
        {
            heard.Clear();
            flags.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Forget();
    }
}
