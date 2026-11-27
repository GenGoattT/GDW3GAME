using System;
using UnityEngine;
using UnityEngine.Events;

namespace Project.Combat
{
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 30f;
        [Tooltip("Seconds of invulnerability after taking a hit. Leave at 0 for enemies.")]
        [SerializeField] private float invulnerableTime = 0f;

        private float lastHitTime = float.NegativeInfinity;

        [Header("Inspector Hooks")]
        public UnityEvent onDied;
        public event Action<float, Vector3, Vector3> Damaged;
        public event Action Died;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public bool IsDead { get; private set; }

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (IsDead || amount <= 0f) return;
            if (Time.time - lastHitTime < invulnerableTime) return;
            lastHitTime = Time.time;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            Damaged?.Invoke(amount, hitPoint, hitDirection);

            if (CurrentHealth <= 0f)
            {
                IsDead = true;
                Died?.Invoke();
                onDied?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        }

        //Adds a revive option for dead enemies for checkpoints
        public void Revive()
        {
            IsDead = false;
        }
    }
}
