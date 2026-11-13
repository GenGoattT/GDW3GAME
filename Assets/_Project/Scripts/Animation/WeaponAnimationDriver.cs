using UnityEngine;
using Project.Player;

public class WeaponAnimationDriver : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private float speedDampTime = 0.08f;
    [SerializeField] private bool idleWhileAirborne = true;
    [SerializeField] private bool idleWhileSliding = true;
    [SerializeField] private bool lockAnimatedRoot = true;

    private PlayerMovement player;
    private int speedHash;

    private Transform animRoot;
    private Vector3 basePosition;
    private Quaternion baseRotation;
    private Vector3 baseScale;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();

        player = GetComponentInParent<PlayerMovement>();
        if (player == null) player = FindFirstObjectByType<PlayerMovement>();

        speedHash = Animator.StringToHash(speedParameter);

        if (animator != null)
        {
            animRoot = animator.transform;
            basePosition = animRoot.localPosition;
            baseRotation = animRoot.localRotation;
            baseScale = animRoot.localScale;
        }
    }

    private void Update()
    {
        if (animator == null || player == null) return;

        float speed = player.HorizontalVelocity.magnitude;

        if (idleWhileAirborne && !player.IsGrounded) speed = 0f;
        if (idleWhileSliding && player.IsSliding) speed = 0f;

        animator.SetFloat(speedHash, speed, speedDampTime, Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (!lockAnimatedRoot || animRoot == null) return;

        animRoot.localPosition = basePosition;
        animRoot.localRotation = baseRotation;
        animRoot.localScale = baseScale;
    }
}
