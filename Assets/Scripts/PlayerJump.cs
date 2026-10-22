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

    [Header("Variable jump height")]
    [SerializeField, Min(0f)] private float maxJumpHoldTime = 0.3f;
    [SerializeField, Range(0.1f, 1f)] private float heldGravityMultiplier = 0.55f;
    [SerializeField, Min(1f)] private float releasedGravityMultiplier = 3f;
    [SerializeField, Min(0.01f)] private float releaseTransitionTime = 0.06f;

    [Header("Falling")]
    [SerializeField] private float gravityScale = 3f;
    [SerializeField] private float fallMultiplier = 1.5f;
    [SerializeField] private float maxFallSpeed = 14f;

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private PlayerMovement movement;
    private bool jumpActive;
    private bool jumpReleased;
    private float jumpHoldElapsed;
    private float releaseElapsed;

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
        if (movement.MovementLocked)
        {
            jumpActive = false;
            return;
        }

        if (Keyboard.current == null)
        {
            jumpReleased = true;
            return;
        }

        bool jumpHeld = Keyboard.current.spaceKey.isPressed
            || Keyboard.current.wKey.isPressed
            || Keyboard.current.upArrowKey.isPressed;

        // Once released, pressing again in midair cannot extend this jump.
        if (jumpActive && !jumpHeld)
            jumpReleased = true;

        bool jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame
            || Keyboard.current.wKey.wasPressedThisFrame
            || Keyboard.current.upArrowKey.wasPressedThisFrame;

        if (jumpPressed && rb.linearVelocity.y <= 0f && IsGrounded())
        {
            jumpActive = true;
            jumpReleased = false;
            jumpHoldElapsed = 0f;
            releaseElapsed = 0f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpSpeed);
        }
    }

    private void FixedUpdate()
    {
        if (movement.MovementLocked)
        {
            jumpActive = false;
            return;
        }

        rb.gravityScale = gravityScale;

        if (jumpActive && rb.linearVelocity.y > 0f)
        {
            if (!jumpReleased)
            {
                jumpHoldElapsed += Time.fixedDeltaTime;
                if (jumpHoldElapsed >= maxJumpHoldTime)
                    jumpReleased = true;
            }

            float ascentMultiplier = heldGravityMultiplier;
            if (jumpReleased)
            {
                releaseElapsed += Time.fixedDeltaTime;
                ascentMultiplier = Mathf.Lerp(heldGravityMultiplier, releasedGravityMultiplier,
                    releaseElapsed / Mathf.Max(0.01f, releaseTransitionTime));
            }

            // Increase gravity smoothly on release, preserving a brief upward arc.
            rb.gravityScale = gravityScale * ascentMultiplier;
        }
        else
        {
            jumpActive = false;
        }

        if (rb.linearVelocity.y < 0f)
        {
            float extraGravity = Physics2D.gravity.y * gravityScale * (fallMultiplier - 1f);
            float fallSpeed = rb.linearVelocity.y + extraGravity * Time.fixedDeltaTime;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(fallSpeed, -maxFallSpeed));
        }
    }

    private void OnDisable()
    {
        jumpActive = false;
        if (rb != null)
            rb.gravityScale = gravityScale;
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
