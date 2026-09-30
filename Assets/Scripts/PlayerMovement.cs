using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Horizontal movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float acceleration = 40f;
    [SerializeField] private float deceleration = 30f;

    private Rigidbody2D rb;
    private float moveInput;
    private float speedBeforeLock;

    public bool MovementLocked { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
    }

    private void Update()
    {
        moveInput = 0f;
        if (Keyboard.current == null) return;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            moveInput -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            moveInput += 1f;
    }

    private void FixedUpdate()
    {
        if (MovementLocked)
        {
            rb.linearVelocity = Vector2.zero;
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

        if (locked)
            speedBeforeLock = rb.linearVelocity.x;

        MovementLocked = locked;
        if (locked)
            rb.linearVelocity = Vector2.zero;
        else
            rb.linearVelocity = new Vector2(speedBeforeLock, rb.linearVelocity.y);
    }
}
