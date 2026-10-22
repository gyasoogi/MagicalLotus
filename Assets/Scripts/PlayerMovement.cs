using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Horizontal movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float acceleration = 40f;
    [SerializeField] private float deceleration = 30f;

    [Header("Dash")]
    [SerializeField, Min(0.1f)] private float dashSpeed = 18f;
    [SerializeField, Min(0.02f)] private float dashDuration = 0.18f;
    [SerializeField, Min(0.1f)] private float dashCooldown = 0.8f;

    private Rigidbody2D rb;
    private PhysicsMaterial2D movementMaterial;
    private PhysicsMaterial2D originalMaterial;
    private float moveInput;
    private float speedBeforeLock;
    private float facing = 1f;
    private float dashDirection, dashRemaining, nextDashAt;

    public bool MovementLocked { get; private set; }
    public bool IsDashing => dashRemaining > 0f;
    public float DashCooldownRemaining => Mathf.Max(0f, nextDashAt - Time.time);

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;

        // Let the movement code handle braking so landing friction cannot
        // turn a vertical impact into a sudden loss of horizontal speed.
        originalMaterial = rb.sharedMaterial;
        movementMaterial = new PhysicsMaterial2D("Player Movement")
        {
            friction = 0f,
            bounciness = 0f
        };
        rb.sharedMaterial = movementMaterial;
    }

    private void OnDestroy()
    {
        if (rb != null && rb.sharedMaterial == movementMaterial)
            rb.sharedMaterial = originalMaterial;

        if (movementMaterial != null)
            Destroy(movementMaterial);
    }

    private void Update()
    {
        moveInput = 0f;
        if (Keyboard.current == null) return;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            moveInput -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            moveInput += 1f;
        if (moveInput != 0f) facing = Mathf.Sign(moveInput);
        if (Keyboard.current.leftShiftKey.wasPressedThisFrame || Keyboard.current.rightShiftKey.wasPressedThisFrame)
            TryDash();
    }

    public bool TryDash()
    {
        if (!isActiveAndEnabled || MovementLocked || IsDashing || DashCooldownRemaining > 0f
            || (GetComponent<PlayerHealth>()?.IsDead ?? false)
            || (GetComponent<StringDrawer>()?.IsDrawing ?? false)
            || (GetComponent<KarmaBeadHud>()?.IsDragging ?? false)) return false;
        dashDirection = facing;
        dashRemaining = dashDuration;
        nextDashAt = Time.time + dashCooldown;
        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, rb.linearVelocity.y);
        return true;
    }

    public void CancelDash()
    {
        if (!IsDashing) return;
        dashRemaining = 0f;
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
    }

    private void OnDisable() { CancelDash(); }

    private void FixedUpdate()
    {
        if (MovementLocked)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (IsDashing)
        {
            dashRemaining = Mathf.Max(0f, dashRemaining - Time.fixedDeltaTime);
            rb.linearVelocity = new Vector2(IsDashing ? dashDirection * dashSpeed : moveInput * moveSpeed, rb.linearVelocity.y);
            return;
        }

        float targetSpeed = moveInput * moveSpeed;
        float rate = Mathf.Abs(moveInput) > 0.01f ? acceleration : deceleration;
        float newSpeed = Mathf.MoveTowards(
            rb.linearVelocity.x,
            targetSpeed,
            rate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newSpeed, rb.linearVelocity.y);
    }

    public void SetMovementLocked(bool locked)
    {
        if (MovementLocked == locked) return;

        if (locked) CancelDash();
        if (locked)
            speedBeforeLock = rb.linearVelocity.x;

        MovementLocked = locked;
        if (locked)
            rb.linearVelocity = Vector2.zero;
        else
            rb.linearVelocity = new Vector2(speedBeforeLock, rb.linearVelocity.y);
    }
}
