using Project.Audio;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Project.Enemies;
using Project.Player;

namespace Project.Level
{
    [RequireComponent(typeof(BoxCollider))]
    public class ArenaLockdown : MonoBehaviour
    {
        private enum ArenaState { Waiting, Locked, Cleared }

        [Header("Arena")]
        [Tooltip("Place each wall where it should block the path. They stay hidden until the player crosses the trigger.")]
        [SerializeField] private Transform[] walls;
        [Tooltip("Leave empty to grab every enemy inside Enemy Search Bounds when the arena locks.")]
        [SerializeField] private List<BaseEnemy> enemies = new List<BaseEnemy>();
        [SerializeField] private Bounds enemySearchBounds = new Bounds(Vector3.zero, new Vector3(30f, 10f, 30f));
        [Tooltip("Enemies stand still until the arena locks or they get shot.")]
        [SerializeField] private bool holdEnemiesUntilLocked = true;

        [Header("Walls")]
        [SerializeField] private float dropHeight = 10f;
        [SerializeField] private float slamTime = 0.18f;
        [SerializeField] private float openTime = 0.8f;
        [SerializeField] private bool reopenWhenCleared = true;

        [Header("Feedback")]
        [SerializeField] private float slamShakeStrength = 0.4f;
        [SerializeField] private float slamShakeTime = 0.25f;

        [Header("Inspector Hooks")]
        public UnityEvent onLocked;
        public UnityEvent onCleared;

        private ArenaState state = ArenaState.Waiting;
        private Vector3[] closedPositions;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;

            closedPositions = new Vector3[walls.Length];
            for (int i = 0; i < walls.Length; i++)
            {
                if (walls[i] == null) continue;
                closedPositions[i] = walls[i].position;
                walls[i].gameObject.SetActive(false);
            }

            if (enemies.Count == 0) FindEnemiesInBounds();
            SetEnemiesDormant(holdEnemiesUntilLocked);
        }

        private void Update()
        {
            if (state != ArenaState.Locked || !AllEnemiesDead()) return;

            state = ArenaState.Cleared;
            onCleared?.Invoke();

            if (reopenWhenCleared) StartCoroutine(MoveWalls(false));
        }

        private void OnTriggerEnter(Collider other)
        {
            if (state != ArenaState.Waiting || !other.CompareTag("Player")) return;

            state = ArenaState.Locked;
            SetEnemiesDormant(false);
            onLocked?.Invoke();
            StartCoroutine(MoveWalls(true));
        }

        public void ResetArena()
        {
            StopAllCoroutines();
            state = ArenaState.Waiting;

            for (int i = 0; i < walls.Length; i++)
            {
                if (walls[i] == null) continue;
                walls[i].position = closedPositions[i];
                walls[i].gameObject.SetActive(false);
            }

            SetEnemiesDormant(holdEnemiesUntilLocked);
        }

        private void SetEnemiesDormant(bool dormant)
        {
            foreach (BaseEnemy enemy in enemies)
            {
                if (enemy != null && !enemy.IsEnemyDead) enemy.SetDormant(dormant);
            }
        }

        private void FindEnemiesInBounds()
        {
            Bounds worldBounds = new Bounds(transform.TransformPoint(enemySearchBounds.center), enemySearchBounds.size);

            foreach (BaseEnemy enemy in FindObjectsByType<BaseEnemy>(FindObjectsSortMode.None))
            {
                if (worldBounds.Contains(enemy.transform.position))
                    enemies.Add(enemy);
            }
        }

        private bool AllEnemiesDead()
        {
            foreach (BaseEnemy enemy in enemies)
            {
                if (enemy != null && !enemy.IsEnemyDead) return false;
            }
            return true;
        }

        private IEnumerator MoveWalls(bool closing)
        {
            Vector3 raise = Vector3.up * dropHeight;
            float duration = closing ? slamTime : openTime;

            if (!closing) GameAudio.Play(SfxId.GateOpen, WallsCenter());

            for (int i = 0; i < walls.Length; i++)
            {
                if (walls[i] == null) continue;
                walls[i].gameObject.SetActive(true);
                walls[i].position = closing ? closedPositions[i] + raise : closedPositions[i];
            }

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float eased = closing ? k * k : 1f - (1f - k) * (1f - k);

                for (int i = 0; i < walls.Length; i++)
                {
                    if (walls[i] == null) continue;
                    Vector3 from = closing ? closedPositions[i] + raise : closedPositions[i];
                    Vector3 to = closing ? closedPositions[i] : closedPositions[i] + raise;
                    walls[i].position = Vector3.LerpUnclamped(from, to, eased);
                }

                yield return null;
            }

            for (int i = 0; i < walls.Length; i++)
            {
                if (walls[i] == null) continue;
                walls[i].position = closedPositions[i];
                walls[i].gameObject.SetActive(closing);
            }

            if (closing)
            {
                GameAudio.Play(SfxId.WallSlam, WallsCenter());
                ShakeCamera();
            }
        }

        private Vector3 WallsCenter()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < closedPositions.Length; i++)
            {
                if (walls[i] == null) continue;
                sum += closedPositions[i];
                count++;
            }
            return count > 0 ? sum / count : transform.position;
        }

        private void ShakeCamera()
        {
            PlayerCamera cam = FindFirstObjectByType<PlayerCamera>();
            if (cam != null) cam.Shake(slamShakeStrength, slamShakeTime);
        }

        private void OnDrawGizmos()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }

            Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
            Gizmos.DrawWireCube(transform.TransformPoint(enemySearchBounds.center), enemySearchBounds.size);

            if (walls == null) return;

            Gizmos.color = Color.cyan;
            foreach (Transform wall in walls)
            {
                if (wall == null) continue;
                Gizmos.matrix = wall.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
