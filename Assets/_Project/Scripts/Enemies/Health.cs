using System;
using UnityEngine;
using UnityEngine.Events;

namespace Project.Combat
{
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 30f;

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
    }
}
