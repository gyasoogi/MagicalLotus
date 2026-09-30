using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class CapturableProjectile : MonoBehaviour
{
    [SerializeField] private float lifeTime = 8f;

    private Rigidbody2D rb;

    public float GravityScale => rb.gravityScale;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    public void Launch(Vector2 velocity)
    {
        rb.linearVelocity = velocity;
    }

    public void Capture()
    {
        Destroy(gameObject);
    }
}
