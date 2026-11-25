using Project.Player;
using UnityEngine;

namespace Project.Tutorial
{
    [RequireComponent(typeof(BoxCollider))]
    public abstract class PlayerZone : MonoBehaviour
    {
        private static PlayerMovement cachedPlayer;

        [SerializeField] private bool zoneEnabled = true;

        private BoxCollider box;
        private bool playerInside;

        protected abstract Color GizmoColor { get; }

        public bool ZoneEnabled
        {
            get => zoneEnabled;
            set => zoneEnabled = value;
        }

        protected virtual void Reset()
        {
            BoxCollider b = GetComponent<BoxCollider>();
            b.isTrigger = true;
            b.size = new Vector3(4f, 4f, 4f);
            b.center = new Vector3(0f, 2f, 0f);
        }

        protected virtual void Awake()
        {
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerMovement>() != null) MarkInside();
        }

        protected virtual void Update()
        {
            if (!zoneEnabled) return;

            PlayerMovement player = FindPlayer();
            if (player == null) return;

            bool inside = Contains(player);
            if (inside && !playerInside) MarkInside();
            else if (!inside) playerInside = false;
        }

        private void MarkInside()
        {
            if (playerInside) return;
            playerInside = true;
            if (zoneEnabled) OnPlayerEntered();
        }

        private bool Contains(PlayerMovement player)
        {
            CharacterController controller = player.Controller;
            Vector3 point = controller != null ? controller.bounds.center : player.transform.position;
            Vector3 local = transform.InverseTransformPoint(point) - box.center;
            Vector3 half = box.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        private static PlayerMovement FindPlayer()
        {
            if (cachedPlayer == null) cachedPlayer = FindFirstObjectByType<PlayerMovement>();
            return cachedPlayer;
        }

        protected abstract void OnPlayerEntered();

        protected virtual void OnDrawGizmos()
        {
            BoxCollider b = GetComponent<BoxCollider>();
            if (b == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Color c = GizmoColor;
            Gizmos.color = new Color(c.r, c.g, c.b, zoneEnabled ? 0.18f : 0.06f);
            Gizmos.DrawCube(b.center, b.size);
            Gizmos.color = c;
            Gizmos.DrawWireCube(b.center, b.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
