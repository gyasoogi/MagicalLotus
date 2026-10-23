using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class CapturableProjectile : MonoBehaviour
{
    [SerializeField] private float lifeTime = 8f;
    [SerializeField] private KarmaKeyword keyword;
    [SerializeField, Min(0f)] private float contactDamage = 34f;
    [SerializeField] private bool absorbable = true;
    private bool captured;
    private bool karmaExtracted, energyClaimed;
    private float slowRemaining;
    private float originalGravity;
    private const float SlowFactor = 0.25f;

    private Rigidbody2D rb;

    public float GravityScale => rb.gravityScale;
    public KarmaKeyword Keyword => keyword;
    public bool CanCapture => !captured && !karmaExtracted && absorbable && isActiveAndEnabled;
    public bool CanSlow => !captured && isActiveAndEnabled;
    public bool IsAbsorbable => absorbable;
    public bool KarmaExtracted => karmaExtracted;
    public bool IsSlowed => slowRemaining > 0f;
    private LineRenderer marker;

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
        if (captured) return;
        captured = true;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    public bool TryClaimKarma()
    {
        if (!CanCapture) return false;
        karmaExtracted = true;
        SetKeyword(keyword);
        UpdateMarker();
        return true;
    }

    public bool TryClaimEnergy()
    {
        if (!CanSlow || energyClaimed) return false;
        energyClaimed = true;
        return true;
    }

    public void SetAbsorbable(bool value) { absorbable = value; UpdateMarker(); }

    private void UpdateMarker()
    {
        if (absorbable && !karmaExtracted) return;
        if (marker == null)
        {
            marker = gameObject.AddComponent<LineRenderer>();
            marker.material = new Material(Shader.Find("Sprites/Default"));
            marker.useWorldSpace = false;
            marker.loop = true;
            marker.positionCount = 20;
            marker.widthMultiplier = 0.045f;
            marker.sortingOrder = 35;
            for (int i = 0; i < 20; i++)
            {
                float angle = i * Mathf.PI * 2f / 20;
                marker.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.65f, Mathf.Sin(angle) * 0.65f, -0.6f));
            }
        }
        marker.startColor = marker.endColor = absorbable ? Color.gray : new Color(1f, 0.15f, 0.12f);
    }

    public void SetKeyword(KarmaKeyword value)
    {
        keyword = value;
        Color color = karmaExtracted ? Color.gray : KarmaBeadHud.KeywordColor(value);
        SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null) sprite.color = color;
        Renderer body = GetComponent<Renderer>();
        if (body != null)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            body.GetPropertyBlock(properties);
            properties.SetColor("_Color", color);
            properties.SetColor("_BaseColor", color);
            body.SetPropertyBlock(properties);
        }
    }

    public void SlowOnString()
    {
        if (!CanSlow) return;
        if (slowRemaining <= 0f)
        {
            originalGravity = rb.gravityScale;
            rb.linearVelocity *= SlowFactor;
            rb.gravityScale *= SlowFactor;
        }
        slowRemaining = 0.4f;
    }

    private void Update()
    {
        if (slowRemaining <= 0f) return;
        slowRemaining -= Time.deltaTime;
        if (slowRemaining > 0f) return;
        rb.linearVelocity /= SlowFactor;
        rb.gravityScale = originalGravity;
    }
    private void FixedUpdate()
    {
        if (IsSlowed && rb.linearVelocity.sqrMagnitude < 0.04f)
            rb.linearVelocity = Vector2.down * 0.2f;
    }

    private void OnTriggerEnter2D(Collider2D other) { HitPlayer(other); }
    private void OnCollisionEnter2D(Collision2D other) { HitPlayer(other.collider); }
    private void HitPlayer(Collider2D other)
    {
        if (captured) return;
        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null) return;
        // A cursed/darkened enemy weakens the damage of its own bullets.
        health.TakeDamage(contactDamage * sourceDamageMultiplier);
        Capture();
    }
    private float sourceDamageMultiplier = 1f;
    public void SetSourceDamageMultiplier(float value) { sourceDamageMultiplier = Mathf.Clamp01(value); }
    private void OnDestroy()
    {
        if (marker != null && marker.sharedMaterial != null) Destroy(marker.sharedMaterial);
    }
}
