using UnityEngine;

namespace Project.Combat
{
    public interface IDamageable
    {
        bool IsDead { get; }
        void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection);
    }
}
