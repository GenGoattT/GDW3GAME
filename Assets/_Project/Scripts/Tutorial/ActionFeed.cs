using System;
using UnityEngine;

namespace Project.Tutorial
{
    public enum PlayerAction
    {
        Jump,
        Dash,
        Slide,
        WallJump,
        Fire,
        AltFire,
        SwapWeapon,
        Kill
    }

    public static class ActionFeed
    {
        public static event Action<PlayerAction, UnityEngine.Object> Raised;

        public static void Raise(PlayerAction action, UnityEngine.Object source = null)
        {
            Raised?.Invoke(action, source);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Raised = null;
        }
    }
}
