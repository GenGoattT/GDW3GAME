using UnityEngine;
using Project.Combat;

namespace Project.Enemies
{
    public class IceBlockProjectile : MonoBehaviour
    {
        [SerializeField] private float damage = 30f;
        [SerializeField] private float radius = 0.6f;
        [SerializeField] private float lifetime = 8f;
        [SerializeField] private float spinSpeed = 180f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private GameObject shatterEffect;

        private Vector3 velocity;
        private Vector3 gravity;
        private Vector3 spinAxis;
        private Transform owner;
        private bool launched;

        public void Hold(Transform parent)
        {
            launched = false;
            transform.SetParent(parent, true);
        }

        public void Launch(Vector3 initialVelocity, Vector3 gravityVector, Transform thrower)
        {
            transform.SetParent(null, true);
            velocity = initialVelocity;
            gravity = gravityVector;
            owner = thrower;
            spinAxis = Random.onUnitSphere;
            launched = true;
            Destroy(gameObject, lifetime);
        }

        private void Update()
        {
            if (!launched) return;

            float dt = Time.deltaTime;
            velocity += gravity * dt;
            Vector3 step = velocity * dt;
            float distance = step.magnitude;

            if (distance > 0.0001f && TryFindHit(step / distance, distance, out RaycastHit hit))
            {
                Impact(hit);
                return;
            }

            transform.position += step;
            transform.Rotate(spinAxis, spinSpeed * dt, Space.World);
        }

        private bool TryFindHit(Vector3 direction, float distance, out RaycastHit best)
        {
            best = default;
            bool found = false;
            float nearest = float.MaxValue;

            RaycastHit[] hits = Physics.SphereCastAll(transform.position, radius, direction, distance, hitMask, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (owner != null && hit.transform.IsChildOf(owner)) continue;
                if (hit.collider.GetComponentInParent<BaseEnemy>() != null) continue;
                if (hit.distance >= nearest) continue;

                nearest = hit.distance;
                best = hit;
                found = true;
            }

            return found;
        }

        private void Impact(RaycastHit hit)
        {
            IDamageable dmg = hit.collider.GetComponentInParent<IDamageable>();
            if (dmg != null && !dmg.IsDead)
                dmg.TakeDamage(damage, hit.point, velocity.normalized);

            if (shatterEffect != null)
                Instantiate(shatterEffect, hit.point, Quaternion.LookRotation(hit.normal));

            Destroy(gameObject);
        }
    }
}
