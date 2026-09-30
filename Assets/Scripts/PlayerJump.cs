using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerJump : MonoBehaviour
{
    [Header("Jump")]
    [SerializeField] private float jumpSpeed = 8f;
    [SerializeField] private float groundCheckDistance = 0.12f;

    [Header("Falling")]
    [SerializeField] private float gravityScale = 3f;
    [SerializeField] private float fallMultiplier = 1.5f;
    [SerializeField] private float maxFallSpeed = 14f;

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private PlayerMovement movement;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        movement = GetComponent<PlayerMovement>();
        rb.gravityScale = gravityScale;
        rb.freezeRotation = true;
    }

    private void Update()
    {
        if (Keyboard.current == null || movement.MovementLocked) return;

        bool jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame
            || Keyboard.current.wKey.wasPressedThisFrame
            || Keyboard.current.upArrowKey.wasPressedThisFrame;

        if (jumpPressed && IsGrounded())
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpSpeed);
    }

    private void FixedUpdate()
    {
        if (movement.MovementLocked)
            return;

        if (rb.linearVelocity.y < 0f)
        {
            float extraGravity = Physics2D.gravity.y * gravityScale * (fallMultiplier - 1f);
            float fallSpeed = rb.linearVelocity.y + extraGravity * Time.fixedDeltaTime;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(fallSpeed, -maxFallSpeed));
        }
    }

    private bool IsGrounded()
    {
        Vector2 start = bodyCollider.bounds.center;
        float distance = bodyCollider.bounds.extents.y + groundCheckDistance;
        RaycastHit2D[] hits = Physics2D.RaycastAll(start, Vector2.down, distance);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == bodyCollider || hit.collider.isTrigger) continue;
            return true;
        }

        return false;
    }
}
