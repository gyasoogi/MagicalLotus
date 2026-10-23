using UnityEngine;

public class EnemyProjectileThrower : MonoBehaviour
{
    [SerializeField] private CapturableProjectile projectilePrefab;
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 throwOffset = new Vector2(0f, 0.6f);
    [SerializeField] private float throwInterval = 2.5f;
    [SerializeField] private float firstThrowDelay = 1f;
    [SerializeField] private float flightTime = 1.2f;
    [SerializeField, Min(1f)] private float intervalMultiplier = 1.2f;

    private float throwTimer;
    [SerializeField] private bool cycleKeywords = true;
    private int nextKeyword;
    public int ProjectilesPerVolley => 1;
    public float ThrowInterval => Mathf.Max(0.1f, throwInterval * intervalMultiplier);
    public void SetInitialDelay(float delay) { firstThrowDelay = Mathf.Max(0f, delay); throwTimer = firstThrowDelay; }

    private void Start()
    {
        throwTimer = firstThrowDelay;
    }

    private void Update()
    {
        if (projectilePrefab == null || target == null) return;

        throwTimer -= Time.deltaTime;
        if (throwTimer > 0f) return;

        ThrowProjectile();
        throwTimer = ThrowInterval;
    }

    private void ThrowProjectile()
    {
        Vector2 start = (Vector2)transform.position + throwOffset;
        Enemy enemy = GetComponentInParent<Enemy>();
        float safeFlightTime = Mathf.Max(0.1f, flightTime);
        Vector2 distance = (Vector2)target.position - start;
        CapturableProjectile projectile = Instantiate(projectilePrefab, start, Quaternion.identity);
        if (cycleKeywords) projectile.SetKeyword((KarmaKeyword)nextKeyword);
        if (enemy != null) projectile.SetSourceDamageMultiplier(enemy.OutgoingDamageMultiplier);
        float gravity = Mathf.Abs(Physics2D.gravity.y * projectile.GravityScale);
        Vector2 velocity = new Vector2(distance.x / safeFlightTime,
            distance.y / safeFlightTime + gravity * safeFlightTime * 0.5f);
        projectile.Launch(velocity);
        if (cycleKeywords) nextKeyword = (nextKeyword + 1) % 4;
    }
}
