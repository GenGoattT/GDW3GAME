using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Project.Combat;

namespace Project.Player
{
    [RequireComponent(typeof(Health))]
    public class PlayerDeath : MonoBehaviour
    {
        [SerializeField] private float restartDelay = 1.5f;
        [SerializeField] private float hitShakeStrength = 0.25f;
        [SerializeField] private float hitShakeTime = 0.15f;
        [SerializeField] private float deathShakeStrength = 0.6f;

        private Health health;
        private PlayerMovement movement;
        private WeaponManager weapons;
        private PlayerCamera playerCamera;

        private void Awake()
        {
            health = GetComponent<Health>();
            movement = GetComponent<PlayerMovement>();
            weapons = GetComponentInChildren<WeaponManager>();
            playerCamera = GetComponentInChildren<PlayerCamera>();
        }

        private void OnEnable()
        {
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
        }

        private void OnDisable()
        {
            health.Damaged -= HandleDamaged;
            health.Died -= HandleDied;
        }

        private void HandleDamaged(float amount, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (playerCamera != null) playerCamera.Shake(hitShakeStrength, hitShakeTime);
        }

        private void HandleDied()
        {
            if (movement != null) movement.enabled = false;
            if (weapons != null) weapons.enabled = false;
            if (playerCamera != null) playerCamera.Shake(deathShakeStrength, restartDelay * 0.5f);

            StartCoroutine(RestartRoutine());
        }

        private IEnumerator RestartRoutine()
        {
            yield return new WaitForSecondsRealtime(restartDelay);

            Time.timeScale = 1f;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }
    }
}
